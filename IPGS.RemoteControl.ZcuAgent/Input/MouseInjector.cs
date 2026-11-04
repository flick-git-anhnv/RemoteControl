using IPGS.RemoteControl.Protocol;
using IPGS.RemoteControl.ZcuAgent.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IPGS.RemoteControl.ZcuAgent.Input;

/// <summary>
/// Injects synthetic mouse events into the X11 session via the XTest extension.
/// Uses a separate display connection from the capturer to avoid X11 lock contention.
/// See TDD §9.3.
/// <para>
/// GOTCHA: XFlush must be called after XTestFakeButtonEvent to actually dispatch the
/// event to the X server. Skipping it causes silent drops. TDD §9 gotcha note.
/// </para>
/// </summary>
internal sealed class MouseInjector : IMouseInjector
{
    private readonly ILogger<MouseInjector> _logger;
    private readonly IOptions<AgentOptions>? _options;
    private IntPtr _display = IntPtr.Zero;
    private int    _screen;
    private bool   _initialized;
    private bool   _disposed;

    public MouseInjector(ILogger<MouseInjector> logger, IOptions<AgentOptions>? options = null)
    {
        _logger = logger;
        _options = options;
    }

    // ── IMouseInjector ────────────────────────────────────────────────────

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        // Separate display connection — capturer already called XInitThreads() globally
        var preferred = _options?.Value?.Display;
        var (handle, resolved) = X11DisplayResolver.OpenAvailableDisplay(preferred, _logger);
        _display = handle;

        _screen = X11.XDefaultScreen(_display);
        _logger.LogInformation("MouseInjector: XTest display '{Display}' opened on screen {Screen}", resolved, _screen);
        _initialized = true;
    }

    /// <summary>
    /// Move pointer to (<paramref name="x"/>, <paramref name="y"/>) in ZCU screen space.
    /// Coordinates out of range are clamped by the server.
    /// </summary>
    public void Move(int x, int y)
    {
        if (!_initialized || _disposed) return;
        XTest.XTestFakeMotionEvent(_display, -1, x, y, 0);
        // Audit F-NEW-4: XFlush is sufficient to send buffered motion events.
        // Removed XSync per-move round-trip which blocked reader loop and caused mouse lag.
        X11.XFlush(_display);
    }

    /// <summary>
    /// Press or release a mouse button. Calls XFlush immediately after — required
    /// for the event to be dispatched. See GOTCHA note in class-level doc.
    /// </summary>
    public void Button(MouseButton button, bool isDown)
    {
        if (!_initialized || _disposed) return;
        XTest.XTestFakeButtonEvent(_display, (uint)button, isDown, 0);
        // GOTCHA: XFlush MUST be called after XTestFakeButtonEvent to dispatch buffered event immediately.
        // Removed blocking XSync round-trip (same as Move) to eliminate click/scroll lag under load.
        X11.XFlush(_display);
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_display != IntPtr.Zero)
        {
            X11.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }
}
