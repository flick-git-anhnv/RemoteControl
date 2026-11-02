using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

public partial class KzGroupBox : UserControl
{
    // ── Properties ──────────────────────────────────────────────────────────

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<KzGroupBox, string?>(nameof(Title));

    public static readonly StyledProperty<object?> GroupContentProperty =
        AvaloniaProperty.Register<KzGroupBox, object?>(nameof(GroupContent));

    public static readonly StyledProperty<KzGroupBoxVariant> VariantProperty =
        AvaloniaProperty.Register<KzGroupBox, KzGroupBoxVariant>(nameof(Variant), KzGroupBoxVariant.Default);

    public string? Title
    {
        get => GetValue(TitleProperty);
        set { SetValue(TitleProperty, value); ApplyTitle(value); }
    }

    public object? GroupContent
    {
        get => GetValue(GroupContentProperty);
        set => SetValue(GroupContentProperty, value);
    }

    public KzGroupBoxVariant Variant
    {
        get => GetValue(VariantProperty);
        set { SetValue(VariantProperty, value); ApplyVariant(value); }
    }

    public KzGroupBox()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyTitle(Title);
        ApplyVariant(Variant);
    }

    private void ApplyTitle(string? title)
    {
        bool hasTitle = !string.IsNullOrEmpty(title);

        if (this.FindControl<Border>("HeaderBorder") is { } header)
        {
            header.IsVisible = hasTitle;
            if (this.FindControl<TextBlock>("TitleText") is { } tb)
                tb.Text = title ?? string.Empty;
        }

        if (this.FindControl<Rectangle>("DividerLine") is { } div)
            div.IsVisible = hasTitle;
    }

    private void ApplyVariant(KzGroupBoxVariant variant)
    {
        if (this.FindControl<Border>("GroupRoot") is not { } root) return;

        switch (variant)
        {
            case KzGroupBoxVariant.Filled:
                root.Background    = KzTokens.Navy100Brush;
                root.BorderBrush   = KzTokens.BorderBrush;
                root.BorderThickness = new Thickness(1);
                break;

            case KzGroupBoxVariant.Outlined:
                root.Background    = Brushes.Transparent;
                root.BorderBrush   = KzTokens.DividerBrush;
                root.BorderThickness = new Thickness(2);
                break;

            default: // Default
                root.Background    = KzTokens.BgDefaultBrush;
                root.BorderBrush   = KzTokens.BorderBrush;
                root.BorderThickness = new Thickness(1);
                break;
        }
    }
}
