using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

public partial class KzCard : UserControl
{
    // ── Styled Properties ───────────────────────────────────────────────────
    public static readonly StyledProperty<string?> CardTitleProperty =
        AvaloniaProperty.Register<KzCard, string?>(nameof(CardTitle));

    public static readonly StyledProperty<string?> CardTextProperty =
        AvaloniaProperty.Register<KzCard, string?>(nameof(CardText));

    public static readonly StyledProperty<string?> CardIconProperty =
        AvaloniaProperty.Register<KzCard, string?>(nameof(CardIcon));

    public static readonly StyledProperty<KzCardVariant> VariantProperty =
        AvaloniaProperty.Register<KzCard, KzCardVariant>(nameof(Variant), KzCardVariant.Default);

    public static readonly StyledProperty<object?> CardContentProperty =
        AvaloniaProperty.Register<KzCard, object?>(nameof(CardContent));

    // ── Public Properties ───────────────────────────────────────────────────
    public string? CardTitle
    {
        get => GetValue(CardTitleProperty);
        set { SetValue(CardTitleProperty, value); ApplyTitle(value); }
    }

    public string? CardText
    {
        get => GetValue(CardTextProperty);
        set { SetValue(CardTextProperty, value); ApplyText(value); }
    }

    public string? CardIcon
    {
        get => GetValue(CardIconProperty);
        set { SetValue(CardIconProperty, value); ApplyIcon(value); }
    }

    public KzCardVariant Variant
    {
        get => GetValue(VariantProperty);
        set { SetValue(VariantProperty, value); ApplyVariant(value); }
    }

    public object? CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public KzCard()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<bool> IsHoverableProperty =
        AvaloniaProperty.Register<KzCard, bool>(nameof(IsHoverable), true);

    public bool IsHoverable
    {
        get => GetValue(IsHoverableProperty);
        set => SetValue(IsHoverableProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyVariant(Variant);
        ApplyTitle(CardTitle);
        ApplyText(CardText);
        ApplyIcon(CardIcon);

        PointerEntered += OnPointerEntered;
        PointerExited  += OnPointerExited;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PointerEntered -= OnPointerEntered;
        PointerExited  -= OnPointerExited;
    }

    private static readonly BoxShadows ShadowHover = BoxShadows.Parse("0 8 24 0 #1E251C53");
    private static readonly BoxShadows ShadowNormal = BoxShadows.Parse("0 1 2 0 #0D251C53");

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!IsHoverable || Variant != KzCardVariant.Default) return;
        if (this.FindControl<Border>("CardRoot") is not { } root) return;
        root.BoxShadow = ShadowHover;
        root.BorderBrush = KzTokens.Orange500Brush;
        root.RenderTransform = new TranslateTransform(0, -2);
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!IsHoverable || Variant != KzCardVariant.Default) return;
        if (this.FindControl<Border>("CardRoot") is not { } root) return;
        root.BoxShadow = ShadowNormal;
        root.BorderBrush = KzTokens.BorderBrush;
        root.RenderTransform = null;
    }

    private void ApplyTitle(string? title)
    {
        if (this.FindControl<TextBlock>("CardTitleText") is { } tb)
        {
            tb.Text = title ?? string.Empty;
            tb.IsVisible = !string.IsNullOrEmpty(title);
        }
    }

    private void ApplyText(string? text)
    {
        if (this.FindControl<TextBlock>("CardDescText") is { } tb)
        {
            tb.Text = text ?? string.Empty;
            tb.IsVisible = !string.IsNullOrEmpty(text);
        }
    }

    private void ApplyIcon(string? icon)
    {
        if (this.FindControl<Border>("IconContainer") is { } container
            && this.FindControl<TextBlock>("IconText") is { } tb)
        {
            tb.Text = icon ?? string.Empty;
            container.IsVisible = !string.IsNullOrEmpty(icon);
        }
    }

    private void ApplyVariant(KzCardVariant variant)
    {
        if (this.FindControl<Border>("CardRoot") is not { } root) return;

        switch (variant)
        {
            case KzCardVariant.Dark:
                root.Background = KzTokens.Navy900Brush;
                root.BorderBrush = KzTokens.Navy700Brush;
                if (this.FindControl<TextBlock>("CardTitleText") is { } t1)
                    t1.Foreground = KzTokens.WhiteBrush;
                if (this.FindControl<TextBlock>("CardDescText") is { } d1)
                    d1.Foreground = KzTokens.Navy300Brush;
                if (this.FindControl<Border>("IconContainer") is { } ic1)
                    ic1.Background = KzTokens.Navy700Brush;
                break;

            case KzCardVariant.Feature:
                root.Background = KzTokens.Orange500Brush;
                root.BorderBrush = KzTokens.Orange600Brush;
                if (this.FindControl<TextBlock>("CardTitleText") is { } t2)
                    t2.Foreground = KzTokens.WhiteBrush;
                if (this.FindControl<TextBlock>("CardDescText") is { } d2)
                    d2.Foreground = new SolidColorBrush(Color.Parse("#FFEDE6"));
                if (this.FindControl<Border>("IconContainer") is { } ic2)
                    ic2.Background = KzTokens.Orange600Brush;
                break;

            default: // Default
                root.Background = KzTokens.BgDefaultBrush;
                root.BorderBrush = KzTokens.BorderBrush;
                if (this.FindControl<TextBlock>("CardTitleText") is { } t3)
                    t3.Foreground = KzTokens.Navy900Brush;
                if (this.FindControl<TextBlock>("CardDescText") is { } d3)
                    d3.Foreground = KzTokens.TextMutedBrush;
                if (this.FindControl<Border>("IconContainer") is { } ic3)
                    ic3.Background = KzTokens.Navy100Brush;
                break;
        }
    }
}
