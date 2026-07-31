using IPGS.RemoteControl.ZcuAgent;
using IPGS.RemoteControl.ZcuAgent.Auth;
using IPGS.RemoteControl.ZcuAgent.Capture;
using IPGS.RemoteControl.ZcuAgent.Input;
using IPGS.RemoteControl.ZcuAgent.Net;
using IPGS.RemoteControl.ZcuAgent.Wayland;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── Session-type detection (TDD §9 / §14) ─────────────────────────────────
// x11   → XTest/XShm path (X11ScreenCapturer, MouseInjector, KeyboardInjector).
// wayland → Mutter D-Bus path (WaylandScreenCapturer + Wayland input injectors),
//           GNOME-Shell-specific (see Wayland/MutterDBusInterfaces.cs verification note).
// anything else (headless, unknown) → refuse to start, same fail-fast as before.
var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
var isWayland = string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);
var isX11     = string.Equals(sessionType, "x11",     StringComparison.OrdinalIgnoreCase);
if (!isWayland && !isX11)
{
    Console.Error.WriteLine(
        $"[ZcuAgent] ERROR: XDG_SESSION_TYPE='{sessionType}'. " +
        "This agent requires an X11 or GNOME Wayland session (not headless). " +
        "Set XDG_SESSION_TYPE=x11|wayland or run under a compatible display server.");
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
        services.AddSingleton<IFrameEncoder, JpegEncoder>();

        if (isWayland)
        {
            // Shared Mutter RemoteDesktop+ScreenCast session pair — one per running
            // agent process, referenced by both the capturer and the input injectors.
            services.AddSingleton<MutterSessionManager>();
            services.AddSingleton<IScreenCapturer,   WaylandScreenCapturer>();
            services.AddSingleton<IMouseInjector,    WaylandMouseInjector>();
            services.AddSingleton<IKeyboardInjector, WaylandKeyboardInjector>();
        }
        else
        {
            services.AddSingleton<IScreenCapturer,   X11ScreenCapturer>();
            services.AddSingleton<IMouseInjector,    MouseInjector>();
            services.AddSingleton<IKeyboardInjector, KeyboardInjector>();
        }

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
