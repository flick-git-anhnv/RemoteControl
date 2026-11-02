using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Rounded-corner button — port of WinForms KzArcButton.
/// Uses DrawingContext.DrawRectangle() with corner radius instead of GDI+ GraphicsPath/Region.
/// All rendering is explicit in Render(DrawingContext) — no ControlTemplate required.
/// </summary>
public class KzArcButton : Control
{
    // ── Styled Properties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<double> BorderRadiusProperty =
        AvaloniaProperty.Register<KzArcButton, double>(nameof(BorderRadius), 0d);

    public static readonly StyledProperty<double> BorderSizeProperty =
        AvaloniaProperty.Register<KzArcButton, double>(nameof(BorderSize), 0d);

    public static readonly StyledProperty<IBrush?> BorderColorProperty =
        AvaloniaProperty.Register<KzArcButton, IBrush?>(nameof(BorderColor), Brushes.White);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<KzArcButton, IBrush?>(nameof(Background), Brushes.White);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<KzArcButton, IBrush?>(nameof(Foreground), Brushes.Black);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<KzArcButton, string?>(nameof(Text));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<KzArcButton, double>(nameof(FontSize), 14d);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<KzArcButton, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<KzArcButton, FontWeight>(nameof(FontWeight), FontWeight.Normal);

    // Explicit hover / pressed background colors (parity with Siticone/Guna buttons which
    // change fill on state). When null, a generic lighten(hover)/darken(pressed) overlay
    // reproduces the WinForms FlatStyle.Flat default hover/pressed feel.
    public static readonly StyledProperty<IBrush?> HoverColorProperty =
        AvaloniaProperty.Register<KzArcButton, IBrush?>(nameof(HoverColor));

    public static readonly StyledProperty<IBrush?> HoverForegroundProperty =
        AvaloniaProperty.Register<KzArcButton, IBrush?>(nameof(HoverForeground));

    public static readonly StyledProperty<IBrush?> PressedColorProperty =
        AvaloniaProperty.Register<KzArcButton, IBrush?>(nameof(PressedColor));

    public static readonly StyledProperty<IBrush?> PressedForegroundProperty =
        AvaloniaProperty.Register<KzArcButton, IBrush?>(nameof(PressedForeground));

    /// <summary>Vẽ mũi tên quay lại (←) bên trái chữ — dùng cho nút "Quay lại" (Classes="back").</summary>
    public static readonly StyledProperty<bool> ShowBackArrowProperty =
        AvaloniaProperty.Register<KzArcButton, bool>(nameof(ShowBackArrow));

    public bool ShowBackArrow { get => GetValue(ShowBackArrowProperty); set => SetValue(ShowBackArrowProperty, value); }

    /// <summary>Phần bên phải nút dành cho thứ khác đè lên (VD vòng đếm ngược): chữ / mũi tên canh giữa trong phần còn lại, không dư khoảng trống bên trái.</summary>
    public static readonly StyledProperty<double> ContentInsetRightProperty =
        AvaloniaProperty.Register<KzArcButton, double>(nameof(ContentInsetRight));

    public double ContentInsetRight { get => GetValue(ContentInsetRightProperty); set => SetValue(ContentInsetRightProperty, value); }

    // ── CLR wrappers ──────────────────────────────────────────────────────────

    public double  BorderRadius { get => GetValue(BorderRadiusProperty); set => SetValue(BorderRadiusProperty, value); }
    public double  BorderSize   { get => GetValue(BorderSizeProperty);   set => SetValue(BorderSizeProperty,   value); }
    public IBrush? BorderColor  { get => GetValue(BorderColorProperty);  set => SetValue(BorderColorProperty,  value); }
    public IBrush? Background   { get => GetValue(BackgroundProperty);   set => SetValue(BackgroundProperty,   value); }
    public IBrush? Foreground   { get => GetValue(ForegroundProperty);   set => SetValue(ForegroundProperty,   value); }
    public string? Text         { get => GetValue(TextProperty);         set => SetValue(TextProperty,         value); }
    public double  FontSize     { get => GetValue(FontSizeProperty);     set => SetValue(FontSizeProperty,     value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty);  set => SetValue(FontFamilyProperty,   value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty);  set => SetValue(FontWeightProperty,   value); }
    public IBrush? HoverColor   { get => GetValue(HoverColorProperty);   set => SetValue(HoverColorProperty,   value); }
    public IBrush? HoverForeground { get => GetValue(HoverForegroundProperty); set => SetValue(HoverForegroundProperty, value); }
    public IBrush? PressedColor { get => GetValue(PressedColorProperty); set => SetValue(PressedColorProperty, value); }
    public IBrush? PressedForeground { get => GetValue(PressedForegroundProperty); set => SetValue(PressedForegroundProperty, value); }

    // Runtime state (not styled — repainted via InvalidateVisual on pointer events)
    private bool _isPressed;

    // WinForms KzArcButton properties preserved for behavior parity
    public bool    IsRequired    { get; set; }
    public string? CurrentValue  { get; set; }
    public bool    IsPassword    { get; set; }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler? Click;

