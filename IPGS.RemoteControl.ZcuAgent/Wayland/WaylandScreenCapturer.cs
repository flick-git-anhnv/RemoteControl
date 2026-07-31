using System.Diagnostics;
using System.Text.RegularExpressions;
using IPGS.RemoteControl.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IPGS.RemoteControl.ZcuAgent.Wayland;

/// <summary>
/// Captures frames on GNOME Wayland via the Mutter ScreenCast D-Bus session
/// (<see cref="MutterSessionManager"/>), reading raw BGRA video from the resulting
/// PipeWire stream through a <c>gst-launch-1.0</c> subprocess.
/// <para>
/// Why a subprocess instead of a native libpipewire binding: negotiating a PipeWire
/// video stream (format enumeration, buffer import, SPA pod parsing) directly via
/// P/Invoke is a large amount of low-level, poorly-documented surface. GStreamer's
/// <c>pipewiresrc</c> element already does that negotiation correctly and is a
/// standard package (<c>gstreamer1.0-pipewire</c>) — shelling out to it trades a
/// small amount of per-frame IPC overhead for a much smaller, verifiable surface.
/// </para>
/// <para>
/// ⚠️ Runtime dependency: the kiosk image MUST have <c>gstreamer1.0-tools</c>,
/// <c>gstreamer1.0-plugins-base</c> (videoconvert/videorate) and
/// <c>gstreamer1.0-pipewire</c> installed, e.g.
/// <c>apt-get install -y gstreamer1.0-tools gstreamer1.0-plugins-base gstreamer1.0-pipewire</c>.
/// </para>
/// <para>
/// ⚠️ VERIFIED on hardware (192.168.21.230, GNOME Shell 42.9, 2026-07-31): the pixel
/// stream does NOT go over the process's stdout — see the FIFO note on
/// <see cref="StartGStreamerPipeline"/> for why.
/// </para>
/// <para>
/// GOTCHA (push vs pull model): unlike <c>X11ScreenCapturer</c> where <see cref="Capture"/>
/// actively pulls the CURRENT screen state via XShmGetImage, this capturer reads from a
/// continuously-running pipeline. The <c>videorate</c> element throttles the PipeWire
/// stream to <see cref="RemoteControlConstants.TargetFps"/> so <see cref="Capture"/>
/// blocks for at most ~1/TargetFps seconds waiting for the next frame — it does not
/// return instantaneously like the X11 pull. This matches the caller's expectation
/// (called once per frame interval by the encode/send loop) but would behave
/// differently if called faster than the pipeline produces frames.
/// </para>
/// </summary>
internal sealed class WaylandScreenCapturer : IScreenCapturer
{
    private static readonly Regex CapsRegex =
        new(@"width=\(int\)(\d+),\s*height=\(int\)(\d+)", RegexOptions.Compiled);

    private readonly MutterSessionManager _session;
    private readonly int _targetFps;
    private readonly ILogger<WaylandScreenCapturer> _logger;

    private Process?    _gstProcess;
    private FileStream? _fifoStream;
    private string?     _fifoPath;

    private byte[] _pixelBuffer = [];
    private bool   _initialized;
    private bool   _disposed;

    // Audit L9 pattern reused from X11ScreenCapturer: publish ScreenSize as an
    // immutable holder so cross-thread reads never observe a torn (Width,Height) pair.
    private sealed record ScreenSizeHolder(ScreenSize Value);
    private volatile ScreenSizeHolder _screenSize = new(default(ScreenSize));

    public ScreenSize ScreenSize
    {
        get => _screenSize.Value;
        private set => _screenSize = new ScreenSizeHolder(value);
    }

