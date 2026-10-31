using IPGS.RemoteControl.CcuClient.Services;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class WakeOnLanServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMagicPacketAsync_EmptyOrNullMac_ThrowsArgumentException(string? invalidMac)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            WakeOnLanService.SendMagicPacketAsync(invalidMac!));
        Assert.Equal("macAddress", ex.ParamName);
    }

    [Theory]
    [InlineData("00:11:22")] // Too short
    [InlineData("00:11:22:33:44:55:66")] // Too long
    [InlineData("00:11:22:33:44:ZZ")] // Non-hex characters
    [InlineData("NOT_A_MAC_ADDR")]
    [InlineData("12345678901G")] // 'G' is not hex
    public async Task SendMagicPacketAsync_InvalidMacFormat_ThrowsArgumentException(string invalidMac)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            WakeOnLanService.SendMagicPacketAsync(invalidMac));
        Assert.Contains("Phải gồm 12 ký tự hex", ex.Message);
    }

    [Theory]
    [InlineData("00:11:22:33:44:55")] // Colon separated
    [InlineData("00-11-22-33-44-55")] // Hyphen separated
    [InlineData("00 11 22 33 44 55")] // Space separated
    [InlineData("001122334455")]       // No separator
    [InlineData("aa:bb:cc:dd:ee:ff")] // Lowercase hex
    [InlineData("AA:BB:CC:DD:EE:FF")] // Uppercase hex
    public async Task SendMagicPacketAsync_ValidMacFormats_DoesNotThrow(string validMac)
    {
        // Sending broadcast UDP packets to port 9 locally should complete without throwing
        var ex = await Record.ExceptionAsync(() => WakeOnLanService.SendMagicPacketAsync(validMac));
        Assert.Null(ex);
    }
}