    // ── Static ctor ───────────────────────────────────────────────────────────

    static KzArcButton()
    {
        AffectsRender<KzArcButton>(
            BorderRadiusProperty, BorderSizeProperty, BorderColorProperty,
            BackgroundProperty, ForegroundProperty, TextProperty, FontSizeProperty,
            FontFamilyProperty, FontWeightProperty, HoverColorProperty, PressedColorProperty,
            HoverForegroundProperty, PressedForegroundProperty, ShowBackArrowProperty, ContentInsetRightProperty);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public KzArcButton()
    {


        Cursor = new Cursor(StandardCursorType.Hand);
    }

    // ── Pointer events ────────────────────────────────────────────────────────

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPressed = false; // cancel press if pointer leaves (matches WinForms button)
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        _isPressed = true;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true;
        _isPressed = false;
        // Fire Click only when pointer released inside bounds (same as WinForms Button)
        if (new Rect(0, 0, Bounds.Width, Bounds.Height).Contains(e.GetPosition(this)))
            Click?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;

        // Clamp radius so it cannot exceed half the shorter side (mirrors WinForms Button_Resize guard)
        double r = Math.Min(BorderRadius, Math.Min(w, h) / 2.0);

        IBrush baseBg = Background ?? Brushes.White;
        IBrush baseFg = Foreground ?? Brushes.Black;
        IPen?  pen = BorderSize > 0
            ? new Pen(BorderColor ?? Brushes.Transparent, BorderSize)
            : null;

        // Resolve state background (parity with WinForms flat-style hover/pressed).
        // Priority: explicit PressedColor/HoverColor → else translucent overlay applied after.
        IBrush bg = baseBg;
        if (_isPressed && PressedColor != null)
            bg = PressedColor;
        else if (IsPointerOver && !_isPressed && HoverColor != null)
            bg = HoverColor;

        var rect = new Rect(0, 0, w, h);

        // Background + optional border
        if (r > 0)
            ctx.DrawRectangle(bg, pen, rect, r, r);
        else
            ctx.DrawRectangle(bg, pen, rect);

        // Generic state overlay when no explicit hover/pressed color supplied.
        // Pressed → darken (black 0x33); Hover → lighten (white 0x26) — mirrors the
        // default FlatStyle.Flat MouseOver/MouseDown tint of the WinForms original.
        IBrush? overlay = null;
        if (_isPressed && PressedColor == null)
            overlay = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0));
        else if (IsPointerOver && !_isPressed && HoverColor == null)
            overlay = new SolidColorBrush(Color.FromArgb(0x26, 255, 255, 255));

        if (overlay != null)
        {
            if (r > 0) ctx.DrawRectangle(overlay, null, rect, r, r);
            else       ctx.DrawRectangle(overlay, null, rect);
        }

        // Text — centered, equivalent to WinForms base.OnPaint text rendering
        if (!string.IsNullOrEmpty(Text))
        {
            // ⚠️ FIX (2026-07-27): khi ĐANG BẤM mà không khai báo PressedForeground thì logic cũ
            // rơi hết cả 2 nhánh (nhánh sau đòi `!_isPressed`) → chữ trở về màu gốc (đen) TRONG KHI
            // nền đã đổi sang màu nhấn → lúc chạm nút, chữ đen trên nền cam, tương phản kém và
            // nhìn như lỗi nhấp nháy. Không view nào trong app đặt PressedForeground, nên mọi nút
            // đều bị.
            // Nay: pressed thừa hưởng HoverForeground khi không có PressedForeground riêng.
            IBrush textFg = _isPressed
                ? (PressedForeground ?? HoverForeground ?? baseFg)
                : (IsPointerOver ? (HoverForeground ?? baseFg) : baseFg);

            var ft = new FormattedText(
                Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle.Normal, FontWeight),
                FontSize,
                textFg);

            if (ShowBackArrow)
            {
                // ← bên trái chữ; cả cụm (mũi tên + khoảng hở + chữ) canh giữa nút.
                double aw = FontSize * 0.85, gap = FontSize * 0.45;
                double x0 = (w - ContentInsetRight - (aw + gap + ft.Width)) / 2.0;
                double cy = h / 2.0, hd = FontSize * 0.32;
                var arrowPen = new Pen(textFg, Math.Max(2, FontSize * 0.11), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                ctx.DrawLine(arrowPen, new Point(x0 + aw, cy), new Point(x0, cy));
                ctx.DrawLine(arrowPen, new Point(x0 + hd, cy - hd), new Point(x0, cy));
                ctx.DrawLine(arrowPen, new Point(x0 + hd, cy + hd), new Point(x0, cy));
                ctx.DrawText(ft, new Point(x0 + aw + gap, (h - ft.Height) / 2.0));
            }
            else
                ctx.DrawText(ft, new Point((w - ContentInsetRight - ft.Width) / 2.0, (h - ft.Height) / 2.0));
        }
    }
}
