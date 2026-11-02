//using Avalonia;
//using Avalonia.Controls.Primitives;
//using KztekComponentAvalonia.Theme;

//namespace KztekComponentAvalonia.Controls;

///// <summary>
///// KZTEK badge/tag control. Supports 6 variants x 2 types (StatusPill / Tag).
///// Use properties Variant and BadgeType or apply CSS classes directly.
///// </summary>
//public class KzBadge : TemplatedControl
//{
//    public static readonly StyledProperty<string?> ContentProperty =
//        AvaloniaProperty.Register<KzBadge, string?>(nameof(Content), string.Empty);

//    public static readonly StyledProperty<KzBadgeVariant> VariantProperty =
//        AvaloniaProperty.Register<KzBadge, KzBadgeVariant>(nameof(Variant), KzBadgeVariant.Neutral);

//    public static readonly StyledProperty<KzBadgeType> BadgeTypeProperty =
//        AvaloniaProperty.Register<KzBadge, KzBadgeType>(nameof(BadgeType), KzBadgeType.StatusPill);

//    public string? Content
//    {
//        get => GetValue(ContentProperty);
//        set => SetValue(ContentProperty, value);
//    }

//    public KzBadgeVariant Variant
//    {
//        get => GetValue(VariantProperty);
//        set
//        {
//            SetValue(VariantProperty, value);
//            UpdateClasses();
//        }
//    }

//    public KzBadgeType BadgeType
//    {
//        get => GetValue(BadgeTypeProperty);
//        set
//        {
//            SetValue(BadgeTypeProperty, value);
//            UpdateClasses();
//        }
//    }

//    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
//    {
//        base.OnAttachedToVisualTree(e);
//        UpdateClasses();
//    }

//    /// <summary>
//    /// Binding ghi THẲNG vào StyledProperty, KHÔNG đi qua CLR setter ở trên — nên nếu chỉ
//    /// cập nhật class trong setter thì <c>Variant="{Binding ...}"</c> sẽ không đổi màu.
//    ///
//    /// Lộ ra rõ nhất khi đặt KzBadge trong ô DataGrid: DataGrid tái sử dụng container khi
//    /// cuộn hoặc thêm dòng, badge giữ nguyên class của dòng cũ → cùng một nội dung mà mỗi
//    /// dòng hiện một màu khác nhau.
//    /// </summary>
//    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
//    {
//        base.OnPropertyChanged(change);
//        if (change.Property == VariantProperty || change.Property == BadgeTypeProperty)
//            UpdateClasses();
//    }

//    private void UpdateClasses()
//    {
//        // Remove all variant classes
//        Classes.Remove("kz-badge-success");
//        Classes.Remove("kz-badge-warning");
//        Classes.Remove("kz-badge-error");
//        Classes.Remove("kz-badge-info");
//        Classes.Remove("kz-badge-neutral");
//        Classes.Remove("kz-badge-accent");

//        // Remove type classes
//        Classes.Remove("kz-badge-pill");
//        Classes.Remove("kz-badge-tag");

//        // Add variant
//        Classes.Add(Variant switch
//        {
//            KzBadgeVariant.Success => "kz-badge-success",
//            KzBadgeVariant.Warning => "kz-badge-warning",
//            KzBadgeVariant.Error   => "kz-badge-error",
//            KzBadgeVariant.Info    => "kz-badge-info",
//            KzBadgeVariant.Accent  => "kz-badge-accent",
//            _                      => "kz-badge-neutral"
//        });

//        // Add type
//        Classes.Add(BadgeType == KzBadgeType.Tag ? "kz-badge-tag" : "kz-badge-pill");
//    }
//}using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using KztekComponentAvalonia.Theme;
using System;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK badge/tag control. Supports 6 variants x 2 types (StatusPill / Tag).
/// Use properties Variant and BadgeType or apply CSS classes directly.
/// </summary>
public class KzBadge : TemplatedControl
{
    // BẮT BUỘC PHẢI CÓ TRONG AVALONIA 11: Báo cho framework biết ControlTheme nào sẽ được áp dụng
    protected override Type StyleKeyOverride => typeof(KzBadge);

    public static readonly StyledProperty<string?> ContentProperty =
        AvaloniaProperty.Register<KzBadge, string?>(nameof(Content), string.Empty);

    public static readonly StyledProperty<KzBadgeVariant> VariantProperty =
        AvaloniaProperty.Register<KzBadge, KzBadgeVariant>(nameof(Variant), KzBadgeVariant.Neutral);

    public static readonly StyledProperty<KzBadgeType> BadgeTypeProperty =
        AvaloniaProperty.Register<KzBadge, KzBadgeType>(nameof(BadgeType), KzBadgeType.StatusPill);

    public string? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public KzBadgeVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value); // Đã bỏ UpdateClasses() vì OnPropertyChanged đã bao xài
    }

    public KzBadgeType BadgeType
    {
        get => GetValue(BadgeTypeProperty);
        set => SetValue(BadgeTypeProperty, value); // Đã bỏ UpdateClasses() vì OnPropertyChanged đã bao xài
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // CHỈ tự sinh class theo Variant/BadgeType khi 1 trong 2 property này đã được
        // set rõ ràng (qua binding/code). Nếu không, gọi UpdateClasses() vô điều kiện
        // sẽ XÓA class gán tay qua XAML (Classes="kz-badge-pill kz-badge-success") và
        // ép về mặc định "kz-badge-pill kz-badge-neutral" — khiến badge hiện sai màu
        // (hoặc "mất màu" nếu nền neutral trùng màu nền cha, như trong Design.PreviewWith).
        if (IsSet(VariantProperty) || IsSet(BadgeTypeProperty))
        {
            UpdateClasses();
        }
    }

    /// <summary>
    /// Binding ghi THẲNG vào StyledProperty, KHÔNG đi qua CLR setter ở trên — nên nếu chỉ
    /// cập nhật class trong setter thì <c>Variant="{Binding ...}"</c> sẽ không đổi màu.
    ///
    /// Lộ ra rõ nhất khi đặt KzBadge trong ô DataGrid: DataGrid tái sử dụng container khi
    /// cuộn hoặc thêm dòng, badge giữ nguyên class của dòng cũ → cùng một nội dung mà mỗi
    /// dòng hiện một màu khác nhau.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Avalonia tự động gọi hàm này mỗi khi Property thay đổi (dù qua Binding hay Setter)
        if (change.Property == VariantProperty || change.Property == BadgeTypeProperty)
        {
            UpdateClasses();
        }
    }

    private void UpdateClasses()
    {
        // Remove all variant classes
        Classes.Remove("kz-badge-success");
        Classes.Remove("kz-badge-warning");
        Classes.Remove("kz-badge-error");
        Classes.Remove("kz-badge-info");
        Classes.Remove("kz-badge-neutral");
        Classes.Remove("kz-badge-accent");

        // Remove type classes
        Classes.Remove("kz-badge-pill");
        Classes.Remove("kz-badge-tag");

        // Add variant
        Classes.Add(Variant switch
        {
            KzBadgeVariant.Success => "kz-badge-success",
            KzBadgeVariant.Warning => "kz-badge-warning",
            KzBadgeVariant.Error => "kz-badge-error",
            KzBadgeVariant.Info => "kz-badge-info",
            KzBadgeVariant.Accent => "kz-badge-accent",
            _ => "kz-badge-neutral"
        });

        // Add type
        Classes.Add(BadgeType == KzBadgeType.Tag ? "kz-badge-tag" : "kz-badge-pill");
    }
}
