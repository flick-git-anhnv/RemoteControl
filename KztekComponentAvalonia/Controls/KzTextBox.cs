using Avalonia;
using Avalonia.Controls;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK branded TextBox with size variants and error/disabled states.
/// </summary>
public class KzTextBox : TextBox
{
    public static readonly StyledProperty<KzSize> KzSizeProperty =
        AvaloniaProperty.Register<KzTextBox, KzSize>(nameof(KzSize), KzSize.Md);

    public static readonly StyledProperty<KzInputState> InputStateProperty =
        AvaloniaProperty.Register<KzTextBox, KzInputState>(nameof(InputState), KzInputState.Normal);

    public KzSize KzSize
    {
        get => GetValue(KzSizeProperty);
        set
        {
            SetValue(KzSizeProperty, value);
            UpdateClasses();
        }
    }

    public KzInputState InputState
    {
        get => GetValue(InputStateProperty);
        set
        {
            SetValue(InputStateProperty, value);
            UpdateClasses();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateClasses();
    }

    private void UpdateClasses()
    {
        Classes.Remove("kz-size-sm");
        Classes.Remove("kz-size-md");
        Classes.Remove("kz-size-lg");
        Classes.Remove("kz-input-error");

        Classes.Add(KzThemeExtensions.SizeToStyleClass(KzSize));

        if (InputState == KzInputState.Error)
            Classes.Add("kz-input-error");

        IsEnabled = InputState != KzInputState.Disabled;
    }
}
