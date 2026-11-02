using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK Design System virtual keyboard — Alpha / Numeric / Special modes,
/// Portrait and Landscape orientations, with Vietnamese Telex input.
/// </summary>
public class KzKeyboard : Control
{
    // ── Public enums ─────────────────────────────────────────────────────────
    public enum KzKeyboardOrientation { Portrait, Landscape }
    public enum KzKeyboardMode       { Alpha, Numeric, Special }

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<string>?               KeyPressed;
    public event EventHandler<KzKeyboardOrientation>? OrientationChanged;

    // ── Key definition ────────────────────────────────────────────────────────
    private record struct KD(string Lbl, string LblUp, string Val, string ValUp, double W, bool Special);

    private static KD A(string lc) => new(lc, lc.ToUpperInvariant(), lc, lc.ToUpperInvariant(), 1.0, false);
    private static KD C(string ch) => new(ch, ch, ch, ch, 1.0, false);
    private static KD S(string lbl, string cmd, double w = 1.5) => new(lbl, lbl, cmd, cmd, w, true);
    private static KD SP(double w = 3.5) => new("Space", "Space", " ", " ", w, true);

    // ── Key layout tables ─────────────────────────────────────────────────────
    private static readonly KD[][] AlphaRows =
    {
        new[] { A("q"),A("w"),A("e"),A("r"),A("t"),A("y"),A("u"),A("i"),A("o"),A("p") },
        new[] { A("a"),A("s"),A("d"),A("f"),A("g"),A("h"),A("j"),A("k"),A("l") },
        new[] { S("⇧","SHIFT",1.5), A("z"),A("x"),A("c"),A("v"),A("b"),A("n"),A("m"), S("⌫","BACK",1.5) },
        new[] { S("?123","NUM",1.5), C(","), SP(3.5), C("."), S("VIE","TELEX",1.2), S("↵","ENTER",1.5) }
    };

    private static readonly KD[][] NumRows =
    {
        new[] { C("1"),C("2"),C("3"),C("4"),C("5"),C("6"),C("7"),C("8"),C("9"),C("0") },
        new[] { C("@"),C("#"),C("$"),C("%"),C("^"),C("-"),C("+"),C("("),C(")"),C("/") },
        new[] { S("#+=","SPEC",1.5), C("*"),C("\""),C("'"),C(":"),C(";"),C("!"),C("?"), S("⌫","BACK",1.5) },
        new[] { S("ABC","ALPHA",1.5), C(","), SP(4.0), C("."), S("↵","ENTER",1.5) }
    };

    private static readonly KD[][] SpecRows =
    {
        new[] { C("["),C("]"),C("{"),C("}"),C("<"),C(">"),C("="),C("_"),C("\\"),C("|") },
        new[] { C("~"),C("^"),C("`"),C("+"),C("-"),C("*"),C("/"),C(":"),C(";"),C("@") },
        new[] { S("?123","NUM",1.5), C("."),C(","),C(":"),C(";"),C("!"),C("?"), S("⌫","BACK",1.5) },
        new[] { S("ABC","ALPHA",1.5), C("-"), SP(4.0), C("/"), S("↵","ENTER",1.5) }
    };

    // ── Avalonia Styled Properties ────────────────────────────────────────────
    public static readonly StyledProperty<KzKeyboardOrientation> OrientationProperty =
        AvaloniaProperty.Register<KzKeyboard, KzKeyboardOrientation>(
            nameof(Orientation), KzKeyboardOrientation.Portrait);

    public static readonly StyledProperty<TextBox?> TargetTextBoxProperty =
        AvaloniaProperty.Register<KzKeyboard, TextBox?>(nameof(TargetTextBox));

    public static readonly StyledProperty<bool> TelexEnabledProperty =
        AvaloniaProperty.Register<KzKeyboard, bool>(nameof(TelexEnabled), false);

