using System.Net.Sockets;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>Kết quả dò trạng thái kết nối của một máy tính ZCU.</summary>
public readonly record struct ComputerStatusProbeResult(bool SshReachable, bool AgentReachable)
{
    public ComputerConnectivityStatus Status =>
        SshReachable && AgentReachable ? ComputerConnectivityStatus.Online : ComputerConnectivityStatus.Offline;
}

/// <summary>
/// Dò nhanh trạng thái kết nối SSH + Agent Remote Control của một máy tính ZCU
/// bằng TCP connect (không xác thực) — đủ để biết cổng có mở/service có đang
/// chạy hay không, không cần thông tin đăng nhập.
/// </summary>
public static class ComputerStatusChecker
{
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
            _ = Task.Run(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var ssh = new Renci.SshNet.SshClient(profile.Host, sshPort, profile.SshUsername, profile.SshPassword);
                    ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
                    ssh.Connect();

                    cancellationToken.ThrowIfCancellationRequested();
                    using var cpuCmd = ssh.CreateCommand("top -bn1 | grep \"Cpu(s)\" | awk '{print $2 + $4}'");
                    var cpuOut = cpuCmd.Execute().Trim();

                    cancellationToken.ThrowIfCancellationRequested();
                    using var ramCmd = ssh.CreateCommand("free -m | awk '/Mem:/ {printf \"%d%%\", $3/$2*100}'");
                    var ramOut = ramCmd.Execute().Trim();

                    cancellationToken.ThrowIfCancellationRequested();
                    using var diskCmd = ssh.CreateCommand("df -h / | awk 'NR==2 {print $5}'");
                    var diskOut = diskCmd.Execute().Trim();

                    // Q11: set qua uiDispatch — setter raise PropertyChanged, không được
                    // gọi từ thread nền khi có UI binding (nhất quán với ApplyStatusResult).
                    SetUsages(
                        string.IsNullOrEmpty(cpuOut) ? "" : $"CPU: {cpuOut}%",
                        string.IsNullOrEmpty(ramOut) ? "" : $"RAM: {ramOut}",
                        string.IsNullOrEmpty(diskOut) ? "" : $"Disk: {diskOut}");

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
