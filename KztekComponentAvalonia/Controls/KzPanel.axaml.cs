using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

public partial class KzPanel : UserControl
{
    private static readonly BoxShadows ShadowHover  = BoxShadows.Parse("0 8 24 0 #1E251C53");
    private static readonly BoxShadows ShadowNormal = BoxShadows.Parse("0 1 2 0 #0D251C53");

    public static readonly StyledProperty<object?> PanelContentProperty =
        AvaloniaProperty.Register<KzPanel, object?>(nameof(PanelContent));

    public static readonly StyledProperty<bool> IsHoverableProperty =
        AvaloniaProperty.Register<KzPanel, bool>(nameof(IsHoverable), true);

    public object? PanelContent
    {
        get => GetValue(PanelContentProperty);
        set => SetValue(PanelContentProperty, value);
    }

    public bool IsHoverable
    {
        get => GetValue(IsHoverableProperty);
        set => SetValue(IsHoverableProperty, value);
    }

    public KzPanel()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        PointerEntered += OnPointerEntered;
        PointerExited  += OnPointerExited;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PointerEntered -= OnPointerEntered;
        PointerExited  -= OnPointerExited;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!IsHoverable) return;
        if (this.FindControl<Border>("PanelRoot") is not { } root) return;
        root.BoxShadow   = ShadowHover;
        root.BorderBrush = KzTokens.Orange500Brush;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!IsHoverable) return;
        if (this.FindControl<Border>("PanelRoot") is not { } root) return;
        root.BoxShadow   = ShadowNormal;
        root.BorderBrush = KzTokens.BorderBrush;
    }
}
