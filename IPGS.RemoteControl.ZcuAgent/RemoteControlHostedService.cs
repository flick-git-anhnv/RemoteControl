using IPGS.RemoteControl.ZcuAgent.Input;
using IPGS.RemoteControl.ZcuAgent.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<RemoteControlHostedService> _logger;

    public RemoteControlHostedService(
        IScreenCapturer  capturer,
        IMouseInjector   injector,
        IKeyboardInjector keyboard,
        TcpServer        tcpServer,
        ILogger<RemoteControlHostedService> logger)
    {
        _capturer  = capturer;
        _injector  = injector;
        _keyboard  = keyboard;
        _tcpServer = tcpServer;
        _logger    = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RemoteControlHostedService: starting");

        try
        {
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

    public override void Dispose()
    {
        _tcpServer.Dispose();
        _keyboard.Dispose();
        _injector.Dispose();
        _capturer.Dispose();
        base.Dispose();
    }
}
