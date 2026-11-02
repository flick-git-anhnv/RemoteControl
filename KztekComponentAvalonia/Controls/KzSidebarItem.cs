using Avalonia;
using Avalonia.Controls;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// A single navigation item used within KzSidebar.
/// Inherits ListBoxItem so selection is handled by parent ListBox.
/// </summary>
public class KzSidebarItem : ListBoxItem
{
    public static readonly StyledProperty<string?> ItemLabelProperty =
        AvaloniaProperty.Register<KzSidebarItem, string?>(nameof(ItemLabel));

    public static readonly StyledProperty<string?> ItemIconProperty =
        AvaloniaProperty.Register<KzSidebarItem, string?>(nameof(ItemIcon));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<KzSidebarItem, bool>(nameof(IsActive), false);

    public string? ItemLabel
    {
        get => GetValue(ItemLabelProperty);
        set => SetValue(ItemLabelProperty, value);
    }

    public string? ItemIcon
    {
        get => GetValue(ItemIconProperty);
        set => SetValue(ItemIconProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }
}
