using IPGS.RemoteControl.ZcuAgent;
using IPGS.RemoteControl.ZcuAgent.Auth;
using IPGS.RemoteControl.ZcuAgent.Capture;
using IPGS.RemoteControl.ZcuAgent.Input;
using IPGS.RemoteControl.ZcuAgent.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── X11 session guard (TDD §9 gotcha / §14 Wayland risk / Headless & RDP optimization) ─
// Chặn nghiêm ngặt nếu đang chạy Wayland vì XTest/XShm không được hỗ trợ qua ranh giới bảo mật Wayland.
// Trong môi trường Headless, Kiosk không màn hình, systemd daemon hoặc xrdp RDP session,
// biến XDG_SESSION_TYPE thường bị rỗng hoặc chưa được set. ZcuAgent sẽ dùng X11DisplayResolver
// để tự động quét Unix domain socket (/tmp/.X11-unix/X*) hoặc biến DISPLAY.
var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine(
        $"[ZcuAgent] ERROR: XDG_SESSION_TYPE='{sessionType}'. " +
        "This agent requires an X11 session (not Wayland). " +
        "Ubuntu/GDM: đặt WaylandEnable=false trong /etc/gdm3/custom.conf rồi khởi động lại (script setup-zcu-agent.sh tự làm việc này). " +
        "Hoặc khởi chạy dưới một X11 / xrdp / Xvfb display server.");
    return 1;
}

if (!string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(
        $"[ZcuAgent] Notice: XDG_SESSION_TYPE is '{sessionType ?? "<empty>"}'. " +
        "Proceeding with X11DisplayResolver auto-detection for Headless/RDP/Virtual sessions...");
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
