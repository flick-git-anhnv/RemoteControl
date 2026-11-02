using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using KztekComponentAvalonia.Theme;
using System;

namespace KztekComponentAvalonia.Controls;

public class PageChangedEventArgs : EventArgs
{
    public int NewPage { get; }
    public PageChangedEventArgs(int page) => NewPage = page;
}

/// <summary>
/// KZTEK branded pagination bar — First, Prev, manual page input, Next, Last.
/// </summary>
public partial class KzNavigation : UserControl
{
    // ── Styled Properties ──────────────────────────────────────────────────────

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<KzNavigation, int>(nameof(CurrentPage), 1);

    public static readonly StyledProperty<int> TotalPagesProperty =
        AvaloniaProperty.Register<KzNavigation, int>(nameof(TotalPages), 1);

    public static readonly StyledProperty<KzSize> KzSizeProperty =
        AvaloniaProperty.Register<KzNavigation, KzSize>(nameof(KzSize), KzSize.Md);

    // ── Public Properties ──────────────────────────────────────────────────────

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public int TotalPages
    {
        get => GetValue(TotalPagesProperty);
        set => SetValue(TotalPagesProperty, value);
    }

    public KzSize KzSize
    {
        get => GetValue(KzSizeProperty);
        set => SetValue(KzSizeProperty, value);
    }

    // ── Events ─────────────────────────────────────────────────────────────────

    public event EventHandler<PageChangedEventArgs>? PageChanged;

    // ── Private fields ─────────────────────────────────────────────────────────

    private Button?    _btnFirst, _btnPrev, _btnNext, _btnLast;
    private TextBox?   _pageInput;
    private Border?    _inputBorder;
    private TextBlock? _lblTotal;

    private EventHandler<RoutedEventArgs>? _onBtnFirstClick;
    private EventHandler<RoutedEventArgs>? _onBtnPrevClick;
    private EventHandler<RoutedEventArgs>? _onBtnNextClick;
    private EventHandler<RoutedEventArgs>? _onBtnLastClick;
    private EventHandler<FocusChangedEventArgs>? _onPageInputGotFocus;
    private EventHandler<FocusChangedEventArgs>?   _onPageInputLostFocusRing;

    // ── Constructor ────────────────────────────────────────────────────────────

