namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Thống kê thời gian từng giai đoạn của capture loop trong một cửa sổ thời gian (Bước 0 — đo
/// đạc FPS ZCU): capture (XShmGetImage), so sánh khung (bỏ qua frame tĩnh), nén JPEG, ghi socket.
/// Caller lấy <see cref="TakeSnapshot"/> định kỳ để ghi log rồi cửa sổ mới bắt đầu.
/// Không thread-safe: chỉ gọi từ capture loop. Không phụ thuộc logger → test được độc lập.
/// </summary>
internal sealed class StreamStats
{
    private readonly List<double> _capture = new(128);
    private readonly List<double> _compare = new(128);
    private readonly List<double> _encode  = new(128);
    private readonly List<double> _write   = new(128);
    private long _bytes;
    private int _sent;
    private int _skipped;
    private long _windowStartMs;

    public StreamStats(long nowMs) => _windowStartMs = nowMs;

    /// <summary>Một frame đã được nén và gửi đi.</summary>
    public void RecordSent(double captureMs, double compareMs, double encodeMs, double writeMs, int jpegBytes)
    {
        _capture.Add(captureMs);
        _compare.Add(compareMs);
        _encode.Add(encodeMs);
        _write.Add(writeMs);
        _bytes += jpegBytes;
        _sent++;
    }

    /// <summary>Một frame giống hệt khung trước nên bị bỏ qua (không nén, không gửi).</summary>
    public void RecordSkipped(double captureMs, double compareMs)
    {
        _capture.Add(captureMs);
        _compare.Add(compareMs);
        _skipped++;
    }

    /// <summary>Lấy số liệu của cửa sổ hiện tại rồi bắt đầu cửa sổ mới.</summary>
    public StreamStatsSnapshot TakeSnapshot(long nowMs)
    {
        var seconds = Math.Max(0.001, (nowMs - _windowStartMs) / 1000.0);
        var snap = new StreamStatsSnapshot(
            WindowSeconds: seconds,
            SentFps:       _sent / seconds,
            SkippedFps:    _skipped / seconds,
            Capture:       Summarize(_capture),
            Compare:       Summarize(_compare),
            Encode:        Summarize(_encode),
            Write:         Summarize(_write),
            AvgJpegKb:     _sent > 0 ? _bytes / 1024.0 / _sent : 0,
            BitrateKbps:   _bytes * 8.0 / 1000.0 / seconds);

        _capture.Clear();
        _compare.Clear();
        _encode.Clear();
        _write.Clear();
        _bytes = 0;
        _sent = 0;
        _skipped = 0;
        _windowStartMs = nowMs;
        return snap;
    }

    internal static StageTiming Summarize(List<double> samples)
    {
        if (samples.Count == 0) return default;

        var sorted = samples.ToArray();
        Array.Sort(sorted);
        double sum = 0;
        foreach (var s in sorted) sum += s;

        // p95 theo phương pháp nearest-rank.
        var rank = (int)Math.Ceiling(0.95 * sorted.Length) - 1;
        return new StageTiming(sum / sorted.Length, sorted[Math.Clamp(rank, 0, sorted.Length - 1)], sorted[^1]);
    }
}

internal readonly record struct StageTiming(double AvgMs, double P95Ms, double MaxMs);

internal readonly record struct StreamStatsSnapshot(
    double WindowSeconds,
    double SentFps,
    double SkippedFps,
    StageTiming Capture,
    StageTiming Compare,
    StageTiming Encode,
    StageTiming Write,
    double AvgJpegKb,
    double BitrateKbps);