    public WaylandScreenCapturer(
        MutterSessionManager session, IOptions<AgentOptions> options, ILogger<WaylandScreenCapturer> logger)
    {
        _session    = session;
        // BUG FIX: an earlier version hardcoded RemoteControlConstants.TargetFps (15) into
        // the gst pipeline's videorate target, ignoring the configured AgentOptions.TargetFps
        // that ClientSession actually paces its Capture() calls against — a user-configured
        // TargetFps != 15 (e.g. 30) would silently mismatch the two, and was also suspected
        // as a contributor to the delay reported on 192.168.21.96 (2026-07-31).
        _targetFps  = Math.Max(1, options.Value.TargetFps);
        _logger     = logger;
    }

    // ── IScreenCapturer ───────────────────────────────────────────────────

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        // Blocking on purpose: Initialize() is called once at startup from
        // RemoteControlHostedService.ExecuteAsync (background-service thread, not a
        // UI thread), same pattern the X11 path uses for its own blocking Xlib calls.
        _session.StartAsync().GetAwaiter().GetResult();

        var (width, height) = StartGStreamerPipeline(_session.PipeWireNodeId);
        ScreenSize = new ScreenSize(width, height);

        _logger.LogInformation(
            "WaylandScreenCapturer: capturing {W}x{H} from PipeWire node {NodeId} via gst-launch-1.0",
            width, height, _session.PipeWireNodeId);

