using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Avalonia port của ucConfigurationItem (WinForms) — mỗi dòng cấu hình kiosk.
/// Một instance = label bên trái + TextBox bên phải (tuỳ chọn PasswordChar/ReadOnly).
///
/// Khi TextBox nhận focus → Raises InputFocused(TextBox) → parent window gán:
///   kbMain.TargetTextBox = e;
///
/// Sync hai chiều giữa StyledProperty Value và InputBox.Text được xử lý trong
/// OnPropertyChanged + TextChanged — không dùng compiled binding để tránh DataType phức tạp.
/// </summary>
public partial class KzConfigurationItem : UserControl
{
    // ── Styled Properties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<KzConfigurationItem, string>(
            nameof(Title), string.Empty);

    /// <summary>Giá trị text hiện tại — TwoWay binding-ready.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<KzConfigurationItem, string>(
            nameof(Value), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Khi true → InputBox hiển thị ký tự ẩn ('*').</summary>
    public static readonly StyledProperty<bool> IsPasswordProperty =
        AvaloniaProperty.Register<KzConfigurationItem, bool>(nameof(IsPassword));

    /// <summary>Khi true → InputBox chỉ đọc (không gõ được, nhưng vẫn focus được).</summary>
    public static readonly StyledProperty<bool> ReadOnlyProperty =
        AvaloniaProperty.Register<KzConfigurationItem, bool>(nameof(ReadOnly));

    /// <summary>
    /// Khi true → ô này chỉ nhận số (port, baudrate, thời gian chờ...). Window mở màn Cấu hình
    /// dùng cờ này để tự chuyển bàn phím ảo sang chế độ số khi ô nhận focus, thay vì bắt người
    /// dùng bấm thêm "?123" mỗi lần.
    /// </summary>
    public static readonly StyledProperty<bool> IsNumericProperty =
        AvaloniaProperty.Register<KzConfigurationItem, bool>(nameof(IsNumeric));

    public bool IsNumeric
    {
        get => GetValue(IsNumericProperty);
        set => SetValue(IsNumericProperty, value);
    }

    /// <summary>
    /// Khi != null → hiển thị ComboBox (DropDownList) thay TextBox — port cbXxx của WinForms
    /// (cbPgsDbAuthMode, cbControllerType, cbCardFormat, cbCardFormatOption,
    /// cbControllerMode, cbControllerCommunication, cbQrProvider).
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> OptionsProperty =
        AvaloniaProperty.Register<KzConfigurationItem, IEnumerable?>(nameof(Options));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsPassword
    {
        get => GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public bool ReadOnly
    {
        get => GetValue(ReadOnlyProperty);
        set => SetValue(ReadOnlyProperty, value);
    }

    public IEnumerable? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>true nếu control đang ở chế độ ComboBox (Options != null).</summary>
    public bool IsComboMode => Options != null;

    // ── Event ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised khi InputBox lấy focus.
    /// Parent dùng để gán KzKeyboard.TargetTextBox = e.
    /// </summary>
    public event EventHandler<TextBox>? InputFocused;

    /// <summary>Truy cập trực tiếp vào TextBox bên trong (dùng khi cần gán TargetTextBox từ code).</summary>
    public TextBox InputTextBox => InputBox;

    // ── Constructor ───────────────────────────────────────────────────────────

    public KzConfigurationItem()
    {
        InitializeComponent();

        // GotFocus wired bằng lambda để tránh phụ thuộc tên kiểu GotFocusEventArgs
        // (namespace issue trên một số version Avalonia 12.x — gotcha đã gặp ở 2.B2)
        InputBox.GotFocus += (_, _) => InputFocused?.Invoke(this, InputBox);

        // Reverse-sync: InputBox.Text → Value (tránh vòng lặp: kiểm tra bất đẳng thức trước khi set)
        InputBox.TextChanged += (_, _) =>
        {
            var t = InputBox.Text ?? string.Empty;
            if (Value != t) Value = t;
        };

        // Combo mode: SelectedItem → Value
        ComboInput.SelectionChanged += (_, _) =>
        {
            var s = ComboInput.SelectedItem?.ToString() ?? string.Empty;
            if (Value != s) Value = s;
        };
    }

    // ── Property change → sync to inner controls ──────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
        {
            TitleBlock.Text = change.GetNewValue<string>();
        }
        else if (change.Property == ValueProperty)
        {
            // Forward-sync: Value → InputBox.Text (tránh vòng lặp)
            var v = change.GetNewValue<string>();
            if (InputBox.Text != v) InputBox.Text = v;
            if (IsComboMode)
            {
                var sel = ComboInput.SelectedItem?.ToString();
                if (sel != v) ComboInput.SelectedItem = v;
            }
        }
        else if (change.Property == IsPasswordProperty)
        {
            InputBox.PasswordChar = change.GetNewValue<bool>() ? '*' : '\0';
        }
        else if (change.Property == ReadOnlyProperty)
        {
            InputBox.IsReadOnly = change.GetNewValue<bool>();
        }
        else if (change.Property == OptionsProperty)
        {
            var opts = change.GetNewValue<IEnumerable?>();
            ComboInput.ItemsSource = opts;
            bool combo = opts != null;
            ComboInput.IsVisible = combo;
            InputBox.IsVisible = !combo;
            if (combo && !string.IsNullOrEmpty(Value))
                ComboInput.SelectedItem = Value;
        }
    }

}
