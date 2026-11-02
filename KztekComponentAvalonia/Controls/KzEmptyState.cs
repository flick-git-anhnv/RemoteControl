using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK empty-state — hiển thị khi list/lưới/panel không có dữ liệu.
/// Gồm icon vector (Geometry), tiêu đề đậm, hint mờ và ActionContent tuỳ chọn (nút hành động).
/// Dùng: &lt;kz:KzEmptyState Title="Chưa có sự kiện" Hint="Sự kiện quẹt thẻ sẽ hiển thị tại đây" /&gt;
/// </summary>
public class KzEmptyState : TemplatedControl
{
    /// <summary>Path data icon inbox mặc định (Fluent-style, viewbox 24×24).</summary>
    private const string DefaultIconData =
        "M4 5.5C4 4.12 5.12 3 6.5 3h11C18.88 3 20 4.12 20 5.5v13c0 1.38-1.12 2.5-2.5 2.5h-11" +
        "C5.12 21 4 19.88 4 18.5v-13ZM6.5 4.5c-.55 0-1 .45-1 1V13h3.09c.35 0 .68.19.86.49l.77 1.28" +
        "c.09.15.25.23.42.23h2.72c.17 0 .33-.08.42-.23l.77-1.28c.18-.3.5-.49.86-.49h3.09V5.5" +
        "c0-.55-.45-1-1-1h-11Z";

    /// <summary>Icon vector (path data). Mặc định: icon inbox.</summary>
    public static readonly StyledProperty<Geometry?> IconGeometryProperty =
        AvaloniaProperty.Register<KzEmptyState, Geometry?>(nameof(IconGeometry),
            Geometry.Parse(DefaultIconData));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<KzEmptyState, string?>(nameof(Title), "Không có dữ liệu");

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<KzEmptyState, string?>(nameof(Hint));

    /// <summary>Nội dung hành động tuỳ chọn (VD: nút "Thử lại") — hiển thị dưới hint.</summary>
    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<KzEmptyState, object?>(nameof(ActionContent));

    public Geometry? IconGeometry
    {
        get => GetValue(IconGeometryProperty);
        set => SetValue(IconGeometryProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }
}
