using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using KztekComponentAvalonia.Theme;
using System;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK branded password input with show/hide eye toggle.
/// </summary>
public partial class KzPasswordTextBox : UserControl
{
    // ── Styled Properties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<KzSize> KzSizeProperty =
        AvaloniaProperty.Register<KzPasswordTextBox, KzSize>(nameof(KzSize), KzSize.Md);

    public static readonly StyledProperty<KzInputState> InputStateProperty =
        AvaloniaProperty.Register<KzPasswordTextBox, KzInputState>(nameof(InputState), KzInputState.Normal);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<KzPasswordTextBox, string?>(nameof(Watermark), "Nhập mật khẩu");

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<KzPasswordTextBox, string?>(nameof(Text));

    // ── Public Properties ─────────────────────────────────────────────────────

    public KzSize KzSize
    {
        get => GetValue(KzSizeProperty);
        set { SetValue(KzSizeProperty, value); ApplySize(); }
    }

    public KzInputState InputState
    {
        get => GetValue(InputStateProperty);
        set { SetValue(InputStateProperty, value); ApplyInputState(); }
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set { SetValue(WatermarkProperty, value); ApplyWatermark(); }
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // ── State ─────────────────────────────────────────────────────────────────
    private bool _showPassword = false;

    private Button?  _eyeBtn;
    private TextBox? _inputTb;
    private Border?  _inputBorder;
    private EventHandler<FocusChangedEventArgs>? _onTbGotFocus;
    private EventHandler<FocusChangedEventArgs>?   _onTbLostFocus;
    private EventHandler<PointerEventArgs>?  _onBorderPointerEntered;
    private EventHandler<PointerEventArgs>?  _onBorderPointerExited;

    public KzPasswordTextBox()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplySize();
        ApplyInputState();
        ApplyWatermark();

        if (this.FindControl<Button>("PART_EyeBtn") is { } btn)
        {
            _eyeBtn = btn;
            btn.Click += OnEyeToggle;
        }

        if (this.FindControl<TextBox>("PART_Input") is { } tb)
        {
            _inputTb = tb;
            tb.Bind(TextBox.TextProperty, new Avalonia.Data.Binding(nameof(Text))
            {
                Source = this,
                Mode   = Avalonia.Data.BindingMode.TwoWay,
            });

            _onTbGotFocus  = (_, _) => ApplyBorderFocused(true);
            _onTbLostFocus = (_, _) => ApplyBorderFocused(false);
            tb.GotFocus  += _onTbGotFocus;
            tb.LostFocus += _onTbLostFocus;
        }

        if (this.FindControl<Border>("PART_Border") is { } border)
        {
            _inputBorder = border;
            _onBorderPointerEntered = (_, _) =>
            {
                if (InputState != KzInputState.Disabled && InputState != KzInputState.Error)
                    border.BorderBrush = KzTokens.Navy700Brush;
            };
            _onBorderPointerExited = (_, _) => ApplyBorderDefault();
            border.PointerEntered += _onBorderPointerEntered;
            border.PointerExited  += _onBorderPointerExited;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_eyeBtn     is not null) _eyeBtn.Click        -= OnEyeToggle;
        if (_inputTb    is not null)
        {
            _inputTb.GotFocus  -= _onTbGotFocus;
            _inputTb.LostFocus -= _onTbLostFocus;
        }
        if (_inputBorder is not null)
        {
            _inputBorder.PointerEntered -= _onBorderPointerEntered;
            _inputBorder.PointerExited  -= _onBorderPointerExited;
        }
    }

    // ── Toggle eye ────────────────────────────────────────────────────────────

    private void OnEyeToggle(object? sender, RoutedEventArgs e)
    {
        _showPassword = !_showPassword;

        if (this.FindControl<TextBox>("PART_Input") is { } tb)
            tb.PasswordChar = _showPassword ? '\0' : '●';

        if (this.FindControl<Path>("PART_EyeOpen") is { } eyeOpen)
            eyeOpen.IsVisible = !_showPassword;

        if (this.FindControl<Path>("PART_EyeSlash") is { } eyeSlash)
            eyeSlash.IsVisible = _showPassword;
    }

    // ── Size ──────────────────────────────────────────────────────────────────

    private void ApplySize()
    {
        double h = KzSize switch
        {
            KzSize.Sm => KzTokens.HeightSm,
            KzSize.Lg => KzTokens.HeightLg,
            _          => KzTokens.HeightMd,
        };

        if (this.FindControl<Border>("PART_Border") is { } border)
            border.MinHeight = h;

        double fontSize = KzSize switch
        {
            KzSize.Sm => KzTokens.FontSizeSm,
            KzSize.Lg => KzTokens.FontSizeLg,
            _          => KzTokens.FontSizeMd,
        };

        if (this.FindControl<TextBox>("PART_Input") is { } tb)
            tb.FontSize = fontSize;
    }

    // ── Input state ───────────────────────────────────────────────────────────

    private void ApplyInputState()
    {
        if (this.FindControl<Border>("PART_Border") is not { } border) return;
        if (this.FindControl<TextBox>("PART_Input") is not { } tb) return;

        switch (InputState)
        {
            case KzInputState.Error:
                border.BorderBrush    = KzTokens.ErrorBrush;
                border.BorderThickness = new Thickness(1);
                border.Background     = KzTokens.BgDefaultBrush;
                tb.IsEnabled          = true;
                break;

            case KzInputState.Disabled:
                border.BorderBrush    = KzTokens.BorderBrush;
                border.BorderThickness = new Thickness(1);
                border.Background     = KzTokens.Navy100Brush;
                tb.IsEnabled          = false;
                break;

            default:
                border.BorderBrush    = KzTokens.BorderBrush;
                border.BorderThickness = new Thickness(1);
                border.Background     = KzTokens.BgDefaultBrush;
                tb.IsEnabled          = true;
                break;
        }
    }

    private void ApplyBorderFocused(bool focused)
    {
        if (this.FindControl<Border>("PART_Border") is not { } border) return;
        if (InputState == KzInputState.Error) return;

        border.BorderBrush    = focused ? KzTokens.Orange500Brush : KzTokens.BorderBrush;
        border.BorderThickness = new Thickness(focused ? 2 : 1);
    }

    private void ApplyBorderDefault()
    {
        if (this.FindControl<Border>("PART_Border") is not { } border) return;
        if (InputState == KzInputState.Error)
        { border.BorderBrush = KzTokens.ErrorBrush; return; }
        border.BorderBrush    = KzTokens.BorderBrush;
        border.BorderThickness = new Thickness(1);
    }

    // ── Watermark ─────────────────────────────────────────────────────────────

    private void ApplyWatermark()
    {
        if (this.FindControl<TextBox>("PART_Input") is { } tb)
            tb.PlaceholderText = Watermark;
    }
}
