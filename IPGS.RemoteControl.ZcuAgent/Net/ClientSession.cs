using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Channels;
using IPGS.RemoteControl.Protocol;
using IPGS.RemoteControl.ZcuAgent.Auth;
using IPGS.RemoteControl.ZcuAgent.Capture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Manages one connected CCU client: handshake → auth → frame-send + input-receive loops.
/// State machine mirrors TDD §7 (server side).
/// </summary>
internal sealed class ClientSession
{
    private readonly TcpClient         _tcp;
    private readonly string            _remoteIp;
    private readonly IScreenCapturer   _capturer;
    private readonly IFrameEncoder     _encoder;
    private readonly IMouseInjector    _injector;
    private readonly IKeyboardInjector _keyboard;
    private readonly IClipboardService _clipboard;
    private readonly IMonitorProvider  _monitorProvider;
    private readonly IDesktopPlatformService _platform;
    private readonly AuthManager       _auth;
    private readonly AgentOptions      _options;
    private readonly ILogger           _logger;

    // Stream: NetworkStream (plain) hoặc SslStream (TLS) — tự nhận diện ở RunAsync.
    private Stream? _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Per-write timeout (ms) for <see cref="WriteAsync"/>. A slow/stalled reader would
    /// otherwise block WriteAsync forever via TCP backpressure and — because v1 serves a
    /// single client — hold the only session slot indefinitely (audit L3).
    /// NOTE: NetworkStream.WriteTimeout only applies to SYNC writes; async writes must be
    /// bounded with a CancellationToken, which is what this constant drives.
    /// </summary>
    private const int WriteTimeoutMs = 10_000;

    /// <summary>Max accepted CHAT text length (chars) — audit S2 (was: 8MB frame cap).</summary>
    private const int MaxChatTextChars = 4_096;

    /// <summary>Max accepted clipboard text length (chars) — audit S2.</summary>
    private const int MaxClipboardChars = 262_144;

    /// <summary>Chu kỳ (ms) poll clipboard ZCU để đồng bộ ngược về CCU. 1s: mỗi lần poll spawn 1 tiến trình xclip, máy Atom/Celeron yếu không nên poll dày hơn.</summary>
    private const int ClipboardPollMs = 2_000;

    /// <summary>Màn hình đang được chọn (null = toàn desktop). Ghi từ receive loop, đọc từ capture loop và khi enqueue chuột.</summary>
    private volatile MonitorInfo? _monitor;
    private FileTransferHandler? _files;
    private List<MonitorInfo> _monitors = [];
    private byte[] _cropBuffer = [];
    private readonly byte[] _frameHeaderBuffer = new byte[MessageCodec.HeaderSize + MessageCodec.FrameMetadataHeaderSize];

    /// <summary>
    /// Màn hình tĩnh (SkipUnchangedFrames): vẫn gửi 1 khung làm mới sau mỗi khoảng này để CCU/recorder
    /// không bao giờ bị "đứng hình" quá lâu nếu có gì đó lệch.
    /// </summary>
    private const int StaticRefreshMs = 2_000;

    /// <summary>
    /// Sau <see cref="IdleStreakThreshold"/> khung liên tiếp không đổi thì chỉ chụp thưa lại mỗi
    /// <see cref="IdlePollMs"/> ms (thay vì theo trần FPS) — bản thân XShmGetImage cũng tốn CPU.
    /// Độ trễ tối đa khi màn hình bắt đầu đổi trở lại ≈ IdlePollMs.
    /// </summary>
    private const int IdlePollMs          = 100;
    private const int IdleStreakThreshold = 5;

    /// <summary>
    /// Hash nội dung clipboard ZCU lần gần nhất đã biết (CCU vừa set, hoặc poll vừa gửi đi).
    /// Chống echo: text CCU đẩy sang ZCU không bị gửi ngược lại CCU. Truy cập từ receive loop
    /// và clipboard loop → dùng Volatile.
    /// </summary>
    private int _lastClipboardHash;

    /// <summary>
    /// Timestamp (Environment.TickCount64) of the last received PONG. Written by
    /// the receive loop, read by the capture loop for TDD §7 15s heartbeat timeout.
    /// </summary>
    private long _lastPongTicks;

    /// <summary>
    /// F25: hàng đợi lệnh inject chuột/bàn phím — tách khỏi <see cref="RunReceiveLoopAsync"/>.
    /// <see cref="Input.MouseInjector.Move"/>/<c>Button</c> gọi <c>XSync</c> (round-trip đồng bộ
    /// tới X server); nếu X server bận (VD capture đang fallback XGetImage sau khi XShmAttach
    /// bị từ chối — nặng tải hơn XShm nhiều), XSync có thể trễ đủ lâu để chặn luôn việc đọc
    /// gói Pong kế tiếp trong CÙNG vòng lặp → watchdog heartbeat (chạy độc lập, xem
    /// <see cref="RunHeartbeatWatchdogAsync"/>) tưởng mất kết nối và ngắt session dù đường
    /// truyền vẫn ổn — verify thật trên ZCU kztek-ZCU2 (192.168.21.93/96): remote xem được
    /// nhưng không điều khiển được + kết nối liên tục bị ngắt/nối lại ~15s/lần.
    /// SingleReader/SingleWriter=true vì chỉ 1 producer (receive loop) và 1 consumer
    /// (<see cref="RunInputInjectorLoopAsync"/>) mỗi bên — giữ đúng thứ tự sự kiện input.
    /// </summary>
    internal enum InputCommandKind : byte { MouseMove, MouseButton, KeyEvent, KeyEventRaw }
    internal readonly record struct InputCommand(InputCommandKind Kind, int X, int Y, MouseButton Button = MouseButton.Left, bool IsDown = false, uint Key = 0);

