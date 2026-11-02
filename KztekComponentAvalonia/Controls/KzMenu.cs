using Avalonia;
using Avalonia.Controls;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK-styled horizontal menu bar (Navy900 background, Orange accents).
/// Use standard MenuItem children — styling is applied via KzMenu.axaml Styles.
/// </summary>
public class KzMenu : Menu
{
    public KzMenu()
    {
        Background = KzTokens.Navy900Brush;
        Foreground = KzTokens.WhiteBrush;
        Height     = 44;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Ensure styles applied (css class used by KzMenu.axaml selectors)
        if (!Classes.Contains("kz-menu-bar"))
            Classes.Add("kz-menu-bar");
    }
}
