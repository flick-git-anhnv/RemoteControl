using IPGS.RemoteControl.WinAgent.Capture;
using IPGS.RemoteControl.WinAgent.Input;
using IPGS.RemoteControl.WinAgent.Native;
using IPGS.RemoteControl.WinAgent.Platform;
using IPGS.RemoteControl.ZcuAgent;
using IPGS.RemoteControl.ZcuAgent.Auth;
using IPGS.RemoteControl.ZcuAgent.Capture;
using IPGS.RemoteControl.ZcuAgent.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// DPI awareness PHẢI đặt trước mọi lời gọi user32/gdi32: nếu không, ở scale 125%/150% Windows trả kích thước
// màn hình "ảo" đã co lại → ảnh chụp mờ/cắt góc và chuột bấm lệch. app.manifest đã khai báo; gọi thêm ở đây
// để vẫn đúng khi manifest bị bỏ qua (kết quả false khi manifest đã đặt rồi là bình thường).
if (!NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiPerMonitorAwareV2))
    NativeMethods.SetProcessDPIAware();

// Tuỳ chọn dòng lệnh (tự đọc, không đưa vào cấu hình Host): chạy ẩn bằng Task Scheduler.
//   --hide-console      ẩn cửa sổ console
//   --log <đường dẫn>   ghi log ra file (cắt khi > 5 MB lúc khởi động)
var hideConsole = false;
string? logPath = null;
for (var i = 0; i < args.Length; i++)
{
    if (string.Equals(args[i], "--hide-console", StringComparison.OrdinalIgnoreCase)) hideConsole = true;
    else if (string.Equals(args[i], "--log", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) logPath = args[++i];
}

if (logPath is not null)
{
    try
    {
        if (File.Exists(logPath) && new FileInfo(logPath).Length > 5L * 1024 * 1024) File.Delete(logPath);
        var writer = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true,
        };
        Console.SetOut(writer);
        Console.SetError(writer);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[WinAgent] Không mở được file log '{logPath}': {ex.Message}");
    }
}

if (hideConsole)
{
    var console = NativeMethods.GetConsoleWindow();
    if (console != IntPtr.Zero) NativeMethods.ShowWindow(console, NativeMethods.SW_HIDE);
}

// Máy đích không được tự tắt màn hình/ngủ khi đang chờ remote (tương đương XSetScreenSaver bên Linux).
NativeMethods.SetThreadExecutionState(
    NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED);

var host = Host.CreateDefaultBuilder()
    // Task Scheduler chạy với thư mục làm việc System32 → phải trỏ về thư mục chứa agent để đọc appsettings.json.
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<AgentOptions>(ctx.Configuration.GetSection(AgentOptions.Section));
        // Điều chỉnh độ phân giải headless dùng xrandr (X11) — không áp dụng cho Windows.
        services.PostConfigure<AgentOptions>(o => o.AutoHeadlessResolution = false);

        services.AddSingleton<AuthManager>();
        services.AddSingleton<IScreenCapturer, GdiScreenCapturer>();
        services.AddSingleton<IFrameEncoder, JpegEncoder>(); // Skia; libturbojpeg chưa dùng trên Windows
        services.AddSingleton<IMouseInjector, Win32MouseInjector>();
        services.AddSingleton<IKeyboardInjector, Win32KeyboardInjector>();
        services.AddSingleton<IClipboardService, Win32ClipboardService>();
        services.AddSingleton<IMonitorProvider, Win32MonitorProvider>();
        services.AddSingleton<IDesktopPlatformService, WindowsDesktopPlatformService>();
        services.AddSingleton<TcpServer>();

        services.AddHostedService<RemoteControlHostedService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        // Tắt màu: log ra file không được lẫn mã ANSI.
        logging.AddSimpleConsole(o => o.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled);
    })
    .Build();

await host.RunAsync();
return 0;
