using IPGS.RemoteControl.WinAgent.Native;
using IPGS.RemoteControl.ZcuAgent;
using Microsoft.Extensions.Logging;
using static IPGS.RemoteControl.WinAgent.Native.NativeMethods;

namespace IPGS.RemoteControl.WinAgent.Capture;

/// <summary>
/// Chụp toàn bộ virtual screen (gộp mọi màn hình) bằng GDI <c>BitBlt</c> vào một DIB section 32bpp top-down
/// (BGRX) rồi đưa con trỏ pixel cho bộ nén JPEG — cùng hợp đồng zero-copy với MIT-SHM bên Linux.
/// <para>
/// Khung hình có gốc (0,0) tại góc trên-trái của virtual screen (toạ độ màn hình phụ nằm bên trái/trên màn hình
/// chính có thể âm trong Windows — <see cref="VirtualScreen"/> trừ đi gốc đó; chuột/monitor list dùng cùng gốc).
/// </para>
/// <para>
/// GDI BitBlt KHÔNG vẽ con trỏ chuột → vẽ thêm bằng <c>DrawIconEx</c>. Cần process DPI-aware (xem Program.cs +
/// app.manifest) để kích thước chụp = pixel vật lý, không bị Windows co theo scale 125%/150%.
/// </para>
/// <para>
/// Thread safety: Initialize và Capture phải gọi từ cùng luồng (ClientSession gọi từ capture loop).
/// </para>
/// </summary>
internal sealed class GdiScreenCapturer : IScreenCapturer
{
    private const int ResolutionCheckInterval = 15;
    private const int ReacquireDcAfterFailures = 10;

    private readonly ILogger<GdiScreenCapturer> _logger;

    private IntPtr _screenDc;
    private IntPtr _memDc;
    private IntPtr _bitmap;
    private IntPtr _oldBitmap;
    private IntPtr _bits;
    private int _left, _top, _width, _height;

    private int  _framesSinceResolutionCheck;
    private int  _consecutiveFailures;
    private long _totalFailures;
    private IntPtr _cachedCursor;
    private int _hotspotX, _hotspotY;

    private bool _initialized;
    private bool _disposed;

    // Cùng lý do với X11ScreenCapturer (audit L9): đọc ScreenSize từ luồng khác phải nguyên tử.
    private sealed record ScreenSizeHolder(ScreenSize Value);
    private volatile ScreenSizeHolder _screenSize = new(default(ScreenSize));

    public GdiScreenCapturer(ILogger<GdiScreenCapturer> logger) => _logger = logger;

    public ScreenSize ScreenSize => _screenSize.Value;

