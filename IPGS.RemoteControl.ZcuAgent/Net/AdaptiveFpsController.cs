namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Bộ điều khiển FPS tự thích ứng theo tải thực tế của máy ZCU (Phần 1 - Adaptive FPS).
/// <para>
/// Đo 3 tín hiệu độc lập theo mức độ ưu tiên:
/// 1. X-Latency Probe (Độ trễ vòng X11 RTT ms): Phản ánh hàng đợi Xorg bận / UI render / nghẽn đồ họa.
/// 2. CPU Pressure (PSI some avg10 %): Phản ánh tình trạng CPU bị thiếu và tác vụ phải chờ nhân.
/// 3. Network Backpressure (Socket writeMs): Phản ánh mạng chậm hoặc CCU nhận không kịp.
/// </para>
/// <para>
/// Thuật toán: AIMD (Additive Increase / Multiplicative Decrease) + Vùng chết (Deadband):
/// - Sàn tối thiểu: MinFps = 15 (cố định theo lệnh người dùng, không bao giờ hạ dưới 15).
/// - Trần tối đa: MaxFps = TargetFps cấu hình (mặc định 30).
/// - Giảm nhanh: 2 mẫu xấu liên tiếp → FPS = max(MinFps, round(FPS * 0.8)).
/// - Tăng chậm: 10 mẫu tốt liên tiếp (~5 giây ổn định) → FPS = min(MaxFps, FPS + 2.0).
/// - Vùng chết: Nằm giữa ngưỡng xấu và ngưỡng tốt thì giữ nguyên tốc độ, tránh dao động giật cục.
/// - Giữ nguyên chất lượng JPEG (không hạ q khi CPU/X11 tải cao).
/// </para>
/// Thuần logic, không phụ thuộc phần cứng/OS → test được độc lập bằng unit test.
/// </summary>
public sealed class AdaptiveFpsController
{
    public const int DefaultMinFps = 15;
    public const int DefaultMaxFps = 60;

    // Ngưỡng X-Latency (ms)
    public const double XLatencyBadMs  = 5.0;
    public const double XLatencyGoodMs = 2.5;

    // Ngưỡng CPU Pressure PSI (% thời gian chờ CPU trong 10s gần nhất)
    public const double CpuPressureBadPct  = 15.0;
    public const double CpuPressureGoodPct = 5.0;

    // Ngưỡng Network Write (ms)
    public const double WriteBadMs  = 120.0;
    public const double WriteGoodMs = 30.0;

    public const int DegradeStreak = 2;   // ~1.0 giây xấu liên tiếp
    public const int RecoverStreak = 10;  // ~5.0 giây tốt liên tiếp
    public const double DownFactor = 0.8;
    public const double UpStep     = 2.0;

    private readonly bool _enabled;
    private int _badStreak;
    private int _goodStreak;

    public AdaptiveFpsController(int maxFps = DefaultMaxFps, int minFps = DefaultMinFps, bool enabled = true)
    {
        MaxFps = Math.Clamp(maxFps, 1, 60);
        MinFps = Math.Clamp(minFps, 1, MaxFps);
        _enabled = enabled;

        CurrentFps = MaxFps;
        LastReason = "init";
    }

    public int MaxFps { get; }
    public int MinFps { get; }
    public bool Enabled => _enabled;

    /// <summary>Trần FPS hiện tại (vòng lặp capture không gửi nhanh hơn mức này).</summary>
    public double CurrentFps { get; private set; }

    /// <summary>Khoảng cách tối thiểu giữa 2 lần chụp (ms) ứng với <see cref="CurrentFps"/>.</summary>
    public int FrameIntervalMs => (int)Math.Max(1, Math.Round(1000.0 / CurrentFps));

    /// <summary>Lý do điều chỉnh FPS gần nhất.</summary>
    public string LastReason { get; private set; }

    /// <summary>
    /// Ghi nhận 1 mẫu đo lường tải máy và socket.
    /// Trả về true nếu trần FPS vừa thay đổi để caller ghi log.
    /// </summary>
    /// <param name="xLatencyMs">Độ trễ X11 RTT (ms) từ X-probe. Bỏ qua nếu &lt; 0.</param>
    /// <param name="cpuPressureAvg10">CPU PSI avg10 (%). Bỏ qua nếu &lt; 0.</param>
    /// <param name="writeMs">Thời gian ghi frame socket (ms). Bỏ qua nếu &lt; 0.</param>
    public bool OnSample(double xLatencyMs, double cpuPressureAvg10, double writeMs)
    {
        if (!_enabled) return false;

        var isBad = false;
        string badReason = string.Empty;

        // Ưu tiên 1: X-Latency
        if (xLatencyMs >= XLatencyBadMs)
        {
            isBad = true;
            badReason = $"x-latency {xLatencyMs:F1}ms >= {XLatencyBadMs}ms";
        }
        // Ưu tiên 2: CPU Pressure
        else if (cpuPressureAvg10 >= CpuPressureBadPct)
        {
            isBad = true;
            badReason = $"cpu-psi {cpuPressureAvg10:F1}% >= {CpuPressureBadPct}%";
        }
        // Ưu tiên 3: Socket Write
        else if (writeMs >= WriteBadMs)
        {
            isBad = true;
            badReason = $"net-write {writeMs:F1}ms >= {WriteBadMs}ms";
        }

        if (isBad)
        {
            _badStreak++;
            _goodStreak = 0;

            if (_badStreak >= DegradeStreak)
            {
                _badStreak = 0;
                return Degrade(badReason);
            }
            return false;
        }

        // Kiểm tra xem tất cả các tín hiệu có nằm trong vùng Good không
        var xGood = xLatencyMs < 0 || xLatencyMs <= XLatencyGoodMs;
        var psiGood = cpuPressureAvg10 < 0 || cpuPressureAvg10 <= CpuPressureGoodPct;
        var writeGood = writeMs < 0 || writeMs <= WriteGoodMs;

        if (xGood && psiGood && writeGood)
        {
            _goodStreak++;
            _badStreak = 0;

            if (_goodStreak >= RecoverStreak)
            {
                _goodStreak = 0;
                return Recover();
            }
        }
        else
        {
            // Nằm trong Deadband (giữa Good và Bad): giữ nguyên, không tính vào chuỗi phục hồi
            _goodStreak = 0;
        }

        return false;
    }

    private bool Degrade(string reason)
    {
        if (CurrentFps > MinFps)
        {
            var next = Math.Max((double)MinFps, Math.Round(CurrentFps * DownFactor, 1));
            if (next < CurrentFps)
            {
                CurrentFps = next;
                LastReason = reason;
                return true;
            }
        }
        return false;
    }

    private bool Recover()
    {
        if (CurrentFps < MaxFps)
        {
            var next = Math.Min((double)MaxFps, Math.Round(CurrentFps + UpStep, 1));
            if (next > CurrentFps)
            {
                CurrentFps = next;
                LastReason = "recovered (machine load normalized)";
                return true;
            }
        }
        return false;
    }
}
