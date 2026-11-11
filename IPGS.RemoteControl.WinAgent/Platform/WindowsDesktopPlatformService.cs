using System.Runtime.InteropServices;
using System.Text.Json;
using IPGS.RemoteControl.ZcuAgent;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using static IPGS.RemoteControl.WinAgent.Native.NativeMethods;

namespace IPGS.RemoteControl.WinAgent.Platform;

/// <summary>Tác vụ gắn với Windows: hộp thoại chat, thông tin máy (registry / GlobalMemoryStatusEx).</summary>
internal sealed class WindowsDesktopPlatformService : IDesktopPlatformService
{
    /// <summary>Số hộp thoại chat tối đa đang mở cùng lúc (mỗi hộp giữ một luồng cho tới khi người dùng bấm OK).</summary>
    private const int MaxOpenChatBoxes = 3;

    private readonly ILogger<WindowsDesktopPlatformService> _logger;
    private int _openChatBoxes;

    public WindowsDesktopPlatformService(ILogger<WindowsDesktopPlatformService> logger) => _logger = logger;

    public string AgentName => "WinAgent";

    /// <summary>
    /// Windows chưa có bộ đo tải tương đương X-latency/PSI → không điều tiết FPS theo tải máy
    /// (đặt <c>CpuAdaptive=false</c> trong appsettings). Điều tiết theo mạng vẫn hoạt động.
    /// </summary>
    public IMachineLoadSampler CreateLoadSampler() => new UnavailableLoadSampler();

    public Task ShowChatNotificationAsync(string message, CancellationToken ct)
    {
        if (Interlocked.Increment(ref _openChatBoxes) > MaxOpenChatBoxes)
        {
            Interlocked.Decrement(ref _openChatBoxes);
            _logger.LogWarning("Bỏ qua tin chat: đã có {Max} hộp thoại chưa đóng trên máy đích", MaxOpenChatBoxes);
            return Task.CompletedTask;
        }

        // MessageBox chặn tới khi người dùng bấm OK → chạy trên luồng riêng, không chờ ở đây.
        _ = Task.Run(() =>
        {
            try
            {
                MessageBoxW(IntPtr.Zero, message, "Remote Admin", MB_OK | MB_ICONINFORMATION | MB_TOPMOST | MB_SETFOREGROUND);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Không hiện được tin chat"); }
            finally { Interlocked.Decrement(ref _openChatBoxes); }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public string GatherSystemInfoJson()
    {
        try
        {
            var cpu = "Unknown CPU";
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string name && !string.IsNullOrWhiteSpace(name))
                    cpu = name.Trim();
            }
            catch { cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU"; }

            var mem = "Unknown RAM";
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref status))
                mem = $"{status.ullTotalPhys / 1024 / 1024} MB";

            return JsonSerializer.Serialize(new
            {
                cpu,
                memory = mem,
                os     = RuntimeInformation.OSDescription,
                arch   = RuntimeInformation.OSArchitecture.ToString(),
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather sysinfo");
            return "{ \"error\": \"Failed to gather sysinfo\" }";
        }
    }

    private sealed class UnavailableLoadSampler : IMachineLoadSampler
    {
        // Mốc thời gian 0 → ClientSession không bao giờ coi là mẫu mới.
        public MachineLoad Current => new(-1, -1, 0);
        public void Start(CancellationToken ct) { }
        public void Dispose() { }
    }
}
