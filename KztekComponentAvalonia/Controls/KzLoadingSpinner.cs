using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK loading spinner — vòng cung xoay liên tục.
/// LƯU Ý (gotcha G007 iAccess): style Animation KeyFrame trên RotateTransform.Angle
/// KHÔNG chạy (silent) trên Avalonia 12.1 → control này xoay bằng DispatcherTimer
/// code-behind + vẽ trực tiếp trong Render (cross-platform, không System.Drawing).
/// Dùng: &lt;kz:KzLoadingSpinner Size="32" /&gt; — tự chạy khi attach, tự dừng khi detach.
/// </summary>
public class KzLoadingSpinner : Control
{
    /// <summary>Đường kính spinner (px). Mặc định 24.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<KzLoadingSpinner, double>(nameof(Size), 24.0);

    /// <summary>Màu cung xoay. Mặc định cam accent KZTEK.</summary>
    public static readonly StyledProperty<IBrush?> SpinnerBrushProperty =
        AvaloniaProperty.Register<KzLoadingSpinner, IBrush?>(nameof(SpinnerBrush));

    /// <summary>Độ dày nét. Mặc định 3.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<KzLoadingSpinner, double>(nameof(StrokeThickness), 3.0);

    /// <summary>Bật/tắt xoay. Mặc định true.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<KzLoadingSpinner, bool>(nameof(IsActive), true);

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public IBrush? SpinnerBrush
    {
        get => GetValue(SpinnerBrushProperty);
        set => SetValue(SpinnerBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private DispatcherTimer? _timer;
    private double _angle;

    static KzLoadingSpinner()
    {
        AffectsMeasure<KzLoadingSpinner>(SizeProperty);
        AffectsRender<KzLoadingSpinner>(SpinnerBrushProperty, StrokeThicknessProperty);
    }

    protected override Avalonia.Size MeasureOverride(Avalonia.Size availableSize)
        => new(Size, Size);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartOrStop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimer();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
            StartOrStop();
    }

    private void StartOrStop()
    {
        if (IsActive && VisualRoot is not null)
        {
            if (_timer is null)
            {
                // ~60fps; 4.5°/tick ≈ 270°/s — tốc độ spinner chuẩn material.
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _timer.Tick += (_, _) =>
                {
                    _angle = (_angle + 4.5) % 360.0;
                    InvalidateVisual();
                };
            }
            _timer.Start();
        }
        else
        {
            StopTimer();
        }
    }

    private void StopTimer() => _timer?.Stop();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Size;
        var thickness = StrokeThickness;
        var brush = SpinnerBrush ?? KzTokens.Orange500Brush;
        var radius = (size - thickness) / 2.0;
        if (radius <= 0) return;

        var center = new Point(Bounds.Width / 2.0, Bounds.Height / 2.0);

        // Track mờ (vòng nền)
        var trackPen = new Pen(new SolidColorBrush(KzTokens.Border), thickness);
        context.DrawEllipse(null, trackPen, center, radius, radius);

        // Cung 90° xoay theo _angle
        var pen = new Pen(brush, thickness) { LineCap = PenLineCap.Round };
        var startRad = _angle * Math.PI / 180.0;
        var sweepRad = Math.PI / 2.0; // 90°
        var p1 = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
        var p2 = new Point(center.X + radius * Math.Cos(startRad + sweepRad), center.Y + radius * Math.Sin(startRad + sweepRad));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p1, false);
            ctx.ArcTo(p2, new Avalonia.Size(radius, radius), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geo);
    }
}
