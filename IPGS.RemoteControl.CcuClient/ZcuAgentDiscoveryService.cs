using System.Net.NetworkInformation;
using System.Net.Sockets;
using IPGS.RemoteControl.Protocol;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>Một máy ZCU đã cài ZcuAgent, tìm thấy qua quét mạng LAN.</summary>
public sealed record DiscoveredZcuAgent(string Host, int Port, string ServerName, int ScreenWidth, int ScreenHeight);

/// <summary>
/// Quét dải mạng LAN (subnet /24) để tìm các máy đã cài ZcuAgent — xác nhận bằng
/// bắt tay giao thức thật (HELLO → HELLO_ACK, TDD §5.2), KHÔNG chỉ dò cổng TCP mở,
/// để tránh nhận nhầm máy có port trùng nhưng không phải ZcuAgent.
/// </summary>
public static class ZcuAgentDiscoveryService
{
    /// <summary>
    /// Trả về danh sách các subnet base (dạng "192.168.1.") của các card mạng IPv4
    /// đang hoạt động trên máy hiện tại (bỏ qua loopback và APIPA 169.254.x.x).
    /// </summary>
    public static IReadOnlyList<string> GetLocalSubnetBases()
    {
        var result = new List<string>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                var bytes = ua.Address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254) continue; // APIPA — bỏ qua

                string subnetBase = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.";
                if (!result.Contains(subnetBase))
                    result.Add(subnetBase);
            }
        }

        return result;
    }

    /// <summary>
    /// Quét .1 → .254 trong <paramref name="subnetBase"/> (VD: "192.168.1.") trên cổng
    /// <paramref name="port"/>, gọi <paramref name="onFound"/> mỗi khi xác nhận 1 ZcuAgent thật.
    /// <paramref name="progress"/> báo số host đã quét xong (0-254), không phân biệt thành/bại.
    /// </summary>
    public static async Task ScanAsync(
        string subnetBase,
        int port,
        Action<DiscoveredZcuAgent> onFound,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        int maxConcurrency = 40,
        int connectTimeoutMs = 300,
        int handshakeTimeoutMs = 800)
    {
        var hosts = Enumerable.Range(1, 254).Select(i => $"{subnetBase}{i}").ToList();
        using var throttle = new SemaphoreSlim(maxConcurrency);
        int completed = 0;

        var tasks = hosts.Select(async host =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var found = await ProbeHostAsync(host, port, connectTimeoutMs, handshakeTimeoutMs, cancellationToken)
                    .ConfigureAwait(false);
                if (found != null) onFound(found);
            }
            finally
            {
                throttle.Release();
                int done = Interlocked.Increment(ref completed);
                progress?.Report(done);
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<DiscoveredZcuAgent?> ProbeHostAsync(
        string host, int port, int connectTimeoutMs, int handshakeTimeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var tcp = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(connectTimeoutMs);
            await tcp.ConnectAsync(host, port, connectCts.Token).ConfigureAwait(false);

            await using var stream = tcp.GetStream();
            using var hsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            hsCts.CancelAfter(handshakeTimeoutMs);

            await MessageCodec.WriteMessageAsync(
                stream, MessageType.Hello, MessageCodec.EncodeHello("IPGS-CCU-Scan"), hsCts.Token)
                .ConfigureAwait(false);

            var (type, payload) = await MessageCodec.ReadMessageAsync(stream, hsCts.Token).ConfigureAwait(false);
            if (type != MessageType.HelloAck) return null;

            var (_, screenW, screenH, serverName) = MessageCodec.DecodeHelloAck(payload);
            return new DiscoveredZcuAgent(host, port, serverName, (int)screenW, (int)screenH);
        }
        catch
        {
            return null;
        }
    }
}
