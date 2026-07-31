using IPGS.RemoteControl.Protocol;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Wayland;

/// <summary>
/// Injects synthetic mouse events on GNOME Wayland via the Mutter RemoteDesktop D-Bus
/// session shared with <see cref="WaylandScreenCapturer"/> (see <see cref="MutterSessionManager"/>).
/// <para>
/// Unlike the X11 path (XTestFakeButtonEvent), the Mutter API has no discrete "wheel
/// button" concept — mouse wheel is a pointer AXIS event, not a button. Wheel up/down
/// is translated to one <c>NotifyPointerAxisDiscrete</c> step on the press edge only;
/// the matching release edge sent by the CCU client (mirroring X11 click semantics) is
/// a no-op here.
/// </para>
/// </summary>
internal sealed class WaylandMouseInjector : IMouseInjector
{
    private readonly MutterSessionManager _session;
    private readonly ILogger<WaylandMouseInjector> _logger;
    private bool _initialized;
    private bool _disposed;

    public WaylandMouseInjector(MutterSessionManager session, ILogger<WaylandMouseInjector> logger)
    {
        _session = session;
        _logger  = logger;
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        // Idempotent — the capturer normally starts the shared session first.
        _session.StartAsync().GetAwaiter().GetResult();
        _initialized = true;
    }

    public void Move(int x, int y)
    {
        if (!_initialized || _disposed) return;
        try
        {
            _session.RemoteDesktopSession
                .NotifyPointerMotionAbsoluteAsync(_session.StreamPath.ToString(), x, y)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WaylandMouseInjector: pointer motion notify failed");
        }
    }

    public void Button(MouseButton button, bool isDown)
    {
        if (!_initialized || _disposed) return;
        try
        {
            if (button is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (!isDown) return; // one discrete step per press edge only
                // libinput/wl_pointer axis convention: vertical axis = 0,
                // negative steps = scroll up/away from user.
                var steps = button == MouseButton.WheelUp ? -1 : 1;
                _session.RemoteDesktopSession.NotifyPointerAxisDiscreteAsync(0, steps).GetAwaiter().GetResult();
                return;
            }

            _session.RemoteDesktopSession
                .NotifyPointerButtonAsync(ToEvdevButtonCode(button), isDown)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WaylandMouseInjector: pointer button notify failed");
        }
    }

    /// <summary>Linux evdev button codes (linux/input-event-codes.h) — Mutter expects these, not X11 button numbers.</summary>
    private static int ToEvdevButtonCode(MouseButton button) => button switch
    {
        MouseButton.Left   => 0x110, // BTN_LEFT
        MouseButton.Right  => 0x111, // BTN_RIGHT
        MouseButton.Middle => 0x112, // BTN_MIDDLE
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unhandled MouseButton"),
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Shared MutterSessionManager is disposed by its own DI singleton lifetime, not here.
    }
}
