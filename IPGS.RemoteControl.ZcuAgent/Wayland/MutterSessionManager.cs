using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace IPGS.RemoteControl.ZcuAgent.Wayland;

/// <summary>
/// Owns the single paired RemoteDesktop + ScreenCast session against GNOME Shell's
/// private Mutter D-Bus API (see <see cref="MutterDBusInterfaces"/> for the interface
/// definitions and the on-hardware verification note).
/// <para>
/// Shared singleton: <see cref="WaylandScreenCapturer"/> and the Wayland input
/// injectors all reference the SAME RemoteDesktop session, because a ScreenCast
/// session created with "remote-desktop-session-id" pointing at an unrelated/second
/// RemoteDesktop session would not be granted access — Mutter only auto-approves the
/// pairing that matches. One CCU connection ⇒ one Mutter session pair, mirroring the
/// v1 "single active client" design already used by X11ScreenCapturer/TcpServer.
/// </para>
/// <para>
/// Order matters: RemoteDesktop session MUST be created (and its SessionId read)
/// BEFORE the ScreenCast session, so the "remote-desktop-session-id" property can be
/// passed. RecordMonitor() on ScreenCast sets up the stream (triggers the
/// PipeWireStreamAdded signal), then <b>only RemoteDesktopSession.Start()/Stop() are
/// called</b> — see the VERIFIED gotcha below.
/// </para>
/// <para>
/// ⚠️ VERIFIED on real hardware (192.168.21.230, GNOME Shell 42.9, 2026-07-31) via a
/// throwaway PyGObject script issuing raw D-Bus calls: once a ScreenCast session is
/// paired to a RemoteDesktop session via "remote-desktop-session-id", calling
/// <c>ScreenCastSession.Start()</c> or <c>.Stop()</c> DIRECTLY fails with
/// <c>org.freedesktop.DBus.Error.Failed: "Must be started/stopped from remote desktop
/// session"</c>. Starting/stopping the RemoteDesktop session auto-starts/stops the
/// paired ScreenCast session — the PipeWireStreamAdded signal already fires right
/// after RemoteDesktopSession.Start(), before any ScreenCast-side call. An earlier
/// version of this class called ScreenCastSession.Start()/Stop() explicitly, which
/// would have thrown at runtime; this was caught ONLY by testing against the live
/// D-Bus service, not by anything introspectable ahead of time (GNOME Shell's GJS
/// D-Bus implementation returns an empty introspection XML for these objects — no
/// method list to check signatures against beforehand).
/// </para>
/// </summary>
internal sealed class MutterSessionManager : IDisposable
{
    private const string ShellService    = "org.gnome.Shell";
    private const string ScreenCastPath  = "/org/gnome/Mutter/ScreenCast";
    private const string RemoteDesktopPath = "/org/gnome/Mutter/RemoteDesktop";

    private readonly ILogger<MutterSessionManager> _logger;

    private Connection?            _connection;
    private IRemoteDesktopSession? _remoteDesktopSession;
    private IScreenCastSession?    _screenCastSession;
    private IScreenCastStream?     _screenCastStream;
    private ObjectPath             _streamPath;

    private bool _started;
    private bool _disposed;

    public MutterSessionManager(ILogger<MutterSessionManager> logger) => _logger = logger;

    /// <summary>PipeWire node id backing the recorded monitor, set once the stream signals ready.</summary>
    public uint PipeWireNodeId { get; private set; }

    /// <summary>Object path of the ScreenCast stream — required by NotifyPointerMotionAbsolute.</summary>
    public ObjectPath StreamPath => _streamPath;

    public IRemoteDesktopSession RemoteDesktopSession =>
        _remoteDesktopSession ?? throw new InvalidOperationException("MutterSessionManager: call StartAsync() first");

    /// <summary>
    /// How long to keep retrying while org.gnome.Shell is not yet on the session bus.
    /// VERIFIED on hardware (192.168.21.96, GNOME Shell 42.9, 2026-07-31): right after a
    /// reboot, systemd starts this <c>--user</c> service (After=graphical-session.target)
    /// BEFORE GNOME Shell has actually registered its D-Bus name — CreateSessionAsync()
    /// fails with <c>org.freedesktop.DBus.Error.ServiceUnknown: The name org.gnome.Shell
    /// was not provided by any .service files</c>. Observed on that machine: GNOME Shell's
    /// name became available ~20s after boot, after 4 systemd Restart=on-failure cycles —
    /// each cycle is an unhandled-exception crash (core dump) rather than a graceful wait,
    /// which is noisy and wastes disk. Retrying INSIDE this method avoids that crash-loop.
    /// </summary>
    private static readonly TimeSpan ShellReadyTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ShellReadyRetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Establish the paired RemoteDesktop + ScreenCast session and wait for the
    /// PipeWire node id to become available. Idempotent — a second call is a no-op.
    /// </summary>
    public async Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;