    public KzNavigation()
    {
        InitializeComponent();
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _btnFirst    = this.FindControl<Button>   ("PART_BtnFirst")!;
        _btnPrev     = this.FindControl<Button>   ("PART_BtnPrev")!;
        _btnNext     = this.FindControl<Button>   ("PART_BtnNext")!;
        _btnLast     = this.FindControl<Button>   ("PART_BtnLast")!;
        _pageInput   = this.FindControl<TextBox>  ("PART_PageInput")!;
        _inputBorder = this.FindControl<Border>   ("PART_InputBorder")!;
        _lblTotal    = this.FindControl<TextBlock>("PART_Total")!;

        _onBtnFirstClick = (_, _) => GoToPage(1);
        _onBtnPrevClick  = (_, _) => GoToPage(CurrentPage - 1);
        _onBtnNextClick  = (_, _) => GoToPage(CurrentPage + 1);
        _onBtnLastClick  = (_, _) => GoToPage(TotalPages);
        _btnFirst.Click += _onBtnFirstClick;
        _btnPrev.Click  += _onBtnPrevClick;
        _btnNext.Click  += _onBtnNextClick;
        _btnLast.Click  += _onBtnLastClick;

        _onPageInputGotFocus      = (_, _) => ApplyInputBorderFocus(true);
        _onPageInputLostFocusRing = (_, _) => ApplyInputBorderFocus(false);
        _pageInput.GotFocus  += _onPageInputGotFocus;
        _pageInput.LostFocus += _onPageInputLostFocusRing;

        _pageInput.KeyDown   += OnInputKeyDown;
        _pageInput.LostFocus += OnInputLostFocus;

        ApplySize();
        SyncUI();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_btnFirst  is not null) _btnFirst.Click  -= _onBtnFirstClick;
        if (_btnPrev   is not null) _btnPrev.Click   -= _onBtnPrevClick;
        if (_btnNext   is not null) _btnNext.Click   -= _onBtnNextClick;
        if (_btnLast   is not null) _btnLast.Click   -= _onBtnLastClick;
        if (_pageInput is not null)
        {
            _pageInput.GotFocus  -= _onPageInputGotFocus;
            _pageInput.LostFocus -= _onPageInputLostFocusRing;
            _pageInput.KeyDown   -= OnInputKeyDown;
            _pageInput.LostFocus -= OnInputLostFocus;
        }
    }

    // ── Property changed ───────────────────────────────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CurrentPageProperty || change.Property == TotalPagesProperty)
        {
            // Keep CurrentPage clamped within [1, TotalPages]
            int cp = GetValue(CurrentPageProperty);
            int tp = Math.Max(1, GetValue(TotalPagesProperty));
            int safe = Math.Max(1, Math.Min(cp, tp));
            if (safe != cp)
            {
                SetValue(CurrentPageProperty, safe); // re-fires handler; safe==cp then → SyncUI
                return;
            }
            SyncUI();
        }
        else if (change.Property == KzSizeProperty)
        {
            ApplySize();
        }
    }

    // ── Navigation ─────────────────────────────────────────────────────────────

    private void GoToPage(int page)
    {
        int clamped = Math.Max(1, Math.Min(page, TotalPages));
        if (clamped == CurrentPage) return;
        SetValue(CurrentPageProperty, clamped); // triggers OnPropertyChanged → SyncUI
        PageChanged?.Invoke(this, new PageChangedEventArgs(CurrentPage));
    }

    // ── Input handlers ─────────────────────────────────────────────────────────

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            CommitInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_pageInput is not null)
                _pageInput.Text = CurrentPage.ToString();
            e.Handled = true;
        }
    }

    private void OnInputLostFocus(object? sender, FocusChangedEventArgs e) => CommitInput();

    private void CommitInput()
    {
        if (_pageInput is null) return;
        if (int.TryParse(_pageInput.Text, out int p) && p >= 1 && p <= TotalPages)
            GoToPage(p);
        else
            _pageInput.Text = CurrentPage.ToString();
    }

    // ── UI sync ────────────────────────────────────────────────────────────────

    private void SyncUI()
    {
        if (_pageInput is null) return;
        _pageInput.Text       = CurrentPage.ToString();
        _lblTotal!.Text       = TotalPages.ToString();
        _btnFirst!.IsEnabled  = CurrentPage > 1;
        _btnPrev!.IsEnabled   = CurrentPage > 1;
        _btnNext!.IsEnabled   = CurrentPage < TotalPages;
        _btnLast!.IsEnabled   = CurrentPage < TotalPages;
    }

    private void ApplyInputBorderFocus(bool focused)
    {
        if (_inputBorder is null) return;
        _inputBorder.BorderBrush     = focused ? KzTokens.Orange500Brush : KzTokens.BorderBrush;
        _inputBorder.BorderThickness = new Thickness(focused ? 2 : 1);
    }

    // ── Size ───────────────────────────────────────────────────────────────────

    private void ApplySize()
    {
        if (_btnFirst is null) return;

        double btnSize = KzSize switch
        {
            KzSize.Sm => 28,
            KzSize.Lg => 40,
            _          => 34,
        };

        double fontSize = KzSize switch
        {
            KzSize.Sm => KzTokens.FontSizeSm,
            KzSize.Lg => KzTokens.FontSizeLg,
            _          => KzTokens.FontSizeMd,
        };

        foreach (var btn in new[] { _btnFirst, _btnPrev, _btnNext, _btnLast })
        {
            btn.Width    = btnSize;
            btn.Height   = btnSize;
            btn.FontSize = fontSize;
        }

        if (_inputBorder is not null) _inputBorder.MinHeight = btnSize;
        if (_pageInput   is not null) _pageInput.FontSize    = fontSize;
        if (_lblTotal    is not null) _lblTotal.FontSize      = fontSize;
    }
}
