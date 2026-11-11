using System.Globalization;
using System.Net.Sockets;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>Kết quả dò trạng thái kết nối của một máy tính ZCU.</summary>
public readonly record struct ComputerStatusProbeResult(bool SshReachable, bool AgentReachable)
{
    public ComputerConnectivityStatus Status =>
        SshReachable && AgentReachable ? ComputerConnectivityStatus.Online : ComputerConnectivityStatus.Offline;
}

/// <summary>
/// Dò nhanh trạng thái kết nối SSH + Agent Remote Control của một máy tính ZCU/Windows
/// bằng TCP connect (không xác thực) — đủ để biết cổng có mở/service có đang
/// chạy hay không, không cần thông tin đăng nhập.
/// Khi có thông tin SSH, tự động lấy thêm chỉ số CPU / RAM / Disk cho cả Linux và Windows.
/// </summary>
public static class ComputerStatusChecker
{
    /// <summary>
    /// Câu lệnh Linux một dòng xuất 3 dòng chuẩn:
    /// Line 1: CPU % (từ mẫu thứ 2 của top để tránh spike lúc boot)
    /// Line 2: RAM <c>usedMb totalMb</c>
    /// Line 3: Disk <c>usedG totalG pct%</c>
    /// </summary>
    public const string LinuxMetricsCommand =
        "LC_ALL=C top -b -n 2 -d 0.3 | grep -E '^%Cpu|Cpu\\(s\\)' | tail -n 1 | awk '{for(i=1;i<=NF;i++) if($i==\"id,\" || $i==\"id\") {gsub(\",\",\".\",$(i-1)); printf \"%.1f\\n\", 100-$(i-1); exit}}'; " +
        "LC_ALL=C free -m | awk '/^Mem:/ {print $3, $2}'; " +
        "LC_ALL=C df -hP / | awk 'NR==2 {print $3, $2, $5}'";

    /// <summary>
    /// Câu lệnh PowerShell một dòng trên Windows xuất đúng 3 dòng chuẩn tương đương Linux:
    /// Line 1: CPU % (trung bình LoadPercentage từ Win32_Processor)
    /// Line 2: RAM <c>usedMb totalMb</c> (từ Win32_OperatingSystem, đổi KB -> MB)
    /// Line 3: Disk <c>usedG totalG pct%</c> (từ Win32_LogicalDisk ổ C:)
    /// </summary>
    public const string WindowsMetricsPowerShellCommand =
        "powershell.exe -NoProfile -NonInteractive -Command \"" +
        "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
        "$cpu = (Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue | Measure-Object -Property LoadPercentage -Average).Average; " +
        "if ($null -eq $cpu) { $cpu = 0 }; " +
        "Write-Output ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.0}', $cpu)); " +
        "$os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue; " +
        "if ($null -ne $os -and $os.TotalVisibleMemorySize -gt 0) { " +
        "$totMb = [int][math]::Round($os.TotalVisibleMemorySize / 1024.0); " +
        "$usedMb = [int][math]::Round(($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / 1024.0); " +
        "Write-Output ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0} {1}', $usedMb, $totMb)) " +
        "} else { Write-Output '' }; " +
        "$d = Get-CimInstance Win32_LogicalDisk -Filter 'DeviceID=''C:''' -ErrorAction SilentlyContinue; " +
        "if ($null -ne $d -and $d.Size -gt 0) { " +
        "$totG = $d.Size / 1GB; $usedG = ($d.Size - $d.FreeSpace) / 1GB; $pct = [int][math]::Round(($usedG / $totG) * 100); " +
        "Write-Output ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.0}G {1:0.0}G {2}%', $usedG, $totG, $pct)) " +
        "} else { Write-Output '' }\"";

    /// <summary>Trả về câu lệnh PowerShell thu thập CPU/RAM/Disk trên máy đích Windows.</summary>
    public static string BuildWindowsMetricsCommand() => WindowsMetricsPowerShellCommand;

    /// <summary>
    /// Phân tích đầu ra 3 dòng chuẩn (<c>CPU</c>, <c>usedMb totalMb</c>, <c>usedG totalG pct%</c>)
    /// từ cả Linux và Windows thành bộ 3 chuỗi hiển thị trên thẻ máy tính.
    /// Trả về <c>(null, null, null)</c> nếu đầu ra rỗng hoặc không đúng định dạng.
    /// </summary>
    public static (string? CpuUsage, string? RamUsage, string? DiskUsage) ParseMetricsOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return (null, null, null);

        var lines = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToArray();

        if (lines.Length == 0)
            return (null, null, null);

        string? cpuUsage = null;
        string? ramUsage = null;
        string? diskUsage = null;

        // Line 1: CPU %
        string cpuRaw = lines[0].TrimEnd('%').Replace(',', '.');
        if (cpuRaw.StartsWith("CPU:", StringComparison.OrdinalIgnoreCase))
            cpuRaw = cpuRaw.Substring(4).Trim().TrimEnd('%');

        if (double.TryParse(cpuRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out double cpuVal)
            && cpuVal >= 0.0 && cpuVal <= 100.0)
        {
            cpuUsage = $"CPU: {cpuVal.ToString("0.0", CultureInfo.InvariantCulture)}%";
        }

