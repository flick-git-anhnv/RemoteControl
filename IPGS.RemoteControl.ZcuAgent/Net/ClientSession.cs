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

            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var captureTask  = RunCaptureSendLoopAsync(sessionCts.Token);
            var receiveTask  = RunReceiveLoopAsync(sessionCts.Token);
            // Heartbeat timeout runs in its OWN task (audit L3): if the check lived in the
            // capture loop it could never fire while that loop is blocked in WriteAsync on
            // a slow reader — the exact situation the timeout exists to detect.
            var watchdogTask = RunHeartbeatWatchdogAsync(sessionCts.Token);

            // Stop everything when any loop ends
            await Task.WhenAny(captureTask, receiveTask, watchdogTask);
            await sessionCts.CancelAsync();
            await Task.WhenAll(captureTask, receiveTask, watchdogTask);
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

                // Capture frame
                var frame = _capturer.Capture();
                if (frame is not null)
                {
                    // NOTE: jpeg is backed by the encoder's reused buffer (audit Q3) —
                    // consumed synchronously below, before the next EncodeJpeg call.
                    var jpeg = _encoder.EncodeJpeg(frame, _options.JpegQuality);
                    if (jpeg.Length > 0 && jpeg.Length <= _options.MaxFrameBytes)
                    {
                        var tsMs = (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0xFFFF_FFFF);
                        var payload = MessageCodec.EncodeFrameJpeg(
                            frameId++, tsMs,
                            frame.Width, frame.Height,
                            jpeg.Span);
                        await WriteAsync(MessageType.FrameJpeg, payload, ct);
                    }
                    else if (jpeg.Length > _options.MaxFrameBytes)
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
                    case MessageType.MouseMove:
                        var (mx, my) = MessageCodec.DecodeMouseMove(payload);
                        _injector.Move(mx, my);
                        break;

                    case MessageType.MouseButton:
                        var (btn, down, bx, by) = MessageCodec.DecodeMouseButton(payload);
                        // Clamp to screen bounds per TDD §5.3.
                        // Read ScreenSize ONCE into a local (audit L9): the property is
                        // republished atomically by the capture thread on resolution change;
                        // two separate reads could observe new-W paired with old-H.
                        var screen = _capturer.ScreenSize;
                        var sx = Math.Clamp(bx, 0, screen.Width  - 1);
                        var sy = Math.Clamp(by, 0, screen.Height - 1);
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
                            await ShowChatNotificationAsync(chatMsg, ct);
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
                            await SetClipboardAsync(clipData, ct);
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
        // NOTE (audit L4): ProtocolException from MessageCodec decoders (malformed payload)
        // intentionally propagates out of this loop — RunAsync catches it separately,
        // logs a Warning ("protocol error") and closes the session cleanly. It must NOT
        // be swallowed here nor fall into a generic Error log.
    }

    // ── Thread-safe write ─────────────────────────────────────────────────

    /// <summary>
    /// Serialized, timeout-bounded write (audit L3). NetworkStream.WriteTimeout does NOT
    /// apply to async writes, so a per-write CancellationToken bounds the operation: a
    /// client that stops reading (TCP backpressure) fails the session after
    /// <see cref="WriteTimeoutMs"/> instead of holding the single v1 session slot forever.
    /// </summary>
    private async Task WriteAsync(MessageType type, byte[] payload, CancellationToken ct)
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

    // ── Desktop integration helpers (audit S2 + L2) ───────────────────────

    /// <summary>
    /// Show a chat message via <c>notify-send</c>. Uses ProcessStartInfo.ArgumentList so
    /// the client-supplied text is passed as ONE argv element — no manual quoting, no
    /// interpretation of quotes/backslashes/newlines (audit S2). Fully async + disposed:
    /// never blocks the receive loop and never leaks the Process (audit L2).
    /// </summary>
    private async Task ShowChatNotificationAsync(string message, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("notify-send") { UseShellExecute = false };
            psi.ArgumentList.Add("Remote Admin");
            psi.ArgumentList.Add(message);

            using var proc = Process.Start(psi);
            if (proc is null) return;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session {IP}: notify-send failed for chat message", _remoteIp);
        }
    }

    /// <summary>
    /// Push text into the X11 clipboard via <c>xclip</c>, async and fully disposed (audit L2).
    /// GOTCHA: xclip daemonizes (forks a child that owns the selection) — the parent we
    /// spawn exits almost immediately after stdin closes, but if it has not exited within
    /// the grace period we simply release our handle WITHOUT killing it: killing the tree
    /// would drop the clipboard content we just set.
    /// </summary>
    private async Task SetClipboardAsync(string text, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("xclip")
            {
                RedirectStandardInput = true,
                UseShellExecute       = false,
            };
            psi.ArgumentList.Add("-selection");
            psi.ArgumentList.Add("clipboard");

            using var proc = Process.Start(psi);
            if (proc is null) return;

            await proc.StandardInput.WriteAsync(text.AsMemory(), ct);
            proc.StandardInput.Close();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("Session {IP}: xclip did not exit within grace period " +
                                 "(likely daemonized) — releasing handle without killing", _remoteIp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session {IP}: failed to set clipboard via xclip", _remoteIp);
        }
    }

    // ── Phase 6 Helper ────────────────────────────────────────────────────

    private string GatherSystemInfo()
    {
        try 
        {
            var cpu = "Unknown CPU";
            try {
                cpu = System.IO.File.ReadAllLines("/proc/cpuinfo")
                    .FirstOrDefault(l => l.StartsWith("model name"))?.Split(':').LastOrDefault()?.Trim() ?? "Unknown CPU";
            } catch {
                cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
            }
            
            var mem = "Unknown RAM";
            try {
                var memLine = System.IO.File.ReadAllLines("/proc/meminfo").FirstOrDefault(l => l.StartsWith("MemTotal"));
                if (memLine != null) {
                    var parts = memLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb)) {
                        mem = $"{kb / 1024} MB";
                    }
                }
            } catch {
                var gcInfo = GC.GetGCMemoryInfo();
                mem = $"{gcInfo.TotalAvailableMemoryBytes / 1024 / 1024} MB";
            }

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
