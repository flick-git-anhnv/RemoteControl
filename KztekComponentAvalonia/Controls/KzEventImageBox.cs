using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace KztekComponentAvalonia.Controls;

/// <summary>Trạng thái ô ảnh sự kiện.</summary>
public enum KzEventImageState
{
    /// <summary>Chưa cấu hình nguồn ảnh — hiển thị empty-state nêu rõ lý do.</summary>
    NoSource,
    /// <summary>Có ảnh — hiển thị fit-contain + caption.</summary>
    HasImage,
    /// <summary>Lỗi tải ảnh (file mất/hỏng/IO) — hiển thị lỗi + nút Thử lại.</summary>
    LoadError
}

/// <summary>
/// KZTEK event image box — ô hiển thị ảnh sự kiện (đối chiếu người quẹt thẻ) với 3 trạng thái:
/// NoSource (empty-state rõ lý do) / HasImage (ảnh + caption) / LoadError (lỗi + Thử lại).
/// Nguồn ảnh do tầng app quyết định (interface IEventImageProvider phía app) — control chỉ nhận
/// <see cref="Source"/> là Avalonia IImage (Bitmap — cross-platform, KHÔNG System.Drawing).
/// Dùng: &lt;kz:KzEventImageBox State="NoSource" /&gt;
/// </summary>
public class KzEventImageBox : TemplatedControl
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<KzEventImageBox, IImage?>(nameof(Source));

    public static readonly StyledProperty<KzEventImageState> StateProperty =
        AvaloniaProperty.Register<KzEventImageBox, KzEventImageState>(nameof(State), KzEventImageState.NoSource);

    /// <summary>Caption dưới ảnh (tên + thời gian + thiết bị của sự kiện).</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<KzEventImageBox, string?>(nameof(Caption));

    /// <summary>Tiêu đề empty-state khi chưa có nguồn ảnh.</summary>
    public static readonly StyledProperty<string> NoSourceTitleProperty =
        AvaloniaProperty.Register<KzEventImageBox, string>(nameof(NoSourceTitle), "Chưa có nguồn ảnh");

    /// <summary>Hint empty-state — PHẢI nêu rõ lý do, không để ô xám vô nghĩa (quyết định gate 8.3).</summary>
    public static readonly StyledProperty<string> NoSourceHintProperty =
        AvaloniaProperty.Register<KzEventImageBox, string>(nameof(NoSourceHint),
            "Ảnh sự kiện sẽ hiển thị khi nguồn ảnh được cấu hình");

    /// <summary>Chi tiết lỗi khi State=LoadError (VD: đường dẫn file hỏng).</summary>
    public static readonly StyledProperty<string?> ErrorDetailProperty =
        AvaloniaProperty.Register<KzEventImageBox, string?>(nameof(ErrorDetail));

    /// <summary>Command nút "Thử lại" khi State=LoadError.</summary>
    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<KzEventImageBox, ICommand?>(nameof(RetryCommand));

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public KzEventImageState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string NoSourceTitle
    {
        get => GetValue(NoSourceTitleProperty);
        set => SetValue(NoSourceTitleProperty, value);
    }

    public string NoSourceHint
    {
        get => GetValue(NoSourceHintProperty);
        set => SetValue(NoSourceHintProperty, value);
    }

    public string? ErrorDetail
    {
        get => GetValue(ErrorDetailProperty);
        set => SetValue(ErrorDetailProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateClasses();
    }

    /// <summary>Class đổi theo State cả khi set qua binding (cùng lý do KzBadge/KzStatusDot).</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
            UpdateClasses();
    }

    private void UpdateClasses()
    {
        Classes.Remove("kz-eib-nosource");
        Classes.Remove("kz-eib-hasimage");
        Classes.Remove("kz-eib-error");

        Classes.Add(State switch
        {
            KzEventImageState.HasImage  => "kz-eib-hasimage",
            KzEventImageState.LoadError => "kz-eib-error",
            _                           => "kz-eib-nosource"
        });
    }
}
