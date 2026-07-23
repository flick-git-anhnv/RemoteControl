using System.Net;
using System.Net.Sockets;
using IPGS.RemoteControl.ZcuAgent.Auth;
using IPGS.RemoteControl.ZcuAgent.Capture;
using IPGS.RemoteControl.ZcuAgent.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Listens on TCP port <see cref="AgentOptions.Port"/> and manages one active
/// client session at a time (v1 — multi-client is a v2 concern per TDD §2).
/// Enforces IP whitelist before reading any data (TDD §8.2).
/// </summary>
internal sealed class TcpServer : IDisposable
{
    private readonly ILogger<TcpServer>          _logger;
    private readonly AgentOptions                _options;
    private readonly IScreenCapturer             _capturer;
    private readonly IFrameEncoder               _encoder;
    private readonly IMouseInjector              _injector;
    private readonly IKeyboardInjector           _keyboard;
    private readonly AuthManager                 _auth;
    private readonly IOptions<AgentOptions>      _optionsWrapper;

    private TcpListener? _listener;
    private bool _disposed;

    public TcpServer(
        IOptions<AgentOptions>   options,
        IScreenCapturer          capturer,
        IFrameEncoder            encoder,
        IMouseInjector           injector,
        IKeyboardInjector        keyboard,
        AuthManager              auth,
        ILogger<TcpServer>       logger)
    {
        _optionsWrapper = options;
        _options        = options.Value;
        _capturer       = capturer;
        _encoder        = encoder;
        _injector       = injector;
        _keyboard       = keyboard;
        _auth           = auth;
        _logger         = logger;
    }

    // ── Entry point ───────────────────────────────────────────────────────

    /// <summary>
    /// Accept and serve clients until <paramref name="ct"/> is cancelled.
    /// Only one client is served at a time; concurrent connections are queued
    /// by the OS backlog but rejected at application level (v1 simplification).
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, _options.Port);
        _listener.Start();
        _logger.LogInformation("TcpServer: listening on port {Port}", _options.Port);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var remoteEndpoint = client.Client.RemoteEndPoint as IPEndPoint;
                var remoteIp = remoteEndpoint?.Address.ToString() ?? "unknown";

                // IP whitelist check (TDD §8.2) — close socket before reading anything
                if (!_auth.IsIpAllowed(remoteIp))
                {
                    _logger.LogWarning("TcpServer: connection from {IP} rejected by whitelist", remoteIp);
                    client.Close();
                    continue;
                }

                if (_auth.IsBanned(remoteIp))
                {
                    _logger.LogWarning("TcpServer: connection from {IP} rejected — temporarily banned", remoteIp);
                    client.Close();
                    continue;
                }

                _logger.LogInformation("TcpServer: accepted connection from {IP}", remoteIp);

                // Enable TCP keepalive (TDD §14 — guard against half-open TCP)
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // Run session — await so only 1 session at a time (v1)
                var session = new ClientSession(
                    client, remoteIp,
                    _capturer, _encoder, _injector, _keyboard,
                    _auth, _optionsWrapper, _logger);

                try
                {
                    await session.RunAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "TcpServer: session from {IP} ended with error", remoteIp);
                }
            }
        }
        finally
        {
            _listener.Stop();
            _logger.LogInformation("TcpServer: stopped");
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _listener?.Stop();
    }
}
