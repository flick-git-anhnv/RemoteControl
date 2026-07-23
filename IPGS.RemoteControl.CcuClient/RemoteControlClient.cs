using System.Net.Sockets;
using IPGS.RemoteControl.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// TCP client that connects to a ZcuAgent, authenticates, receives JPEG frames and
/// forwards mouse events. Implements the state machine from TDD §7.
/// <para>
/// Threading model:
///   - One reader task (<c>ReceiveLoopAsync</c>) — sole consumer of NetworkStream.Read
///   - All writes serialised via <c>_sendLock</c> (ping sender + mouse events + pong replies)
///   - <c>_lastPongTicks</c> is updated by the reader, checked by the ping sender
/// </para>
/// </summary>
public sealed class RemoteControlClient : IRemoteControlClient
{
    // ── State ─────────────────────────────────────────────────────────────
    private volatile ConnectionState _state = ConnectionState.Disconnected;

    // Server-reported screen resolution (set during HELLO_ACK)
    private int _screenWidth;
    private int _screenHeight;

    // ── Connection parameters (set once per ConnectAsync call) ────────────
    private string _host = "";
    private int    _port;
    private string _token = "";

    // ── Cancellation hierarchy ────────────────────────────────────────────
    // _globalCts: cancelled by DisconnectAsync or outer CancellationToken.
    //             Stops the whole reconnect loop permanently.
    // _sessionCts: cancelled on each individual connection failure/timeout.
    //              Derived from _globalCts so _globalCts cancel also kills the session.
    private CancellationTokenSource? _globalCts;
    private CancellationTokenSource? _sessionCts;

    // ── Network ───────────────────────────────────────────────────────────
    private TcpClient?     _tcp;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // ── PING / PONG keepalive tracking ────────────────────────────────────
    // Stored as UTC ticks (long) so Interlocked can be used for lock-free access.
    private long _lastPongTicks;

    // ── Reconnect tracking ────────────────────────────────────────────────
    private int  _reconnectAttempts;
    private bool _authFailed;         // true → no auto-reconnect

    // ── Logging ───────────────────────────────────────────────────────────
    private readonly ILogger<RemoteControlClient> _logger;

    // ── Public API ────────────────────────────────────────────────────────
    public ConnectionState State => _state;
    public event EventHandler<FrameReceivedEventArgs>?        FrameReceived;
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    // ── Constructor ───────────────────────────────────────────────────────
    public RemoteControlClient(ILogger<RemoteControlClient>? logger = null)
        => _logger = logger ?? NullLogger<RemoteControlClient>.Instance;

    // ── IRemoteControlClient — public async operations ────────────────────

    /// <inheritdoc/>
    public Task ConnectAsync(string host, int port, string token, CancellationToken ct = default)
    {
        var s = _state;
        if (s is not (ConnectionState.Disconnected or ConnectionState.Faulted))
            throw new InvalidOperationException($"Cannot connect while in state {s}");

        _host              = host;
        _port              = port;
        _token             = token;
        _authFailed        = false;
        _reconnectAttempts = 0;

        _globalCts?.Dispose();
        _globalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Fire and forget — caller monitors StateChanged events.
        _ = Task.Run(() => ConnectionLoopAsync(_globalCts.Token));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync()
    {
        _globalCts?.Cancel();
        await CloseConnectionAsync("User requested disconnect").ConfigureAwait(false);
        SetState(ConnectionState.Disconnected, "User requested disconnect");
    }

    /// <inheritdoc/>
    /// <remarks>Coordinates must already be in ZCU screen space. See TDD §5.3.</remarks>
    public Task SendMouseMoveAsync(int x, int y)
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        var payload = MessageCodec.EncodeMouseMove(x, y);
        return SendLockedAsync(MessageType.MouseMove, payload, CancellationToken.None);
    }

