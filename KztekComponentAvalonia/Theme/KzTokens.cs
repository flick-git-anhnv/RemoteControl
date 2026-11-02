using Avalonia.Media;
using Avalonia;

namespace KztekComponentAvalonia.Theme;

public static class KzTokens
{
    // ── Colors ──────────────────────────────────────────────────────────────
    public static readonly Color Navy900  = Color.Parse("#251C53");
    public static readonly Color Navy700  = Color.Parse("#4A3F8C");
    public static readonly Color Navy300  = Color.Parse("#B8B3D6");
    public static readonly Color Navy100  = Color.Parse("#F5F3FA");

    public static readonly Color Orange500 = Color.Parse("#F05922");
    public static readonly Color Orange600 = Color.Parse("#D94817");
    public static readonly Color Orange300 = Color.Parse("#FFAA80");

    public static readonly Color Success = Color.Parse("#16A34A");
    public static readonly Color Warning = Color.Parse("#F59E0B");
    public static readonly Color Error   = Color.Parse("#DC2626");
    public static readonly Color Info    = Color.Parse("#2563EB");

    public static readonly Color BgDefault = Color.Parse("#FFFFFF");
    public static readonly Color Border    = Color.Parse("#E5E5E5");
    public static readonly Color Divider   = Color.Parse("#CBCBCB");
    public static readonly Color Text      = Color.Parse("#1A1A1A");
    public static readonly Color TextMuted = Color.Parse("#6B7280");

    // ── Brushes ─────────────────────────────────────────────────────────────
    public static readonly SolidColorBrush Navy900Brush   = new(Navy900);
    public static readonly SolidColorBrush Navy700Brush   = new(Navy700);
    public static readonly SolidColorBrush Navy300Brush   = new(Navy300);
    public static readonly SolidColorBrush Navy100Brush   = new(Navy100);

    public static readonly SolidColorBrush Orange500Brush = new(Orange500);
    public static readonly SolidColorBrush Orange600Brush = new(Orange600);
    public static readonly SolidColorBrush Orange300Brush = new(Orange300);

    public static readonly SolidColorBrush SuccessBrush  = new(Success);
    public static readonly SolidColorBrush WarningBrush  = new(Warning);
    public static readonly SolidColorBrush ErrorBrush    = new(Error);
    public static readonly SolidColorBrush InfoBrush     = new(Info);

    public static readonly SolidColorBrush BgDefaultBrush = new(BgDefault);
    public static readonly SolidColorBrush BorderBrush    = new(Border);
    public static readonly SolidColorBrush DividerBrush   = new(Divider);
    public static readonly SolidColorBrush TextBrush      = new(Text);
    public static readonly SolidColorBrush TextMutedBrush = new(TextMuted);
    public static readonly SolidColorBrush WhiteBrush     = new(Colors.White);

    // ── Corner Radii ─────────────────────────────────────────────────────────
    public static readonly CornerRadius RadiusSm   = new(4);
    public static readonly CornerRadius RadiusMd   = new(8);
    public static readonly CornerRadius RadiusLg   = new(12);
    public static readonly CornerRadius RadiusXl   = new(16);
    public static readonly CornerRadius RadiusPill = new(9999);

    // ── Heights ──────────────────────────────────────────────────────────────
    public const double HeightSm = 32;
    public const double HeightMd = 40;
    public const double HeightLg = 48;

    // ── Spacing ──────────────────────────────────────────────────────────────
    public const double Space1 = 4;
    public const double Space2 = 8;
    public const double Space3 = 12;
    public const double Space4 = 16;
    public const double Space5 = 20;
    public const double Space6 = 24;

    // ── Typography ───────────────────────────────────────────────────────────
    public const double FontSizeSm  = 12;
    public const double FontSizeMd  = 14;
    public const double FontSizeLg  = 16;
    public const double FontSizeXl  = 20;
    public const double FontSize2xl = 24;
    public const double FontSize3xl = 30;
}
