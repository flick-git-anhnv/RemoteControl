using Avalonia;
using Avalonia.Controls.Primitives;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK status dot — chấm tròn trạng thái (Online/Offline/Warning/Unknown) + label tuỳ chọn.
/// Dùng cho status bar, panel thiết bị, dashboard.
/// Dùng: &lt;kz:KzStatusDot Status="Online" Label="Server" /&gt;
/// </summary>
public class KzStatusDot : TemplatedControl
{
    public static readonly StyledProperty<KzDeviceStatus> StatusProperty =
        AvaloniaProperty.Register<KzStatusDot, KzDeviceStatus>(nameof(Status), KzDeviceStatus.Unknown);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<KzStatusDot, string?>(nameof(Label));

    /// <summary>Hiện label cạnh dot. Mặc định true (ẩn tự động khi Label null).</summary>
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<KzStatusDot, bool>(nameof(ShowLabel), true);

    /// <summary>Đường kính dot (px). Mặc định 10.</summary>
    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<KzStatusDot, double>(nameof(DotSize), 10.0);

    public KzDeviceStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool ShowLabel
    {
        get => GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
    }

    public double DotSize
    {
        get => GetValue(DotSizeProperty);
        set => SetValue(DotSizeProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateClasses();
    }

    /// <summary>
    /// Binding ghi thẳng StyledProperty không qua CLR setter (cùng lý do KzBadge) —
    /// phải bắt OnPropertyChanged để class đổi màu khi binding cập nhật.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
            UpdateClasses();
    }

    private void UpdateClasses()
    {
        Classes.Remove("kz-dot-online");
        Classes.Remove("kz-dot-offline");
        Classes.Remove("kz-dot-warning");
        Classes.Remove("kz-dot-unknown");

        Classes.Add(Status switch
        {
            KzDeviceStatus.Online  => "kz-dot-online",
            KzDeviceStatus.Offline => "kz-dot-offline",
            KzDeviceStatus.Warning => "kz-dot-warning",
            _                      => "kz-dot-unknown"
        });
    }
}
