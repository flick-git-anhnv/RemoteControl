using System.Diagnostics;
using IPGS.RemoteControl.ZcuAgent.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IPGS.RemoteControl.ZcuAgent.Platform;

/// <summary>
/// Tác vụ gắn với Linux/X11: <c>notify-send</c>, <c>/proc</c>, đo tải bằng X-latency + PSI.
/// Logic chuyển nguyên từ ClientSession.
/// </summary>
internal sealed class LinuxDesktopPlatformService : IDesktopPlatformService
{
    private readonly AgentOptions _options;
    private readonly ILogger<LinuxDesktopPlatformService> _logger;

    public LinuxDesktopPlatformService(IOptions<AgentOptions> options, ILogger<LinuxDesktopPlatformService> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public string AgentName => "ZcuAgent";

    public IMachineLoadSampler CreateLoadSampler() => new MachineLoadSampler(_options.Display, _logger);

    /// <summary>
    /// Show a chat message via <c>notify-send</c>. Uses ProcessStartInfo.ArgumentList so
    /// the client-supplied text is passed as ONE argv element — no manual quoting, no
    /// interpretation of quotes/backslashes/newlines (audit S2). Fully async + disposed:
    /// never blocks the receive loop and never leaks the Process (audit L2).
    /// </summary>
    public async Task ShowChatNotificationAsync(string message, CancellationToken ct)
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
            _logger.LogWarning(ex, "notify-send failed for chat message");
        }
    }

    public string GatherSystemInfoJson()
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
