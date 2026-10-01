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

    // Server-reported screen resolution (set during HELLO_ACK, refreshed per frame — F01)
    private int _screenWidth;
    private int _screenHeight;

    // Server-reported agent name/version from HELLO_ACK (F02)
    private volatile string _serverName = "";

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
    private Stream?        _stream;   // NetworkStream (plain) hoặc SslStream (TLS)
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // TLS: thử mã hóa trước; agent cũ không hiểu TLS → lùi về TCP thường và nhớ để khỏi thử lại.
    private volatile bool _tlsUnsupported;
    private volatile bool _isEncrypted;

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

    // ── Phase 6 Events ────────────────────────────────────────────────────
    public event EventHandler<string>? ChatMessageReceived;
    public event EventHandler<string>? ClipboardDataReceived;
    public event EventHandler<string>? SysInfoReceived;

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
        _tlsUnsupported    = false;

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

    /// <summary>Phím vật lý (X11 keycode = evdev + 8). Cần agent ≥ 1.2.</summary>
    public Task SendRawKeyEventAsync(byte keycode, bool isDown)
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        return SendLockedAsync(MessageType.KeyEventRaw,
            MessageCodec.EncodeKeyEventRaw(keycode, isDown), CancellationToken.None);
    }

    /// <summary>Hỏi agent danh sách màn hình (kết quả qua <see cref="MonitorListReceived"/>). Cần agent ≥ 1.2.</summary>
    public Task RequestMonitorsAsync()
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        return SendLockedAsync(MessageType.MonitorListReq, Array.Empty<byte>(), CancellationToken.None);
    }

    /// <summary>Chọn màn hình theo index; 0xFF = toàn bộ desktop.</summary>
    public Task SelectMonitorAsync(byte index)
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        return SendLockedAsync(MessageType.MonitorSelect, new[] { index }, CancellationToken.None);
    }

    // ── File transfer (chunked, hai chiều; agent ≥ 1.2) ───────────────────

    private const int FileChunkSize = 64 * 1024;

    private sealed class UploadState
    {
        public readonly TaskCompletionSource<string> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<string> Done  = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class DownloadState
    {
        public required string LocalPath { get; init; }
        public IProgress<(long Done, long Total)>? Progress { get; init; }
        public readonly TaskCompletionSource Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public FileStream? Stream;
        public int  Id = -1;
        public long Size;
        public long Received;
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<byte, UploadState> _uploads = new();
    private DownloadState? _download;
    private int _nextUploadId;

    /// <summary>JSON danh sách file trên máy đích: [{"name":"a.txt","size":123}, …]</summary>
    public event EventHandler<string>? FileListReceived;

    public Task RequestFileListAsync()
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        return SendLockedAsync(MessageType.FileListReq, Array.Empty<byte>(), CancellationToken.None);
    }

    /// <summary>Gửi 1 file lên thư mục nhận của máy đích. Ném <see cref="IOException"/> nếu agent từ chối/lỗi.</summary>
    public async Task UploadFileAsync(string localPath, IProgress<(long Done, long Total)>? progress = null,
        CancellationToken ct = default)
    {
        if (_state != ConnectionState.Streaming) throw new InvalidOperationException("Chưa kết nối");

        var id = (byte)(1 + (Interlocked.Increment(ref _nextUploadId) % 0x7F)); // 1..0x7F
        var st = new UploadState();
        _uploads[id] = st;

        try
        {
            await using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var name = System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(localPath));
            var begin = new byte[9 + name.Length];
            begin[0] = id;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(begin.AsSpan(1), (ulong)fs.Length);
            name.CopyTo(begin, 9);
            await SendLockedAsync(MessageType.FileBegin, begin, ct).ConfigureAwait(false);

            await WaitAckAsync(st.Ready.Task, 15_000, ct).ConfigureAwait(false);

            var buf = new byte[1 + FileChunkSize];
            buf[0] = id;
            long sent = 0;
            int n;
            try
            {
                while ((n = await fs.ReadAsync(buf.AsMemory(1, FileChunkSize), ct).ConfigureAwait(false)) > 0)
                {
                    await SendLockedAsync(MessageType.FileChunk, buf.AsSpan(0, 1 + n).ToArray(), ct).ConfigureAwait(false);
                    sent += n;
                    progress?.Report((sent, fs.Length));
                    if (st.Done.Task.IsFaulted) break; // agent báo lỗi giữa chừng
                }
            }
            catch (OperationCanceledException)
            {
                // Kết thúc sớm → agent thấy thiếu dữ liệu và xoá file .part.
                try { await SendLockedAsync(MessageType.FileEnd, new[] { id }, CancellationToken.None).ConfigureAwait(false); } catch { }
                throw;
            }

            await SendLockedAsync(MessageType.FileEnd, new[] { id }, ct).ConfigureAwait(false);
            await WaitAckAsync(st.Done.Task, 60_000, ct).ConfigureAwait(false);
        }
        finally
        {
            _uploads.TryRemove(id, out _);
        }
    }

    /// <summary>Tải file (theo tên trong thư mục nhận của máy đích) về <paramref name="localPath"/>.</summary>
    public async Task DownloadFileAsync(string remoteName, string localPath,
        IProgress<(long Done, long Total)>? progress = null, CancellationToken ct = default)
    {
        if (_state != ConnectionState.Streaming) throw new InvalidOperationException("Chưa kết nối");

        var st = new DownloadState { LocalPath = localPath, Progress = progress };
        if (Interlocked.CompareExchange(ref _download, st, null) is not null)
            throw new InvalidOperationException("Đang có một file được tải");

        try
        {
            await SendLockedAsync(MessageType.FileGet, System.Text.Encoding.UTF8.GetBytes(remoteName), ct).ConfigureAwait(false);
            using var reg = ct.Register(() => st.Completed.TrySetCanceled(ct));
            await st.Completed.Task.ConfigureAwait(false);
        }
        catch
        {
            try { st.Stream?.Dispose(); File.Delete(localPath); } catch { /* ignore */ }
            throw;
        }
        finally
        {
            Interlocked.CompareExchange(ref _download, null, st);
        }
    }

    private static async Task WaitAckAsync(Task<string> ack, int timeoutMs, CancellationToken ct)
    {
        var done = await Task.WhenAny(ack, Task.Delay(timeoutMs, ct)).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (done != ack) throw new TimeoutException("Máy đích không phản hồi (agent có thể là bản cũ < 1.2)");
        await ack.ConfigureAwait(false); // ném IOException nếu agent báo lỗi
    }

    private void HandleFileAck(byte[] payload)
    {
        if (payload.Length < 2) return;
        byte id = payload[0], status = payload[1];
        var msg = System.Text.Encoding.UTF8.GetString(payload.AsSpan(2));

        if (_uploads.TryGetValue(id, out var up))
        {
            switch (status)
            {
                case 0: up.Ready.TrySetResult(msg); break;
                case 1: up.Ready.TrySetResult(msg); up.Done.TrySetResult(msg); break;
                default:
                    var ex = new IOException(msg);
                    up.Ready.TrySetException(ex); up.Done.TrySetException(ex);
                    break;
            }
        }
        else if (status == 2 && _download is { } dl && (id == 0 || id == dl.Id))
        {
            dl.Completed.TrySetException(new IOException(msg));
        }
    }

    private async Task HandleFileBeginFromServerAsync(byte[] payload)
    {
        if (payload.Length < 10 || _download is not { } dl) return;
        dl.Id   = payload[0];
        dl.Size = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(payload.AsSpan(1));
        try
        {
            dl.Stream = new FileStream(dl.LocalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        }
        catch (Exception ex) { dl.Completed.TrySetException(ex); }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task HandleFileChunkFromServerAsync(byte[] payload)
    {
        if (payload.Length < 1 || _download is not { } dl || dl.Id != payload[0] || dl.Stream is null) return;
        try
        {
            await dl.Stream.WriteAsync(payload.AsMemory(1)).ConfigureAwait(false);
            dl.Received += payload.Length - 1;
            dl.Progress?.Report((dl.Received, dl.Size));
        }
        catch (Exception ex) { dl.Completed.TrySetException(ex); }
    }

    private async Task HandleFileEndFromServerAsync(byte[] payload)
    {
        if (payload.Length < 1 || _download is not { } dl || dl.Id != payload[0] || dl.Stream is null) return;
        try
        {
            await dl.Stream.FlushAsync().ConfigureAwait(false);
            await dl.Stream.DisposeAsync().ConfigureAwait(false);
            dl.Stream = null;
            if (dl.Received == dl.Size) dl.Completed.TrySetResult();
            else dl.Completed.TrySetException(new IOException($"Thiếu dữ liệu ({dl.Received}/{dl.Size} B)"));
        }
        catch (Exception ex) { dl.Completed.TrySetException(ex); }
    }

    /// <summary>JSON danh sách màn hình: [{"index":0,"name":"HDMI-1","x":0,"y":0,"w":1920,"h":1080}, …]</summary>
    public event EventHandler<string>? MonitorListReceived;

    // ── Phase 6 Methods ───────────────────────────────────────────────────

    public Task SendChatMessageAsync(string message)
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        var payload = MessageCodec.EncodeStringMessage(message);
        return SendLockedAsync(MessageType.ChatText, payload, CancellationToken.None);
    }

    public Task SendClipboardTextAsync(string text)
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        var payload = MessageCodec.EncodeStringMessage(text);
        return SendLockedAsync(MessageType.ClipboardData, payload, CancellationToken.None);
    }

    /// <summary>Yêu cầu agent gửi ngay clipboard hiện tại của ZCU (kết quả qua ClipboardDataReceived). Agent ≥ 1.2.</summary>
    public Task RequestClipboardAsync()
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        return SendLockedAsync(MessageType.ClipboardGet, Array.Empty<byte>(), CancellationToken.None);
    }

    public Task SetPrivacyModeAsync(bool enabled)
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        var payload = MessageCodec.EncodeBooleanMessage(enabled);
        return SendLockedAsync(MessageType.PrivacyMode, payload, CancellationToken.None);
    }

    public Task RequestSysInfoAsync()
    {
        if (_state != ConnectionState.Streaming) return Task.CompletedTask;
        return SendLockedAsync(MessageType.SysInfoReq, Array.Empty<byte>(), CancellationToken.None);
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

        // Everything from ConnectAsync through the handshake can throw BEFORE the
        // streaming try/finally below is reached. Without this guard the freshly
        // assigned _tcp/_stream would never be disposed — the next retry overwrites
        // the references and leaks one socket + NetworkStream per failed attempt
        // (unbounded on a flaky network).
        try
        {
            await _tcp.ConnectAsync(_host, _port, sessionCt).ConfigureAwait(false);
            _stream = await NegotiateStreamAsync(sessionCt).ConfigureAwait(false);

            SetState(ConnectionState.Authenticating);

            // ── HELLO ──────────────────────────────────────────────────────
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
            _serverName   = serverName;   // F02: expose cho UI so sánh version agent
            _logger.LogInformation("Connected to {ServerName}, screen {W}×{H}", serverName, screenW, screenH);

            // ── AUTH ───────────────────────────────────────────────────────
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
        catch
        {
            // Dispose _tcp/_stream on ANY failure path (TCP connect, HELLO/AUTH
            // handshake, streaming). CloseConnectionAsync is idempotent
            // (Interlocked.Exchange to null) so the double call after the inner
            // finally above is harmless.
            await CloseConnectionAsync("Connection attempt failed").ConfigureAwait(false);
            throw;
        }
    }

    // ── TLS negotiation ───────────────────────────────────────────────────

    /// <summary>
    /// Thử bắt tay TLS trên <c>_tcp</c> vừa kết nối. Chứng chỉ agent là tự ký nên không kiểm chuỗi tin cậy
    /// (mã hóa chống nghe lén thụ động; token nằm trong kênh đã mã hóa). Nếu agent là bản cũ không hiểu
    /// TLS, agent đóng kết nối → mở lại socket và dùng TCP thường (kèm nhớ để lần sau bỏ qua bước thử).
    /// </summary>
    private async Task<Stream> NegotiateStreamAsync(CancellationToken sessionCt)
    {
        _isEncrypted = false;
        if (!_tlsUnsupported)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(sessionCt);
                cts.CancelAfter(RemoteControlConstants.HandshakeTimeoutMs);

                var ssl = new System.Net.Security.SslStream(
                    _tcp!.GetStream(), leaveInnerStreamOpen: false,
                    userCertificateValidationCallback: (_, _, _, _) => true);
                await ssl.AuthenticateAsClientAsync(new System.Net.Security.SslClientAuthenticationOptions
                {
                    TargetHost = _host,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                                        | System.Security.Authentication.SslProtocols.Tls13,
                }, cts.Token).ConfigureAwait(false);

                _isEncrypted = true;
                return ssl;
            }
            catch (Exception ex) when (!sessionCt.IsCancellationRequested)
            {
                _logger.LogInformation(ex, "TLS handshake với {Host} thất bại — agent có thể là bản cũ, lùi về TCP thường", _host);
                _tlsUnsupported = true;
                try { _tcp?.Dispose(); } catch { /* ignore */ }
                _tcp = new TcpClient { NoDelay = true };
                _tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                await _tcp.ConnectAsync(_host, _port, sessionCt).ConfigureAwait(false);
            }
        }
        return _tcp!.GetStream();
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

                // ── Phase 6 ───────────────────────────────────────────────────
                case MessageType.ChatText:
                    ChatMessageReceived?.Invoke(this, MessageCodec.DecodeStringMessage(payload));
                    break;
                
                case MessageType.ClipboardData:
                    ClipboardDataReceived?.Invoke(this, MessageCodec.DecodeStringMessage(payload));
                    break;

                case MessageType.FileAck:   HandleFileAck(payload); break;
                case MessageType.FileBegin: await HandleFileBeginFromServerAsync(payload).ConfigureAwait(false); break;
                case MessageType.FileChunk: await HandleFileChunkFromServerAsync(payload).ConfigureAwait(false); break;
                case MessageType.FileEnd:   await HandleFileEndFromServerAsync(payload).ConfigureAwait(false); break;
                case MessageType.FileList:
                    FileListReceived?.Invoke(this, MessageCodec.DecodeStringMessage(payload));
                    break;

                case MessageType.MonitorList:
                    MonitorListReceived?.Invoke(this, MessageCodec.DecodeStringMessage(payload));
                    break;

                case MessageType.SysInfoResp:
                    SysInfoReceived?.Invoke(this, MessageCodec.DecodeStringMessage(payload));
                    break;

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

        // F01: cập nhật ScreenWidth/Height theo TỪNG frame — không chỉ theo HELLO_ACK.
        // Nếu ZCU đổi độ phân giải giữa phiên, HELLO_ACK snapshot trở nên lạc hậu;
        // consumer (VD: SessionRecorder khởi tạo AVI header, mapping tọa độ chuột)
        // đọc property này phải nhận đúng kích thước frame ĐANG stream.
        if (frame.Width > 0 && frame.Height > 0)
        {
            _screenWidth  = frame.Width;
            _screenHeight = frame.Height;
        }

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
            // Keep as double + clamp ≥ 0 — a straight (int) cast can overflow/underflow
            // at the boundaries (e.g. clock adjustment) and corrupt the timeout check.
            var elapsedMs   = Math.Max(0d, (DateTime.UtcNow - lastPong).TotalMilliseconds);
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

        // Đang upload/download dở thì rớt kết nối → báo lỗi thay vì treo chờ.
        var lost = new IOException("Mất kết nối trong lúc truyền file");
        foreach (var up in _uploads.Values) { up.Ready.TrySetException(lost); up.Done.TrySetException(lost); }
        _download?.Completed.TrySetException(lost);

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

    /// <summary>
    /// F02: Tên/phiên bản agent phía ZCU báo trong HELLO_ACK (VD "ZcuAgent/1.1").
    /// Valid sau khi handshake xong — UI dùng để cảnh báo agent phiên bản cũ
    /// (agent cũ bỏ qua âm thầm SysInfo/Privacy/Chat/Clipboard, không có ACK trong protocol v1).
    /// </summary>
    public string ServerName => _serverName;

    /// <summary>true nếu kết nối hiện tại được mã hóa TLS; false = TCP thường (agent bản cũ).</summary>
    public bool IsEncrypted => _isEncrypted;

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
