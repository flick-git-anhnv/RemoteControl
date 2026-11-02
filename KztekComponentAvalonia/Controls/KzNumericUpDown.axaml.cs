using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

public partial class KzNumericUpDown : UserControl
{
    // ── Styled Properties ───────────────────────────────────────────────────
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<KzNumericUpDown, double>(nameof(Value), 0);

    public static readonly StyledProperty<double> MinProperty =
        AvaloniaProperty.Register<KzNumericUpDown, double>(nameof(Min), 0);

    public static readonly StyledProperty<double> MaxProperty =
        AvaloniaProperty.Register<KzNumericUpDown, double>(nameof(Max), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<KzNumericUpDown, double>(nameof(Step), 1);

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<KzNumericUpDown, string?>(nameof(Suffix));

    public static readonly StyledProperty<KzNumericLayout> LayoutProperty =
        AvaloniaProperty.Register<KzNumericUpDown, KzNumericLayout>(nameof(Layout), KzNumericLayout.Horizontal);

    public static readonly StyledProperty<KzNumericVariant> VariantProperty =
        AvaloniaProperty.Register<KzNumericUpDown, KzNumericVariant>(nameof(Variant), KzNumericVariant.Default);

    public static readonly StyledProperty<KzSize> KzSizeProperty =
        AvaloniaProperty.Register<KzNumericUpDown, KzSize>(nameof(KzSize), KzSize.Md);

    // ── Events ───────────────────────────────────────────────────────────────
    public event EventHandler<double>? ValueChanged;

    // ── Properties ───────────────────────────────────────────────────────────
    public double Value
    {
        get => GetValue(ValueProperty);
        set { SetValue(ValueProperty, Clamp(value)); SyncInputs(); }
    }

    public double Min
    {
        get => GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    public double Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public string? Suffix
    {
        get => GetValue(SuffixProperty);
        set { SetValue(SuffixProperty, value); ApplySuffix(value); }
    }

    public KzNumericLayout Layout
    {
        get => GetValue(LayoutProperty);
        set { SetValue(LayoutProperty, value); ApplyLayout(value); }
    }

    public KzNumericVariant Variant
    {
        get => GetValue(VariantProperty);
        set { SetValue(VariantProperty, value); ApplyVariant(value); }
    }

    public KzSize KzSize
    {
        get => GetValue(KzSizeProperty);
        set { SetValue(KzSizeProperty, value); ApplySize(value); }
    }

    public KzNumericUpDown()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Wire up buttons
        if (this.FindControl<KzButton>("IncreaseBtn") is { } incH)
            incH.Click += OnIncrease;
        if (this.FindControl<KzButton>("DecreaseBtn") is { } decH)
            decH.Click += OnDecrease;
        if (this.FindControl<KzButton>("IncreaseBtnV") is { } incV)
            incV.Click += OnIncrease;
        if (this.FindControl<KzButton>("DecreaseBtnV") is { } decV)
            decV.Click += OnDecrease;

        // Wire up text inputs
        if (this.FindControl<TextBox>("ValueInput") is { } inputH)
            inputH.LostFocus += OnInputLostFocus;
        if (this.FindControl<TextBox>("ValueInputV") is { } inputV)
            inputV.LostFocus += OnInputLostFocus;

        ApplyLayout(Layout);
        ApplySuffix(Suffix);
        ApplyVariant(Variant);
        ApplySize(KzSize);
        SyncInputs();
    }

    private void OnIncrease(object? sender, RoutedEventArgs e)
    {
        Value = Clamp(Value + Step);
        ValueChanged?.Invoke(this, Value);
    }

    private void OnDecrease(object? sender, RoutedEventArgs e)
    {
        Value = Clamp(Value - Step);
        ValueChanged?.Invoke(this, Value);
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && double.TryParse(tb.Text, out var parsed))
        {
            Value = Clamp(parsed);
            ValueChanged?.Invoke(this, Value);
        }
        else
        {
            SyncInputs();
        }
    }

    private void SyncInputs()
    {
        var text = Value % 1 == 0 ? $"{(long)Value}" : $"{Value:F2}";
        if (this.FindControl<TextBox>("ValueInput") is { } h)  h.Text = text;
        if (this.FindControl<TextBox>("ValueInputV") is { } v) v.Text = text;
    }

    private void ApplyLayout(KzNumericLayout layout)
    {
        var hPanel = this.FindControl<StackPanel>("HorizontalLayout");
        var vPanel = this.FindControl<StackPanel>("VerticalLayout");
        if (hPanel is null || vPanel is null) return;

        hPanel.IsVisible = layout == KzNumericLayout.Horizontal;
        vPanel.IsVisible = layout == KzNumericLayout.Vertical;
    }

    private void ApplySuffix(string? suffix)
    {
        var visible = !string.IsNullOrEmpty(suffix);

        if (this.FindControl<TextBlock>("SuffixLabel") is { } sh)
        {
            sh.Text = suffix ?? string.Empty;
            sh.IsVisible = visible;
        }
        if (this.FindControl<TextBlock>("SuffixLabelV") is { } sv)
        {
            sv.Text = suffix ?? string.Empty;
            sv.IsVisible = visible;
        }
    }

    private void ApplyVariant(KzNumericVariant variant)
    {
        var incH = this.FindControl<KzButton>("IncreaseBtn");
        var decH = this.FindControl<KzButton>("DecreaseBtn");
        var incV = this.FindControl<KzButton>("IncreaseBtnV");
        var decV = this.FindControl<KzButton>("DecreaseBtnV");

        var buttons = new[] { incH, decH, incV, decV };
        foreach (var btn in buttons)
        {
            if (btn is null) continue;
            btn.Variant = variant switch
            {
                KzNumericVariant.Navy   => KzButtonVariant.Primary,
                KzNumericVariant.Accent => KzButtonVariant.Accent,
                _                       => KzButtonVariant.Secondary
            };
        }
    }

    private void ApplySize(KzSize size)
    {
        double btnSize  = size switch { KzSize.Sm => 32, KzSize.Lg => 48, _ => 40 };
        double inputH   = btnSize;
        double inputW   = size switch { KzSize.Sm => 64, KzSize.Lg => 96, _ => 80 };
        string sizeClass = size switch { KzSize.Sm => "kz-size-sm", KzSize.Lg => "kz-size-lg", _ => "kz-size-md" };

        foreach (var name in new[] { "IncreaseBtn", "DecreaseBtn" })
        {
            if (this.FindControl<KzButton>(name) is not { } btn) continue;
            btn.Width  = btnSize;
            btn.Height = btnSize;
            btn.Classes.Remove("kz-size-sm"); btn.Classes.Remove("kz-size-md"); btn.Classes.Remove("kz-size-lg");
            btn.Classes.Add(sizeClass);
        }

        if (this.FindControl<TextBox>("ValueInput") is { } tb)  tb.Height = inputH;
        if (this.FindControl<Border>("ValueInputBorder") is { } b) b.Width = inputW;
    }

    private double Clamp(double v) => Math.Max(Min, Math.Min(Max, v));
}