    private readonly Channel<InputCommand> _inputQueue = Channel.CreateUnbounded<InputCommand>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    /// <summary>Ngưỡng cảnh báo (không phải hủy) khi 1 lệnh inject chạy lâu hơn mức này — xem log để biết X server đang chậm.</summary>
    private const int InjectWarnMs = 2_000;

    public ClientSession(
        TcpClient tcp, string remoteIp,
        IScreenCapturer capturer, IFrameEncoder encoder,
        IMouseInjector injector, IKeyboardInjector keyboard,
        IClipboardService clipboard, IMonitorProvider monitorProvider, IDesktopPlatformService platform,
        AuthManager auth, IOptions<AgentOptions> options, ILogger logger)
    {
        _tcp      = tcp;
        _remoteIp = remoteIp;
        _capturer = capturer;
        _encoder  = encoder;
        _injector = injector;
        _keyboard = keyboard;
        _clipboard = clipboard;
        _monitorProvider = monitorProvider;
        _platform = platform;
        _auth     = auth;
        _options  = options.Value;
        _logger   = logger;
    }

    // ── Entry point ───────────────────────────────────────────────────────

    /// <summary>
    /// Run the full session lifecycle. Returns when the client disconnects or an
    /// unrecoverable error occurs. Always safe to await from TcpServer.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            _stream = await OpenStreamAsync(ct);
            await DoHandshakeAsync(ct);
            await DoAuthAsync(ct);
            _logger.LogInformation("Session {IP}: streaming started", _remoteIp);
            Interlocked.Exchange(ref _lastPongTicks, Environment.TickCount64);
            _files = new FileTransferHandler(_options, _logger, _clipboard, WriteAsync);

            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var captureTask  = RunCaptureSendLoopAsync(sessionCts.Token);
            var receiveTask  = RunReceiveLoopAsync(sessionCts.Token);
            // Heartbeat timeout runs in its OWN task (audit L3): if the check lived in the
            // capture loop it could never fire while that loop is blocked in WriteAsync on
            // a slow reader — the exact situation the timeout exists to detect.
            var watchdogTask = RunHeartbeatWatchdogAsync(sessionCts.Token);
            // F25: inject chuột/bàn phím chạy trên task RIÊNG, không còn nằm trong receiveTask
            // — xem giải thích tại _inputQueue.
            var inputTask    = RunInputInjectorLoopAsync(sessionCts.Token);
            // Đồng bộ clipboard ZCU→CCU (clipboard hai chiều).
            var clipboardTask = RunClipboardWatchLoopAsync(sessionCts.Token);

            // Stop everything when any loop ends
            await Task.WhenAny(captureTask, receiveTask, watchdogTask, inputTask, clipboardTask);
            await sessionCts.CancelAsync();
            _inputQueue.Writer.TryComplete();
            await Task.WhenAll(captureTask, receiveTask, watchdogTask, inputTask, clipboardTask);
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (EndOfStreamException ex)
        {
            _logger.LogInformation("Session {IP}: client closed connection ({Msg})", _remoteIp, ex.Message);
        }
        catch (ProtocolException ex)
        {
            _logger.LogWarning("Session {IP}: protocol error — {Msg}", _remoteIp, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {IP}: unexpected error", _remoteIp);
        }
        finally
        {
            // Release any stuck keys before closing so the ZCU desktop does not
            // see permanently-pressed modifiers (Shift, Ctrl, Alt, …). TDD §17.4.
            _keyboard.ReleaseAllKeys();
            _files?.Dispose();
            _writeLock.Dispose();
            try { _tcp.Close(); } catch { }
            _logger.LogInformation("Session {IP}: closed", _remoteIp);
        }
    }

    // ── TLS detection ─────────────────────────────────────────────────────

