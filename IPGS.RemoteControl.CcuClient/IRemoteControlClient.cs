using IPGS.RemoteControl.Protocol;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// CCU-side remote control client. Handles the full connection lifecycle including
/// automatic reconnect (except after AUTH_FAIL). See TDD §7 state machine and §10.1.
/// </summary>
public interface IRemoteControlClient : IDisposable
{
    /// <summary>Current connection state (thread-safe read).</summary>
    ConnectionState State { get; }

    /// <summary>
    /// Raised on the receive thread every time a JPEG frame arrives.
    /// Handler must return quickly; queue heavy work elsewhere.
    /// </summary>
    event EventHandler<FrameReceivedEventArgs>? FrameReceived;

    /// <summary>Raised whenever <see cref="State"/> transitions.</summary>
    event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    // ── Phase 6 Events ──
    event EventHandler<string>? ChatMessageReceived;
    event EventHandler<string>? ClipboardDataReceived;
    event EventHandler<string>? SysInfoReceived;

    /// <summary>
    /// Begin connecting to <paramref name="host"/>:<paramref name="port"/> using
    /// <paramref name="token"/> as the shared secret. Returns immediately; connection
    /// proceeds in the background. Monitor <see cref="StateChanged"/> for progress.
    /// Auto-reconnects unless AUTH_FAIL or <see cref="DisconnectAsync"/> is called.
    /// </summary>
    Task ConnectAsync(string host, int port, string token, CancellationToken ct = default);

    /// <summary>Gracefully close the connection and stop auto-reconnect.</summary>
    Task DisconnectAsync();

    /// <summary>
    /// Send a MOUSE_MOVE message. No-op if not in <see cref="ConnectionState.Streaming"/>.
    /// Coordinates are in ZCU screen space (0..screenWidth-1, 0..screenHeight-1).
    /// </summary>
    Task SendMouseMoveAsync(int x, int y);

    /// <summary>
    /// Send a MOUSE_BUTTON message. No-op if not in <see cref="ConnectionState.Streaming"/>.
    /// </summary>
    Task SendMouseButtonAsync(MouseButton button, bool isDown, int x, int y);

    /// <summary>
    /// Send a KEY_EVENT message (TDD §17.2). No-op if not in <see cref="ConnectionState.Streaming"/>.
    /// <para>
    /// <paramref name="keysym"/> must be an X11 keysym value — see <c>KeyboardMapper</c> for
    /// the Avalonia→keysym mapping logic.  <paramref name="isDown"/> true = key press, false = release.
    /// </para>
    /// </summary>
    Task SendKeyEventAsync(uint keysym, bool isDown);

    // ── Phase 6 Methods ──
    Task SendChatMessageAsync(string message);
    Task SendClipboardTextAsync(string text);
    Task SetPrivacyModeAsync(bool enabled);
    Task RequestSysInfoAsync();
}

// ── Enums ──────────────────────────────────────────────────────────────────

/// <summary>Connection states per TDD §7 state machine.</summary>
public enum ConnectionState
{
    /// <summary>Not connected; auto-reconnect may be pending.</summary>
    Disconnected,
    /// <summary>TCP SYN sent, waiting for connection.</summary>
    Connecting,
    /// <summary>TCP connected, HELLO/AUTH exchange in progress.</summary>
    Authenticating,
    /// <summary>Fully authenticated, frames are streaming.</summary>
    Streaming,
    /// <summary>Terminal state: max retries exceeded or AUTH_FAIL. Requires explicit reconnect.</summary>
    Faulted,
}

// ── Event args ─────────────────────────────────────────────────────────────

/// <summary>
/// Carries a single received JPEG frame. The caller is responsible for decoding
/// (SkiaSharp recommended — TDD §6.2). <see cref="JpegData"/> is valid only during
/// the event handler; copy it if needed beyond the call.
/// </summary>
public sealed class FrameReceivedEventArgs : EventArgs
{
    public long FrameId { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>Raw JPEG bytes from the server. Slice of an internal buffer — copy if retained.</summary>
    public ReadOnlyMemory<byte> JpegData { get; init; }
    public DateTime CapturedUtc { get; init; }
}

/// <summary>State transition notification.</summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionState Previous { get; init; }
    public ConnectionState Current { get; init; }

    /// <summary>Human-readable reason (e.g. "AUTH_FAIL: bad token", "PING timeout 16042 ms").</summary>
    public string? Reason { get; init; }
}