        _initialized = true;
    }

    public CapturedFrame? Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized) throw new InvalidOperationException("Call Initialize() first");

        var stride    = ScreenSize.Width * 4;
        var frameSize = stride * ScreenSize.Height;

        if (_pixelBuffer.Length < frameSize)
            _pixelBuffer = GC.AllocateUninitializedArray<byte>(frameSize);

        try
        {
            var offset = 0;
            while (offset < frameSize)
            {
                var read = _fifoStream!.Read(_pixelBuffer, offset, frameSize - offset);
                if (read == 0)
                {
                    _logger.LogWarning("WaylandScreenCapturer: gst-launch-1.0 FIFO closed — pipeline died, skipping frame");
                    return null;
                }
                offset += read;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WaylandScreenCapturer: read from gst-launch-1.0 FIFO failed — skipping frame");
            return null;
        }

        return new CapturedFrame
        {
            PixelData   = _pixelBuffer,
            Width       = ScreenSize.Width,
            Height      = ScreenSize.Height,
            BytesPerRow = stride,
        };
    }

    // ── GStreamer pipeline launch ─────────────────────────────────────────

    /// <summary>
    /// Starts <c>gst-launch-1.0</c> targeting the given PipeWire node, and blocks until
    /// the negotiated video caps (width/height) are parsed out of its verbose (-v)
    /// output. There is no other cheap way to learn the monitor's resolution from this
    /// pipeline before the first frame arrives.
    /// <para>
    /// ⚠️ VERIFIED on hardware: an earlier version wrote pixel data to <c>fdsink fd=1</c>
    /// (the process's own stdout) while ALSO parsing <c>-v</c> caps-negotiation text from
    /// that same stdout — this GStreamer build prints <c>-v</c> messages to STDOUT, not
    /// stderr, so the text and binary pixel bytes interleaved into one corrupted stream
    /// (confirmed via <c>file /tmp/out.raw</c> reporting "ASCII text" instead of binary,
    /// and the captured bytes literally starting with "Pipeline is live and does not need
    /// PREROLL ..."). Fixed by routing pixel data to a named pipe (FIFO) via
    /// <c>filesink location=&lt;fifo&gt;</c> instead, keeping the process's own stdout
    /// free for the <c>-v</c> caps text. The FIFO read-open and the caps-text wait run
    /// concurrently (not sequentially) to avoid a rendezvous deadlock: opening a FIFO for
    /// reading blocks until a writer connects, and gst-launch's <c>filesink</c> opening
    /// the FIFO for writing blocks until a reader connects — if one waited fully for the
    /// other first, neither would ever proceed.
    /// </para>
    /// </summary>
    private (int width, int height) StartGStreamerPipeline(uint nodeId)
    {
        _fifoPath = Path.Combine(Path.GetTempPath(), $"ipgs-zcuagent-capture-{Environment.ProcessId}.fifo");
        if (File.Exists(_fifoPath)) File.Delete(_fifoPath);

        using (var mkfifo = Process.Start(new ProcessStartInfo("mkfifo", _fifoPath) { UseShellExecute = false }))
        {
            mkfifo!.WaitForExit();
            if (mkfifo.ExitCode != 0)
                throw new InvalidOperationException($"WaylandScreenCapturer: mkfifo failed for '{_fifoPath}' (exit {mkfifo.ExitCode})");
        }

        var psi = new ProcessStartInfo
        {
            FileName               = "gst-launch-1.0",
            // "queue leaky=downstream" right after the source: if WaylandScreenCapturer.Capture()
            // (the FIFO reader) ever falls even briefly behind the pipeline's production rate,
            // this drops the OLDEST buffered frame instead of piling frames up — without it, a
            // GStreamer queue defaults to blocking (not dropping), so a transient slowdown turns
            // into an ever-growing backlog of stale frames that never catches back up to
            // real time. Suspected contributor to the growing capture delay reported on
            // 192.168.21.96 (2026-07-31) — standard fix for live/lossy real-time GStreamer
            // pipelines (same pattern used for webcam preview / screen-share latency).
            Arguments              = "-v pipewiresrc path=" + nodeId +
                                     " ! queue leaky=downstream max-size-buffers=1 max-size-bytes=0 max-size-time=0" +
                                     " ! videoconvert ! videorate" +
                                     $" ! video/x-raw,format=BGRA,framerate={_targetFps}/1" +
                                     $" ! filesink location={_fifoPath} sync=false",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        _gstProcess = Process.Start(psi) ??
            throw new InvalidOperationException(
                "WaylandScreenCapturer: failed to start gst-launch-1.0 — is gstreamer1.0-tools installed?");

        // Start opening the FIFO for reading CONCURRENTLY with the caps wait below —
        // see the deadlock note in the doc comment above for why this can't be sequential.
        var fifoOpenTask = Task.Run(() => new FileStream(_fifoPath, FileMode.Open, FileAccess.Read));

        var capsReady = new TaskCompletionSource<(int, int)>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await _gstProcess.StandardOutput.ReadLineAsync()) != null)
                {
                    var m = CapsRegex.Match(line);
                    if (m.Success)
                        capsReady.TrySetResult((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
                }
            }
            catch
            {
                // Process likely exited — capsReady.Task will time out below and surface a clear error.
            }
        });

        // Drain stderr concurrently so a full pipe buffer can't stall the process if it emits errors.
        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await _gstProcess.StandardError.ReadLineAsync()) != null)
                    _logger.LogWarning("gst-launch-1.0 stderr: {Line}", line);
            }
            catch { }
        });

        if (!capsReady.Task.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException(
                "WaylandScreenCapturer: timed out waiting for gst-launch-1.0 to negotiate video caps. " +
                "Check that gstreamer1.0-pipewire is installed and the PipeWire node id is valid " +
                "(run the same gst-launch-1.0 command by hand on the kiosk to see the raw error).");
        }

        if (!fifoOpenTask.Wait(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("WaylandScreenCapturer: timed out opening the capture FIFO for reading.");
        _fifoStream = fifoOpenTask.Result;

        return capsReady.Task.Result;
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _fifoStream?.Dispose(); } catch { }

        // GOTCHA: gst-launch-1.0 does not react to its FIFO reader closing by exiting —
        // it must be killed explicitly, including any child processes it may have spawned.
        if (_gstProcess is { HasExited: false })
        {
            try { _gstProcess.Kill(entireProcessTree: true); } catch { }
        }
        _gstProcess?.Dispose();

        if (_fifoPath is not null)
        {
            try { File.Delete(_fifoPath); } catch { }
        }

        // NOTE: MutterSessionManager is a separately-registered DI singleton shared with
        // the Wayland input injectors — it is NOT disposed here, only by the host's
        // own singleton disposal at shutdown.
    }
}