    /// <inheritdoc/>
    /// <remarks>Coordinates must already be in ZCU screen space. See TDD §5.3.</remarks>
    public Task SendMouseButtonAsync(MouseButton button, bool isDown, int x, int y)
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        var payload = MessageCodec.EncodeMouseButton(button, isDown, x, y);
        return SendLockedAsync(MessageType.MouseButton, payload, CancellationToken.None);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Keysym must be an X11 keysym value — see <c>KeyboardMapper</c> in CcuUI.
    /// Uses the same <c>_sendLock</c> as mouse sends to prevent concurrent writes
    /// on NetworkStream (TDD §17.6).
    /// </remarks>
    public Task SendKeyEventAsync(uint keysym, bool isDown)
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        var payload = MessageCodec.EncodeKeyEvent(keysym, isDown);
        return SendLockedAsync(MessageType.KeyEvent, payload, CancellationToken.None);
    }

    // ── Connection loop (reconnect logic, TDD §7) ─────────────────────────

    private async Task ConnectionLoopAsync(CancellationToken globalCt)
    {
        while (!globalCt.IsCancellationRequested)
        {
            if (_reconnectAttempts >= RemoteControlConstants.MaxReconnectAttempts)
            {
                SetState(ConnectionState.Faulted,
                    $"Max reconnect attempts ({RemoteControlConstants.MaxReconnectAttempts}) exceeded");
                return;
            }

            try
            {
                await ConnectOnceAsync(globalCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (globalCt.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Connection attempt {Attempt}/{Max} failed",
                    _reconnectAttempts + 1, RemoteControlConstants.MaxReconnectAttempts);
            }

            // AUTH_FAIL or explicit cancel → stop reconnecting.
            if (_authFailed || globalCt.IsCancellationRequested) break;

            _reconnectAttempts++;

            // Signal UI that we are temporarily disconnected while waiting to retry.
            SetState(ConnectionState.Disconnected,
                $"Will retry in ~{RemoteControlConstants.ReconnectDelayMs / 1000}s " +
                $"(attempt {_reconnectAttempts}/{RemoteControlConstants.MaxReconnectAttempts})");

            // Reconnect delay ± jitter (TDD §11: 3 000 ms ± 1 000 ms).
            var jitter = Random.Shared.Next(
                -RemoteControlConstants.ReconnectJitterMs,
                RemoteControlConstants.ReconnectJitterMs + 1);
            var delay = Math.Max(500, RemoteControlConstants.ReconnectDelayMs + jitter);

            try { await Task.Delay(delay, globalCt).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        // Ensure we land on a terminal state.
        if (_state is not (ConnectionState.Disconnected or ConnectionState.Faulted))
            SetState(ConnectionState.Disconnected, "Connection loop ended");
    }

    // ── Single connection attempt (TCP + handshake + streaming) ──────────

    private async Task ConnectOnceAsync(CancellationToken globalCt)
    {
        SetState(ConnectionState.Connecting);

        // Per-session CTS: cancelled on timeout, PING failure, or socket error.
        _sessionCts?.Dispose();
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
        var sessionCt = _sessionCts.Token;

        _tcp = new TcpClient { NoDelay = true };
        // Enable TCP keepalive to catch half-open connections (TDD §14).
        _tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        await _tcp.ConnectAsync(_host, _port, sessionCt).ConfigureAwait(false);
        _stream = _tcp.GetStream();

        SetState(ConnectionState.Authenticating);

        // ── HELLO ──────────────────────────────────────────────────────────
        using var helloTo  = new CancellationTokenSource(RemoteControlConstants.HandshakeTimeoutMs);
        using var helloCt  = CancellationTokenSource.CreateLinkedTokenSource(sessionCt, helloTo.Token);

        await MessageCodec.WriteMessageAsync(
            _stream, MessageType.Hello, MessageCodec.EncodeHello("IPGS-CCU-v1"), helloCt.Token)
            .ConfigureAwait(false);

        var (helloAckType, helloAckPayload) = await MessageCodec
            .ReadMessageAsync(_stream, helloCt.Token).ConfigureAwait(false);

        if (helloAckType != MessageType.HelloAck)
            throw new ProtocolException($"Expected HELLO_ACK, got {helloAckType}");

        var (_, screenW, screenH, serverName) = MessageCodec.DecodeHelloAck(helloAckPayload);
        _screenWidth  = (int)screenW;
        _screenHeight = (int)screenH;
        _logger.LogInformation("Connected to {ServerName}, screen {W}×{H}", serverName, screenW, screenH);

        // ── AUTH ───────────────────────────────────────────────────────────
        using var authTo = new CancellationTokenSource(RemoteControlConstants.HandshakeTimeoutMs);
        using var authCt = CancellationTokenSource.CreateLinkedTokenSource(sessionCt, authTo.Token);

        await MessageCodec.WriteMessageAsync(
            _stream, MessageType.Auth, MessageCodec.EncodeAuth(_token), authCt.Token)
            .ConfigureAwait(false);

        var (authResultType, authResultPayload) = await MessageCodec
            .ReadMessageAsync(_stream, authCt.Token).ConfigureAwait(false);

        if (authResultType == MessageType.AuthFail)
        {
            var reason = MessageCodec.DecodeAuthFail(authResultPayload);
            _logger.LogWarning("AUTH_FAIL from {Host}: {Reason}", _host, reason);
            _authFailed = true;
            // Server closes the connection; clean up our side too.
            await CloseConnectionAsync($"AUTH_FAIL: {reason}").ConfigureAwait(false);
            // Transition to Faulted — caller (ConnectionLoopAsync) will not retry.
            SetState(ConnectionState.Faulted, $"AUTH_FAIL: {reason}");
            return;
        }

        if (authResultType != MessageType.AuthOk)
            throw new ProtocolException($"Expected AUTH_OK or AUTH_FAIL, got {authResultType}");

        // Auth succeeded — reset reconnect counter.
        _reconnectAttempts = 0;
        SetState(ConnectionState.Streaming);

        // ── Streaming phase: receive loop + ping sender run concurrently ───
        // Initialise PONG timestamp BEFORE starting PingSender so the first timeout
        // window starts from "now", not from an earlier timestamp.
        Interlocked.Exchange(ref _lastPongTicks, DateTime.UtcNow.Ticks);

        var pingSenderTask = Task.Run(() => PingSenderLoopAsync(sessionCt), sessionCt);

        try
        {
            await ReceiveLoopAsync(sessionCt).ConfigureAwait(false);
        }
        finally
        {
            // Cancel session to stop PingSenderLoop, wait for it to finish.
            _sessionCts?.Cancel();
            try { await pingSenderTask.ConfigureAwait(false); } catch { /* ignore */ }
            await CloseConnectionAsync("Session ended").ConfigureAwait(false);
        }
    }

    // ── Receive loop ──────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken sessionCt)
    {
        while (!sessionCt.IsCancellationRequested)
        {
            var (type, payload) = await MessageCodec
                .ReadMessageAsync(_stream!, sessionCt).ConfigureAwait(false);

            switch (type)
            {
                case MessageType.FrameJpeg:
                    HandleFrameJpeg(payload);
                    break;

                case MessageType.Ping:
                    // Reply with same nonce, do NOT update _lastPongTicks (server PING is not our PONG).
                    var pingNonce = MessageCodec.DecodePingPong(payload);
                    await SendLockedAsync(MessageType.Pong,
                        MessageCodec.EncodePingPong(pingNonce), sessionCt).ConfigureAwait(false);
                    break;

                case MessageType.Pong:
                    // Update PONG timestamp — checked by PingSenderLoop for timeout.
                    Interlocked.Exchange(ref _lastPongTicks, DateTime.UtcNow.Ticks);
                    break;

                case MessageType.Bye:
                    _logger.LogInformation("Received BYE from server — closing session");
                    return;

                default:
                    // Unexpected message in Streaming state — log and keep going (lenient).
                    _logger.LogDebug("Unexpected message type 0x{Type:X2} in Streaming state — ignored", (byte)type);
                    break;
            }
        }
    }

    private void HandleFrameJpeg(byte[] payload)
    {
        var frame = MessageCodec.DecodeFrameJpeg(payload);
        var tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(frame.TimestampMs).UtcDateTime;

        // Fire event. Handler is responsible for copying JpegData if it needs to retain it
        // beyond the call. JpegData is a slice of the payload byte[] allocated by ReadMessageAsync
        // so it remains valid — but the caller should not assume long-term ownership.
        FrameReceived?.Invoke(this, new FrameReceivedEventArgs
        {
            FrameId     = frame.FrameId,
            Width       = frame.Width,
            Height      = frame.Height,
            JpegData    = frame.JpegData,
            CapturedUtc = tsUtc,
        });
    }

    // ── Ping sender loop (TDD §11: PingIntervalMs=5000, PingTimeoutMs=15000) ──

    private async Task PingSenderLoopAsync(CancellationToken sessionCt)
    {
        var rng = Random.Shared;

        while (!sessionCt.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RemoteControlConstants.PingIntervalMs, sessionCt)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Check PING timeout: if no PONG received in PingTimeoutMs, abort session.
            var lastPong    = new DateTime(Interlocked.Read(ref _lastPongTicks), DateTimeKind.Utc);
            var elapsedMs   = (int)(DateTime.UtcNow - lastPong).TotalMilliseconds;
            if (elapsedMs > RemoteControlConstants.PingTimeoutMs)
            {
                _logger.LogWarning("PING timeout: no PONG in {Elapsed} ms — aborting session", elapsedMs);
                // Cancel the session CTS so the receive loop terminates.
                _sessionCts?.Cancel();
                return;
            }

            // Send PING with random nonce.
            if (!sessionCt.IsCancellationRequested && _state == ConnectionState.Streaming)
            {
                try
                {
                    var nonce = (ulong)rng.NextInt64();
                    await SendLockedAsync(MessageType.Ping,
                        MessageCodec.EncodePingPong(nonce), sessionCt).ConfigureAwait(false);
                }
                catch (Exception ex) when (!sessionCt.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "Ping send failed — session probably terminating");
                    break;
                }
            }
        }
    }

    // ── Send helper (serialises concurrent writes) ────────────────────────

    private async Task SendLockedAsync(MessageType type, byte[] payload, CancellationToken ct)
    {
        var stream = _stream;
        if (stream is null) return;

        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await MessageCodec.WriteMessageAsync(stream, type, payload, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Write failed on {Type}", type);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // ── Close helper ──────────────────────────────────────────────────────

    private async Task CloseConnectionAsync(string reason)
    {
        // Exchange nulls atomically to prevent double-close.
        var stream = Interlocked.Exchange(ref _stream, null);
        var tcp    = Interlocked.Exchange(ref _tcp, null);

        if (stream is not null)
        {
            try
            {
                // Best-effort BYE (ignore errors — peer may have already closed).
                using var cts = new CancellationTokenSource(200);
                await MessageCodec.WriteEmptyAsync(stream, MessageType.Bye, cts.Token)
                    .ConfigureAwait(false);
            }
            catch { /* intentionally swallowed */ }

            try { await stream.DisposeAsync().ConfigureAwait(false); }
            catch { /* ignore */ }
        }

        try { tcp?.Dispose(); } catch { /* ignore */ }

        _logger.LogDebug("Connection closed: {Reason}", reason);
    }

    // ── State transition helper ───────────────────────────────────────────

    private void SetState(ConnectionState next, string? reason = null)
    {
        var prev = _state;
        if (prev == next) return;
        _state = next;

        _logger.LogDebug("State: {Prev} → {Next}{ReasonSuffix}",
            prev, next, reason is null ? "" : $" ({reason})");

        StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
        {
            Previous = prev,
            Current  = next,
            Reason   = reason,
        });
    }

    // ── Properties for CcuUI ─────────────────────────────────────────────

    /// <summary>ZCU primary display width (pixels). Valid after <see cref="ConnectionState.Streaming"/>.</summary>
    public int ScreenWidth => _screenWidth;

    /// <summary>ZCU primary display height (pixels). Valid after <see cref="ConnectionState.Streaming"/>.</summary>
    public int ScreenHeight => _screenHeight;

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        _globalCts?.Cancel();
        _sessionCts?.Cancel();

        var stream = Interlocked.Exchange(ref _stream, null);
        var tcp    = Interlocked.Exchange(ref _tcp, null);

        stream?.Dispose();
        tcp?.Dispose();

        _sendLock.Dispose();
        _globalCts?.Dispose();
        _sessionCts?.Dispose();
    }
}
