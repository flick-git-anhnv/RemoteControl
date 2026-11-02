using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KztekComponentAvalonia.Theme;

public static class KzThemeExtensions
{
    /// <summary>Gets a token brush from application resources by key.</summary>
    public static IBrush? GetKzBrush(string key)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var res) == true
            && res is IBrush brush)
            return brush;
        return null;
    }

    /// <summary>Gets a token color from application resources by key.</summary>
    public static Color? GetKzColor(string key)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var res) == true
            && res is Color color)
            return color;
        return null;
    }

    public static string VariantToStyleClass(KzButtonVariant variant) => variant switch
    {
        KzButtonVariant.Primary   => "kz-btn-primary",
        KzButtonVariant.Accent    => "kz-btn-accent",
        KzButtonVariant.Secondary => "kz-btn-secondary",
        KzButtonVariant.Ghost     => "kz-btn-ghost",
        KzButtonVariant.Danger    => "kz-btn-danger",
        _                          => "kz-btn-primary"
    };

    public static string SizeToStyleClass(KzSize size) => size switch
    {
        KzSize.Sm => "kz-size-sm",
        KzSize.Md => "kz-size-md",
        KzSize.Lg => "kz-size-lg",
        _          => "kz-size-md"
    };
}
