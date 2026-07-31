using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Wayland;

/// <summary>
/// Injects synthetic keyboard events on GNOME Wayland via the Mutter RemoteDesktop
/// D-Bus session shared with <see cref="WaylandScreenCapturer"/>.
/// <para>
/// Simpler than the X11 path: <c>NotifyKeyboardKeysym</c> takes the X11 keysym
/// directly — Mutter resolves it against the compositor's own xkb keymap
/// internally (virtual-keyboard-in-libei under the hood), so there is no
/// <c>XKeysymToKeycode</c> equivalent lookup or "unmappable keysym" case to handle here.
/// </para>
/// </summary>
internal sealed class WaylandKeyboardInjector : IKeyboardInjector
{
    private readonly MutterSessionManager _session;
    private readonly ILogger<WaylandKeyboardInjector> _logger;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Same stuck-key mitigation as <c>KeyboardInjector</c> (TDD §17.4).</summary>
    private readonly HashSet<uint> _downKeys = new();

    public WaylandKeyboardInjector(MutterSessionManager session, ILogger<WaylandKeyboardInjector> logger)
    {
        _session = session;
        _logger  = logger;
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        _session.StartAsync().GetAwaiter().GetResult();
        _initialized = true;
    }

    public void SendKey(uint keysym, bool isDown)
    {
        if (!_initialized || _disposed) return;

        // Track BEFORE sending, mirroring KeyboardInjector's defensive ordering so
        // ReleaseAllKeys stays consistent even if the D-Bus call below throws.
        if (isDown) _downKeys.Add(keysym);
        else        _downKeys.Remove(keysym);

        try
        {
            _session.RemoteDesktopSession.NotifyKeyboardKeysymAsync(keysym, isDown).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WaylandKeyboardInjector: keysym 0x{Keysym:X} notify failed", keysym);
        }
    }

    public void ReleaseAllKeys()
    {
        if (!_initialized || _disposed || _downKeys.Count == 0) return;

        _logger.LogInformation(
            "WaylandKeyboardInjector: releasing {Count} stuck key(s) on session end", _downKeys.Count);

        var stuck = _downKeys.ToArray();
        foreach (var keysym in stuck)
        {
            try
            {
                _session.RemoteDesktopSession.NotifyKeyboardKeysymAsync(keysym, false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WaylandKeyboardInjector: release for stuck keysym 0x{Keysym:X} failed", keysym);
            }
        }

        _downKeys.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_initialized && _downKeys.Count > 0)
            ReleaseAllKeys();
        // Shared MutterSessionManager is disposed by its own DI singleton lifetime, not here.
    }
}
