using System.Diagnostics;
using System.Net.Sockets;
using IPGS.RemoteControl.Protocol;
using IPGS.RemoteControl.ZcuAgent.Auth;
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
    private readonly AuthManager       _auth;
    private readonly AgentOptions      _options;
    private readonly ILogger           _logger;

    private NetworkStream? _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Timestamp (Environment.TickCount64) of the last received PONG. Written by
    /// the receive loop, read by the capture loop for TDD §7 15s heartbeat timeout.
    /// </summary>
    private long _lastPongTicks;

    public ClientSession(
        TcpClient tcp, string remoteIp,
        IScreenCapturer capturer, IFrameEncoder encoder,
        IMouseInjector injector, IKeyboardInjector keyboard,
        AuthManager auth, IOptions<AgentOptions> options, ILogger logger)
    {
        _tcp      = tcp;
        _remoteIp = remoteIp;
        _capturer = capturer;
        _encoder  = encoder;
        _injector = injector;
        _keyboard = keyboard;
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
        _stream = _tcp.GetStream();
        try
        {
            await DoHandshakeAsync(ct);
            await DoAuthAsync(ct);
            _logger.LogInformation("Session {IP}: streaming started", _remoteIp);
            Interlocked.Exchange(ref _lastPongTicks, Environment.TickCount64);

            // Signal the kiosk app (separate process, same machine) that input from now
            // on may be remotely injected, so it can suppress auto-showing the on-screen
            // keyboard on TextBox focus. See RemoteSessionMarker for the full rationale.
            RemoteSessionMarker.Create(_logger);

            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var captureTask = RunCaptureSendLoopAsync(sessionCts.Token);
            var receiveTask = RunReceiveLoopAsync(sessionCts.Token);

            // Stop everything when either loop ends
            await Task.WhenAny(captureTask, receiveTask);
            await sessionCts.CancelAsync();
            await Task.WhenAll(captureTask, receiveTask);
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
            // v1 serves one session at a time (see TcpServer), so it's always safe to
            // clear the marker here — no other active session could still need it.
            RemoteSessionMarker.Remove(_logger);
            _writeLock.Dispose();
            try { _tcp.Close(); } catch { }
            _logger.LogInformation("Session {IP}: closed", _remoteIp);
        }
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
        var ackPayload = MessageCodec.EncodeHelloAck(
            (uint)screenSize.Width, (uint)screenSize.Height, "ZcuAgent/1.0");
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
        var frameId       = 0L;
        var frameInterval = 1000 / Math.Max(1, _options.TargetFps);
        var sw            = Stopwatch.StartNew();
        var nextPing      = Environment.TickCount64 + RemoteControlConstants.PingIntervalMs;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                sw.Restart();

                // PING keepalive + PONG timeout (TDD §7): disconnect if no PONG in PingTimeoutMs (15s)
                var now = Environment.TickCount64;
                var lastPong = Interlocked.Read(ref _lastPongTicks);
                if (lastPong != 0 && now - lastPong > RemoteControlConstants.PingTimeoutMs)
                {
                    _logger.LogWarning("Session {IP}: no PONG within {Ms}ms — disconnecting",
                        _remoteIp, RemoteControlConstants.PingTimeoutMs);
                    return;
                }

                if (now >= nextPing)
                {
                    await WriteAsync(MessageType.Ping,
                        MessageCodec.EncodePingPong((ulong)now), ct);
                    nextPing = now + RemoteControlConstants.PingIntervalMs;
                }

                // Capture frame
                var frame = _capturer.Capture();
                if (frame is not null)
                {
                    var jpeg = _encoder.EncodeJpeg(frame, _options.JpegQuality);
                    if (jpeg is not null && jpeg.Length > 0 && jpeg.Length <= _options.MaxFrameBytes)
                    {
                        var tsMs = (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0xFFFF_FFFF);
                        var payload = MessageCodec.EncodeFrameJpeg(
                            frameId++, tsMs,
                            frame.Width, frame.Height,
                            jpeg.AsSpan());
                        await WriteAsync(MessageType.FrameJpeg, payload, ct);
                    }
                    else if (jpeg is not null && jpeg.Length > _options.MaxFrameBytes)
                    {
                        _logger.LogWarning("Frame {Id} JPEG {Len} B exceeds MaxFrameBytes — dropped",
                            frameId, jpeg.Length);
                    }
                }

                // Throttle to target FPS
                var elapsed = (int)sw.ElapsedMilliseconds;
                var sleep   = frameInterval - elapsed;
                if (sleep > 0)
                    await Task.Delay(sleep, ct);
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
                    case MessageType.MouseMove:
                        var (mx, my) = MessageCodec.DecodeMouseMove(payload);
                        _injector.Move(mx, my);
                        break;

                    case MessageType.MouseButton:
                        var (btn, down, bx, by) = MessageCodec.DecodeMouseButton(payload);
                        // Clamp to screen bounds per TDD §5.3
                        var sx = Math.Clamp(bx, 0, _capturer.ScreenSize.Width  - 1);
                        var sy = Math.Clamp(by, 0, _capturer.ScreenSize.Height - 1);
                        _injector.Move(sx, sy);
                        _injector.Button(btn, down);
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
                        _keyboard.SendKey(keysym, isDown);
                        break;
                        
                    // ── Phase 6 Enterprise Features ───────────────────────────
                    case MessageType.ChatText:
                        var chatMsg = MessageCodec.DecodeStringMessage(payload);
                        _logger.LogInformation("CHAT from CCU: {Msg}", chatMsg);
                        try {
                            System.Diagnostics.Process.Start("notify-send", $"\"Remote Admin\" \"{chatMsg.Replace("\"", "\\\"")}\"");
                        } catch { /* ignore if notify-send missing */ }
                        break;

                    case MessageType.ClipboardData:
                        var clipData = MessageCodec.DecodeStringMessage(payload);
                        _logger.LogInformation("CLIPBOARD from CCU (len: {Len})", clipData.Length);
                        try {
                            // Try xclip (Linux)
                            var psi = new System.Diagnostics.ProcessStartInfo("xclip", "-selection clipboard")
                            {
                                RedirectStandardInput = true,
                                UseShellExecute = false
                            };
                            var proc = System.Diagnostics.Process.Start(psi);
                            if (proc != null) {
                                proc.StandardInput.Write(clipData);
                                proc.StandardInput.Close();
                                proc.WaitForExit(1000);
                            }
                        } catch { /* ignore */ }
                        break;

                    case MessageType.PrivacyMode:
                        var privacyEnabled = MessageCodec.DecodeBooleanMessage(payload);
                        _logger.LogWarning("PRIVACY MODE: {State}", privacyEnabled ? "ON" : "OFF");
                        // In a real prod env, this would use X11 APIs to map a black window on top 
                        // and grab pointer/keyboard exclusively. For now, just log.
                        break;

                    case MessageType.SysInfoReq:
                        _logger.LogInformation("SysInfoReq received, gathering system info...");
                        string sysInfoJson = GatherSystemInfo();
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
    }

    // ── Thread-safe write ─────────────────────────────────────────────────

    private async Task WriteAsync(MessageType type, byte[] payload, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            await MessageCodec.WriteMessageAsync(_stream!, type, payload, ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Phase 6 Helper ────────────────────────────────────────────────────

    private string GatherSystemInfo()
    {
        try 
        {
            var cpu = System.IO.File.ReadAllLines("/proc/cpuinfo")
                .FirstOrDefault(l => l.StartsWith("model name"))?.Split(':').LastOrDefault()?.Trim() ?? "Unknown CPU";
            
            var mem = "Unknown RAM";
            try {
                var memLine = System.IO.File.ReadAllLines("/proc/meminfo").FirstOrDefault(l => l.StartsWith("MemTotal"));
                if (memLine != null) {
                    var parts = memLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb)) {
                        mem = $"{kb / 1024} MB";
                    }
                }
            } catch {}

            var os = Environment.OSVersion.ToString();
            var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();

            return $$"""
            {
                "cpu": "{{cpu}}",
                "memory": "{{mem}}",
                "os": "{{os}}",
                "arch": "{{arch}}"
            }
            """;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather sysinfo");
            return "{ \"error\": \"Failed to gather sysinfo\" }";
        }
    }
}