        _connection = new Connection(Address.Session ??
            throw new InvalidOperationException("DBUS_SESSION_BUS_ADDRESS is not set — is this running inside the kiosk user's login session?"));
        await _connection.ConnectAsync();

        var remoteDesktopService = _connection.CreateProxy<IRemoteDesktopService>(ShellService, RemoteDesktopPath);
        var remoteDesktopSessionPath = await CreateSessionWithShellReadyRetryAsync(remoteDesktopService.CreateSessionAsync);
        _remoteDesktopSession = _connection.CreateProxy<IRemoteDesktopSession>(ShellService, remoteDesktopSessionPath);

        var remoteDesktopProps = await _remoteDesktopSession.GetAllAsync();
        var remoteDesktopSessionId = (string)remoteDesktopProps["SessionId"];
        _logger.LogInformation("MutterSessionManager: RemoteDesktop session created ({SessionId})", remoteDesktopSessionId);

        var screenCastService = _connection.CreateProxy<IScreenCastService>(ShellService, ScreenCastPath);
        var screenCastSessionPath = await screenCastService.CreateSessionAsync(
            new Dictionary<string, object> { ["remote-desktop-session-id"] = remoteDesktopSessionId });
        _screenCastSession = _connection.CreateProxy<IScreenCastSession>(ShellService, screenCastSessionPath);

        // Empty connector = primary monitor. TDD/roadmap: multi-monitor selection is v2 scope.
        _streamPath = await _screenCastSession.RecordMonitorAsync("", new Dictionary<string, object>());
        _screenCastStream = _connection.CreateProxy<IScreenCastStream>(ShellService, _streamPath);

        var nodeIdReady = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _screenCastStream.WatchPipeWireStreamAddedAsync(nodeId => nodeIdReady.TrySetResult(nodeId));

        // VERIFIED on hardware: starting the RemoteDesktop session ALSO starts the
        // paired ScreenCast session — calling ScreenCastSession.StartAsync() here
        // throws "Must be started from remote desktop session". Do NOT call it.
        await _remoteDesktopSession.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using (cts.Token.Register(() => nodeIdReady.TrySetCanceled()))
        {
            PipeWireNodeId = await nodeIdReady.Task;
        }

        _logger.LogInformation("MutterSessionManager: ScreenCast stream ready, PipeWire node {NodeId}", PipeWireNodeId);
        _started = true;
    }

    /// <summary>
    /// Calls <paramref name="createSession"/>, retrying while it fails with either:
    /// <list type="bullet">
    ///   <item><c>org.freedesktop.DBus.Error.ServiceUnknown</c> — org.gnome.Shell not
    ///   registered on the bus yet.</item>
    ///   <item><c>org.freedesktop.DBus.Error.UnknownMethod</c> with message "Object does
    ///   not exist at path ..." — ⚠️ VERIFIED on hardware (192.168.21.96, 2026-07-31):
    ///   org.gnome.Shell can claim the bus NAME before Mutter has actually exported the
    ///   <c>/org/gnome/Mutter/ScreenCast</c> / <c>/org/gnome/Mutter/RemoteDesktop</c>
    ///   OBJECTS on it — these are two separate readiness events with a real gap between
    ///   them. The first version of this retry only caught ServiceUnknown, so it still
    ///   crash-looped (core-dump) once per boot on this exact error before systemd's
    ///   external Restart=on-failure eventually got lucky on a later attempt.</item>
    /// </list>
    /// Any other error propagates immediately since retrying it would not help.
    /// </summary>
    private async Task<ObjectPath> CreateSessionWithShellReadyRetryAsync(Func<Task<ObjectPath>> createSession)
    {
        var deadline = DateTime.UtcNow + ShellReadyTimeout;
        var attempt = 0;

        while (true)
        {
            try
            {
                return await createSession();
            }
            catch (DBusException ex) when (
                IsMutterNotReadyYet(ex) &&
                DateTime.UtcNow < deadline)
            {
                attempt++;
                _logger.LogWarning(
                    "MutterSessionManager: Mutter/GNOME Shell D-Bus surface not ready yet ({ErrorName}, attempt {Attempt}) — " +
                    "retrying in {DelaySec}s. Normal for the first ~20-30s after login/reboot.",
                    ex.ErrorName, attempt, ShellReadyRetryDelay.TotalSeconds);
                await Task.Delay(ShellReadyRetryDelay);
            }
        }

        static bool IsMutterNotReadyYet(DBusException ex) =>
            ex.ErrorName == "org.freedesktop.DBus.Error.ServiceUnknown" ||
            (ex.ErrorName == "org.freedesktop.DBus.Error.UnknownMethod" &&
             ex.Message.Contains("Object does not exist", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // VERIFIED on hardware: ScreenCastSession.StopAsync() called directly throws
        // "Must be stopped from remote desktop session" — stopping RemoteDesktop tears
        // down the paired ScreenCast session too. Do NOT call ScreenCastSession.Stop().
        try { _remoteDesktopSession?.StopAsync().GetAwaiter().GetResult(); } catch { }
        _connection?.Dispose();
    }
}
