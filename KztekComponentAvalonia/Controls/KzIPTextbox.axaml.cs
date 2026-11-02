using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KztekComponentAvalonia.Theme;
using System;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK branded IPv4 address input — 4 octets, auto-advance, 0-255 validation.
/// </summary>
public partial class KzIPTextbox : UserControl
{
    // ── Styled Properties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<KzInputState> InputStateProperty =
        AvaloniaProperty.Register<KzIPTextbox, KzInputState>(nameof(InputState), KzInputState.Normal);

    public static readonly StyledProperty<KzSize> KzSizeProperty =
        AvaloniaProperty.Register<KzIPTextbox, KzSize>(nameof(KzSize), KzSize.Md);

    /// <summary>Bindable IP address ("192.168.1.1"). Supports TwoWay binding.</summary>
    public static readonly StyledProperty<string> IPAddressProperty =
        AvaloniaProperty.Register<KzIPTextbox, string>(nameof(IPAddress), "0.0.0.0",
            defaultBindingMode: BindingMode.TwoWay);

    // ── Direct Properties ─────────────────────────────────────────────────────

    private bool _isValid;

    /// <summary>True when all 4 octets are valid integers 0–255. Bindable, raises change notification.</summary>
    public static readonly DirectProperty<KzIPTextbox, bool> IsValidProperty =
        AvaloniaProperty.RegisterDirect<KzIPTextbox, bool>(nameof(IsValid),
            o => o._isValid);

    // ── Public Properties ─────────────────────────────────────────────────────

    public KzInputState InputState
    {
        get => GetValue(InputStateProperty);
        set => SetValue(InputStateProperty, value);
    }

    public KzSize KzSize
    {
        get => GetValue(KzSizeProperty);
        set => SetValue(KzSizeProperty, value);
    }

