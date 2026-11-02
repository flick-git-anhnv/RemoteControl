using IPGS.RemoteControl.CcuClient;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class ComputerStatusCheckerTests
{
    [Fact]
    public void ProbeResult_BothReachable_ReturnsOnline()
    {
        var result = new ComputerStatusProbeResult(SshReachable: true, AgentReachable: true);
        Assert.Equal(ComputerConnectivityStatus.Online, result.Status);
    }

    [Fact]
    public void ProbeResult_SshReachableOnly_ReturnsOffline()
    {
        var result = new ComputerStatusProbeResult(SshReachable: true, AgentReachable: false);
        Assert.Equal(ComputerConnectivityStatus.Offline, result.Status);
    }

    [Fact]
    public void ProbeResult_AgentReachableOnly_ReturnsOffline()
    {
        var result = new ComputerStatusProbeResult(SshReachable: false, AgentReachable: true);
        Assert.Equal(ComputerConnectivityStatus.Offline, result.Status);
    }

    [Fact]
    public void ProbeResult_NeitherReachable_ReturnsOffline()
    {
        var result = new ComputerStatusProbeResult(SshReachable: false, AgentReachable: false);
        Assert.Equal(ComputerConnectivityStatus.Offline, result.Status);
    }

    [Fact]
    public async Task ProbeAsync_InvalidOrUnreachableHost_ReturnsBothFalse()
    {
        var profile = new ComputerProfile
        {
            Name = "Test Unreachable",
            Host = "192.0.2.1", // TEST-NET-1 (RFC 5737) non-routable IP
            Port = 65432,
            SshPort = 65433
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = await ComputerStatusChecker.ProbeAsync(profile, cts.Token);

        Assert.False(result.SshReachable);
        Assert.False(result.AgentReachable);
        Assert.Equal(ComputerConnectivityStatus.Offline, result.Status);
    }

    [Fact]
    public async Task ProbeAsync_EmptyHost_ReturnsImmediatelyWithoutThrowing()
    {
        var profile = new ComputerProfile
        {
            Name = "Empty Host",
            Host = "",
            Port = 17600
        };

        var result = await ComputerStatusChecker.ProbeAsync(profile);

        Assert.False(result.SshReachable);
        Assert.False(result.AgentReachable);
        Assert.Equal(ComputerConnectivityStatus.Offline, result.Status);
    }
}
