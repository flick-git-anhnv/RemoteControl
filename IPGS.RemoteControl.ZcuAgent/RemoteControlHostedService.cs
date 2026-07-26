using IPGS.RemoteControl.ZcuAgent.Input;
using IPGS.RemoteControl.ZcuAgent.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IPGS.RemoteControl.ZcuAgent;

/// <summary>
/// Generic Host background service that:
/// <list type="number">
///   <item>Initializes X11 display + mouse injector.</item>
///   <item>Runs the TCP server until cancellation.</item>
///   <item>Disposes X11 resources on shutdown.</item>
/// </list>
/// Lifetime: singleton, started/stopped by the Generic Host. See TDD §10.2.
/// </summary>
internal sealed class RemoteControlHostedService : BackgroundService
{
    private readonly IScreenCapturer       _capturer;
    private readonly IMouseInjector        _injector;
    private readonly IKeyboardInjector     _keyboard;
    private readonly TcpServer             _tcpServer;
    private readonly AgentOptions          _options;
    private readonly ILogger<RemoteControlHostedService> _logger;

    public RemoteControlHostedService(
        IScreenCapturer  capturer,
        IMouseInjector   injector,
        IKeyboardInjector keyboard,
        TcpServer        tcpServer,
        IOptions<AgentOptions> options,
        ILogger<RemoteControlHostedService> logger)
    {
        _capturer  = capturer;
        _injector  = injector;
        _keyboard  = keyboard;
        _tcpServer = tcpServer;
        _options   = options.Value;
        _logger    = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RemoteControlHostedService: starting");

        try
        {
            // Fail-fast on insecure configuration BEFORE opening any listener
            // (security audit S4): a missing/placeholder token means every client
            // would be one guess away from full desktop control.
            ValidateSecurityConfig();

            // Initialize X11 resources before accepting any TCP connection.
            // GOTCHA: XInitThreads() is called inside X11ScreenCapturer.Initialize(),
            // which must happen before XOpenDisplay in any thread. Capturer, mouse injector,
            // and keyboard injector each open their own display connections, but
            // XInitThreads is global — capturer must Initialize() first.
            _capturer.Initialize();
            _injector.Initialize();
            _keyboard.Initialize();

            _logger.LogInformation("RemoteControlHostedService: X11 initialized — starting TCP server");
            await _tcpServer.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "RemoteControlHostedService: fatal error — service stopping");
            throw;
        }
        finally
        {
            _logger.LogInformation("RemoteControlHostedService: stopped");
        }
    }

    /// <summary>
    /// Validates security-critical configuration (security audit S4):
    /// <list type="bullet">
    ///   <item>Token missing or still the shipped placeholder → LogCritical + throw
    ///         (service refuses to start the listener).</item>
    ///   <item>Empty AllowedClientIPs → prominent warning (deny-by-default: nothing
    ///         will be able to connect until the whitelist is configured).</item>
    ///   <item>Catch-all whitelist (0.0.0.0/0, ::/0) → prominent warning.</item>
    /// </list>
    /// </summary>
    private void ValidateSecurityConfig()
    {
        if (string.IsNullOrWhiteSpace(_options.Token) ||
            _options.Token.StartsWith("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogCritical(
                "RemoteControl:Token is missing or still the placeholder value — " +
                "REFUSING TO START. Set a strong random token (>= 32 chars) in appsettings.json.");
            throw new InvalidOperationException(
                "RemoteControl:Token is not configured — agent refuses to start (fail-fast, security audit S4).");
        }

        // F06: token quá ngắn (< 16 ký tự) là token từ điển/dễ đoán — cảnh báo nổi bật
        // nhưng KHÔNG fail-fast để không phá deployment hiện có; khuyến nghị sinh lại
        // bằng nút 🎲 Sinh Token của ZcuSetupWizard (32 hex chars).
        if (_options.Token.Trim().Length < 16)
        {
            _logger.LogWarning(
                "SECURITY: RemoteControl:Token is only {Len} chars — short tokens are guessable. " +
                "Generate a strong random token (>= 32 chars) via the CCU Setup Wizard.",
                _options.Token.Trim().Length);
        }

        if (_options.AllowedClientIPs.Count == 0)
        {
            _logger.LogWarning(
                "RemoteControl:AllowedClientIPs is empty — deny-by-default is active and ALL " +
                "client connections will be REJECTED. Add allowed IPs/CIDRs to accept connections.");
        }
        else if (_options.AllowedClientIPs.Any(e =>
                     string.Equals(e.Trim(), "0.0.0.0/0", StringComparison.Ordinal) ||
                     string.Equals(e.Trim(), "::/0",      StringComparison.Ordinal)))
        {
            _logger.LogWarning(
                "RemoteControl:AllowedClientIPs contains a catch-all range ({Entries}) — every IP " +
                "on the network can attempt authentication. Restrict to specific CIDRs in production.",
                string.Join(", ", _options.AllowedClientIPs));
        }
    }

    public override void Dispose()
    {
        _tcpServer.Dispose();
        _keyboard.Dispose();
        _injector.Dispose();
        _capturer.Dispose();
        base.Dispose();
    }
}
