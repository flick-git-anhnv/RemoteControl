using Avalonia;
using Avalonia.Controls;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Clickable link styled per KZTEK design system.
/// Default: Navy700 #4A3F8C. Hover: Orange #F05922 + underline.
/// </summary>
public class KzLink : Button
{
    public static readonly StyledProperty<string> LinkTextProperty =
        AvaloniaProperty.Register<KzLink, string>(nameof(LinkText), string.Empty);

    public string LinkText
    {
        get => GetValue(LinkTextProperty);
        set => SetValue(LinkTextProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Use content as link text if not explicitly set via LinkText
        if (string.IsNullOrEmpty(LinkText) && Content is string s)
            LinkText = s;
    }
}
