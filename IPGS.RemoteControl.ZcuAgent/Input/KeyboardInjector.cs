using IPGS.RemoteControl.ZcuAgent.Interop;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Input;

/// <summary>
/// Injects synthetic keyboard events into the X11 session via the XTest extension.
/// Uses a separate display connection from the capturer and mouse injector to avoid
/// X11 lock contention. See TDD §17.4.
/// <para>
/// GOTCHA: XFlush must be called after XTestFakeKeyEvent — same requirement as
/// XTestFakeButtonEvent — or the event is silently dropped by the X server.
/// </para>
/// <para>
/// GOTCHA: XKeysymToKeycode returning 0 is NOT an error; it means the keysym has no
/// physical key on the current keyboard layout. Log warning + skip, do NOT throw.
/// </para>
/// <para>
/// Stuck-key mitigation: all keys currently pressed are tracked in <see cref="_downKeys"/>.
/// Call <see cref="ReleaseAllKeys"/> on session end (disconnect / BYE / timeout / exception)
/// so the ZCU desktop does not see permanently pressed modifier keys. TDD §17.4.
/// </para>
/// </summary>
internal sealed class KeyboardInjector : IKeyboardInjector
{
    private readonly ILogger<KeyboardInjector> _logger;
    private IntPtr   _display = IntPtr.Zero;
    private bool     _initialized;
    private bool     _disposed;

    /// <summary>
    /// Set of X11 keysyms currently in the "pressed" state — updated by SendKey.
    /// Used by ReleaseAllKeys to synthesize a release event for every stuck key.
    /// Not thread-safe by design: all calls come from the single receive-loop task.
    /// </summary>
    private readonly HashSet<uint> _downKeys = new();

    public KeyboardInjector(ILogger<KeyboardInjector> logger) => _logger = logger;

    // ── IKeyboardInjector ─────────────────────────────────────────────────

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        // Separate display connection — capturer already called XInitThreads() globally.
        _display = X11.XOpenDisplay(null);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("KeyboardInjector: XOpenDisplay failed");

        _logger.LogInformation("KeyboardInjector: XTest display opened for keyboard injection");
        _initialized = true;
    }

    /// <summary>
    /// Inject a key press (<paramref name="isDown"/>=true) or release (false) for the
    /// given X11 <paramref name="keysym"/>. Tracks pressed state for stuck-key protection.
    /// </summary>
    public void SendKey(uint keysym, bool isDown)
    {
        if (!_initialized || _disposed) return;

        // XKeysymToKeycode returns 0 when the keysym has no physical key on the
        // current keyboard layout — this is NOT an error, just skip gracefully.
        var keycode = X11.XKeysymToKeycode(_display, keysym);
        if (keycode == 0)
        {
            _logger.LogWarning(
                "KeyboardInjector: keysym 0x{Keysym:X} không map được keycode trên keymap hiện tại — bỏ qua",
                keysym);
            return;
        }

        // Track pressed state BEFORE sending so ReleaseAllKeys stays consistent
        // even if XTest fails (rare, but defensive).
        if (isDown)
            _downKeys.Add(keysym);
        else
            _downKeys.Remove(keysym);

        XTest.XTestFakeKeyEvent(_display, keycode, isDown, 0);
        // GOTCHA: XFlush is mandatory — same rule as XTestFakeButtonEvent in MouseInjector.
        X11.XFlush(_display);
        // XSync surfaces any async X errors immediately (e.g. BadAccess).
        X11.XSync(_display, false);
    }

    /// <summary>
    /// Release every key that is currently tracked as pressed.
    /// Called on session end (disconnect / BYE / timeout / exception) to prevent
    /// "stuck modifier keys" (Shift, Ctrl, Alt, …) on the ZCU desktop. TDD §17.4.
    /// </summary>
    public void ReleaseAllKeys()
    {
        if (!_initialized || _disposed || _downKeys.Count == 0) return;

        _logger.LogInformation(
            "KeyboardInjector: releasing {Count} stuck key(s) on session end", _downKeys.Count);

        // Snapshot the set before iterating because SendKey modifies _downKeys.
        var stuck = _downKeys.ToArray();
        foreach (var keysym in stuck)
        {
            var keycode = X11.XKeysymToKeycode(_display, keysym);
            if (keycode == 0) continue;   // keysym no longer mappable — just discard

            XTest.XTestFakeKeyEvent(_display, keycode, isPress: false, delay: 0);
        }

        // One flush + sync covers all release events — saves round-trips.
        X11.XFlush(_display);
        X11.XSync(_display, false);

        _downKeys.Clear();
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Best-effort release before closing display (display still open here).
        if (_initialized && _downKeys.Count > 0)
            ReleaseAllKeys();

        if (_display != IntPtr.Zero)
        {
            X11.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }
}