        // Line 2: RAM "usedMb totalMb" (hoặc "50%")
        if (lines.Length >= 2)
        {
            var ramParts = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ramParts.Length >= 2
                && double.TryParse(ramParts[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double usedMb)
                && double.TryParse(ramParts[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double totalMb)
                && totalMb > 0 && usedMb >= 0)
            {
                int ramPct = (int)Math.Clamp(Math.Round(usedMb / totalMb * 100.0), 0, 100);
                ramUsage = $"RAM: {(int)Math.Round(usedMb)}/{(int)Math.Round(totalMb)} MB ({ramPct}%)";
            }
            else if (ramParts.Length == 1 && ramParts[0].EndsWith('%')
                     && double.TryParse(ramParts[0].TrimEnd('%').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                ramUsage = $"RAM: {ramParts[0]}";
            }
        }

        // Line 3: Disk "usedG totalG pct%" (hoặc "42%")
        if (lines.Length >= 3)
        {
            var diskParts = lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (diskParts.Length >= 3 && diskParts[2].EndsWith('%'))
            {
                diskUsage = $"Disk: {diskParts[0]}/{diskParts[1]} ({diskParts[2]})";
            }
            else if (diskParts.Length == 1 && diskParts[0].EndsWith('%')
                     && double.TryParse(diskParts[0].TrimEnd('%').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                diskUsage = $"Disk: {diskParts[0]}";
            }
        }

        return (cpuUsage, ramUsage, diskUsage);
    }

    /// <summary>
    /// Lấy chỉ số hệ thống (CPU, RAM, Disk) qua phiên SSH đang mở.
    /// Thử lệnh Linux trước; nếu rỗng hoặc không parse được (máy đích là Windows),
    /// tự động fallback sang lệnh PowerShell của Windows.
    /// </summary>
    public static Task<(string? CpuUsage, string? RamUsage, string? DiskUsage)> FetchSystemMetricsOverSshAsync(
        Renci.SshNet.SshClient ssh,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var linuxCmd = ssh.CreateCommand(LinuxMetricsCommand);
                linuxCmd.CommandTimeout = TimeSpan.FromSeconds(6);
                string linuxOut = linuxCmd.Execute() ?? string.Empty;
                var parsedLinux = ParseMetricsOutput(linuxOut);
                if (parsedLinux.CpuUsage is not null || parsedLinux.RamUsage is not null || parsedLinux.DiskUsage is not null)
                {
                    return parsedLinux;
                }
            }
            catch
            {
                // Máy đích có thể là Windows hoặc không hỗ trợ lệnh POSIX -> thử lệnh PowerShell bên dưới
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var winCmd = ssh.CreateCommand(WindowsMetricsPowerShellCommand);
            winCmd.CommandTimeout = TimeSpan.FromSeconds(8);
            string winOut = winCmd.Execute() ?? string.Empty;
            return ParseMetricsOutput(winOut);
        }, cancellationToken);
    }

    /// <param name="profile">Profile máy ZCU cần dò.</param>
    /// <param name="cancellationToken">Token huỷ — được tôn trọng cả trong task nền lấy CPU/RAM/Disk.</param>
    /// <param name="uiDispatch">
    /// Q11: hàm marshal về UI thread (VD Avalonia: <c>a => Dispatcher.UIThread.Post(a)</c>).
    /// Setter <c>CpuUsage/RamUsage/DiskUsage</c> raise <c>PropertyChanged</c> — giống
    /// <see cref="ComputerProfile.ApplyStatusResult"/>, PHẢI chạy trên UI thread khi có binding.
    /// Nếu null → invoke trực tiếp trên thread nền (chỉ an toàn khi không có UI binding).
    /// </param>
    public static async Task<ComputerStatusProbeResult> ProbeAsync(
        ComputerProfile profile,
        CancellationToken cancellationToken = default,
        Action<Action>? uiDispatch = null)
    {
        int sshPort = profile.SshPort > 0 ? profile.SshPort : 22;

        bool sshOk = await IsPortOpenAsync(profile.Host, sshPort, cancellationToken);
        bool agentOk = await IsPortOpenAsync(profile.Host, profile.Port, cancellationToken);

        void SetUsages(string cpu, string ram, string disk)
        {
            void Apply()
            {
                profile.CpuUsage = cpu;
                profile.RamUsage = ram;
                profile.DiskUsage = disk;
            }
            if (uiDispatch != null) uiDispatch(Apply); else Apply();
        }

        if (sshOk && !string.IsNullOrEmpty(profile.SshUsername) && !string.IsNullOrEmpty(profile.SshPassword))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var ssh = new Renci.SshNet.SshClient(profile.Host, sshPort, profile.SshUsername, profile.SshPassword);
                    ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
                    ssh.Connect();

                    var (cpu, ram, disk) = await FetchSystemMetricsOverSshAsync(ssh, cancellationToken);

                    // Q11: set qua uiDispatch — setter raise PropertyChanged, không được
                    // gọi từ thread nền khi có UI binding (nhất quán với ApplyStatusResult).
                    SetUsages(cpu ?? "", ram ?? "", disk ?? "");

                    ssh.Disconnect();
                }
                catch (OperationCanceledException)
                {
                    // Bị huỷ — bỏ qua có chủ đích.
                }
                catch (Exception ex)
                {
                    // Q11: không nuốt im lặng — log để chẩn đoán được lỗi SSH.
                    System.Diagnostics.Trace.TraceWarning(
                        $"[ComputerStatusChecker] Lỗi lấy CPU/RAM/Disk qua SSH {profile.Host}:{sshPort}: {ex.Message}");
                }
            }, cancellationToken);
        }
        else
        {
            SetUsages("", "", "");
        }

        return new ComputerStatusProbeResult(sshOk, agentOk);
    }

    private static async Task<bool> IsPortOpenAsync(string host, int port, CancellationToken cancellationToken, int timeoutMs = 1500)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0) return false;

        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, timeoutCts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
