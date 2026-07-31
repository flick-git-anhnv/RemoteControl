using Tmds.DBus;

namespace IPGS.RemoteControl.ZcuAgent.Wayland;

// ── Tmds.DBus proxy interfaces for GNOME Shell's private Mutter D-Bus API ────
//
// These map org.gnome.Mutter.ScreenCast / org.gnome.Mutter.RemoteDesktop, the
// same private (non-portal) interfaces gnome-remote-desktop uses internally to
// implement GNOME's built-in VNC/RDP backend. Because the ZcuAgent runs as a
// regular process in the SAME login session as the kiosk desktop (not inside a
// sandboxed/Flatpak app), it can call these directly on the session bus without
// triggering the xdg-desktop-portal consent dialog that a sandboxed app would
// get — required since the kiosk runs unattended (no user to click "Allow").
//
// ⚠️ VERIFICATION NEEDED ON REAL HARDWARE: these interface/method names come
// from Mutter's own D-Bus interface XML (data/dbus-interfaces/org.gnome.Mutter.*
// .xml) as of the GNOME 40–45 era, matching the kiosk's GNOME Shell 42 (Ubuntu
// 22.04). Confirm the exact signatures on the target machine BEFORE relying on
// this in production:
//   busctl --user introspect org.gnome.Shell /org/gnome/Mutter/ScreenCast
//   busctl --user introspect org.gnome.Shell /org/gnome/Mutter/RemoteDesktop
// If a signature differs, Tmds.DBus throws at the first call (not at compile
// time), so treat any Wayland session start failure as "re-check introspection
// output against the interfaces below."

[DBusInterface("org.gnome.Mutter.ScreenCast")]
public interface IScreenCastService : IDBusObject
{
    Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> properties);
}

[DBusInterface("org.gnome.Mutter.ScreenCast.Session")]
public interface IScreenCastSession : IDBusObject
{
    /// <summary>connector = output name (e.g. "eDP-1"); empty string records the primary monitor.</summary>
    Task<ObjectPath> RecordMonitorAsync(string connector, IDictionary<string, object> properties);

    /// <summary>
    /// ⚠️ VERIFIED on hardware (GNOME Shell 42.9): do NOT call this when the session was
    /// created with a "remote-desktop-session-id" pairing — it throws
    /// <c>Must be started from remote desktop session</c>. Call
    /// <see cref="IRemoteDesktopSession.StartAsync"/> instead; it starts the paired
    /// ScreenCast session automatically. Kept here only for the (untested) case of an
    /// unpaired/standalone ScreenCast session.
    /// </summary>
    Task StartAsync();

    /// <summary>⚠️ Same caveat as <see cref="StartAsync"/> — use <see cref="IRemoteDesktopSession.StopAsync"/> instead when paired.</summary>
    Task StopAsync();

    Task<IDisposable> WatchClosedAsync(Action handler);
}

[DBusInterface("org.gnome.Mutter.ScreenCast.Stream")]
public interface IScreenCastStream : IDBusObject
{
    /// <summary>
    /// Fired once the PipeWire node backing this stream is ready. The u32 is the
    /// PipeWire node id to target from a pipewiresrc element (path=&lt;node_id&gt;).
    /// </summary>
    Task<IDisposable> WatchPipeWireStreamAddedAsync(Action<uint> handler);
}

[DBusInterface("org.gnome.Mutter.RemoteDesktop")]
public interface IRemoteDesktopService : IDBusObject
{
    Task<ObjectPath> CreateSessionAsync();
}

[DBusInterface("org.gnome.Mutter.RemoteDesktop.Session")]
public interface IRemoteDesktopSession : IDBusObject
{
    Task StartAsync();
    Task StopAsync();

    /// <summary>
    /// Absolute pointer motion within the coordinate space of the given ScreenCast
    /// stream — links input injection to the recorded monitor.
    /// ⚠️ VERIFIED on hardware: the first parameter's D-Bus signature is <c>s</c>
    /// (plain string), NOT <c>o</c> (object path) — even though the value passed is the
    /// ScreenCast stream's object path string. Declaring this as <see cref="ObjectPath"/>
    /// throws <c>InvalidArgs: Type of message, "(odd)", does not match expected type
    /// "(sdd)"</c>. Pass <c>streamPath.ToString()</c> from the caller.
    /// </summary>
    Task NotifyPointerMotionAbsoluteAsync(string stream, double x, double y);

    /// <summary>
    /// Button code per Linux evdev (BTN_LEFT=0x110, BTN_RIGHT=0x111, BTN_MIDDLE=0x112).
    /// ⚠️ VERIFIED on hardware: signature is <c>(ib)</c> — signed int, NOT uint (a <c>(ub)</c>
    /// call throws InvalidArgs: expected type "(ib)"). <see langword="int"/> here is correct as-is.
    /// </summary>
    Task NotifyPointerButtonAsync(int button, bool state);

    /// <summary>⚠️ VERIFIED on hardware: signature <c>(ui)</c> — matches as declared.</summary>
    Task NotifyPointerAxisDiscreteAsync(uint axis, int steps);

    Task NotifyKeyboardKeysymAsync(uint keysym, bool state);

    Task<IDisposable> WatchClosedAsync(Action handler);

    /// <summary>
    /// ⚠️ VERIFIED on hardware: <c>SessionId</c> is a D-Bus PROPERTY (read via
    /// org.freedesktop.DBus.Properties.GetAll), NOT a plain method — a
    /// <c>Task&lt;string&gt; GetSessionIdAsync()</c> declared directly on this interface
    /// throws <c>UnknownMethod: No such method "GetSessionId"</c> because Tmds.DBus maps
    /// undecorated interface methods straight to D-Bus method calls with the matching
    /// name, not to property getters. Use <see cref="GetAllAsync"/> and read the
    /// "SessionId" key instead — this is how <see cref="MutterSessionManager"/> gets the
    /// id used for the ScreenCast "remote-desktop-session-id" pairing property.
    /// </summary>
    Task<IDictionary<string, object>> GetAllAsync();
}