    public KzKeyboardOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set
        {
            if (GetValue(OrientationProperty) == value) return;
            SetValue(OrientationProperty, value);
            ApplyOrientation(value);
            InvalidateVisual();
        }
    }

    public TextBox? TargetTextBox
    {
        get => GetValue(TargetTextBoxProperty);
        set => SetValue(TargetTextBoxProperty, value);
    }

    public bool TelexEnabled
    {
        get => GetValue(TelexEnabledProperty);
        set
        {
            if (GetValue(TelexEnabledProperty) == value) return;
            SetValue(TelexEnabledProperty, value);
            TelexResetSyllable();
            InvalidateVisual();
        }
    }

    /// <summary>Current key layout. Lets the host preselect Numeric for number-only fields.</summary>
    public KzKeyboardMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            TelexResetSyllable();
            InvalidateVisual();
        }
    }

    // ── State ─────────────────────────────────────────────────────────────────
    private KzKeyboardMode _mode           = KzKeyboardMode.Alpha;
    private bool           _shifted        = false;
    private bool           _capsLock       = false;
    private string?        _pressedVal;
    private long           _pressTimestamp;
    private bool           _isPointerDown;
    private DispatcherTimer? _clearTimer;
    private static readonly Typeface KeyTypeface = new(new FontFamily("Segoe UI, Inter, Roboto, Noto Sans, sans-serif"));

    private readonly List<(Rect R, KD Key)> _layout = new();

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly IBrush KeyBg     = Brushes.White;
    private static readonly IBrush KeyBgSpec = new SolidColorBrush(Color.Parse("#CBC7BF"));
    private static readonly IBrush KeyFg     = new SolidColorBrush(Color.Parse("#4A4A4A"));
    private static readonly IBrush KbBg      = new SolidColorBrush(Color.Parse("#F0EBE1"));
    private static readonly IBrush PressedBg = KzTokens.Navy900Brush;
    private static readonly IBrush PressedFg = Brushes.White;
    private static readonly IBrush Shadow    = new SolidColorBrush(Color.FromArgb(22, 0, 0, 0));
    private static readonly IPen   Border    = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 0.5);
    private const double           Pad       = 8;
    private const double           Gap       = 9;
    private const double           KeyRadius = 8;

    // ── Portrait / Landscape sizes ────────────────────────────────────────────
    public static readonly Size PortraitSize  = new(390, 300);
    public static readonly Size LandscapeSize = new(640, 220);

    // ── Constructor ───────────────────────────────────────────────────────────
    public KzKeyboard()
    {
        Width  = PortraitSize.Width;
        Height = PortraitSize.Height;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    // ── Public methods ────────────────────────────────────────────────────────
    public void ToggleOrientation() =>
        Orientation = Orientation == KzKeyboardOrientation.Portrait
            ? KzKeyboardOrientation.Landscape
            : KzKeyboardOrientation.Portrait;

    private void ApplyOrientation(KzKeyboardOrientation o)
    {
        if (o == KzKeyboardOrientation.Portrait)
        { Width = PortraitSize.Width;  Height = PortraitSize.Height; }
        else
        { Width = LandscapeSize.Width; Height = LandscapeSize.Height; }
        OrientationChanged?.Invoke(this, o);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────
    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        _layout.Clear();

        var rows = _mode switch
        {
            KzKeyboardMode.Numeric => NumRows,
            KzKeyboardMode.Special => SpecRows,
            _                      => AlphaRows
        };

        double W     = Bounds.Width  - Pad * 2;
        double H     = Bounds.Height - Pad * 2;
        int    nRows = rows.Length;
        double rowH  = (H - Gap * (nRows - 1)) / nRows;
        double y     = Pad;

        foreach (var row in rows)
        {
            double totalW = 0;
            foreach (var k in row) totalW += k.W;
            double unitW = (W - Gap * (row.Length - 1)) / totalW;
            double x = Pad;

            foreach (var key in row)
            {
                var rect = new Rect(x, y, unitW * key.W, rowH);
                _layout.Add((rect, key));
                DrawKey(ctx, rect, key);
                x += unitW * key.W + Gap;
            }
            y += rowH + Gap;
        }
    }

    private void DrawKey(DrawingContext ctx, Rect r, KD key)
    {
        string val         = ResolveVal(key);
        bool   pressed     = _pressedVal == val;
        bool   shiftActive = val == "SHIFT" && (_shifted || _capsLock);
        bool   telexActive = val == "TELEX" && TelexEnabled;

        IBrush bg = pressed || shiftActive || telexActive ? PressedBg
                  : key.Special                           ? KeyBgSpec
                  : KeyBg;
        IBrush fg = (pressed || shiftActive || telexActive) ? PressedFg : KeyFg;

        // Telex key label reflects the CURRENT active mode (matches the dark/active highlight),
        // not the mode you'd switch into — VIE lit up while Telex is on, ENG while it's off.
        string lbl = val == "TELEX"
            ? (TelexEnabled ? "VIE" : "ENG")
            : val == "SHIFT"
                ? (_capsLock ? "⇪" : _shifted ? "⬆" : "⇧")
                : ResolveLabel(key);

        // Drop shadow
        if (!pressed)
        {
            var shadowRect = new Rect(r.X + 1, r.Y + 2, r.Width, r.Height);
            ctx.DrawRectangle(Shadow, null, shadowRect, KeyRadius, KeyRadius);
        }

        // Key body
        ctx.DrawRectangle(bg, Border, r, KeyRadius, KeyRadius);

        if (lbl.Length == 0) return;

        double fs = Math.Clamp(r.Height * 0.34, 7.5, 34.0);

        if      (lbl.Length >= 9) fs = Math.Max(fs * 0.52, 6.0);
        else if (lbl.Length >= 5) fs = Math.Max(fs * 0.85, 7.0);
        else if (lbl.Length >= 3) fs = Math.Max(fs * 0.90, 7.0);

        var ft = new FormattedText(
            lbl,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            KeyTypeface,
            fs,
            fg);

        var textOrigin = new Point(
            r.X + (r.Width  - ft.Width)  / 2,
            r.Y + (r.Height - ft.Height) / 2);

        ctx.DrawText(ft, textOrigin);
    }

    private string ResolveLabel(KD k) =>
        k.Special ? k.Lbl : (_shifted || _capsLock ? k.LblUp : k.Lbl);

    private string ResolveVal(KD k) =>
        k.Special ? k.Val : (_shifted || _capsLock ? k.ValUp : k.Val);

    // ── Pointer handling ──────────────────────────────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);

        foreach (var (rect, key) in _layout)
        {
            if (!rect.Contains(pos)) continue;
            string val = ResolveVal(key);
            _pressedVal = val;
            InvalidateVisual();
            ProcessKey(val);

            _pressTimestamp = Stopwatch.GetTimestamp();
            _isPointerDown = true;
            if (val == "BACK") StartBackspaceRepeat(); else StopBackspaceRepeat();
            _clearTimer?.Stop();
            _clearTimer = null;
            e.Pointer.Capture(this);
            e.Handled = true;

            break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        StopBackspaceRepeat();
        if (_isPointerDown)
        {
            _isPointerDown = false;
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
            e.Handled = true;

            long elapsedMs = (Stopwatch.GetTimestamp() - _pressTimestamp) * 1000 / Stopwatch.Frequency;
            const long minFlashDurationMs = 70;
            if (elapsedMs < minFlashDurationMs)
            {
                long delay = minFlashDurationMs - elapsedMs;
                _clearTimer?.Stop();
                _clearTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(delay)
                };
                _clearTimer.Tick += (_, _) =>
                {
                    _clearTimer.Stop();
                    _clearTimer = null;
                    if (!_isPointerDown)
                    {
                        _pressedVal = null;
                        InvalidateVisual();
                    }
                };
                _clearTimer.Start();
            }
            else
            {
                _pressedVal = null;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        StopBackspaceRepeat();
        _isPointerDown = false;
        _clearTimer?.Stop();
        _clearTimer = null;
        if (_pressedVal != null)
        {
            _pressedVal = null;
            InvalidateVisual();
        }
    }

    // ── Giu nut xoa (⌫) de xoa lien tuc: tre 400ms, sau do 70ms/ky tu, tang toc xuong 35ms sau 12 ky tu ──
    private DispatcherTimer? _repeatTimer;
    private int _repeatCount;

    private void StartBackspaceRepeat()
    {
        StopBackspaceRepeat();
        _repeatCount = 0;
        _repeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _repeatTimer.Tick += (_, _) =>
        {
            if (!_isPointerDown || _repeatTimer is null)
            {
                StopBackspaceRepeat();
                return;
            }

            _repeatCount++;
            _repeatTimer.Interval = TimeSpan.FromMilliseconds(_repeatCount > 12 ? 35 : 70);
            ProcessKey("BACK");
        };
        _repeatTimer.Start();
    }

    private void StopBackspaceRepeat()
    {
        _repeatTimer?.Stop();
        _repeatTimer = null;
    }

    private void ProcessKey(string val)
    {
        switch (val)
        {
            case "SHIFT":
                if (_capsLock)     { _capsLock = false; _shifted = false; }
                else if (_shifted) { _shifted  = false; _capsLock = true; }
                else               { _shifted  = true; }
                InvalidateVisual();
                return;

            case "TELEX":
                TelexEnabled = !TelexEnabled;
                return;

            case "BACK":
                DoBackspace();
                KeyPressed?.Invoke(this, "BACKSPACE");
                return;

            case "ENTER":
                TelexResetSyllable();
                if (TargetTextBox?.AcceptsReturn == true)
                    TypeTextRaw("\n");
                KeyPressed?.Invoke(this, "ENTER");
                return;

            case "FLIP":
                ToggleOrientation();
                return;

            case "NUM":   _mode = KzKeyboardMode.Numeric; TelexResetSyllable(); InvalidateVisual(); return;
            case "ALPHA": _mode = KzKeyboardMode.Alpha;   TelexResetSyllable(); InvalidateVisual(); return;
            case "SPEC":  _mode = KzKeyboardMode.Special; TelexResetSyllable(); InvalidateVisual(); return;

            case " ":
                TelexResetSyllable();
                TypeTextRaw(" ");
                if (_shifted && !_capsLock) { _shifted = false; InvalidateVisual(); }
                KeyPressed?.Invoke(this, val);
                return;

            default:
                if (TelexEnabled && val.Length == 1 && char.IsLetter(val[0]))
                    TypeTelex(val);
                else
                {
                    TelexResetSyllable();
                    TypeTextRaw(val);
                    if (_shifted && !_capsLock) { _shifted = false; InvalidateVisual(); }
                }
                KeyPressed?.Invoke(this, val);
                return;
        }
    }

    // ── Telex input logic ─────────────────────────────────────────────────────

    private string GetTextBeforeCursor()
    {
        var tb = TargetTextBox;
        if (tb == null) return "";
        return (tb.Text ?? "").Substring(0, System.Math.Max(0, tb.CaretIndex));
    }

    private void TypeTelex(string val)
    {
        if (TargetTextBox == null) return;
        DeleteSelection();
        string before = GetTextBeforeCursor();
        var (del, ins) = KzTelexEngine.Process(before, val[0]);
        for (int i = 0; i < del; i++) DoBackspaceRaw();
        TypeTextRaw(ins);

        if (_shifted && !_capsLock) { _shifted = false; InvalidateVisual(); }
    }

    private void TelexResetSyllable()
    {
        // No longer needed for stateless KzTelexEngine
    }

    // ── Text target helpers ───────────────────────────────────────────────────

    /// <summary>Xoa doan van ban dang duoc boi chon (neu co). Tra ve true neu da xoa.</summary>
    private bool DeleteSelection()
    {
        var tb = TargetTextBox;
        if (tb is null) return false;
        string txt = tb.Text ?? "";
        int a = Math.Clamp(Math.Min(tb.SelectionStart, tb.SelectionEnd), 0, txt.Length);
        int b = Math.Clamp(Math.Max(tb.SelectionStart, tb.SelectionEnd), 0, txt.Length);
        if (a == b) return false;
        tb.Text = txt.Remove(a, b - a);
        tb.CaretIndex = a;
        tb.SelectionStart = a;
        tb.SelectionEnd = a;
        return true;
    }

    private void TypeTextRaw(string text)
    {
        var tb = TargetTextBox;
        if (tb is null) return;
        DeleteSelection(); // go ky tu khi dang boi chon -> thay the doan chon
        int pos = tb.CaretIndex;
        tb.Text = (tb.Text ?? "").Insert(pos, text);
        tb.CaretIndex = pos + text.Length;
    }

    private void InsertAtCursor(string text) => TypeTextRaw(text);

    private void DeleteCharsBeforeCursor(int count)
    {
        var tb = TargetTextBox;
        if (tb is null || count <= 0) return;
        int pos = tb.CaretIndex;
        int del = Math.Min(count, pos);
        if (del <= 0) return;
        string txt = tb.Text ?? "";
        tb.Text       = txt.Remove(pos - del, del);
        tb.CaretIndex = pos - del;
    }

    private void DoBackspace()
    {
        DoBackspaceRaw();
    }

    private void DoBackspaceRaw()
    {
        var tb = TargetTextBox;
        if (tb is null) return;
        if (DeleteSelection()) return; // dang boi chon: xoa ca doan chon
        int pos = tb.CaretIndex;
        if (pos <= 0 || string.IsNullOrEmpty(tb.Text)) return;
        tb.Text       = tb.Text.Remove(pos - 1, 1);
        tb.CaretIndex = pos - 1;
    }
}