    /// <summary>
    /// Nhìn trước (peek) byte đầu tiên: 0x16 = TLS ClientHello → bọc SslStream; 0x01 (HELLO của
    /// giao thức) = TCP thường của client cũ. Không đọc/tiêu thụ byte nào của luồng dữ liệu.
    /// </summary>
    private async Task<Stream> OpenStreamAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RemoteControlConstants.HandshakeTimeoutMs);

        var first = new byte[1];
        var n = await _tcp.Client.ReceiveAsync(first, System.Net.Sockets.SocketFlags.Peek, cts.Token);
        if (n == 0) throw new EndOfStreamException("Client đóng kết nối trước khi gửi dữ liệu");

        if (first[0] == 0x16)
        {
            var cert = TlsCertificateProvider.GetOrCreate(_options, _logger);
            var ssl = new System.Net.Security.SslStream(_tcp.GetStream(), leaveInnerStreamOpen: false);
            await ssl.AuthenticateAsServerAsync(new System.Net.Security.SslServerAuthenticationOptions
            {
                ServerCertificate = cert,
                ClientCertificateRequired = false,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                                    | System.Security.Authentication.SslProtocols.Tls13,
            }, cts.Token);
            _logger.LogInformation("Session {IP}: TLS established ({Proto})", _remoteIp, ssl.SslProtocol);
            return ssl;
        }

        if (_options.RequireTls)
            throw new ProtocolException("RequireTls=true nhưng client kết nối không mã hóa — từ chối");

        _logger.LogWarning("Session {IP}: kết nối KHÔNG mã hóa (client cũ) — nên cập nhật CCU rồi bật RequireTls", _remoteIp);
        return _tcp.GetStream();
    }

    // ── Handshake ─────────────────────────────────────────────────────────

    private async Task DoHandshakeAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RemoteControlConstants.HandshakeTimeoutMs);
        var hsCt = cts.Token;

        var (type, payload) = await MessageCodec.ReadMessageAsync(_stream!, hsCt);
        if (type != MessageType.Hello)
            throw new ProtocolException($"Expected HELLO, got {type}");

        // payload: u8 version + u16 nameLen + utf8 name — just accept any v1
        _logger.LogDebug("Session {IP}: HELLO received", _remoteIp);

        var screenSize = _capturer.ScreenSize;
        // F02: bump 1.0 → 1.1 khi đã hỗ trợ nhóm tính năng Enterprise (SysInfo/Privacy/
        // Chat/Clipboard). Client so sánh chuỗi này để cảnh báo agent phiên bản cũ
        // (agent 1.0 bỏ qua các message đó âm thầm — protocol v1 không có ACK).
        var ackPayload = MessageCodec.EncodeHelloAck(
            (uint)screenSize.Width, (uint)screenSize.Height, $"{_platform.AgentName}/1.2");
        await WriteAsync(MessageType.HelloAck, ackPayload, hsCt);
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    private async Task DoAuthAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RemoteControlConstants.HandshakeTimeoutMs);
        var authCt = cts.Token;

        var (type, payload) = await MessageCodec.ReadMessageAsync(_stream!, authCt);
        if (type != MessageType.Auth)
            throw new ProtocolException($"Expected AUTH, got {type}");

        var token = MessageCodec.DecodeAuth(payload);

        var (ok, reason) = _auth.ValidateToken(_remoteIp, token);
        if (!ok)
        {
            var failPayload = MessageCodec.EncodeAuthFail(reason);
            await WriteAsync(MessageType.AuthFail, failPayload, authCt);
            throw new ProtocolException($"AUTH_FAIL: {reason}");
        }

        await WriteAsync(MessageType.AuthOk, Array.Empty<byte>(), authCt);
    }

    // ── Capture + send loop (producer) ────────────────────────────────────

    private async Task RunCaptureSendLoopAsync(CancellationToken ct)
    {
        var frameId = 0L;

        // Nấc 1 — FPS ZCU: chỉ điều tiết theo MẠNG (xem AdaptiveRateController). Khi CPU là giới
        // hạn, vòng lặp tự chạy liền mạch ở tốc độ nén tối đa (sleep ≤ 0), giữ nguyên chất lượng.
        // Nấc 1 — FPS ZCU: điều tiết theo mạng (AdaptiveRateController) và tải thực tế của máy (AdaptiveFpsController).
        var rate = new AdaptiveRateController(_options.TargetFps, _options.JpegQuality, _options.AdaptiveQuality);
        var fpsController = new AdaptiveFpsController(
            maxFps: _options.TargetFps,
            minFps: _options.MinFps,
            enabled: _options.CpuAdaptive);

        using var sampler = _platform.CreateLoadSampler();
        if (_options.CpuAdaptive)
        {
            sampler.Start(ct);
        }

        var lastSampledTimestamp = 0L;
        var lastWriteMs = 0.0;

        // Bỏ qua khung tĩnh: mỗi session một detector riêng → khung đầu tiên luôn được gửi.
        var detector = _options.SkipUnchangedFrames ? new FrameChangeDetector() : null;

        // Bước 0 — đo đạc từng giai đoạn, ghi log định kỳ.
        var perfIntervalMs = Math.Max(0, _options.PerfLogIntervalSec) * 1000L;
        var stats          = new StreamStats(Environment.TickCount64);
        var nextPerfLog    = Environment.TickCount64 + perfIntervalMs;

        var lastSentMs      = 0L;
        var unchangedStreak = 0;
        var sw              = Stopwatch.StartNew();
        var nextPing        = Environment.TickCount64 + RemoteControlConstants.PingIntervalMs;

        _logger.LogInformation(
            "Session {IP}: stream {Fps}fps (sàn {MinFps}fps, cpu-adaptive={CpuAdaptive}) q={Q}, net-adaptive={NetAdaptive}, bỏ qua khung tĩnh={Skip}, perf log {Perf}s",
            _remoteIp, fpsController.MaxFps, fpsController.MinFps, _options.CpuAdaptive, rate.MaxQuality, _options.AdaptiveQuality, detector is not null,
            _options.PerfLogIntervalSec);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                sw.Restart();

                // PING keepalive (TDD §7). The PONG-timeout check deliberately does NOT
                // live here — see RunHeartbeatWatchdogAsync (audit L3): this loop can be
                // blocked inside WriteAsync by a slow reader, so a timeout check placed
                // here would never fire exactly when it is needed.
                var now = Environment.TickCount64;
                if (now >= nextPing)
                {
                    await WriteAsync(MessageType.Ping,
                        MessageCodec.EncodePingPong((ulong)now), ct);
                    nextPing = now + RemoteControlConstants.PingIntervalMs;
                }

                // Cập nhật tình trạng tải máy ZCU từ MachineLoadSampler (~500ms / mẫu)
                if (_options.CpuAdaptive)
                {
                    var load = sampler.Current;
                    if (load.TimestampMs > lastSampledTimestamp)
                    {
                        lastSampledTimestamp = load.TimestampMs;
                        if (fpsController.OnSample(load.XLatencyMs, load.CpuPressureAvg10, lastWriteMs))
                        {
                            _logger.LogInformation(
                                "Session {IP}: điều tiết FPS theo TẢI MÁY ({Reason}) → trần {Fps:F1}fps",
                                _remoteIp, fpsController.LastReason, fpsController.CurrentFps);
                        }
                    }
                }

                // 1) Capture
                var t0 = Stopwatch.GetTimestamp();
                var frame = _capturer.Capture();
                var captureMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;

                if (frame is not null)
                {
                    // 2) Crop theo màn hình đã chọn + so sánh với khung trước
                    var t1 = Stopwatch.GetTimestamp();
                    frame = CropToMonitor(frame);
                    var changed = detector is null || HasFrameChanged(detector, frame);
                    var compareMs = Stopwatch.GetElapsedTime(t1).TotalMilliseconds;

                    var nowMs = Environment.TickCount64;
                    if (!changed && nowMs - lastSentMs < StaticRefreshMs)
                    {
                        unchangedStreak++;
                        stats.RecordSkipped(captureMs, compareMs);
                    }
                    else
                    {
                        unchangedStreak = 0;

                        // 3) Encode
                        // NOTE: jpeg is backed by the encoder's reused buffer (audit Q3) —
                        // consumed synchronously below, before the next EncodeJpeg call.
                        var t2 = Stopwatch.GetTimestamp();
                        var jpeg = _encoder.EncodeJpeg(frame, rate.CurrentQuality);
                        var encodeMs = Stopwatch.GetElapsedTime(t2).TotalMilliseconds;

                        if (jpeg.Length > 0 && jpeg.Length <= _options.MaxFrameBytes)
                        {
                            // 4) Write
                            var tsMs = (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0xFFFF_FFFF);
                            var t3 = Stopwatch.GetTimestamp();
                            // Zero-Allocation Streaming (F-ROUND7-1): Stream trực tiếp qua WriteFrameJpegDirectAsync
                            await WriteFrameJpegDirectAsync(frameId++, tsMs, frame.Width, frame.Height, jpeg, ct);
                            var writeMs = Stopwatch.GetElapsedTime(t3).TotalMilliseconds;

                            lastWriteMs = writeMs;
                            lastSentMs = Environment.TickCount64;
                            stats.RecordSent(captureMs, compareMs, encodeMs, writeMs, jpeg.Length);

                            if (rate.OnFrameWritten(writeMs))
                            {
                                _logger.LogInformation(
                                    "Session {IP}: điều tiết theo MẠNG (ghi {Write:F0}ms, ngưỡng {Slow}ms) → trần {Fps:F1}fps, q={Q}",
                                    _remoteIp, writeMs, rate.SlowWriteMs, rate.CurrentFps, rate.CurrentQuality);
                            }
                        }
                        else if (jpeg.Length > _options.MaxFrameBytes)
                        {
                            _logger.LogWarning("Frame {Id} JPEG {Len} B exceeds MaxFrameBytes — dropped",
                                frameId, jpeg.Length);
                        }
                        else
                        {
                            // Nén lỗi (đã log trong encoder) → lần sau phải gửi lại dù màn hình không đổi.
                            detector?.Reset();
                        }
                    }
                }

                // Log hiệu năng định kỳ (Bước 0)
                if (perfIntervalMs > 0 && Environment.TickCount64 >= nextPerfLog)
                {
                    var s = stats.TakeSnapshot(Environment.TickCount64);
                    nextPerfLog = Environment.TickCount64 + perfIntervalMs;
                    _logger.LogInformation(
                        "Perf {IP}: gửi {Sent:F1}fps, bỏ qua(tĩnh) {Skip:F1}fps | capture {CapAvg:F1}/{CapP95:F1}ms | " +
                        "so sánh {CmpAvg:F1}/{CmpP95:F1}ms | nén {EncAvg:F1}/{EncP95:F1}ms | ghi {WrAvg:F1}/{WrP95:F1}ms | " +
                        "{Kb:F0}KB/frame, {Kbps:F0}kbps | trần {Fps:F1}fps (X-RTT {XRtt:F1}ms, PSI {Psi:F1}%) q={Q} (avg/p95)",
                        _remoteIp, s.SentFps, s.SkippedFps,
                        s.Capture.AvgMs, s.Capture.P95Ms,
                        s.Compare.AvgMs, s.Compare.P95Ms,
                        s.Encode.AvgMs, s.Encode.P95Ms,
                        s.Write.AvgMs, s.Write.P95Ms,
                        s.AvgJpegKb, s.BitrateKbps, Math.Min(rate.CurrentFps, fpsController.CurrentFps),
                        sampler.Current.XLatencyMs >= 0 ? sampler.Current.XLatencyMs : 0,
                        sampler.Current.CpuPressureAvg10 >= 0 ? sampler.Current.CpuPressureAvg10 : 0,
                        rate.CurrentQuality);
                }

                // Throttle: theo trần FPS; màn hình tĩnh một lúc thì chụp thưa lại (IdlePollMs).
                var effectiveFps = Math.Min(rate.CurrentFps, fpsController.CurrentFps);
                var interval = (int)Math.Max(1, Math.Round(1000.0 / effectiveFps));
                if (unchangedStreak >= IdleStreakThreshold)
                    interval = Math.Max(interval, IdlePollMs);

                var sleep = interval - (int)sw.ElapsedMilliseconds;
                if (sleep > 0)
                {
                    await Task.Delay(sleep, ct);
                }
                else
                {
                    // CPU là giới hạn: chạy liền mạch, chỉ yield để giải phóng _writeLock cho Pong/Input.
                    await Task.Yield();
                }
            }
        }
        catch (OperationCanceledException) { /* expected */ }
    }

    /// <summary>
    /// So sánh khung hiện tại với khung trước (bỏ qua khung tĩnh). Tách thành method đồng bộ
    /// vì ReadOnlySpan không được dùng làm biến cục bộ trong async method.
    /// </summary>
    private static unsafe bool HasFrameChanged(FrameChangeDetector detector, CapturedFrame frame)
    {
        var length = (long)frame.BytesPerRow * frame.Height;
        if (length <= 0 || length > int.MaxValue) return true;

        var pixels = frame.PixelPtr != IntPtr.Zero
            ? new ReadOnlySpan<byte>((void*)frame.PixelPtr, (int)length)
            : frame.PixelData.AsSpan(0, (int)Math.Min(length, frame.PixelData.Length));

        return detector.HasChanged(pixels, frame.Width, frame.Height, frame.BytesPerRow);
    }

    // ── Multi-monitor helpers ─────────────────────────────────────────────

    private static string MonitorsToJson(List<MonitorInfo> list)
        => System.Text.Json.JsonSerializer.Serialize(list.Select(m => new
        {
            index = m.Index, name = m.Name, x = m.X, y = m.Y, w = m.W, h = m.H,
        }));

    /// <summary>
    /// Cắt vùng màn hình đã chọn ra khỏi khung toàn desktop (BGRA, 4 byte/pixel) vào buffer tái sử dụng.
    /// Vùng nằm ngoài khung (đổi độ phân giải/cắm rút màn hình) → trả nguyên khung toàn desktop.
    /// </summary>
    private CapturedFrame CropToMonitor(CapturedFrame frame)
    {
        var m = _monitor;
        if (m is null) return frame;

        if (m.X < 0 || m.Y < 0 || m.W <= 0 || m.H <= 0 ||
            m.X + m.W > frame.Width || m.Y + m.H > frame.Height)
            return frame;

        var rowBytes = m.W * 4;
        var need = rowBytes * m.H;
        if (_cropBuffer.Length < need) _cropBuffer = new byte[need];

        if (frame.PixelPtr != IntPtr.Zero)
        {
            for (var row = 0; row < m.H; row++)
            {
                var srcPtr = IntPtr.Add(frame.PixelPtr, (m.Y + row) * frame.BytesPerRow + m.X * 4);
                System.Runtime.InteropServices.Marshal.Copy(srcPtr, _cropBuffer, row * rowBytes, rowBytes);
            }
        }
        else
        {
            for (var row = 0; row < m.H; row++)
                Buffer.BlockCopy(frame.PixelData, (m.Y + row) * frame.BytesPerRow + m.X * 4,
                                 _cropBuffer, row * rowBytes, rowBytes);
        }

        return new CapturedFrame
        {
            PixelData   = _cropBuffer,
            Width       = m.W,
            Height      = m.H,
            BytesPerRow = rowBytes,
        };
    }

    // ── Heartbeat watchdog (audit L3) ─────────────────────────────────────

    /// <summary>
    /// Independent PONG-timeout watchdog (TDD §7, 15s). Runs in its own task so it
    /// keeps firing even when the capture loop is blocked in <see cref="WriteAsync"/>
    /// on a slow/stalled reader. Performs NO network writes — returning is enough:
    /// RunAsync cancels the session when any loop completes.
    /// </summary>
    private async Task RunHeartbeatWatchdogAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
                var lastPong = Interlocked.Read(ref _lastPongTicks);
                if (lastPong != 0 &&
                    Environment.TickCount64 - lastPong > RemoteControlConstants.PingTimeoutMs)
                {
                    _logger.LogWarning("Session {IP}: no PONG within {Ms}ms — disconnecting",
                        _remoteIp, RemoteControlConstants.PingTimeoutMs);
                    return;
                }
            }
        }
        catch (OperationCanceledException) { /* expected */ }
    }

    // ── Receive loop (consumer) ───────────────────────────────────────────

    private async Task RunReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var (type, payload) = await MessageCodec.ReadMessageAsync(_stream!, ct);
                switch (type)
                {
                    // F25: MouseMove/MouseButton/KeyEvent chỉ ENQUEUE (không chặn) — thực thi
                    // thật nằm ở RunInputInjectorLoopAsync, tách khỏi vòng đọc để không làm
                    // trễ việc nhận Pong khi XSync của injector bị X server làm chậm.
                    case MessageType.MouseMove:
                        var (mx, my) = MessageCodec.DecodeMouseMove(payload);
                        // Toạ độ client gửi là trong khung hình đang xem → cộng offset màn hình đã chọn.
                        if (_monitor is { } mm) { mx += mm.X; my += mm.Y; }
                        _inputQueue.Writer.TryWrite(new InputCommand(InputCommandKind.MouseMove, mx, my));
                        break;

                    case MessageType.MouseButton:
                        var (btn, down, bx, by) = MessageCodec.DecodeMouseButton(payload);
                        // Clamp to screen bounds per TDD §5.3.
                        // Read ScreenSize ONCE into a local (audit L9): the property is
                        // republished atomically by the capture thread on resolution change;
                        // two separate reads could observe new-W paired with old-H.
                        var screen = _capturer.ScreenSize;
                        int offX = 0, offY = 0, areaW = screen.Width, areaH = screen.Height;
                        if (_monitor is { } sel) { offX = sel.X; offY = sel.Y; areaW = sel.W; areaH = sel.H; }
                        var sx = offX + Math.Clamp(bx, 0, areaW - 1);
                        var sy = offY + Math.Clamp(by, 0, areaH - 1);
                        _inputQueue.Writer.TryWrite(new InputCommand(InputCommandKind.MouseButton, sx, sy, btn, down));
                        break;

                    case MessageType.Ping:
                        var nonce = MessageCodec.DecodePingPong(payload);
                        await WriteAsync(MessageType.Pong, MessageCodec.EncodePingPong(nonce), ct);
                        break;

                    case MessageType.Pong:
                        // CCU responded to our PING — refresh heartbeat timestamp (TDD §7)
                        Interlocked.Exchange(ref _lastPongTicks, Environment.TickCount64);
                        break;

                    case MessageType.KeyEvent:
                        var (keysym, isDown) = MessageCodec.DecodeKeyEvent(payload);
                        _inputQueue.Writer.TryWrite(new InputCommand(InputCommandKind.KeyEvent, 0, 0, MouseButton.Left, isDown, keysym));
                        break;
                        
                    // ── File transfer (chỉ trong TransferDir) ─────────────────
                    case MessageType.FileBegin:
                    case MessageType.FileChunk:
                    case MessageType.FileEnd:
                    case MessageType.FileListReq:
                    case MessageType.FileGet:
                    case MessageType.FileGetPath:
                    case MessageType.ClipboardFiles:
                        if (_files is not { Enabled: true })
                        {
                            if (type == MessageType.FileBegin)
                                await WriteAsync(MessageType.FileAck,
                                    [payload.Length > 0 ? payload[0] : (byte)0, 2, ..System.Text.Encoding.UTF8.GetBytes("Máy đích tắt tính năng truyền file")], ct);
                            break;
                        }
                        switch (type)
                        {
                            case MessageType.FileBegin:   await _files.OnBeginAsync(payload, ct); break;
                            case MessageType.FileChunk:   await _files.OnChunkAsync(payload, ct); break;
                            case MessageType.FileEnd:     await _files.OnEndAsync(payload, ct);   break;
                            case MessageType.FileListReq: await _files.OnListAsync(ct);           break;
                            case MessageType.FileGet:     await _files.OnGetAsync(payload, ct);   break;
                            case MessageType.FileGetPath: await _files.OnGetPathAsync(payload, ct); break;
                            case MessageType.ClipboardFiles:
                                // Chặn poll gửi ngược chính các file vừa đặt lên clipboard ZCU về CCU.
                                if (_files.OnClipboardFiles(payload) is { } placed)
                                    Volatile.Write(ref _lastClipboardHash, placed.GetHashCode());
                                break;
                        }
                        break;

                    case MessageType.MonitorListReq:
                        _monitors = await _monitorProvider.ListAsync(ct);
                        await WriteAsync(MessageType.MonitorList,
                            MessageCodec.EncodeStringMessage(MonitorsToJson(_monitors)), ct);
                        break;

                    case MessageType.MonitorSelect:
                        if (payload.Length >= 1)
                        {
                            var idx = payload[0];
                            _monitor = _monitors.FirstOrDefault(m => m.Index == idx); // 0xFF/không có → null = toàn desktop
                            _logger.LogInformation("Session {IP}: chọn màn hình {Idx} → {Mon}", _remoteIp, idx,
                                _monitor is null ? "toàn desktop" : $"{_monitor.Name} {_monitor.W}x{_monitor.H}+{_monitor.X}+{_monitor.Y}");
                        }
                        break;

                    case MessageType.KeyEventRaw:
                        var (rawCode, rawDown) = MessageCodec.DecodeKeyEventRaw(payload);
                        _inputQueue.Writer.TryWrite(new InputCommand(InputCommandKind.KeyEventRaw, 0, 0, MouseButton.Left, rawDown, rawCode));
                        break;

                    // ── Phase 6 Enterprise Features ───────────────────────────
                    case MessageType.ChatText:
                        var chatMsg = MessageCodec.DecodeStringMessage(payload);
                        if (chatMsg.Length > MaxChatTextChars)
                        {
                            _logger.LogWarning("Session {IP}: CHAT text {Len} chars exceeds cap {Cap} — truncated",
                                _remoteIp, chatMsg.Length, MaxChatTextChars);
                            chatMsg = chatMsg[..MaxChatTextChars];
                        }
                        _logger.LogInformation("CHAT from CCU: {Msg}", chatMsg);
                        // Agent is a Linux/X11 service (Program.cs enforces X11) — the old
                        // Win32NT "msg" branch was dead code and has been removed (audit Q2).
                        if (_options.EnableDesktopIntegration)
                            await _platform.ShowChatNotificationAsync(chatMsg, ct);
                        break;

                    case MessageType.ClipboardData:
                        var clipData = MessageCodec.DecodeStringMessage(payload);
                        _logger.LogInformation("CLIPBOARD from CCU (len: {Len})", clipData.Length);
                        if (clipData.Length > MaxClipboardChars)
                        {
                            _logger.LogWarning("Session {IP}: clipboard {Len} chars exceeds cap {Cap} — ignored",
                                _remoteIp, clipData.Length, MaxClipboardChars);
                            break;
                        }
                        if (_options.EnableDesktopIntegration)
                        {
                            Volatile.Write(ref _lastClipboardHash, clipData.GetHashCode());
                            await _clipboard.SetTextAsync(clipData, ct);
                        }
                        break;

                    case MessageType.ClipboardGet:
                        if (_options.EnableDesktopIntegration)
                            await PushClipboardAsync(force: true, markOnly: false, ct);
                        break;

                    case MessageType.PrivacyMode:
                        var privacyEnabled = MessageCodec.DecodeBooleanMessage(payload);
                        _logger.LogWarning("PRIVACY MODE: {State}", privacyEnabled ? "ON" : "OFF");
                        // In a real prod env, this would use X11 APIs to map a black window on top 
                        // and grab pointer/keyboard exclusively. For now, just log.
                        break;

                    case MessageType.SysInfoReq:
                        _logger.LogInformation("SysInfoReq received, gathering system info...");
                        string sysInfoJson = _platform.GatherSystemInfoJson();
                        await WriteAsync(MessageType.SysInfoResp, MessageCodec.EncodeStringMessage(sysInfoJson), ct);
                        break;

                    case MessageType.Bye:
                        _logger.LogInformation("Session {IP}: client sent BYE", _remoteIp);
                        return;

                    default:
                        _logger.LogDebug("Session {IP}: unknown type 0x{T:X2} — ignored", _remoteIp, (byte)type);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { /* expected */ }
        catch (EndOfStreamException)       { /* client disconnected */ throw; }
        // NOTE (audit L4): ProtocolException from MessageCodec decoders (malformed payload)
        // intentionally propagates out of this loop — RunAsync catches it separately,
        // logs a Warning ("protocol error") and closes the session cleanly. It must NOT
        // be swallowed here nor fall into a generic Error log.
    }

    // ── F25: Input injector consumer (decoupled from receive loop) ────────

    /// <summary>
    /// Thực thi tuần tự các lệnh inject chuột/bàn phím đã enqueue từ <see cref="RunReceiveLoopAsync"/>.
    /// Chạy trên task riêng để không chặn việc xử lý Pong (F25, xem giải thích ở _inputQueue).
    /// <para>
    /// F26 (đã verify thật trên ZCU kztek — 192.168.21.97): bản trước KHÔNG await task chậm mà
    /// đọc luôn lệnh kế tiếp và bắn <c>Task.Run</c> MỚI — tưởng là "bỏ qua lệnh chậm, không bị
    /// chặn", nhưng <see cref="Input.MouseInjector"/>/<see cref="Input.KeyboardInjector"/> dùng
    /// CHUNG 1 <c>Display*</c> Xlib, và Xlib tự khoá nội bộ mỗi <c>Display*</c> (kể cả sau
    /// <c>XInitThreads</c>) — mọi <c>Task.Run</c> mới chỉ NẰM CHỜ khoá đó, KHÔNG chạy song song
    /// thật. Kết quả: mỗi task "bị bỏ qua" vẫn chiếm 1 thread pool thread bị block, và MỌI task
    /// kế tiếp lại tự tính thêm 2000ms rồi cảnh báo — tạo hiệu ứng domino (verify thật: 1 lần X
    /// server khựng sinh ra 66-118 dòng cảnh báo liên tiếp trong vài giây, y hệt triệu chứng
    /// "remote lag/không ổn định" user báo). Fix: vẫn log cảnh báo sớm ở mốc InjectWarnMs (giữ
    /// nguyên khả năng chẩn đoán), nhưng LUÔN <c>await task</c> tới khi xong thật trước khi đọc
    /// lệnh kế tiếp — đảm bảo tại một thời điểm chỉ có ĐÚNG 1 lệnh Xlib đang chạy, không còn
    /// pile-up thread bị khoá.
    /// </para>
    /// </summary>
    private async Task RunInputInjectorLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var cmd in _inputQueue.Reader.ReadAllAsync(ct))
            {
                var sw = Stopwatch.StartNew();
                switch (cmd.Kind)
                {
                    case InputCommandKind.MouseMove:
                        // Mouse coalescing (audit C3): nếu các lệnh tiếp theo cũng là MouseMove thì nhảy thẳng đến toạ độ mới nhất
                        var finalX = cmd.X;
                        var finalY = cmd.Y;
                        while (_inputQueue.Reader.TryPeek(out var next) && next.Kind == InputCommandKind.MouseMove)
                        {
                            _inputQueue.Reader.TryRead(out next);
                            finalX = next.X;
                            finalY = next.Y;
                        }
                        _injector.Move(finalX, finalY);
                        break;

                    case InputCommandKind.MouseButton:
                        _injector.Move(cmd.X, cmd.Y);
                        _injector.Button(cmd.Button, cmd.IsDown);
                        break;

                    case InputCommandKind.KeyEvent:
                        _keyboard.SendKey(cmd.Key, cmd.IsDown);
                        break;

                    case InputCommandKind.KeyEventRaw:
                        _keyboard.SendRawKey((byte)cmd.Key, cmd.IsDown);
                        break;
                }

                if (sw.ElapsedMilliseconds > InjectWarnMs)
                {
                    _logger.LogWarning(
                        "Session {IP}: lệnh inject {Kind} chạy quá {Ms}ms (X server có thể đang chậm)",
                        _remoteIp, cmd.Kind, sw.ElapsedMilliseconds);
                }
            }
        }
        catch (OperationCanceledException) { /* expected */ }
    }

    // ── Thread-safe write ─────────────────────────────────────────────────

    /// <summary>
    /// Serialized, timeout-bounded write (audit L3). NetworkStream.WriteTimeout does NOT
    /// apply to async writes, so a per-write CancellationToken bounds the operation: a
    /// client that stops reading (TCP backpressure) fails the session after
    /// <see cref="WriteTimeoutMs"/> instead of holding the single v1 session slot forever.
    /// </summary>
    /// <summary>
    /// Stream khung hình JPEG trực tiếp không cấp phát bộ nhớ heap (F-ROUND7-1 Zero-Allocation).
    /// Ghép 5 bytes wire header + 24 bytes metadata vào <see cref="_frameHeaderBuffer"/> (29 bytes cố định),
    /// sau đó stream trực tiếp mảng pixel JPEG đã nén từ encoder buffer mà không tạo bản sao trung gian.
    /// </summary>
    private async Task WriteFrameJpegDirectAsync(
        long frameId, uint timestampMs, int width, int height,
        ReadOnlyMemory<byte> jpegData, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(WriteTimeoutMs);
            var writeCt = cts.Token;

            // 1. Wire header (5 bytes)
            var totalPayloadLen = MessageCodec.FrameMetadataHeaderSize + jpegData.Length;
            _frameHeaderBuffer[0] = (byte)MessageType.FrameJpeg;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
                _frameHeaderBuffer.AsSpan(1, 4), (uint)totalPayloadLen);

            // 2. Frame metadata (24 bytes)
            MessageCodec.WriteFrameMetadataHeader(
                _frameHeaderBuffer.AsSpan(MessageCodec.HeaderSize, MessageCodec.FrameMetadataHeaderSize),
                frameId, timestampMs, width, height, jpegData.Length);

            // 3. Ghi 29 bytes header vào stream
            await _stream!.WriteAsync(_frameHeaderBuffer.AsMemory(0, MessageCodec.HeaderSize + MessageCodec.FrameMetadataHeaderSize), writeCt);

            // 4. Ghi trực tiếp jpegData từ buffer tái sử dụng của JpegEncoder vào stream
            await _stream!.WriteAsync(jpegData, writeCt);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new IOException(
                $"Write of FrameJpeg timed out after {WriteTimeoutMs} ms — slow or stalled reader");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private Task WriteAsync(MessageType type, byte[] payload, CancellationToken ct)
        => WriteAsync(type, (ReadOnlyMemory<byte>)payload.AsMemory(), ct);

    private async Task WriteAsync(MessageType type, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(WriteTimeoutMs);
            try
            {
                await MessageCodec.WriteMessageAsync(_stream!, type, payload, cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout (not session shutdown) — surface as IOException so RunAsync
                // tears the session down instead of treating it as a normal cancel.
                throw new IOException(
                    $"Write of {type} timed out after {WriteTimeoutMs} ms — slow or stalled reader");
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Poll clipboard ZCU định kỳ và đẩy sang CCU khi nội dung đổi (chữ → ClipboardData, file vừa Copy trong
    /// trình quản lý file → ClipboardFileList). Nếu xclip không có/không đọc được thì bỏ qua êm.
    /// </summary>
    private async Task RunClipboardWatchLoopAsync(CancellationToken ct)
    {
        try
        {
            if (!_options.EnableDesktopIntegration)
            {
                await Task.Delay(Timeout.Infinite, ct);
                return;
            }

            // Mốc ban đầu: nội dung có sẵn trên ZCU không tự đẩy sang CCU.
            await PushClipboardAsync(force: false, markOnly: true, ct);

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(ClipboardPollMs, ct);
                await PushClipboardAsync(force: false, markOnly: false, ct);
            }
        }
        catch (OperationCanceledException) { /* expected */ }
    }

    /// <summary>
    /// Đọc clipboard ZCU và gửi sang CCU. <paramref name="force"/>: gửi dù không đổi (CCU chủ động kéo sau Ctrl+C).
    /// <paramref name="markOnly"/>: chỉ ghi nhớ mốc, không gửi.
    /// </summary>
    private async Task PushClipboardCoreAsync(bool force, bool markOnly, CancellationToken ct)
    {
        // 1) File vừa Copy trong trình quản lý file?
        var files = await _clipboard.GetFilePathsAsync(ct);
        if (files is not null)
        {
            var key = string.Join("\n", files).GetHashCode();
            if (markOnly) { Volatile.Write(ref _lastClipboardHash, key); return; }
            if (!force && key == Volatile.Read(ref _lastClipboardHash)) return;
            Volatile.Write(ref _lastClipboardHash, key);

            var infos = ExpandClipboardEntries(files);
            if (infos.Count == 0 || _files is not { Enabled: true }) return;

            _files.SetAdvertised(infos.Where(i => !i.dir).Select(i => i.path));
            _logger.LogInformation("CLIPBOARD files to CCU ({Count} mục)", infos.Count);
            await WriteAsync(MessageType.ClipboardFileList,
                MessageCodec.EncodeStringMessage(System.Text.Json.JsonSerializer.Serialize(infos)), ct);
            return;
        }

        // 2) Chữ
        var text = await _clipboard.GetTextAsync(ct);
        if (string.IsNullOrEmpty(text) || text.Length > MaxClipboardChars) return;

        var hash = text.GetHashCode();
        if (markOnly) { Volatile.Write(ref _lastClipboardHash, hash); return; }
        if (!force && hash == Volatile.Read(ref _lastClipboardHash)) return;

        Volatile.Write(ref _lastClipboardHash, hash);
        _logger.LogInformation("CLIPBOARD to CCU (len: {Len})", text.Length);
        await WriteAsync(MessageType.ClipboardData, MessageCodec.EncodeStringMessage(text), ct);
    }

    private const int MaxClipboardEntries = 5_000;

    private sealed record ClipEntry(string path, string name, long size, bool dir);

    /// <summary>
    /// Mở rộng các mục vừa Copy trên ZCU thành danh sách phẳng: file → 1 mục; thư mục → chính nó (dir=true, để giữ
    /// cả thư mục rỗng) rồi mọi mục con. <c>name</c> là đường dẫn tương đối từ mục gốc, phân tách bằng '/' (vd
    /// "Báo cáo/2026/a.xlsx"). Không đi theo liên kết tượng trưng; bỏ file vượt giới hạn; tối đa
    /// <see cref="MaxClipboardEntries"/> mục.
    /// </summary>
    private List<ClipEntry> ExpandClipboardEntries(List<string> tops)
    {
        var result = new List<ClipEntry>();
        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip      = FileAttributes.ReparsePoint, // không theo symlink
            IgnoreInaccessible    = true,
        };

        foreach (var top in tops)
        {
            if (result.Count >= MaxClipboardEntries) break;
            try
            {
                if (File.Exists(top))
                {
                    var fi = new FileInfo(top);
                    if (fi.Length <= _options.MaxTransferBytes)
                        result.Add(new ClipEntry(fi.FullName, fi.Name, fi.Length, false));
                }
                else if (Directory.Exists(top))
                {
                    var root = new DirectoryInfo(Path.TrimEndingDirectorySeparator(top));
                    result.Add(new ClipEntry(root.FullName, root.Name, 0, true));
                    foreach (var fsi in root.EnumerateFileSystemInfos("*", opts))
                    {
                        if (result.Count >= MaxClipboardEntries) break;
                        var rel = root.Name + "/" + Path.GetRelativePath(root.FullName, fsi.FullName).Replace('\\', '/');
                        if (fsi is FileInfo f)
                        {
                            if (f.Length <= _options.MaxTransferBytes)
                                result.Add(new ClipEntry(f.FullName, rel, f.Length, false));
                        }
                        else result.Add(new ClipEntry(fsi.FullName, rel, 0, true));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Session {IP}: không đọc được {Path}", _remoteIp, top);
            }
        }
        return result;
    }

    // Poll và ClipboardGet không được chạy chồng nhau (nếu không có thể gửi trùng một nội dung).
    private readonly SemaphoreSlim _clipLock = new(1, 1);

    private async Task PushClipboardAsync(bool force, bool markOnly, CancellationToken ct)
    {
        await _clipLock.WaitAsync(ct);
        try { await PushClipboardCoreAsync(force, markOnly, ct); }
        finally { _clipLock.Release(); }
    }
}
