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
