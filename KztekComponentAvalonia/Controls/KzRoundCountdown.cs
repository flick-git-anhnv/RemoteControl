using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK circular countdown ring — Avalonia port of WinForms KzRoundCountdown.
///
/// Vẽ vòng tròn viền (ring) giảm dần theo Remaining/Duration, số ở giữa, sub-label bên dưới.
/// Control KHÔNG tự chạy timer — nhận Remaining qua binding từ ViewModel (hoặc set từ code-behind).
/// Parity: WinForms KzRoundCountdown.OnPaint (CCW arc từ đỉnh, tương ứng DrawArc startAngle=-90 negSweep).
/// </summary>
public class KzRoundCountdown : Control
{
    // ── Styled Properties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<int> DurationProperty =
        AvaloniaProperty.Register<KzRoundCountdown, int>(nameof(Duration), 60);

    public static readonly StyledProperty<int> RemainingProperty =
        AvaloniaProperty.Register<KzRoundCountdown, int>(nameof(Remaining), 60);

    public static readonly StyledProperty<bool> ShowNumberProperty =
        AvaloniaProperty.Register<KzRoundCountdown, bool>(nameof(ShowNumber), true);

    public static readonly StyledProperty<string> SubLabelProperty =
        AvaloniaProperty.Register<KzRoundCountdown, string>(nameof(SubLabel), string.Empty);

    public static readonly StyledProperty<double> TrackWidthProperty =
        AvaloniaProperty.Register<KzRoundCountdown, double>(nameof(TrackWidth), 8.0);

    public static readonly StyledProperty<KzCountDownVariant> VariantProperty =
        AvaloniaProperty.Register<KzRoundCountdown, KzCountDownVariant>(nameof(Variant), KzCountDownVariant.Default);

    // ── CLR wrappers ──────────────────────────────────────────────────────────

