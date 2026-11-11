using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace IPGS.RemoteControl.CcuUI.Controls;

/// <summary>
/// Icon nét (stroke) kiểu Lucide: vẽ <see cref="Data"/> trong khung 24×24, scale theo kích thước control
/// (mặc định 16×16). Màu lấy theo <see cref="Foreground"/> — kế thừa từ Button/TextBlock cha nên icon
/// tự đổi màu cùng chữ khi hover/disabled. Geometry nằm ở Resources/Icons.axaml (key "ic.&lt;tên&gt;").
/// </summary>
public class LucideIcon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<LucideIcon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LucideIcon>();

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<LucideIcon, double>(nameof(StrokeThickness), 2.0);

    static LucideIcon()
    {
        AffectsRender<LucideIcon>(DataProperty, ForegroundProperty, StrokeThicknessProperty);
        AffectsMeasure<LucideIcon>(DataProperty);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 16 : Math.Min(16, availableSize.Width);
        double h = double.IsInfinity(availableSize.Height) ? 16 : Math.Min(16, availableSize.Height);
        return new Size(w, h);
    }

    public override void Render(DrawingContext context)
    {
        var data = Data;
        var brush = Foreground;
        if (data is null || brush is null) return;

        double side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= 0) return;
        double scale = side / 24.0;
        double dx = (Bounds.Width - side) / 2, dy = (Bounds.Height - side) / 2;

        // StrokeThickness tính theo đơn vị khung 24 (như stroke-width của SVG Lucide) → scale cùng icon.
        var pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(dx, dy)))
        {
            context.DrawGeometry(null, pen, data);
        }
    }
}
