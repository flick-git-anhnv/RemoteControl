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
    public static async Task<ComputerStatusProbeResult> ProbeAsync(ComputerProfile profile, CancellationToken cancellationToken = default)
    {
        int sshPort = profile.SshPort > 0 ? profile.SshPort : 22;

        bool sshOk = await IsPortOpenAsync(profile.Host, sshPort, cancellationToken);
        bool agentOk = await IsPortOpenAsync(profile.Host, profile.Port, cancellationToken);

        if (sshOk && !string.IsNullOrEmpty(profile.SshUsername) && !string.IsNullOrEmpty(profile.SshPassword))
        {
            _ = Task.Run(() =>
            {
                try
                {
                    using var ssh = new Renci.SshNet.SshClient(profile.Host, sshPort, profile.SshUsername, profile.SshPassword);
                    ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
                    ssh.Connect();
                    
                    var cmd = ssh.CreateCommand("top -bn1 | grep \"Cpu(s)\" | awk '{print $2 + $4}'");
                    var cpuOut = cmd.Execute().Trim();
                    
                    cmd = ssh.CreateCommand("free -m | awk '/Mem:/ {printf \"%d%%\", $3/$2*100}'");
                    var ramOut = cmd.Execute().Trim();
                    
                    cmd = ssh.CreateCommand("df -h / | awk 'NR==2 {print $5}'");
                    var diskOut = cmd.Execute().Trim();

                    profile.CpuUsage = string.IsNullOrEmpty(cpuOut) ? "" : $"CPU: {cpuOut}%";
                    profile.RamUsage = string.IsNullOrEmpty(ramOut) ? "" : $"RAM: {ramOut}";
                    profile.DiskUsage = string.IsNullOrEmpty(diskOut) ? "" : $"Disk: {diskOut}";
                    
                    ssh.Disconnect();
                }
                catch { }
            }, cancellationToken);
        }
        else
        {
            profile.CpuUsage = "";
            profile.RamUsage = "";
            profile.DiskUsage = "";
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
