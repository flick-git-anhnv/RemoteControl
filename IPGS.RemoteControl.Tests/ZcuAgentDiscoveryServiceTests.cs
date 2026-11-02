using IPGS.RemoteControl.CcuClient;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class ZcuAgentDiscoveryServiceTests
{
    [Fact]
    public void GetLocalSubnetBases_DoesNotThrow_AndReturnsValidFormat()
    {
        var subnets = ZcuAgentDiscoveryService.GetLocalSubnetBases();
        Assert.NotNull(subnets);

        foreach (var subnet in subnets)
        {
            // Subnet base must end with a dot, e.g. "192.168.1."
            Assert.EndsWith(".", subnet);
            var parts = subnet.Split('.', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, parts.Length);
            foreach (var part in parts)
            {
                Assert.True(int.TryParse(part, out int octet));
                Assert.InRange(octet, 0, 255);
            }
        }
    }

    [Fact]
    public void DiscoveredZcuAgent_RecordEquality_WorksCorrectly()
    {
        var a1 = new DiscoveredZcuAgent("192.168.1.100", 17600, "ZCU-Kiosk-1", 1920, 1080, "SHA256:ABCDEF");
        var a2 = new DiscoveredZcuAgent("192.168.1.100", 17600, "ZCU-Kiosk-1", 1920, 1080, "SHA256:ABCDEF");
        var a3 = new DiscoveredZcuAgent("192.168.1.100", 17600, "ZCU-Kiosk-1", 1920, 1080, "SHA256:DIFFERENT");

        Assert.Equal(a1, a2);
        Assert.NotEqual(a1, a3);
        Assert.Equal("SHA256:ABCDEF", a1.TlsCertFingerprint);
    }

    [Fact]
    public void DiscoveredZcuAgent_WithoutFingerprint_DefaultsToNull()
    {
        var agent = new DiscoveredZcuAgent("10.0.0.5", 17600, "ZCU-Agent", 1280, 720);
        Assert.Null(agent.TlsCertFingerprint);
        Assert.Equal("10.0.0.5", agent.Host);
        Assert.Equal(17600, agent.Port);
        Assert.Equal("ZCU-Agent", agent.ServerName);
        Assert.Equal(1280, agent.ScreenWidth);
        Assert.Equal(720, agent.ScreenHeight);
    }

    [Fact]
    public async Task ScanAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        var foundList = new List<DiscoveredZcuAgent>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ZcuAgentDiscoveryService.ScanAsync(
                "192.168.99.",
                17600,
                agent => foundList.Add(agent),
                cancellationToken: cts.Token));

        Assert.Empty(foundList);
    }
}
