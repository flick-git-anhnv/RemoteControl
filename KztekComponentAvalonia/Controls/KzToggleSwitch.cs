using Avalonia;
using Avalonia.Controls;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK branded ToggleSwitch — Navy on (checked), Gray off, animated thumb.
/// Styling is defined in Themes/KzToggleSwitch.axaml.
/// </summary>
public class KzToggleSwitch : ToggleSwitch
{
    public KzToggleSwitch()
    {
        // Hide the built-in on/off text content
        OnContent  = null;
        OffContent = null;
    }
}
