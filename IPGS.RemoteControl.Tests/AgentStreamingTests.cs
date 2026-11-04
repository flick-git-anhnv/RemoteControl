using IPGS.RemoteControl.ZcuAgent.Capture;
using IPGS.RemoteControl.ZcuAgent.Net;
using Xunit;

namespace IPGS.RemoteControl.Tests;

/// <summary>
/// Nấc 1 — FPS ZCU: bộ phát hiện khung tĩnh, bộ điều tiết theo mạng, thống kê từng giai đoạn.
/// Các class này được link thẳng từ project ZcuAgent (xem IPGS.RemoteControl.Tests.csproj).
/// </summary>
public class FrameChangeDetectorTests
{
    private static byte[] MakeFrame(int width, int height, int stride, byte fill)
    {
        var buf = new byte[stride * height];
        Array.Fill(buf, fill);
        return buf;
    }

    [Fact]
    public void FirstFrame_IsAlwaysChanged()
    {
        var d = new FrameChangeDetector();
        Assert.True(d.HasChanged(MakeFrame(8, 4, 32, 1), 8, 4, 32));
    }

    [Fact]
    public void IdenticalFrame_IsNotChanged()
    {
        var d = new FrameChangeDetector();
        var f = MakeFrame(8, 4, 32, 1);
        d.HasChanged(f, 8, 4, 32);
        Assert.False(d.HasChanged((byte[])f.Clone(), 8, 4, 32));
        Assert.Equal(-1, d.LastFirstChangedRow);
    }

    [Fact]
    public void SinglePixelChange_IsDetected_AndReportsRow()
    {
        var d = new FrameChangeDetector();
        var f = MakeFrame(8, 4, 32, 1);
        d.HasChanged(f, 8, 4, 32);

        var g = (byte[])f.Clone();
        g[2 * 32 + 5] = 99; // hàng 2
        Assert.True(d.HasChanged(g, 8, 4, 32));
        Assert.Equal(2, d.LastFirstChangedRow);

        // Bản lưu đã được cập nhật → gửi lại đúng khung đó thì không còn "đổi".
        Assert.False(d.HasChanged(g, 8, 4, 32));
    }

    [Fact]
    public void ChangeInLastRow_IsDetected()
    {
        var d = new FrameChangeDetector();
        var f = MakeFrame(8, 4, 32, 0);
        d.HasChanged(f, 8, 4, 32);
        var g = (byte[])f.Clone();
        g[3 * 32 + 31] = 1;
        Assert.True(d.HasChanged(g, 8, 4, 32));
    }

    [Fact]
    public void StridePadding_IsIgnored()
    {
        // width 8 × 4B = 32B dữ liệu thật, stride 40 → 8B padding cuối mỗi hàng.
        var d = new FrameChangeDetector();
        var f = MakeFrame(8, 4, 40, 7);
        d.HasChanged(f, 8, 4, 40);

        var g = (byte[])f.Clone();
        for (var y = 0; y < 4; y++) g[y * 40 + 35] = 200; // chỉ đổi vùng padding
        Assert.False(d.HasChanged(g, 8, 4, 40));

        g[1 * 40 + 3] = 200; // đổi dữ liệu thật
        Assert.True(d.HasChanged(g, 8, 4, 40));
    }

    [Fact]
    public void DimensionChange_IsChanged()
    {
        var d = new FrameChangeDetector();
        d.HasChanged(MakeFrame(8, 4, 32, 1), 8, 4, 32);
        Assert.True(d.HasChanged(MakeFrame(8, 5, 32, 1), 8, 5, 32));
        Assert.True(d.HasChanged(MakeFrame(4, 5, 16, 1), 4, 5, 16));
        Assert.False(d.HasChanged(MakeFrame(4, 5, 16, 1), 4, 5, 16));
    }

    [Fact]
    public void Reset_ForcesNextFrameChanged()
    {
        var d = new FrameChangeDetector();
        var f = MakeFrame(8, 4, 32, 1);
        d.HasChanged(f, 8, 4, 32);
        d.Reset();
        Assert.True(d.HasChanged(f, 8, 4, 32));
    }

    [Fact]
    public void InvalidInput_IsTreatedAsChanged()
    {
        var d = new FrameChangeDetector();
        Assert.True(d.HasChanged(new byte[10], 8, 4, 32));   // buffer quá ngắn
        Assert.True(d.HasChanged(new byte[128], 8, 4, 16));  // stride < rowBytes
        Assert.True(d.HasChanged(new byte[128], 0, 4, 32));  // width 0
    }
}

public class AdaptiveRateControllerTests
{
    [Fact]
    public void StartsAtMaximum()
    {
        var c = new AdaptiveRateController(maxFps: 20, maxQuality: 65, enabled: true);
        Assert.Equal(20, c.CurrentFps);
        Assert.Equal(65, c.CurrentQuality);
        Assert.Equal(50, c.FrameIntervalMs);
    }

    [Fact]
    public void FastWrites_NeverDegrade_EvenForManyFrames()
    {
        // Mô phỏng đúng tình huống máy .56: CPU là giới hạn, mạng nhanh (write ~1ms).
        var c = new AdaptiveRateController(20, 65, enabled: true);
        for (var i = 0; i < 10_000; i++) c.OnFrameWritten(1);
        Assert.Equal(20, c.CurrentFps);
        Assert.Equal(65, c.CurrentQuality);
    }

