using System.Runtime.InteropServices;
using IPGS.RemoteControl.ZcuAgent.Interop;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>
/// Captures frames from the primary X11 display using the MIT-SHM extension
/// (XShmGetImage) with an XGetImage fallback. See TDD §6.1 and §9.1–9.2.
/// <para>
/// Thread safety: <see cref="Initialize"/> and <see cref="Capture"/> must be called
/// from the same thread (X11 connection is per-thread in multi-threaded mode after
/// XInitThreads). The injector uses a separate display connection.
/// </para>
/// <para>
/// GOTCHA — live resolution detection: <c>XDisplayWidth</c> / <c>XDisplayHeight</c> are
/// client-side Xlib MACROS that cache the screen dimensions at <c>XOpenDisplay()</c> time
/// and NEVER update when RandR changes resolution/rotation while the connection is open.
/// Use <c>XGetGeometry</c> (a real server round-trip) instead — it always returns the
/// current root-window size. Failure to do so causes persistent <c>BadMatch</c> / null
/// frames (X_GetImage error_code=8) after any in-session rotate/resize event.
/// </para>
/// </summary>
internal sealed class X11ScreenCapturer : IScreenCapturer
{
    private readonly ILogger<X11ScreenCapturer> _logger;

    private IntPtr _display = IntPtr.Zero;
    private IntPtr _rootWindow = IntPtr.Zero;
    private int    _screen;

    // XShm state
    private bool   _useSHM;
    private IntPtr _shmXImage = IntPtr.Zero;     // XImage* backed by SHM
    private XShmSegmentInfo _shmInfo;
    private int    _shmId = -1;
    private IntPtr _shmAddr = IntPtr.Zero;        // virtual address of SHM block

    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Check resolution every N frames instead of every frame to amortise the cost of
    /// XGetGeometry (a real X server round-trip, ~100–300 µs on LAN). At 15 fps this
    /// yields one check per second — fast enough to react to a RandR rotate/resize event
    /// within ~1 s without adding per-frame latency.
    /// </summary>
    private const int ResolutionCheckInterval = 15;
    private int _framesSinceResolutionCheck;

    /// <summary>
    /// Reused pixel buffer for <see cref="CopyPixels"/> (audit Q3 — GC pressure):
    /// grown on demand, never shrunk. Safe because Capture() is single-threaded and the
    /// returned <see cref="CapturedFrame"/> is consumed synchronously before the next
    /// Capture() call (see IScreenCapturer doc).
    /// </summary>
    private byte[] _pixelBuffer = [];

    public X11ScreenCapturer(ILogger<X11ScreenCapturer> logger)
        => _logger = logger;

    // Audit L9: ScreenSize (2×int, 8-byte struct) is written by the capture thread on
    // resolution change and read by the receive thread for mouse clamping. A raw struct
    // property can tear (new Width paired with old Height). Publishing an immutable
    // holder object makes both read and write single atomic reference operations.
    private sealed record ScreenSizeHolder(ScreenSize Value);
    private volatile ScreenSizeHolder _screenSize = new(default(ScreenSize));

    public ScreenSize ScreenSize
    {
        get => _screenSize.Value;
        private set => _screenSize = new ScreenSizeHolder(value);
    }

    // ── IScreenCapturer ───────────────────────────────────────────────────

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        // GOTCHA (TDD §9 / GOTCHAS): XInitThreads MUST precede XOpenDisplay
        // when X11 is used from multiple threads.
        X11.XInitThreads();

        _display = X11.XOpenDisplay(null);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("XOpenDisplay failed. Is DISPLAY set correctly?");

        // Install the global X11 error handler once, as early as possible after
        // XOpenDisplay, so errors from ANY subsequent X call (SHM, XTest, etc.) are
        // logged instead of silently killing the process via Xlib's default exit().
        X11ErrorTracker.Install(_logger);

        _screen     = X11.XDefaultScreen(_display);
        _rootWindow = X11.XDefaultRootWindow(_display);
        var w       = X11.XDisplayWidth(_display, _screen);
        var h       = X11.XDisplayHeight(_display, _screen);
        ScreenSize  = new ScreenSize(w, h);

        _logger.LogInformation("X11 display opened: {W}x{H} on screen {Screen}", w, h, _screen);

