using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using System;

namespace KztekComponentAvalonia.Controls;

public class CroppedEventArgs : EventArgs
{
    /// <summary>Selection rect in image pixel coordinates (integer, matches bitmap pixel grid).</summary>
    public PixelRect ImageRect { get; }
    public CroppedEventArgs(PixelRect imageRect) => ImageRect = imageRect;
}

/// <summary>
/// Image crop control: displays a bitmap, user drags to select a region, fires Cropped event.
/// </summary>
public partial class KzCutImage : UserControl
{
    // ── Styled Properties ──────────────────────────────────────────────────────

    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<KzCutImage, Bitmap?>(nameof(Source));

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    // ── Events ─────────────────────────────────────────────────────────────────

    public event EventHandler<CroppedEventArgs>? Cropped;
    public event EventHandler?                   Cancelled;

    // ── Privates ───────────────────────────────────────────────────────────────

    private Image?      _image;
    private Canvas?     _overlay;
    private Border?     _selectionRect;
    private KzButton?   _btnCrop, _btnCancel;
    private TextBlock?  _txtInfo;

    private bool  _isDragging;
    private Point _dragStart;
    private Rect  _selection;

    private EventHandler<RoutedEventArgs>? _onBtnCropClick;
    private EventHandler<RoutedEventArgs>? _onBtnCancelClick;

    public KzCutImage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _image         = this.FindControl<Image>    ("PART_Image")!;
        _overlay       = this.FindControl<Canvas>   ("PART_OverlayCanvas")!;
        _selectionRect = this.FindControl<Border>   ("PART_SelectionRect")!;
        _btnCrop       = this.FindControl<KzButton> ("PART_BtnCrop")!;
        _btnCancel     = this.FindControl<KzButton> ("PART_BtnCancel")!;
        _txtInfo       = this.FindControl<TextBlock>("PART_TxtInfo")!;

        _overlay.PointerPressed  += OnPointerPressed;
        _overlay.PointerMoved    += OnPointerMoved;
        _overlay.PointerReleased += OnPointerReleased;

        _onBtnCropClick   = (_, _) => CommitCrop();
        _onBtnCancelClick = (_, _) => { ClearSelection(); Cancelled?.Invoke(this, EventArgs.Empty); };
        _btnCrop.Click   += _onBtnCropClick;
        _btnCancel.Click += _onBtnCancelClick;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_overlay is not null)
        {
            _overlay.PointerPressed  -= OnPointerPressed;
            _overlay.PointerMoved    -= OnPointerMoved;
            _overlay.PointerReleased -= OnPointerReleased;
        }
        if (_btnCrop   is not null) _btnCrop.Click   -= _onBtnCropClick;
        if (_btnCancel is not null) _btnCancel.Click -= _onBtnCancelClick;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty && _image is not null)
        {
            _image.Source = Source;
            ClearSelection();
        }
    }

    // ── Mouse ──────────────────────────────────────────────────────────────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _dragStart  = e.GetPosition(_overlay);
        ClearSelection();
        e.Pointer.Capture(_overlay);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        var pt  = e.GetPosition(_overlay);
        var x   = Math.Min(_dragStart.X, pt.X);
        var y   = Math.Min(_dragStart.Y, pt.Y);
        var w   = Math.Abs(pt.X - _dragStart.X);
        var h   = Math.Abs(pt.Y - _dragStart.Y);
        _selection = new Rect(x, y, w, h);
        UpdateSelectionUI();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);

        bool hasSelection = _selection.Width >= 4 && _selection.Height >= 4;
        if (_btnCrop is not null) _btnCrop.IsEnabled = hasSelection;
        if (_txtInfo is not null)
            _txtInfo.Text = hasSelection
                ? $"Vùng chọn: {(int)_selection.Width}×{(int)_selection.Height}px — nhấn 'Cắt ảnh' để xác nhận"
                : "Kéo để chọn vùng cắt";

        e.Handled = true;
    }

    // ── Selection UI ──────────────────────────────────────────────────────────

    private void UpdateSelectionUI()
    {
        if (_selectionRect is null || _overlay is null) return;

        _selectionRect.IsVisible = true;
        _selectionRect.Margin    = new Thickness(_selection.X, _selection.Y, 0, 0);
        _selectionRect.Width     = Math.Max(0, _selection.Width);
        _selectionRect.Height    = Math.Max(0, _selection.Height);
    }

    private void ClearSelection()
    {
        _selection = default;
        if (_selectionRect is not null) _selectionRect.IsVisible = false;
        if (_btnCrop is not null) _btnCrop.IsEnabled = false;
        if (_txtInfo is not null) _txtInfo.Text = "Kéo để chọn vùng cắt";
    }

    // ── Crop ──────────────────────────────────────────────────────────────────

    private void CommitCrop()
    {
        if (_image is null || Source is null || _selection.Width < 4) return;

        var pixelRect = ToImagePixelCoords(_selection);
        Cropped?.Invoke(this, new CroppedEventArgs(pixelRect));
        ClearSelection();
    }

    /// <summary>Converts canvas (overlay) coordinates to integer image pixel coords, accounting for Stretch=Uniform letterboxing.</summary>
    private PixelRect ToImagePixelCoords(Rect canvasRect)
    {
        if (Source is null || _overlay is null) return default(PixelRect);

        double cw = _overlay.Bounds.Width;
        double ch = _overlay.Bounds.Height;
        double iw = Source.PixelSize.Width;
        double ih = Source.PixelSize.Height;

        if (cw <= 0 || ch <= 0 || iw <= 0 || ih <= 0) return default(PixelRect);

        double scale = Math.Min(cw / iw, ch / ih);
        double offX  = (cw - iw * scale) / 2;
        double offY  = (ch - ih * scale) / 2;

        double x = (canvasRect.X - offX) / scale;
        double y = (canvasRect.Y - offY) / scale;
        double w = canvasRect.Width  / scale;
        double h = canvasRect.Height / scale;

        // Clamp to image bounds before rounding
        x = Math.Max(0, x);
        y = Math.Max(0, y);
        w = Math.Min(w, iw - x);
        h = Math.Min(h, ih - y);

        return new PixelRect((int)Math.Round(x), (int)Math.Round(y),
                             (int)Math.Round(w), (int)Math.Round(h));
    }
}
