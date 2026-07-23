using IPGS.RemoteControl.ZcuAgent;
using IPGS.RemoteControl.ZcuAgent.Auth;
using IPGS.RemoteControl.ZcuAgent.Capture;
using IPGS.RemoteControl.ZcuAgent.Input;
using IPGS.RemoteControl.ZcuAgent.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── X11 session guard (TDD §9 gotcha / §14 Wayland risk) ─────────────────
// Refuse to start if the session is not X11 — XTest/XShm do not work on Wayland.
var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
if (!string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine(
        $"[ZcuAgent] ERROR: XDG_SESSION_TYPE='{sessionType}'. " +
        "This agent requires an X11 session (not Wayland or headless). " +
        "Set XDG_SESSION_TYPE=x11 or run under a compatible X11 display server.");
    return 1;
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        // Bind AgentOptions from appsettings.json "RemoteControl" section (TDD §12)
        services.Configure<AgentOptions>(
            ctx.Configuration.GetSection(AgentOptions.Section));

        // Internal services
        services.AddSingleton<AuthManager>();
        services.AddSingleton<IScreenCapturer,   X11ScreenCapturer>();
        services.AddSingleton<IFrameEncoder,     JpegEncoder>();
        services.AddSingleton<IMouseInjector,    MouseInjector>();
        services.AddSingleton<IKeyboardInjector, KeyboardInjector>();
        services.AddSingleton<TcpServer>();

        // Hosted service orchestrates everything
        services.AddHostedService<RemoteControlHostedService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

await host.RunAsync();
return 0;
