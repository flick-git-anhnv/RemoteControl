using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Port of the WinForms PictureBox icon-buttons used in ucPaymentInfo / bike payment
/// (btnQrCode, btnVisaCard, btnCash, btnVoucher).
///
/// WinForms parity:
///   - Hiển thị NormalImage ở trạng thái thường.
///   - MouseEnter → đổi sang HoverImage (parity: BtnQrCode_MouseEnter đổi Image sang *_red.png).
///   - MouseLeave → trả về NormalImage.
///   - SizeMode = Zoom → Stretch = Uniform.
///   - Click → raise Click event.
///   - Disabled → mờ 40% (parity Enabled=false của PictureBox).
/// </summary>
public class KzHoverImageButton : Control
{
    private Bitmap? _normal;
    private Bitmap? _hover;
    private bool _isHover;

    public static readonly StyledProperty<string> NormalImageProperty =
        AvaloniaProperty.Register<KzHoverImageButton, string>(nameof(NormalImage), string.Empty);

    public static readonly StyledProperty<string> HoverImageProperty =
        AvaloniaProperty.Register<KzHoverImageButton, string>(nameof(HoverImage), string.Empty);

    public string NormalImage
    {
        get => GetValue(NormalImageProperty);
        set => SetValue(NormalImageProperty, value);
    }

    public string HoverImage
    {
        get => GetValue(HoverImageProperty);
        set => SetValue(HoverImageProperty, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<KzHoverImageButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    public event System.EventHandler<RoutedEventArgs> Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    static KzHoverImageButton()
    {
        AffectsRender<KzHoverImageButton>(NormalImageProperty, HoverImageProperty);
        NormalImageProperty.Changed.AddClassHandler<KzHoverImageButton>((c, _) => c.LoadBitmaps());
        HoverImageProperty.Changed.AddClassHandler<KzHoverImageButton>((c, _) => c.LoadBitmaps());
    }

    public KzHoverImageButton()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadBitmaps();
    }

    private static Bitmap? Load(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return null;
        try
        {
            var u = new Uri(uri);
            if (u.Scheme == "avares")
            {
                using var s = AssetLoader.Open(u);
                return new Bitmap(s);
            }
            return new Bitmap(u.LocalPath);
        }
        catch { return null; }
    }

    private void LoadBitmaps()
    {
        _normal = Load(NormalImage);
        _hover  = Load(HoverImage);
        InvalidateVisual();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (!IsEffectivelyEnabled) return;
        _isHover = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isHover = false;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsEffectivelyEnabled && e.InitialPressMouseButton == MouseButton.Left)
            RaiseEvent(new RoutedEventArgs(ClickEvent));
    }

    public override void Render(DrawingContext ctx)
    {
        // ⚠️ PHẢI tô nền trong suốt phủ trọn Bounds TRƯỚC khi vẽ ảnh.
        // Avalonia hit-test theo hình đã VẼ, không theo Bounds: control này chỉ vẽ bitmap qua
        // DrawImage nên vùng ngoài ảnh (và vùng ảnh trong suốt) KHÔNG nhận chạm. Hệ quả đã quan sát
        // trực tiếp: nút "Bắt đầu" ở màn chờ và 4 nút QR/Visa/Cash/Voucher ở màn thanh toán bấm
        // trượt liên tục — trên kiosk cảm ứng gần như không dùng được. Tô Transparent làm cả khung
        // trở thành vùng chạm mà không thay đổi hình hiển thị.
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        var bmp = _isHover ? (_hover ?? _normal) : _normal;
        if (bmp == null) return;

        // SizeMode = Zoom (Uniform): giữ tỉ lệ, canh giữa.
        double bw = bmp.PixelSize.Width, bh = bmp.PixelSize.Height;
        double aw = Bounds.Width, ah = Bounds.Height;
        if (bw <= 0 || bh <= 0 || aw <= 0 || ah <= 0) return;
        double scale = System.Math.Min(aw / bw, ah / bh);
        double dw = bw * scale, dh = bh * scale;
        var dest = new Rect((aw - dw) / 2, (ah - dh) / 2, dw, dh);

        using (ctx.PushOpacity(IsEffectivelyEnabled ? 1.0 : 0.4))
        {
            ctx.DrawImage(bmp, dest);
        }
    }
}
