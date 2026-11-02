using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK branded TextBlock — typed typography styles (H1–Caption).
/// </summary>
public class KzLabel : TextBlock
{
    public static readonly StyledProperty<KzLabelType> LabelTypeProperty =
        AvaloniaProperty.Register<KzLabel, KzLabelType>(nameof(LabelType), KzLabelType.Body);

    public KzLabelType LabelType
    {
        get => GetValue(LabelTypeProperty);
        set
        {
            SetValue(LabelTypeProperty, value);
            ApplyStyle();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        // Màu theo LabelType chỉ là MẶC ĐỊNH — tính ra trước, cuối hàm mới ghi, và ghi ở
        // mức ưu tiên Style để giá trị đặt trong XAML (ưu tiên LocalValue) luôn thắng.
        //
        // Trước đây hàm này gán thẳng `Foreground = ...` (ưu tiên LocalValue) ngay đầu, mà
        // nó chạy lại mỗi lần control gắn vào visual tree, nên xoá luôn màu đặt trong XAML:
        // tiêu đề chữ trắng trên nền navy bị trả về màu chữ tối, gần như không đọc được
        // (phát hiện trên iPGSUseCam 2026-07-27).
        //
        // Không dùng IsSet(ForegroundProperty) để dò: với {DynamicResource ...} thì lúc
        // OnAttachedToVisualTree chạy, resource có thể chưa phân giải xong nên IsSet vẫn
        // trả về false — đã thử và không ăn.
        IBrush? typeForeground = KzTokens.TextBrush;

        switch (LabelType)
        {
            case KzLabelType.H1:
                FontSize   = KzTokens.FontSize3xl + 6; // 36
                FontWeight = FontWeight.Bold;
                break;

            case KzLabelType.H2:
                FontSize   = KzTokens.FontSize2xl + 4; // 28
                FontWeight = FontWeight.Bold;
                break;

            case KzLabelType.H3:
                FontSize   = KzTokens.FontSizeXl;       // 20
                FontWeight = FontWeight.Bold;
                break;

            case KzLabelType.Subheading:
                FontSize       = KzTokens.FontSizeLg;   // 16
                FontWeight     = FontWeight.SemiBold;
                typeForeground = KzTokens.Navy700Brush;
                break;

            case KzLabelType.Small:
                FontSize   = KzTokens.FontSizeSm;       // 12
                FontWeight = FontWeight.Normal;
                break;

            case KzLabelType.Caption:
                FontSize       = KzTokens.FontSizeSm - 1;   // 11
                FontWeight     = FontWeight.Normal;
                typeForeground = KzTokens.TextMutedBrush;
                break;

            case KzLabelType.Muted:
                FontSize       = KzTokens.FontSizeMd;   // 14
                FontWeight     = FontWeight.Normal;
                typeForeground = KzTokens.TextMutedBrush;
                break;

            default: // Body
                FontSize   = KzTokens.FontSizeMd;       // 14
                FontWeight = FontWeight.Normal;
                break;
        }

        SetValue(ForegroundProperty, typeForeground, BindingPriority.Style);
    }
}
