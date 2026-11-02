using Avalonia;
using Avalonia.Controls;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK branded ProgressBar — pill-shaped, Navy fill, Navy-100 track.
/// Supports Accent (Orange) variant. Styling in Themes/KzProgressBar.axaml.
/// </summary>
public class KzProgressBar : ProgressBar
{
    public static readonly StyledProperty<KzProgressBarVariant> VariantProperty =
        AvaloniaProperty.Register<KzProgressBar, KzProgressBarVariant>(
            nameof(Variant), KzProgressBarVariant.Primary);

    public KzProgressBarVariant Variant
    {
        get => GetValue(VariantProperty);
        set
        {
            SetValue(VariantProperty, value);
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
        Classes.Remove("kz-progress-primary");
        Classes.Remove("kz-progress-accent");
        Classes.Remove("kz-progress-success");

        Classes.Add(Variant switch
        {
            KzProgressBarVariant.Accent  => "kz-progress-accent",
            KzProgressBarVariant.Success => "kz-progress-success",
            _                            => "kz-progress-primary",
        });
    }
}
