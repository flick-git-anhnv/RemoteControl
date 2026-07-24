using IPGS.RemoteControl.Protocol;
using IPGS.RemoteControl.ZcuAgent.Interop;
using Microsoft.Extensions.Logging;

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
    private IntPtr _display = IntPtr.Zero;
    private int    _screen;
    private bool   _initialized;
    private bool   _disposed;

    public MouseInjector(ILogger<MouseInjector> logger) => _logger = logger;

    // ── IMouseInjector ────────────────────────────────────────────────────

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        // Separate display connection — capturer already called XInitThreads() globally
        _display = X11.XOpenDisplay(null);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("MouseInjector: XOpenDisplay failed");

        _screen = X11.XDefaultScreen(_display);
        _logger.LogInformation("MouseInjector: XTest display opened on screen {Screen}", _screen);
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
        X11.XFlush(_display);
        // XSync forces the X server to process the request and send back any pending
        // error events (e.g. BadAccess if XTest is not permitted on this display).
        // Without this, a rejection would be silently dropped; with the global
        // X11ErrorTracker handler installed, it now appears in the log immediately.
        X11.XSync(_display, false);
    }

    /// <summary>
    /// Press or release a mouse button. Calls XFlush immediately after — required
    /// for the event to be dispatched. See GOTCHA note in class-level doc.
    /// </summary>
    public void Button(MouseButton button, bool isDown)
    {
        if (!_initialized || _disposed) return;
        XTest.XTestFakeButtonEvent(_display, (uint)button, isDown, 0);
        // GOTCHA: XFlush MUST be called after XTestFakeButtonEvent
        X11.XFlush(_display);
        // XSync: same rationale as Move() — surface any async X error immediately.
        X11.XSync(_display, false);
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