    /// <summary>Hình chữ nhật virtual screen hiện tại (gốc có thể âm). Dùng chung với chuột / danh sách màn hình.</summary>
    public static (int Left, int Top, int Width, int Height) VirtualScreen() => (
        GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
        GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN));

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        _screenDc = GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero)
            throw new InvalidOperationException("GetDC(NULL) thất bại — không có desktop để chụp (đang chạy ở Session 0?)");

        BuildSurface();
        _initialized = true;
    }

    public CapturedFrame? Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized) throw new InvalidOperationException("Call Initialize() first");

        if (++_framesSinceResolutionCheck >= ResolutionCheckInterval)
        {
            _framesSinceResolutionCheck = 0;
            var (l, t, w, h) = VirtualScreen();
            if (w > 0 && h > 0 && (l != _left || t != _top || w != _width || h != _height))
            {
                _logger.LogInformation(
                    "Virtual screen đổi: {OldW}x{OldH}@({OldL},{OldT}) → {NewW}x{NewH}@({NewL},{NewT}) — dựng lại bề mặt chụp",
                    _width, _height, _left, _top, w, h, l, t);
                BuildSurface();
            }
        }

        if (!BitBlt(_memDc, 0, 0, _width, _height, _screenDc, _left, _top, SRCCOPY | CAPTUREBLT))
        {
            // Thường gặp khi desktop đang chuyển sang UAC / màn hình khoá (secure desktop) hoặc đổi độ phân giải.
            _totalFailures++;
            if (_totalFailures == 1 || _totalFailures % 100 == 0)
                _logger.LogWarning("BitBlt thất bại (lần {N}, lỗi Win32 {Err}) — bỏ qua khung", _totalFailures,
                    System.Runtime.InteropServices.Marshal.GetLastWin32Error());

            if (++_consecutiveFailures >= ReacquireDcAfterFailures)
            {
                _consecutiveFailures = 0;
                ReacquireScreenDc();
            }
            return null;
        }
        _consecutiveFailures = 0;

        DrawCursor();

        return new CapturedFrame
        {
            PixelData   = [],
            PixelPtr    = _bits,
            Width       = _width,
            Height      = _height,
            BytesPerRow = _width * 4,
        };
    }

    // ── Bề mặt chụp ───────────────────────────────────────────────────────

    private void BuildSurface()
    {
        var (l, t, w, h) = VirtualScreen();
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"Virtual screen không hợp lệ ({w}x{h}) — không có màn hình/desktop khả dụng");

        ReleaseSurface();

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize        = (uint)System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth       = w,
                biHeight      = -h,           // âm = top-down, khớp bố cục X11/Skia
                biPlanes      = 1,
                biBitCount    = 32,
                biCompression = BI_RGB,
            },
        };

        _memDc = CreateCompatibleDC(_screenDc);
        if (_memDc == IntPtr.Zero) throw new InvalidOperationException("CreateCompatibleDC thất bại");

        _bitmap = CreateDIBSection(_screenDc, ref bmi, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
        if (_bitmap == IntPtr.Zero || _bits == IntPtr.Zero)
        {
            ReleaseSurface();
            throw new InvalidOperationException($"CreateDIBSection {w}x{h} thất bại (hết bộ nhớ GDI?)");
        }

        _oldBitmap = SelectObject(_memDc, _bitmap);
        _left = l; _top = t; _width = w; _height = h;
        _screenSize = new ScreenSizeHolder(new ScreenSize(w, h));

        _logger.LogInformation("GDI capture: virtual screen {W}x{H} tại ({L},{T})", w, h, l, t);
    }

    private void ReleaseSurface()
    {
        if (_memDc != IntPtr.Zero && _oldBitmap != IntPtr.Zero) SelectObject(_memDc, _oldBitmap);
        if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
        if (_memDc != IntPtr.Zero) DeleteDC(_memDc);
        _bitmap = _memDc = _oldBitmap = _bits = IntPtr.Zero;
    }

    /// <summary>Lấy lại DC màn hình (sau khi chuyển desktop UAC/khoá máy, DC cũ có thể không còn dùng được).</summary>
    private void ReacquireScreenDc()
    {
        try
        {
            ReleaseSurface();
            if (_screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = GetDC(IntPtr.Zero);
            if (_screenDc != IntPtr.Zero) BuildSurface();
            _logger.LogInformation("GDI capture: đã lấy lại DC màn hình");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không lấy lại được DC màn hình — sẽ thử lại");
        }
    }

    // ── Con trỏ chuột ─────────────────────────────────────────────────────

    private void DrawCursor()
    {
        var ci = new CURSORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci) || (ci.flags & CURSOR_SHOWING) == 0 || ci.hCursor == IntPtr.Zero) return;

        if (ci.hCursor != _cachedCursor)
        {
            _hotspotX = _hotspotY = 0;
            if (GetIconInfo(ci.hCursor, out var info))
            {
                _hotspotX = (int)info.xHotspot;
                _hotspotY = (int)info.yHotspot;
                if (info.hbmMask  != IntPtr.Zero) DeleteObject(info.hbmMask);
                if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
            }
            _cachedCursor = ci.hCursor;
        }

        DrawIconEx(_memDc,
            ci.ptScreenPos.x - _hotspotX - _left,
            ci.ptScreenPos.y - _hotspotY - _top,
            ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseSurface();
        if (_screenDc != IntPtr.Zero) { ReleaseDC(IntPtr.Zero, _screenDc); _screenDc = IntPtr.Zero; }
    }
}