    public string IPAddress
    {
        get => GetValue(IPAddressProperty);
        set => SetValue(IPAddressProperty, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetAndRaise(IsValidProperty, ref _isValid, value);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<EventArgs>? IPAddressChanged;

    // ── Private state ─────────────────────────────────────────────────────────

    private TextBox[]? _octets;
    private bool       _suppressEvt;
    private bool       _suppressIPSync;

    private Border? _border;
    private readonly EventHandler<TextChangedEventArgs>?[] _octetTextChangedHandlers = new EventHandler<TextChangedEventArgs>?[4];
    private readonly EventHandler<KeyEventArgs>?[]         _octetKeyDownHandlers      = new EventHandler<KeyEventArgs>?[4];
    private readonly EventHandler<PointerPressedEventArgs>?[] _octetPointerPressedHandlers = new EventHandler<PointerPressedEventArgs>?[4];
    private EventHandler<PointerEventArgs>? _onBorderPointerEntered;
    private EventHandler<PointerEventArgs>? _onBorderPointerExited;

    // ── Constructor ───────────────────────────────────────────────────────────

    public KzIPTextbox() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _octets = new[]
        {
            this.FindControl<TextBox>("PART_Oct0")!,
            this.FindControl<TextBox>("PART_Oct1")!,
            this.FindControl<TextBox>("PART_Oct2")!,
            this.FindControl<TextBox>("PART_Oct3")!,
        };

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            _octetTextChangedHandlers[i] = (_, _) => OnOctetChanged(idx);
            _octetKeyDownHandlers[i]     = (_, ev) => OnOctetKeyDown(idx, ev);
            // Bôi đen toàn bộ nội dung khi CLICK CHUỘT vào ô lần đầu (đang chưa focus) — user gõ đè
            // ngay, không cần xóa tay trước. Chỉ áp dụng khi ô CHƯA focus (tránh đè caret khi user
            // click lại để định vị con trỏ trong ô đang gõ dở). Không dùng GotFocus vì nó cũng fire
            // khi điều hướng bằng phím (Backspace/Left/Right/".") — nơi code đã tự set caret chính
            // xác, SelectAll ở đây sẽ phá hỏng hành vi xóa liên tục qua nhiều ô.
            //
            // BẮT BUỘC đăng ký ở pha Tunnel (không phải Bubble mặc định của "+="): TextBox tự xử lý
            // PointerPressed để set focus + đặt caret theo tọa độ click NGAY khi event tới nó, TRƯỚC
            // khi handler Bubble bên ngoài kịp chạy — nên kiểm tra IsFocused ở pha Bubble luôn thấy
            // "đã focus" (do chính click này vừa set), khiến điều kiện never true. Tunnel chạy sớm
            // hơn, bắt được đúng trạng thái focus TRƯỚC khi TextBox xử lý click.
            _octetPointerPressedHandlers[i] = (_, _) =>
            {
                if (!_octets[idx].IsFocused)
                    Dispatcher.UIThread.Post(() => _octets[idx].SelectAll());
            };
            _octets[i].TextChanged += _octetTextChangedHandlers[i];
            _octets[i].KeyDown     += _octetKeyDownHandlers[i];
            _octets[i].AddHandler(InputElement.PointerPressedEvent, _octetPointerPressedHandlers[i], RoutingStrategies.Tunnel);
        }

        if (this.FindControl<Border>("PART_Border") is { } border)
        {
            _border = border;
            _onBorderPointerEntered = (_, _) =>
            {
                if (InputState != KzInputState.Disabled && InputState != KzInputState.Error)
                    border.BorderBrush = KzTokens.Navy700Brush;
            };
            _onBorderPointerExited = (_, _) => ApplyBorderDefault();
            border.PointerEntered += _onBorderPointerEntered;
            border.PointerExited  += _onBorderPointerExited;
        }

        ApplySize();
        ApplyInputState();

        // Apply current DP value (covers both AXAML set and binding set before attach)
        SetIPToOctets(GetValue(IPAddressProperty));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_octets is not null)
            for (int i = 0; i < 4; i++)
            {
                if (_octetTextChangedHandlers[i]    is not null) _octets[i].TextChanged    -= _octetTextChangedHandlers[i];
                if (_octetKeyDownHandlers[i]        is not null) _octets[i].KeyDown        -= _octetKeyDownHandlers[i];
                if (_octetPointerPressedHandlers[i] is not null) _octets[i].RemoveHandler(InputElement.PointerPressedEvent, _octetPointerPressedHandlers[i]);
            }
        if (_border is not null)
        {
            _border.PointerEntered -= _onBorderPointerEntered;
            _border.PointerExited  -= _onBorderPointerExited;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IPAddressProperty && !_suppressIPSync)
        {
            // Binding pushed a new value → update octets without re-triggering the DP
            if (_octets is not null)
                SetIPToOctets(change.GetNewValue<string>());
        }
        else if (change.Property == IsKeyboardFocusWithinProperty)
            ApplyBorderFocused(change.GetNewValue<bool>());
        else if (change.Property == InputStateProperty)
            ApplyInputState();
        else if (change.Property == KzSizeProperty)
            ApplySize();
    }

    // ── IP helpers ────────────────────────────────────────────────────────────

    private void SetIPToOctets(string? value)
    {
        if (_octets is null) return;
        // Suppress octet events to avoid 4× IPAddressChanged and DP loop
        _suppressEvt = true;
        var parts = (value ?? "").Split('.');
        for (int i = 0; i < 4; i++)
            _octets[i].Text = i < parts.Length ? parts[i] : "";
        _suppressEvt = false;
        IsValid = CheckIsValid();
    }

    private bool CheckIsValid()
    {
        if (_octets is null) return false;
        foreach (var o in _octets)
            if (!int.TryParse(o.Text, out int v) || v < 0 || v > 255) return false;
        return true;
    }

    // ── Octet event handlers ──────────────────────────────────────────────────

    private void OnOctetChanged(int idx)
    {
        if (_suppressEvt || _octets is null) return;

        var tb   = _octets[idx];
        var text = tb.Text ?? "";

        // Strip non-digits (handles paste, IME, etc.)
        string digits = string.Empty;
        foreach (char c in text)
            if (char.IsDigit(c)) digits += c;

        if (digits != text)
        {
            _suppressEvt = true;
            int caret = Math.Min(tb.SelectionStart, digits.Length);
            tb.Text = digits;
            tb.SelectionStart = caret;
            _suppressEvt = false;
            return; // TextChanged will re-fire with clean text
        }

        // Clamp to 255
        if (int.TryParse(text, out int v) && v > 255)
        {
            _suppressEvt = true;
            tb.Text = "255";
            tb.SelectionStart = 3;
            _suppressEvt = false;
        }

        // Auto-advance after 3 digits
        if ((tb.Text?.Length ?? 0) == 3 && idx < 3)
            _octets[idx + 1].Focus();

        // Push new value back to the StyledProperty so bindings update
        _suppressIPSync = true;
        SetValue(IPAddressProperty,
            $"{_octets[0].Text}.{_octets[1].Text}.{_octets[2].Text}.{_octets[3].Text}");
        _suppressIPSync = false;

        IsValid = CheckIsValid();
        IPAddressChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnOctetKeyDown(int idx, KeyEventArgs e)
    {
        if (_octets is null) return;

        switch (e.Key)
        {
            // "." or numpad "." → advance to next octet and suppress the character
            case Key.OemPeriod:
            case Key.Decimal:
                if (idx < 3)
                {
                    _octets[idx + 1].Focus();
                    _octets[idx + 1].SelectAll();
                }
                e.Handled = true;
                break;

            // Backspace on empty octet → go back to previous
            case Key.Back:
                if (string.IsNullOrEmpty(_octets[idx].Text) && idx > 0)
                {
                    _octets[idx - 1].Focus();
                    int len = _octets[idx - 1].Text?.Length ?? 0;
                    _octets[idx - 1].SelectionStart = len;
                    e.Handled = true;
                }
                break;

            // Right-arrow at end of octet → advance
            case Key.Right:
                if (_octets[idx].SelectionStart >= (_octets[idx].Text?.Length ?? 0) && idx < 3)
                {
                    _octets[idx + 1].Focus();
                    _octets[idx + 1].SelectionStart = 0;
                }
                break;

            // Left-arrow at start of octet → go back
            case Key.Left:
                if (_octets[idx].SelectionStart == 0 && idx > 0)
                {
                    _octets[idx - 1].Focus();
                    int len = _octets[idx - 1].Text?.Length ?? 0;
                    _octets[idx - 1].SelectionStart = len;
                }
                break;
        }
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private void ApplyInputState()
    {
        if (this.FindControl<Border>("PART_Border") is not { } border) return;

        bool disabled = InputState == KzInputState.Disabled;
        if (_octets != null)
            foreach (var tb in _octets)
                tb.IsEnabled = !disabled;

        switch (InputState)
        {
            case KzInputState.Error:
                border.BorderBrush     = KzTokens.ErrorBrush;
                border.BorderThickness = new Thickness(1);
                border.Background      = KzTokens.BgDefaultBrush;
                break;

            case KzInputState.Disabled:
                border.BorderBrush     = KzTokens.BorderBrush;
                border.BorderThickness = new Thickness(1);
                border.Background      = KzTokens.Navy100Brush;
                break;

            default:
                border.BorderBrush     = KzTokens.BorderBrush;
                border.BorderThickness = new Thickness(1);
                border.Background      = KzTokens.BgDefaultBrush;
                break;
        }
    }

    private void ApplyBorderFocused(bool focused)
    {
        if (this.FindControl<Border>("PART_Border") is not { } border) return;
        if (InputState == KzInputState.Error) return;
        if (InputState == KzInputState.Disabled) return;

        border.BorderBrush     = focused ? KzTokens.Orange500Brush : KzTokens.BorderBrush;
        border.BorderThickness = new Thickness(focused ? 2 : 1);
    }

    private void ApplyBorderDefault()
    {
        if (this.FindControl<Border>("PART_Border") is not { } border) return;
        if (InputState == KzInputState.Error)   { border.BorderBrush = KzTokens.ErrorBrush;  return; }
        if (InputState == KzInputState.Disabled) return;
        border.BorderBrush     = KzTokens.BorderBrush;
        border.BorderThickness = new Thickness(1);
    }

    private void ApplySize()
    {
        if (this.FindControl<Border>("PART_Border") is not { } border) return;

        double h = KzSize switch
        {
            KzSize.Sm => KzTokens.HeightSm,
            KzSize.Lg => KzTokens.HeightLg,
            _          => KzTokens.HeightMd,
        };
        border.MinHeight = h;

        double fontSize = KzSize switch
        {
            KzSize.Sm => KzTokens.FontSizeSm,
            KzSize.Lg => KzTokens.FontSizeLg,
            _          => KzTokens.FontSizeMd,
        };

        if (_octets != null)
            foreach (var tb in _octets)
                tb.FontSize = fontSize;

        for (int i = 0; i < 3; i++)
            if (this.FindControl<TextBlock>($"PART_Dot{i}") is { } dot)
                dot.FontSize = fontSize;
    }
}