    /// <summary>Total countdown duration in seconds (must be ≥ 1).</summary>
    public int Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, Math.Max(1, value));
    }

    /// <summary>Remaining seconds. Bind to ViewModel property and update every tick.</summary>
    public int Remaining
    {
        get => GetValue(RemainingProperty);
        set => SetValue(RemainingProperty, value);
    }

    /// <summary>Show remaining number in the center of the ring.</summary>
    public bool ShowNumber
    {
        get => GetValue(ShowNumberProperty);
        set => SetValue(ShowNumberProperty, value);
    }

    /// <summary>Sub-label below the number ("giây" / "s" / empty to hide).</summary>
    public string SubLabel
    {
        get => GetValue(SubLabelProperty);
        set => SetValue(SubLabelProperty, value ?? string.Empty);
    }

    /// <summary>Stroke width of the circular ring track in pixels.</summary>
    public double TrackWidth
    {
        get => GetValue(TrackWidthProperty);
        set => SetValue(TrackWidthProperty, Math.Max(2.0, value));
    }

    /// <summary>Color variant of the ring (Default = Navy, Accent = Orange, …).</summary>
    public KzCountDownVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    // ── Invalidate on property change ─────────────────────────────────────────

    static KzRoundCountdown()
    {
        AffectsRender<KzRoundCountdown>(
            DurationProperty,
            RemainingProperty,
            ShowNumberProperty,
            SubLabelProperty,
            TrackWidthProperty,
            VariantProperty);
    }

    // ── Colors by variant ─────────────────────────────────────────────────────

    private void GetVariantBrushes(
        out IBrush trackBrush,
        out IBrush progressBrush,
        out IBrush numberBrush,
        out IBrush labelBrush)
    {
        switch (Variant)
        {
            case KzCountDownVariant.Accent:
                trackBrush    = KzTokens.Orange300Brush;
                progressBrush = KzTokens.Orange500Brush;
                numberBrush   = KzTokens.Orange500Brush;
                labelBrush    = KzTokens.Orange600Brush;
                break;

            case KzCountDownVariant.Success:
                trackBrush    = new SolidColorBrush(Color.Parse("#DCFCE7"));
                progressBrush = KzTokens.SuccessBrush;
                numberBrush   = KzTokens.SuccessBrush;
                labelBrush    = KzTokens.SuccessBrush;
                break;

            case KzCountDownVariant.Danger:
                trackBrush    = new SolidColorBrush(Color.Parse("#FEE2E2"));
                progressBrush = KzTokens.ErrorBrush;
                numberBrush   = KzTokens.ErrorBrush;
                labelBrush    = KzTokens.ErrorBrush;
                break;

            case KzCountDownVariant.Light:
                trackBrush    = KzTokens.BorderBrush;
                progressBrush = KzTokens.Navy300Brush;
                numberBrush   = KzTokens.Navy900Brush;
                labelBrush    = KzTokens.TextMutedBrush;
                break;

            case KzCountDownVariant.OnDark:
                trackBrush    = new SolidColorBrush(Colors.White, 0.35);
                progressBrush = Brushes.White;
                numberBrush   = Brushes.White;
                labelBrush    = Brushes.White;
                break;

            default: // KzCountDownVariant.Default — Navy
                trackBrush    = KzTokens.Navy100Brush;
                progressBrush = KzTokens.Navy900Brush;
                numberBrush   = KzTokens.Navy900Brush;
                labelBrush    = KzTokens.Navy700Brush;
                break;
        }
    }

    // ── Render ────────────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w < 8 || h < 8) return;

        double size  = Math.Min(w, h);
        double track = Math.Max(2.0, Math.Min(TrackWidth, size / 2.0 - 4.0));
        double pad   = track / 2.0 + 1.0;
        double r     = (size - pad * 2.0) / 2.0;
        if (r <= 0) return;

        double cx = w / 2.0;
        double cy = h / 2.0;

        GetVariantBrushes(out var trackBrush, out var progressBrush,
                          out var numberBrush, out var labelBrush);

        var trackPen    = new Pen(trackBrush,    track, null, PenLineCap.Round);
        var progressPen = new Pen(progressBrush, track, null, PenLineCap.Round);

        // ── Track ring (full background circle) ───────────────────────────────
        ctx.DrawEllipse(null, trackPen, new Point(cx, cy), r, r);

        // ── Progress arc ──────────────────────────────────────────────────────
        // Parity WinForms: DrawArc(pen, rect, startAngle=-90, sweepAngle=-sweep)
        // → CCW from top (12 o'clock), depletes counter-clockwise.
        int    dur      = Math.Max(1, Duration);
        int    rem      = Math.Max(0, Math.Min(Remaining, dur));
        double progress = (double)rem / dur;
        double sweepDeg = 360.0 * progress;

        if (sweepDeg > 0.5)
        {
            if (sweepDeg >= 359.5)
            {
                // Full or near-full circle — avoid degenerate arc (start == end point)
                ctx.DrawEllipse(null, progressPen, new Point(cx, cy), r, r);
            }
            else
            {
                DrawArcCCW(ctx, progressPen, cx, cy, r, sweepDeg);
            }
        }

        // ── Center text ───────────────────────────────────────────────────────
        if (!ShowNumber) return;

        double innerD  = size - track * 2.0 - 8.0;
        double numSize = Math.Max(10.0, innerD * 0.35);
        // Không gate theo "size >= 60" nữa — với 1 control chia sẻ cho MỌI màn hình, bất kỳ
        // sai lệch layout nào (DPI, scale-to-fit màn kiosk, TrackWidth khác nhau...) khiến Bounds
        // thực tế < ngưỡng cũ đều âm thầm xoá hẳn label "giây" mà không có cách nào phát hiện từ
        // XAML. Chỉ cần SubLabel khác rỗng là hiển thị — đủ nhỏ (font 12) để luôn vừa trong control.
        bool hasLabel = !string.IsNullOrEmpty(SubLabel);

        var numFt = new FormattedText(
            rem.ToString(),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
            numSize,
            numberBrush);

        FormattedText? lblFt = null;
        if (hasLabel)
        {
            lblFt = new FormattedText(
                SubLabel,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default),
                12.0,
                labelBrush);
        }

        // Dùng chiều cao ĐÃ ĐO THẬT (numFt.Height/lblFt.Height) thay vì hệ số ước lượng
        // (numSize*1.3 / 12*1.4 trước đây) — tránh label bị đẩy chồng lên số hoặc tràn ra
        // ngoài do metric font thực tế lệch so với hệ số gần đúng.
        double numH   = numFt.Height;
        double lblH   = lblFt?.Height ?? 0.0;
        double totalH = numH + lblH;
        double startY = (h - totalH) / 2.0;

        ctx.DrawText(numFt, new Point((w - numFt.Width) / 2.0, startY));

        if (lblFt != null)
        {
            ctx.DrawText(lblFt, new Point((w - lblFt.Width) / 2.0, startY + numH));
        }
    }

    // ── Arc helper ────────────────────────────────────────────────────────────

    /// <summary>
    /// Draw a counter-clockwise arc from 12 o'clock, sweeping <paramref name="sweepDeg"/> degrees.
    /// Parity: WinForms DrawArc(pen, rect, startAngle=-90, sweepAngle=-sweepDeg).
    /// </summary>
    private static void DrawArcCCW(DrawingContext ctx, IPen pen,
                                   double cx, double cy, double r, double sweepDeg)
    {
        // Start: top of circle (-π/2 in standard coords, Y-down screen)
        double startRad = -Math.PI / 2.0;
        // CCW sweep: end angle decreases (WinForms DrawArc negative sweep)
        double endRad   = startRad - sweepDeg * Math.PI / 180.0;

        var startPt = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        var endPt   = new Point(cx + r * Math.Cos(endRad),   cy + r * Math.Sin(endRad));

        var figure = new PathFigure
        {
            StartPoint = startPt,
            IsClosed   = false,
            Segments   = new PathSegments
            {
                new ArcSegment
                {
                    Point          = endPt,
                    Size           = new Size(r, r),
                    IsLargeArc     = sweepDeg > 180.0,
                    SweepDirection = SweepDirection.CounterClockwise
                }
            }
        };

        var geo = new PathGeometry();
        geo.Figures.Add(figure);

        ctx.DrawGeometry(null, pen, geo);
    }
}
