using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK search box — ô tìm kiếm có icon kính lúp, nút xóa nhanh (✕) và debounce 300ms.
/// Dùng cho filter client-side (lưới sự kiện, cây thiết bị).
/// Dùng: &lt;kz:KzSearchBox Watermark="Tìm thiết bị..." /&gt; + subscribe event
/// <see cref="SearchTextChanged"/> (đã debounce) hoặc bind 2 chiều <see cref="Text"/>.
/// </summary>
public class KzSearchBox : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<KzSearchBox, string?>(nameof(Text),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<KzSearchBox, string?>(nameof(Watermark), "Tìm kiếm...");

    /// <summary>Thời gian debounce trước khi bắn SearchTextChanged. Mặc định 300ms.</summary>
    public static readonly StyledProperty<TimeSpan> DebounceIntervalProperty =
        AvaloniaProperty.Register<KzSearchBox, TimeSpan>(nameof(DebounceInterval), TimeSpan.FromMilliseconds(300));

    /// <summary>Bắn SAU debounce khi text đổi — dùng cho filter tốn kém.</summary>
    public event EventHandler<string?>? SearchTextChanged;

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public TimeSpan DebounceInterval
    {
        get => GetValue(DebounceIntervalProperty);
        set => SetValue(DebounceIntervalProperty, value);
    }

    private readonly Border _rootBorder;
    private readonly TextBox _textBox;
    private readonly Button _clearButton;
    private DispatcherTimer? _debounce;
    private bool _syncing;

    public KzSearchBox()
    {
        _textBox = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            MinHeight = 0,
            // FocusAdorner=null KHÔNG đủ: FluentTheme's ControlTheme của TextBox có Style
            // "^:focus /template/ Border#PART_BorderElement" ghi Background/BorderBrush/
            // BorderThickness TRỰC TIẾP lên Border con trong template — Style targeting thẳng
            // phần tử con này LUÔN thắng TemplateBinding (nơi giá trị local BorderThickness=0 ở
            // trên chảy xuống), nên border xanh mặc định của Fluent vẫn hiện khi focus dù đã set
            // local = 0. Phải override đúng 3 Style bên dưới (Setters trang bằng Transparent/0)
            // để triệt tiêu hẳn, KHÔNG chỉ dựa vào local value. Viền focus thật (bao trọn icon +
            // text + nút xóa) được vẽ ở _rootBorder qua GotFocus/LostFocus bên dưới.
            FocusAdorner = null
        };
        _textBox.Styles.Add(new Style(x => x.OfType<TextBox>().Template().OfType<Border>().Name("PART_BorderElement"))
        {
            Setters =
            {
                new Setter(Border.BackgroundProperty, Brushes.Transparent),
                new Setter(Border.BorderBrushProperty, Brushes.Transparent),
                new Setter(Border.BorderThicknessProperty, new Thickness(0))
            }
        });
        _textBox.Styles.Add(new Style(x => x.OfType<TextBox>().Class(":focus").Template().OfType<Border>().Name("PART_BorderElement"))
        {
            Setters =
            {
                new Setter(Border.BackgroundProperty, Brushes.Transparent),
                new Setter(Border.BorderBrushProperty, Brushes.Transparent),
                new Setter(Border.BorderThicknessProperty, new Thickness(0))
            }
        });
        _textBox.Styles.Add(new Style(x => x.OfType<TextBox>().Class(":pointerover").Template().OfType<Border>().Name("PART_BorderElement"))
        {
            Setters =
            {
                new Setter(Border.BackgroundProperty, Brushes.Transparent),
                new Setter(Border.BorderBrushProperty, Brushes.Transparent)
            }
        });
        _textBox.TextChanged += OnInnerTextChanged;
        _textBox.GotFocus += OnInnerTextBoxGotFocus;
        _textBox.LostFocus += OnInnerTextBoxLostFocus;

        _clearButton = new Button
        {
            Content = "✕",
            FontSize = 11,
            Padding = new Thickness(4, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = KzTokens.TextMutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        _clearButton.Click += (_, _) => { _textBox.Text = string.Empty; _textBox.Focus(); };

        var icon = new PathIcon
        {
            // Kính lúp (viewbox 24x24)
            Data = Geometry.Parse("M10 2a8 8 0 1 0 4.9 14.32l5.39 5.39 1.42-1.42-5.39-5.39A8 8 0 0 0 10 2Zm0 2a6 6 0 1 1 0 12 6 6 0 0 1 0-12Z"),
            Width = 14, Height = 14,
            Foreground = KzTokens.TextMutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(_clearButton, Dock.Right);
        icon.Margin = new Thickness(0, 0, 8, 0);
        dock.Children.Add(icon);
        dock.Children.Add(_clearButton);
        dock.Children.Add(_textBox);

        _rootBorder = new Border
        {
            Background = KzTokens.BgDefaultBrush,
            BorderBrush = KzTokens.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = KzTokens.RadiusSm,
            Padding = new Thickness(10, 0),
            Height = 32,
            Child = dock
        };
        Content = _rootBorder;
    }

    private void OnInnerTextBoxGotFocus(object? sender, RoutedEventArgs e)
    {
        _rootBorder.BorderBrush = KzTokens.Orange500Brush;
        _rootBorder.BorderThickness = new Thickness(2);
    }

    private void OnInnerTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        _rootBorder.BorderBrush = KzTokens.BorderBrush;
        _rootBorder.BorderThickness = new Thickness(1);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && !_syncing)
        {
            _syncing = true;
            _textBox.Text = change.GetNewValue<string?>() ?? string.Empty;
            _syncing = false;
        }
        else if (change.Property == WatermarkProperty)
        {
            _textBox.Watermark = change.GetNewValue<string?>();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _textBox.Watermark = Watermark;
        if (Text is { } t && _textBox.Text != t) _textBox.Text = t;
    }

    private void OnInnerTextChanged(object? sender, TextChangedEventArgs e)
    {
        var value = _textBox.Text;
        _clearButton.IsVisible = !string.IsNullOrEmpty(value);

        if (!_syncing)
        {
            _syncing = true;
            SetCurrentValue(TextProperty, value);
            _syncing = false;
        }

        // Debounce: restart timer mỗi lần gõ; handler đăng ký đúng 1 lần (tránh cộng dồn Tick).
        if (_debounce is null)
        {
            _debounce = new DispatcherTimer();
            _debounce.Tick += (_, _) =>
            {
                _debounce!.Stop();
                SearchTextChanged?.Invoke(this, _textBox.Text);
            };
        }
        _debounce.Stop();
        _debounce.Interval = DebounceInterval;
        _debounce.Start();
    }
}