    [Fact]
    public void SlowWrites_LowerQualityFirst_ThenFps()
    {
        var c = new AdaptiveRateController(20, 65, enabled: true);
        var changes = 0;
        for (var i = 0; i < 3; i++) if (c.OnFrameWritten(500)) changes++;
        Assert.Equal(1, changes);
        Assert.Equal(55, c.CurrentQuality);
        Assert.Equal(20, c.CurrentFps);

        // Hạ tiếp tới sàn chất lượng (40) trước khi đụng tới FPS.
        for (var i = 0; i < 3 * 10; i++) c.OnFrameWritten(500);
        Assert.Equal(40, c.CurrentQuality);
        Assert.True(c.CurrentFps < 20);
        Assert.True(c.CurrentFps >= AdaptiveRateController.DefaultMinFps);
    }

    [Fact]
    public void NeverGoesBelowFloors()
    {
        var c = new AdaptiveRateController(20, 65, enabled: true);
        for (var i = 0; i < 1_000; i++) c.OnFrameWritten(10_000);
        Assert.Equal(AdaptiveRateController.DefaultMinQuality, c.CurrentQuality);
        Assert.Equal(AdaptiveRateController.DefaultMinFps, c.CurrentFps);
        for (var i = 0; i < 10; i++) Assert.False(c.OnFrameWritten(10_000)); // đã ở sàn → không còn gì để hạ
    }

    [Fact]
    public void Recovers_FpsFirst_ThenQuality_BackToMaximum()
    {
        var c = new AdaptiveRateController(20, 65, enabled: true);
        for (var i = 0; i < 1_000; i++) c.OnFrameWritten(10_000);

        // Một nấc phục hồi đầu tiên tăng FPS, chưa tăng chất lượng.
        for (var i = 0; i < AdaptiveRateController.RecoverStreak; i++) c.OnFrameWritten(1);
        Assert.True(c.CurrentFps > AdaptiveRateController.DefaultMinFps);
        Assert.Equal(AdaptiveRateController.DefaultMinQuality, c.CurrentQuality);

        // Mạng ổn định đủ lâu → về lại đúng mức tối đa.
        for (var i = 0; i < 100_000; i++) c.OnFrameWritten(1);
        Assert.Equal(20, c.CurrentFps);
        Assert.Equal(65, c.CurrentQuality);
    }

    [Fact]
    public void Disabled_NeverChanges()
    {
        var c = new AdaptiveRateController(20, 65, enabled: false);
        for (var i = 0; i < 1_000; i++) Assert.False(c.OnFrameWritten(10_000));
        Assert.Equal(20, c.CurrentFps);
        Assert.Equal(65, c.CurrentQuality);
    }

    [Fact]
    public void OccasionalSlowWrite_DoesNotDegrade()
    {
        var c = new AdaptiveRateController(20, 65, enabled: true);
        for (var i = 0; i < 1_000; i++) c.OnFrameWritten(i % 10 == 0 ? 500 : 1);
        Assert.Equal(20, c.CurrentFps);
        Assert.Equal(65, c.CurrentQuality);
    }

    [Fact]
    public void ConstructorClampsInvalidLimits()
    {
        var c = new AdaptiveRateController(maxFps: 0, maxQuality: 30, enabled: true);
        Assert.Equal(1, c.MaxFps);
        Assert.Equal(1, c.MinFps);
        Assert.Equal(30, c.MaxQuality);
        Assert.Equal(30, c.MinQuality); // sàn mặc định 40 > trần 30 → ép về trần
    }
}

public class StreamStatsTests
{
    [Fact]
    public void Snapshot_ComputesRatesAndTimings_ThenResets()
    {
        var s = new StreamStats(nowMs: 0);
        for (var i = 1; i <= 20; i++) s.RecordSent(captureMs: i, compareMs: 1, encodeMs: 70, writeMs: 2, jpegBytes: 100 * 1024);
        for (var i = 0; i < 10; i++) s.RecordSkipped(captureMs: 5, compareMs: 3);

        var snap = s.TakeSnapshot(nowMs: 2_000);
        Assert.Equal(2.0, snap.WindowSeconds, 3);
        Assert.Equal(10.0, snap.SentFps, 3);
        Assert.Equal(5.0, snap.SkippedFps, 3);
        Assert.Equal(70.0, snap.Encode.AvgMs, 3);
        Assert.Equal(100.0, snap.AvgJpegKb, 3);
        Assert.Equal(20.0, snap.Capture.MaxMs, 3);

        var empty = s.TakeSnapshot(nowMs: 3_000);
        Assert.Equal(0, empty.SentFps);
        Assert.Equal(0, empty.AvgJpegKb);
        Assert.Equal(default(StageTiming), empty.Encode);
    }

    [Fact]
    public void Summarize_P95_UsesNearestRank()
    {
        var samples = Enumerable.Range(1, 100).Select(i => (double)i).ToList();
        var t = StreamStats.Summarize(samples);
        Assert.Equal(50.5, t.AvgMs, 3);
        Assert.Equal(95.0, t.P95Ms, 3);
        Assert.Equal(100.0, t.MaxMs, 3);
    }
}
