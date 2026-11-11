using IPGS.RemoteControl.CcuUI.ViewModels;
using IPGS.RemoteControl.ZcuAgent.Capture;

namespace IPGS.RemoteControl.Tests;

public sealed class ResolutionAdaptationTests
{
    [Theory]
    [InlineData(1024, 768, 1690, 884, 1.3333)]   // 4:3 Headless fallback
    [InlineData(1280, 1024, 1690, 884, 1.25)]    // 5:4 Physical monitor
    [InlineData(1920, 1080, 1690, 884, 1.7778)]  // 16:9 Full HD
    [InlineData(1080, 1920, 1690, 884, 0.5625)]  // 9:16 Portrait Kiosk
    public void CalculateAutoFitWindowSize_MatchesRemoteAspectRatioWithoutExceedingBounds(
        int remoteW, int remoteH, double maxW, double maxH, double expectedAspect)
    {
        var (w, h) = RemoteScreenViewModel.CalculateAutoFitWindowSize(remoteW, remoteH, maxW, maxH);

        Assert.True(w <= maxW + 1);
        Assert.True(h <= maxH + 1);

        double actualAspect = w / h;
        Assert.InRange(Math.Abs(actualAspect - expectedAspect), 0, 0.01);
    }

    [Fact]
    public void MapToZcuCoords_UniformvsFill_MapsAccurately()
    {
        // Cửa sổ 1600x900 (16:9) hiển thị máy đích 1200x900 (4:3):
        // Ở chế độ Uniform: vùng ảnh thực rộng 1200x900, viền đen mỗi bên 200px (offsetX = 200).
        RemoteScreenViewModel.MapToZcuCoords(
            controlX: 200, controlY: 450,
            controlW: 1600, controlH: 900,
            screenW: 1200, screenH: 900,
            RemoteDisplayMode.Uniform,
            out int zxUniformLeft, out int zyUniformMid);

        Assert.Equal(0, zxUniformLeft);
        Assert.Equal(450, zyUniformMid);

        // Ở chế độ Fill: ảnh kéo giãn toàn khung 1600x900, nên x=800 là chính giữa (600) và x=200 là 150.
        RemoteScreenViewModel.MapToZcuCoords(
            controlX: 200, controlY: 450,
            controlW: 1600, controlH: 900,
            screenW: 1200, screenH: 900,
            RemoteDisplayMode.Fill,
            out int zxFill, out int zyFill);

        Assert.Equal(150, zxFill);
        Assert.Equal(450, zyFill);
    }

    [Fact]
    public void HeadlessResolutionAdapter_DetectsHeadlessZeroMmFallback_ReturnsApplyHeadless1080p()
    {
        const string xrandrHeadless = """
            Screen 0: minimum 320 x 200, current 1024 x 768, maximum 16384 x 16384
            DP-1 disconnected (normal left inverted right x axis y axis)
            HDMI-1 connected primary 1024x768+0+0 (normal left inverted right x axis y axis) 0mm x 0mm
               1024x768      60.00* 
               800x600       60.32    56.25  
               848x480       60.00  
               640x480       59.94  
            """;

        var state = HeadlessResolutionAdapter.ParseXrandrOutput(xrandrHeadless);
        var decision = HeadlessResolutionAdapter.DecideAction(state);

        Assert.Equal(ResolutionAdaptationKind.ApplyHeadless1080p, decision.Kind);
        Assert.Equal("HDMI-1", decision.TargetOutput);
    }

    [Fact]
    public void HeadlessResolutionAdapter_WhenHeadlessAlready1080p_ReturnsNone()
    {
        const string xrandrHeadless1080 = """
            Screen 0: minimum 320 x 200, current 1920 x 1080, maximum 16384 x 16384
            DP-1 disconnected (normal left inverted right x axis y axis)
            HDMI-1 connected primary 1920x1080+0+0 (normal left inverted right x axis y axis) 0mm x 0mm
               1024x768      60.00  
               1920x1080_60.00  59.96* 
            """;

        var state = HeadlessResolutionAdapter.ParseXrandrOutput(xrandrHeadless1080);
        var decision = HeadlessResolutionAdapter.DecideAction(state);

        Assert.Equal(ResolutionAdaptationKind.None, decision.Kind);
    }

    [Fact]
    public void HeadlessResolutionAdapter_WhenPhysicalMonitorConnectedNative_LeavesUntouched()
    {
        const string xrandrPhysical = """
            Screen 0: minimum 320 x 200, current 1280 x 1024, maximum 16384 x 16384
            DP-1 disconnected (normal left inverted right x axis y axis)
            HDMI-1 connected primary 1280x1024+0+0 (normal left inverted right x axis y axis) 376mm x 301mm
               1280x1024     60.02*+  75.02  
               1024x768      75.03    60.00  
            """;

        var state = HeadlessResolutionAdapter.ParseXrandrOutput(xrandrPhysical);
        var decision = HeadlessResolutionAdapter.DecideAction(state);

        Assert.Equal(ResolutionAdaptationKind.None, decision.Kind);
    }

    [Fact]
    public void HeadlessResolutionAdapter_WhenPhysicalMonitorHotpluggedDuringSyntheticMode_RestoresAuto()
    {
        const string xrandrHotplug = """
            Screen 0: minimum 320 x 200, current 1920 x 1080, maximum 16384 x 16384
            DP-1 disconnected (normal left inverted right x axis y axis)
            HDMI-1 connected primary 1920x1080+0+0 (normal left inverted right x axis y axis) 376mm x 301mm
               1280x1024     60.02 +  75.02  
               1024x768      75.03    60.00  
               1920x1080_60.00  59.96* 
            """;

        var state = HeadlessResolutionAdapter.ParseXrandrOutput(xrandrHotplug);
        var decision = HeadlessResolutionAdapter.DecideAction(state);

        Assert.Equal(ResolutionAdaptationKind.RestorePhysicalMonitorAuto, decision.Kind);
        Assert.Equal("HDMI-1", decision.TargetOutput);
    }
}
