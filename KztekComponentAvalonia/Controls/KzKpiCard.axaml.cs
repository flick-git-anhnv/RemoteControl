using Avalonia;
using Avalonia.Controls;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

public partial class KzKpiCard : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<KzKpiCard, string?>(nameof(Label), "Chỉ số");

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<KzKpiCard, string?>(nameof(Value), "0");

    public static readonly StyledProperty<double> DeltaProperty =
        AvaloniaProperty.Register<KzKpiCard, double>(nameof(Delta), 0.0);

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<KzKpiCard, string?>(nameof(Subtitle), "so với kỳ trước");

    /// <summary>Hiện hàng delta ("+x% so với kỳ trước"). Tắt cho KPI trạng thái tức thời
    /// (VD: thiết bị online) — không có "kỳ trước" để so sánh. Mặc định true (giữ hành vi cũ).</summary>
    public static readonly StyledProperty<bool> ShowDeltaProperty =
        AvaloniaProperty.Register<KzKpiCard, bool>(nameof(ShowDelta), true);

    public string? Label
    {
        get => GetValue(LabelProperty);
        set { SetValue(LabelProperty, value); ApplyLabel(value); }
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set { SetValue(ValueProperty, value); ApplyValue(value); }
    }

    public double Delta
    {
        get => GetValue(DeltaProperty);
        set { SetValue(DeltaProperty, value); ApplyDelta(value); }
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set { SetValue(SubtitleProperty, value); ApplySubtitle(value); }
    }

    public bool ShowDelta
    {
        get => GetValue(ShowDeltaProperty);
        set { SetValue(ShowDeltaProperty, value); ApplyShowDelta(value); }
    }

    public KzKpiCard()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyLabel(Label);
        ApplyValue(Value);
        ApplyDelta(Delta);
        ApplySubtitle(Subtitle);
        ApplyShowDelta(ShowDelta);
    }

    private void ApplyLabel(string? text)
    {
        if (this.FindControl<TextBlock>("KpiLabel") is { } tb)
            tb.Text = text ?? string.Empty;
    }

    private void ApplyValue(string? text)
    {
        if (this.FindControl<TextBlock>("KpiValue") is { } tb)
            tb.Text = text ?? "0";
    }

    private void ApplyDelta(double delta)
    {
        if (this.FindControl<KzBadge>("DeltaBadge") is not { } badge) return;

        var sign = delta >= 0 ? "+" : "";
        badge.Content = $"{sign}{delta:F1}%";
        badge.Variant = delta switch
        {
            > 0  => KzBadgeVariant.Success,
            < 0  => KzBadgeVariant.Error,
            _    => KzBadgeVariant.Neutral
        };
    }

    private void ApplySubtitle(string? text)
    {
        if (this.FindControl<TextBlock>("KpiSubtitle") is { } tb)
        {
            tb.Text = text ?? string.Empty;
            tb.IsVisible = !string.IsNullOrEmpty(text);
        }
        UpdateBottomRowVisibility();
    }

    private void ApplyShowDelta(bool show)
    {
        if (this.FindControl<KzBadge>("DeltaBadge") is { } badge)
            badge.IsVisible = show;
        UpdateBottomRowVisibility();
    }

    /// <summary>Tách "hiện delta badge" khỏi "hiện subtitle" (trước đây gộp chung 1 cờ ShowDelta
    /// làm ẩn LUÔN subtitle — không dùng được cho KPI trạng thái tức thời cần phụ đề nhưng không có
    /// delta % kỳ trước, VD Dashboard). DeltaRow (hàng chứa cả 2) chỉ ẩn khi CẢ HAI đều tắt.</summary>
    private void UpdateBottomRowVisibility()
    {
        if (this.FindControl<StackPanel>("DeltaRow") is { } row)
            row.IsVisible = ShowDelta || !string.IsNullOrEmpty(Subtitle);
    }
}
