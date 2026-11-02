using IPGS.RemoteControl.ZcuAgent.Interop;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class X11DisplayResolverTests
{
    [Theory]
    [InlineData("X0", ":0")]
    [InlineData("X1", ":1")]
    [InlineData("X10", ":10")]
    [InlineData("X99", ":99")]
    [InlineData("/tmp/.X11-unix/X0", ":0")]
    [InlineData("/tmp/.X11-unix/X11", ":11")]
    public void ParseDisplayFromSocketName_ValidNames_ReturnsCorrectDisplay(string socketName, string expectedDisplay)
    {
        var result = X11DisplayResolver.ParseDisplayFromSocketName(socketName);
        Assert.Equal(expectedDisplay, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("X")]
    [InlineData("Xabc")]
    [InlineData("socket")]
    [InlineData("Y0")]
    public void ParseDisplayFromSocketName_InvalidNames_ReturnsNull(string? socketName)
    {
        var result = X11DisplayResolver.ParseDisplayFromSocketName(socketName!);
        Assert.Null(result);
    }

    [Fact]
    public void GetCandidateDisplays_PreferredDisplay_HasHighestPriority()
    {
        var candidates = X11DisplayResolver.GetCandidateDisplays(
            preferredDisplay: ":99",
            envDisplay: ":0",
            socketFileNames: ["X1", "X10"]);

        Assert.Equal(":99", candidates[0]);
        Assert.Contains(":0", candidates);
        Assert.Contains(":1", candidates);
        Assert.Contains(":10", candidates);
    }

    [Fact]
    public void GetCandidateDisplays_PreferredDisplayWithoutColon_PrependsColon()
    {
        var candidates = X11DisplayResolver.GetCandidateDisplays(
            preferredDisplay: "42",
            envDisplay: null,
            socketFileNames: null);

        Assert.Equal(":42", candidates[0]);
    }

    [Fact]
    public void GetCandidateDisplays_Deduplication_MaintainsOrder()
    {
        var candidates = X11DisplayResolver.GetCandidateDisplays(
            preferredDisplay: ":0",
            envDisplay: ":0",
            socketFileNames: ["X0", "X1"]);

        // :0 chỉ xuất hiện 1 lần duy nhất ở vị trí đầu tiên
        Assert.Equal(1, candidates.Count(c => c == ":0"));
        Assert.Equal(":0", candidates[0]);
    }

    [Fact]
    public void GetCandidateDisplays_WithSockets_OrdersDisplayNumbersAscending()
    {
        var socketFiles = new[] { "X11", "X1", "X10", "X0" };
        var candidates = X11DisplayResolver.GetCandidateDisplays(
            preferredDisplay: null,
            envDisplay: null,
            socketFileNames: socketFiles);

        var idx0 = candidates.IndexOf(":0");
        var idx1 = candidates.IndexOf(":1");
        var idx10 = candidates.IndexOf(":10");
        var idx11 = candidates.IndexOf(":11");

        Assert.True(idx0 < idx1);
        Assert.True(idx1 < idx10);
        Assert.True(idx10 < idx11);
    }

    [Fact]
    public void OpenAvailableDisplay_WithOpener_ResolvesFirstWorkingDisplay()
    {
        // Giả lập :0 và :1 không kết nối được (máy headless không có màn hình vật lý),
        // phiên ảo xrdp :10.0 kết nối thành công.
        var targetDisplay = ":10.0";
        var mockHandle = new IntPtr(0xCAFE);

        var result = X11DisplayResolver.OpenAvailableDisplay(
            preferredDisplay: null,
            logger: null,
            displayOpener: candidate => candidate == targetDisplay ? mockHandle : IntPtr.Zero);

        Assert.Equal(mockHandle, result.DisplayHandle);
        Assert.Equal(targetDisplay, result.ResolvedDisplayName);
    }

    [Fact]
    public void OpenAvailableDisplay_AllFail_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            X11DisplayResolver.OpenAvailableDisplay(
                preferredDisplay: ":99",
                logger: null,
                displayOpener: _ => IntPtr.Zero);
        });

        Assert.Contains("Không thể kết nối đến bất kỳ X11 Display nào", ex.Message);
        Assert.Contains(":99", ex.Message);
    }
}