        // Try to set up MIT-SHM
        if (XShm.XShmQueryExtension(_display))
        {
            // Audit L8: register the MIT-SHM major opcode so X11ErrorTracker only flags
            // ShmErrorOccurred for errors raised by MIT-SHM requests — errors from other
            // connections/requests (e.g. XTest) no longer poison SHM (re-)init checks.
            if (X11.XQueryExtension(_display, "MIT-SHM", out var shmOpcode, out _, out _))
                X11ErrorTracker.ShmMajorOpcode = shmOpcode;

            if (TryInitSHM(w, h))
            {
                _useSHM = true;
                _logger.LogInformation("Using MIT-SHM for screen capture");
            }
            else
            {
                _logger.LogWarning("MIT-SHM available but setup failed — falling back to XGetImage");
            }
        }
        else
        {
            _logger.LogWarning("MIT-SHM not available — using slower XGetImage fallback");
        }

        _initialized = true;
    }

    /// <summary>
    /// Capture one frame. Returns <c>null</c> on transient failure (caller should skip frame).
    /// </summary>
    /// <remarks>
    /// Also detects live resolution changes (xrandr rotate/resize while agent is running).
    /// Resolution is checked every <see cref="ResolutionCheckInterval"/> frames via
    /// <c>XGetGeometry</c> — a real X server round-trip that always reflects the current
    /// root-window size. Do NOT use <c>XDisplayWidth</c>/<c>XDisplayHeight</c> here: those
    /// are client-side Xlib macros cached at XOpenDisplay() and never updated by RandR.
    /// Reinit happens on the same thread as Capture(), satisfying the single-thread
    /// requirement stated in the class doc comment.
    /// </remarks>
    public CapturedFrame? Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized) throw new InvalidOperationException("Call Initialize() first");

        // Detect live resolution change every ResolutionCheckInterval frames.
        // XGetGeometry is a real server round-trip (~100–300 µs on LAN) — throttling to
        // once per second at 15 fps keeps per-frame overhead negligible.
        _framesSinceResolutionCheck++;
        if (_framesSinceResolutionCheck >= ResolutionCheckInterval)
        {
            _framesSinceResolutionCheck = 0;
        }

        uint currentW, currentH;
        if (_framesSinceResolutionCheck == 0 &&
            X11.XGetGeometry(_display, _rootWindow,
                out _, out _, out _, out currentW, out currentH, out _, out _) != 0 &&
            (currentW != (uint)ScreenSize.Width || currentH != (uint)ScreenSize.Height))
        {
            _logger.LogInformation(
                "Screen resolution changed: {OldW}x{OldH} → {NewW}x{NewH}, reinitializing capture",
                ScreenSize.Width, ScreenSize.Height, currentW, currentH);

            ScreenSize = new ScreenSize((int)currentW, (int)currentH);

            if (_useSHM)
            {
                CleanupSHM();
                if (!TryInitSHM((int)currentW, (int)currentH))
                {
                    _logger.LogWarning(
                        "SHM reinit failed after resolution change — falling back to XGetImage for this session");
                    _useSHM = false;
                }
            }
        }

        return _useSHM ? CaptureSHM() : CaptureXGetImage();
    }

    // ── SHM capture ───────────────────────────────────────────────────────

    private CapturedFrame? CaptureSHM()
    {
        // GOTCHA (TDD §9): check return value — XShmGetImage returns Bool (int), never throws
        var ok = XShm.XShmGetImage(_display, _rootWindow, _shmXImage, 0, 0, X11.AllPlanes);
        if (!ok)
        {
            _logger.LogWarning("XShmGetImage returned false — skipping frame");
            return null;
        }

        var img = Marshal.PtrToStructure<XImageHeader>(_shmXImage);
        return CopyPixels(img);
    }

    // ── XGetImage fallback ────────────────────────────────────────────────

    private CapturedFrame? CaptureXGetImage()
    {
        var w = (uint)ScreenSize.Width;
        var h = (uint)ScreenSize.Height;

        var ximagePtr = X11.XGetImage(_display, _rootWindow, 0, 0, w, h, X11.AllPlanes, X11.ZPixmap);
        if (ximagePtr == IntPtr.Zero)
        {
            _logger.LogWarning("XGetImage returned null — skipping frame");
            return null;
        }

        try
        {
            var img = Marshal.PtrToStructure<XImageHeader>(ximagePtr);
            return CopyPixels(img);
        }
        finally
        {
            X11.XDestroyImage(ximagePtr);
        }
    }

    // ── Pixel copy ────────────────────────────────────────────────────────

    private CapturedFrame CopyPixels(in XImageHeader img)
    {
        var stride     = img.BytesPerLine;
        var dataLength = stride * img.Height;

        // Audit Q3: reuse one buffer across frames instead of allocating ~8MB/frame
        // (1080p ×15fps ≈ 120MB/s of Gen0/LOH garbage). Grow-only; content is fully
        // overwritten each frame up to dataLength.
        if (_pixelBuffer.Length < dataLength)
            _pixelBuffer = GC.AllocateUninitializedArray<byte>(dataLength);
        Marshal.Copy(img.Data, _pixelBuffer, 0, dataLength);

        return new CapturedFrame
        {
            PixelData   = _pixelBuffer,
            Width       = img.Width,
            Height      = img.Height,
            BytesPerRow = stride,
        };
    }

    // ── SHM init / teardown ───────────────────────────────────────────────

    private bool TryInitSHM(int w, int h)
    {
        try
        {
            var visual = X11.XDefaultVisual(_display, _screen);
            var depth  = X11.XDefaultDepth(_display, _screen);

            // Bytes per pixel assumption: depth 24/32 → 4 bytes. depth 16 → 2 bytes.
            var bpp    = depth > 16 ? 4 : 2;
            var size   = (UIntPtr)(w * h * bpp);

            _shmId = XShm.shmget(XShm.IPC_PRIVATE, size, XShm.IPC_CREAT | XShm.SHM_MODE);
            if (_shmId == -1) return false;

            _shmAddr = XShm.shmat(_shmId, IntPtr.Zero, 0);
            // shmat returns (void*)-1 on failure
            if (_shmAddr == new IntPtr(-1)) { CleanupSHM(); return false; }

            _shmInfo = new XShmSegmentInfo
            {
                shmid    = _shmId,
                shmaddr  = _shmAddr,
                readOnly = 0,
            };

            _shmXImage = XShm.XShmCreateImage(_display, visual, depth,
                X11.ZPixmap, _shmAddr, ref _shmInfo, (uint)w, (uint)h);
            if (_shmXImage == IntPtr.Zero) { CleanupSHM(); return false; }

            // Reset the shared error flag immediately before XShmAttach.
            // The global handler (X11ErrorTracker) is already installed at this point
            // (set up in Initialize()). Any BadAccess the X server sends asynchronously
            // will set ShmErrorOccurred = true via that handler.
            X11ErrorTracker.ShmErrorOccurred = false;

            if (!XShm.XShmAttach(_display, ref _shmInfo)) { CleanupSHM(); return false; }

            // XSync flushes the request queue and waits for all pending replies and error
            // events — forces any async BadAccess to arrive and trigger the handler before
            // we check the flag below.
            X11.XSync(_display, false);

            if (X11ErrorTracker.ShmErrorOccurred)
            {
                _logger.LogWarning(
                    "XShmAttach rejected by X server (async BadAccess) — " +
                    "MIT-SHM not permitted on this display. Falling back to XGetImage.");
                CleanupSHM();
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryInitSHM exception");
            CleanupSHM();
            return false;
        }
    }

    private void CleanupSHM()
    {
        if (_display != IntPtr.Zero && _shmXImage != IntPtr.Zero)
        {
            try { XShm.XShmDetach(_display, ref _shmInfo); } catch { }
            try { X11.XDestroyImage(_shmXImage); } catch { }
            _shmXImage = IntPtr.Zero;
        }
        if (_shmAddr != IntPtr.Zero && _shmAddr != new IntPtr(-1))
        {
            try { XShm.shmdt(_shmAddr); } catch { }
            _shmAddr = IntPtr.Zero;
        }
        if (_shmId != -1)
        {
            try { XShm.shmctl(_shmId, XShm.IPC_RMID, IntPtr.Zero); } catch { }
            _shmId = -1;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CleanupSHM();
        if (_display != IntPtr.Zero)
        {
            X11.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }
}
