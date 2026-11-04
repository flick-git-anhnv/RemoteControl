namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Điều tiết FPS / chất lượng JPEG CHỈ theo tình trạng MẠNG (Nấc 1 — FPS ZCU).
/// <para>
/// Bản cũ (STEP-5.2) coi "chu kỳ CPU dài" là nghẽn rồi hạ cả FPS lẫn chất lượng. Trên
/// Celeron J3160 chu kỳ nén ~76ms luôn dài hơn khung 50ms của 20fps → thuật toán tự kéo
/// xuống sàn 5fps / q=40 và không bao giờ phục hồi được, trong khi CPU còn rảnh ~80%
/// (đo thật trên 192.168.21.56 ngày 2026-11-04). Hạ chất lượng cũng gần như không làm
/// nén nhanh hơn — chỉ làm hình mờ đi.
/// </para>
/// <para>
/// Bản này: khi CPU là giới hạn, capture loop tự chạy liền mạch ở tốc độ tối đa CPU cho
/// phép (sleep ≤ 0) — KHÔNG cần và KHÔNG được hạ chất lượng. Chỉ khi ghi socket chậm
/// (TCP backpressure = mạng nghẽn hoặc CCU đọc không kịp) mới giảm: hạ chất lượng trước
/// (giảm số byte/frame), chạm sàn chất lượng rồi mới hạ FPS. Phục hồi theo chiều ngược lại:
/// FPS trước, chất lượng sau.
/// </para>
/// Không thread-safe: chỉ gọi từ capture loop. Không phụ thuộc logger → test được độc lập.
/// </summary>
internal sealed class AdaptiveRateController
{
    public const int DefaultMinFps       = 5;
    public const int DefaultMinQuality   = 40;
    public const int DefaultSlowWriteMs  = 120;

    /// <summary>Số frame ghi chậm liên tiếp để bắt đầu giảm.</summary>
    public const int DegradeStreak = 3;
    /// <summary>Số frame ghi nhanh liên tiếp để tăng lại 1 nấc.</summary>
    public const int RecoverStreak = 30;

    public const int QualityStepDown = 10;
    public const int QualityStepUp   = 5;
    public const double FpsStepUp    = 2.0;
    public const double FpsDownFactor = 0.75;

    private readonly bool _enabled;
    private int _slowStreak;
    private int _fastStreak;

    public AdaptiveRateController(
        int maxFps, int maxQuality, bool enabled,
        int minFps = DefaultMinFps, int minQuality = DefaultMinQuality, int slowWriteMs = DefaultSlowWriteMs)
    {
        MaxFps      = Math.Clamp(maxFps, 1, 60);
        MaxQuality  = Math.Clamp(maxQuality, 1, 100);
        MinFps      = Math.Clamp(minFps, 1, MaxFps);
        MinQuality  = Math.Clamp(minQuality, 1, MaxQuality);
        SlowWriteMs = Math.Max(1, slowWriteMs);
        _enabled    = enabled;

        CurrentFps     = MaxFps;
        CurrentQuality = MaxQuality;
    }

    public int MaxFps { get; }
    public int MaxQuality { get; }
    public int MinFps { get; }
    public int MinQuality { get; }
    public int SlowWriteMs { get; }

    /// <summary>Trần FPS hiện tại (capture loop không chạy nhanh hơn mức này).</summary>
    public double CurrentFps { get; private set; }

    /// <summary>Chất lượng JPEG hiện tại.</summary>
    public int CurrentQuality { get; private set; }

    /// <summary>Khoảng cách tối thiểu giữa 2 lần chụp (ms) ứng với <see cref="CurrentFps"/>.</summary>
    public int FrameIntervalMs => (int)Math.Round(1000.0 / CurrentFps);

    /// <summary>
    /// Ghi nhận thời gian ghi 1 frame lên socket. Trả về true nếu FPS/chất lượng vừa thay đổi
    /// (để caller ghi log).
    /// </summary>
    public bool OnFrameWritten(double writeMs)
    {
        if (!_enabled) return false;

        if (writeMs > SlowWriteMs)
        {
            _slowStreak++;
            _fastStreak = 0;
        }
        else if (writeMs < SlowWriteMs / 4.0)
        {
            _fastStreak++;
            _slowStreak = 0;
        }
        else
        {
            // Vùng trung gian: không đủ căn cứ tăng hay giảm — giữ nguyên, không reset chuỗi chậm.
            _fastStreak = 0;
        }

        if (_slowStreak >= DegradeStreak)
        {
            _slowStreak = 0;
            return Degrade();
        }

        if (_fastStreak >= RecoverStreak)
        {
            _fastStreak = 0;
            return Recover();
        }

        return false;
    }

    private bool Degrade()
    {
        if (CurrentQuality > MinQuality)
        {
            CurrentQuality = Math.Max(MinQuality, CurrentQuality - QualityStepDown);
            return true;
        }

        if (CurrentFps > MinFps)
        {
            CurrentFps = Math.Max(MinFps, Math.Round(CurrentFps * FpsDownFactor, 1));
            return true;
        }

        return false;
    }

    private bool Recover()
    {
        if (CurrentFps < MaxFps)
        {
            CurrentFps = Math.Min(MaxFps, CurrentFps + FpsStepUp);
            return true;
        }

        if (CurrentQuality < MaxQuality)
        {
            CurrentQuality = Math.Min(MaxQuality, CurrentQuality + QualityStepUp);
            return true;
        }

        return false;
    }
}
