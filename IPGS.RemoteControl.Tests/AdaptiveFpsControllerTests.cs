using IPGS.RemoteControl.ZcuAgent.Net;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class AdaptiveFpsControllerTests
{
    [Fact]
    public void StartsAtMaximum()
    {
        var c = new AdaptiveFpsController(maxFps: 30, minFps: 15, enabled: true);
        Assert.Equal(30, c.MaxFps);
        Assert.Equal(15, c.MinFps);
        Assert.Equal(30, c.CurrentFps);
        Assert.Equal(33, c.FrameIntervalMs); // round(1000/30) = 33
    }

    [Fact]
    public void FloorConstraint_NeverDropsBelow15_EvenUnderSevereLoad()
    {
        // YÊU CẦU CỐT LÕI CỦA USER: Sàn MinFps = 15, tuyệt đối không hạ dưới 15
        var c = new AdaptiveFpsController(maxFps: 30, minFps: 15, enabled: true);

        // Giả lập 500 mẫu cực xấu (X-latency 50ms, PSI 80%, Write 300ms)
        for (var i = 0; i < 500; i++)
        {
            c.OnSample(xLatencyMs: 50.0, cpuPressureAvg10: 80.0, writeMs: 300.0);
            Assert.True(c.CurrentFps >= 15.0, $"CurrentFps {c.CurrentFps} dropped below MinFps floor 15!");
        }

        Assert.Equal(15.0, c.CurrentFps);
    }

    [Fact]
    public void AIMD_FastDrop_OnHighXLatency()
    {
        var c = new AdaptiveFpsController(maxFps: 30, minFps: 15, enabled: true);

        // Mẫu 1 xấu: chưa giảm ngay (cần 2 mẫu liên tiếp để tránh đột biến tức thời)
        var changed1 = c.OnSample(xLatencyMs: 6.5, cpuPressureAvg10: 2.0, writeMs: 10.0);
        Assert.False(changed1);
        Assert.Equal(30.0, c.CurrentFps);

        // Mẫu 2 xấu: kích hoạt giảm nhanh (30 * 0.8 = 24.0)
        var changed2 = c.OnSample(xLatencyMs: 7.0, cpuPressureAvg10: 2.0, writeMs: 10.0);
        Assert.True(changed2);
        Assert.Equal(24.0, c.CurrentFps);
        Assert.Contains("x-latency", c.LastReason);

        // Thêm 2 mẫu xấu nữa: 24.0 * 0.8 = 19.2
        c.OnSample(xLatencyMs: 6.0, cpuPressureAvg10: 2.0, writeMs: 10.0);
        var changed3 = c.OnSample(xLatencyMs: 6.0, cpuPressureAvg10: 2.0, writeMs: 10.0);
        Assert.True(changed3);
        Assert.Equal(19.2, c.CurrentFps);

        // Thêm 2 mẫu xấu nữa: 19.2 * 0.8 = 15.4
        c.OnSample(xLatencyMs: 6.0, cpuPressureAvg10: 2.0, writeMs: 10.0);
        c.OnSample(xLatencyMs: 6.0, cpuPressureAvg10: 2.0, writeMs: 10.0);
        Assert.Equal(15.4, c.CurrentFps);

        // Thêm 2 mẫu xấu nữa: chạm sàn 15.0 (không thể xuống dưới 15)
        c.OnSample(xLatencyMs: 6.0, cpuPressureAvg10: 2.0, writeMs: 10.0);
        c.OnSample(xLatencyMs: 6.0, cpuPressureAvg10: 2.0, writeMs: 10.0);
        Assert.Equal(15.0, c.CurrentFps);
    }

    [Fact]
    public void AIMD_FastDrop_OnHighCpuPressure()
    {
        var c = new AdaptiveFpsController(maxFps: 30, minFps: 15, enabled: true);

        // 2 mẫu PSI cao (> 15%)
        c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 22.0, writeMs: 5.0);
        var changed = c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 25.0, writeMs: 5.0);

        Assert.True(changed);
        Assert.Equal(24.0, c.CurrentFps);
        Assert.Contains("cpu-psi", c.LastReason);
    }

    [Fact]
    public void AIMD_FastDrop_OnHighWriteMs()
    {
        var c = new AdaptiveFpsController(maxFps: 30, minFps: 15, enabled: true);

        // 2 mẫu Write chậm (> 120ms)
        c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 2.0, writeMs: 150.0);
        var changed = c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 2.0, writeMs: 180.0);

        Assert.True(changed);
        Assert.Equal(24.0, c.CurrentFps);
        Assert.Contains("net-write", c.LastReason);
    }

    [Fact]
    public void Deadband_PreventsOscillation()
    {
        var c = new AdaptiveFpsController(maxFps: 30, minFps: 15, enabled: true);

        // Đẩy xuống 24 FPS
        c.OnSample(xLatencyMs: 8.0, cpuPressureAvg10: 2.0, writeMs: 5.0);
        c.OnSample(xLatencyMs: 8.0, cpuPressureAvg10: 2.0, writeMs: 5.0);
        Assert.Equal(24.0, c.CurrentFps);

        // Giả lập 50 mẫu nằm trong vùng deadband (X-latency 3.5ms giữa 2.5ms và 5.0ms)
        for (var i = 0; i < 50; i++)
        {
            var changed = c.OnSample(xLatencyMs: 3.5, cpuPressureAvg10: 8.0, writeMs: 50.0);
            Assert.False(changed, "FPS should not oscillate in deadband region");
            Assert.Equal(24.0, c.CurrentFps);
        }
    }

    [Fact]
    public void AIMD_SlowRecover_AfterSustainedGoodSamples()
    {
        var c = new AdaptiveFpsController(maxFps: 30, minFps: 15, enabled: true);

        // Đẩy xuống sàn 15 FPS
        for (var i = 0; i < 20; i++) c.OnSample(xLatencyMs: 10.0, cpuPressureAvg10: 50.0, writeMs: 200.0);
        Assert.Equal(15.0, c.CurrentFps);

        // Cần 10 mẫu tốt liên tiếp để tăng 1 nấc +2.0 FPS
        for (var i = 0; i < 9; i++)
        {
            var changed = c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 2.0, writeMs: 5.0);
            Assert.False(changed);
        }

        var recovered1 = c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 2.0, writeMs: 5.0);
        Assert.True(recovered1);
        Assert.Equal(17.0, c.CurrentFps);

        // Tiếp tục 10 mẫu tốt nữa → 19.0 FPS
        for (var i = 0; i < 9; i++) c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 2.0, writeMs: 5.0);
        var recovered2 = c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 2.0, writeMs: 5.0);
        Assert.True(recovered2);
        Assert.Equal(19.0, c.CurrentFps);

        // Tốt liên tục đủ lâu → phục hồi hoàn toàn về MaxFps = 30
        for (var i = 0; i < 100; i++) c.OnSample(xLatencyMs: 0.5, cpuPressureAvg10: 2.0, writeMs: 5.0);
        Assert.Equal(30.0, c.CurrentFps);
    }

    [Fact]
    public void Disabled_NeverChanges()
    {
        var c = new AdaptiveFpsController(maxFps: 30, minFps: 15, enabled: false);
        for (var i = 0; i < 100; i++)
        {
            var changed = c.OnSample(xLatencyMs: 50.0, cpuPressureAvg10: 100.0, writeMs: 500.0);
            Assert.False(changed);
            Assert.Equal(30.0, c.CurrentFps);
        }
    }

    [Fact]
    public void Constructor_ClampsInvalidInputs()
    {
        // MaxFps < 1 -> 1; MinFps > MaxFps -> MinFps = MaxFps
        var c1 = new AdaptiveFpsController(maxFps: 0, minFps: 15);
        Assert.Equal(1, c1.MaxFps);
        Assert.Equal(1, c1.MinFps);

        var c2 = new AdaptiveFpsController(maxFps: 120, minFps: 0);
        Assert.Equal(60, c2.MaxFps); // clamp to 60
        Assert.Equal(1, c2.MinFps);  // clamp to 1
    }
}
