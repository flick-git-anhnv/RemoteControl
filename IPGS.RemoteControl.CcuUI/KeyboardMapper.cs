using Avalonia.Input;

namespace IPGS.RemoteControl.CcuUI;

/// <summary>
/// Maps Avalonia <see cref="KeyEventArgs"/> to X11 keysym values for forwarding
/// via the remote-control protocol (TDD §17.3).
/// <para>
/// Mapping priority (highest first):
/// <list type="number">
///   <item>Special/modifier keys via <see cref="SpecialKeyMap"/> (fixed keysyms).</item>
///   <item><see cref="KeyEventArgs.KeySymbol"/> Unicode character (Latin-1 direct, or
///         Unicode keysym 0x01000000|codepoint for codepoint &gt; 0xFF).</item>
///   <item>Fallback: derive a best-effort keysym from <see cref="KeyEventArgs.Key"/>.</item>
/// </list>
/// </para>
/// <para>
/// Returns <see langword="null"/> when no mapping exists (e.g. media keys not in X11 keymap).
/// Caller should skip sending a KEY_EVENT in that case.
/// </para>
/// </summary>
public static class KeyboardMapper
{
    // ── SPECIAL_KEY_MAP (TDD §17.3 — sourced from /usr/include/X11/keysymdef.h) ──────────────

    // ReSharper disable InconsistentNaming
    private const uint XK_BackSpace   = 0xFF08;
    private const uint XK_Tab         = 0xFF09;
    private const uint XK_Return      = 0xFF0D;
    private const uint XK_Pause       = 0xFF13;
    private const uint XK_Scroll_Lock = 0xFF14;
    private const uint XK_Escape      = 0xFF1B;
    private const uint XK_space       = 0x0020;
    private const uint XK_Print       = 0xFF61;
    private const uint XK_Insert      = 0xFF63;
    private const uint XK_Delete      = 0xFFFF;
    private const uint XK_Home        = 0xFF50;
    private const uint XK_Left        = 0xFF51;
    private const uint XK_Up          = 0xFF52;
    private const uint XK_Right       = 0xFF53;
    private const uint XK_Down        = 0xFF54;
    private const uint XK_Prior       = 0xFF55; // PageUp
    private const uint XK_Next        = 0xFF56; // PageDown
    private const uint XK_End         = 0xFF57;
    private const uint XK_F1          = 0xFFBE;
    private const uint XK_F2          = 0xFFBF;
    private const uint XK_F3          = 0xFFC0;
    private const uint XK_F4          = 0xFFC1;
    private const uint XK_F5          = 0xFFC2;
    private const uint XK_F6          = 0xFFC3;
    private const uint XK_F7          = 0xFFC4;
    private const uint XK_F8          = 0xFFC5;
    private const uint XK_F9          = 0xFFC6;
    private const uint XK_F10         = 0xFFC7;
    private const uint XK_F11         = 0xFFC8;
    private const uint XK_F12         = 0xFFC9;
    private const uint XK_Num_Lock    = 0xFF7F;
    private const uint XK_Caps_Lock   = 0xFFE5;
    private const uint XK_Shift_L     = 0xFFE1;
    private const uint XK_Shift_R     = 0xFFE2;
    private const uint XK_Control_L   = 0xFFE3;
    private const uint XK_Control_R   = 0xFFE4;
    private const uint XK_Alt_L       = 0xFFE9;
    private const uint XK_Alt_R       = 0xFFEA;
    private const uint XK_Super_L     = 0xFFEB;
    private const uint XK_Super_R     = 0xFFEC;
    private const uint XK_Menu        = 0xFF67;
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// Fixed keysym table for special/modifier keys.
    /// Sourced from <c>/usr/include/X11/keysymdef.h</c> (TDD §17.3).
    /// </summary>
    private static readonly Dictionary<Key, uint> SpecialKeyMap = new()
    {
        [Key.Back]         = XK_BackSpace,
        [Key.Tab]          = XK_Tab,
        [Key.Return]       = XK_Return,
        [Key.Enter]        = XK_Return,   // numpad Enter
        [Key.Pause]        = XK_Pause,
        [Key.Scroll]       = XK_Scroll_Lock,
        [Key.Escape]       = XK_Escape,
        [Key.Space]        = XK_space,
        [Key.Print]        = XK_Print,
        [Key.Insert]       = XK_Insert,
        [Key.Delete]       = XK_Delete,
        [Key.Home]         = XK_Home,
        [Key.Left]         = XK_Left,
        [Key.Up]           = XK_Up,
        [Key.Right]        = XK_Right,
        [Key.Down]         = XK_Down,
        [Key.PageUp]       = XK_Prior,
        [Key.PageDown]     = XK_Next,
        [Key.End]          = XK_End,
        [Key.F1]           = XK_F1,
        [Key.F2]           = XK_F2,
        [Key.F3]           = XK_F3,
        [Key.F4]           = XK_F4,
        [Key.F5]           = XK_F5,
        [Key.F6]           = XK_F6,
        [Key.F7]           = XK_F7,
        [Key.F8]           = XK_F8,
        [Key.F9]           = XK_F9,
        [Key.F10]          = XK_F10,
        [Key.F11]          = XK_F11,
        [Key.F12]          = XK_F12,
        [Key.NumLock]      = XK_Num_Lock,
        [Key.CapsLock]     = XK_Caps_Lock,
        [Key.LeftShift]    = XK_Shift_L,
        [Key.RightShift]   = XK_Shift_R,
        [Key.LeftCtrl]     = XK_Control_L,
        [Key.RightCtrl]    = XK_Control_R,
        [Key.LeftAlt]      = XK_Alt_L,
        [Key.RightAlt]     = XK_Alt_R,
        [Key.LWin]         = XK_Super_L,
        [Key.RWin]         = XK_Super_R,
        [Key.Apps]         = XK_Menu,
    };

    /// <summary>
    /// Resolves an X11 keysym for the given Avalonia key event using 3-tier priority
    /// (TDD §17.3).  Returns <see langword="null"/> if no mapping is available.
    /// </summary>
    /// <param name="key">Avalonia logical key (physical key identity).</param>
    /// <param name="keySymbol">
    /// Avalonia KeySymbol string — a single Unicode character already modified by
    /// Shift/AltGr (e.g. "@" for Shift+2 on US layout).  May be null.
    /// </param>
    public static uint? Resolve(Key key, string? keySymbol)
    {
        // ── Tier 1: special/modifier key table ──────────────────────────────────
        if (SpecialKeyMap.TryGetValue(key, out uint special))
            return special;

        // ── Tier 2: KeySymbol Unicode character ─────────────────────────────────
        if (keySymbol is { Length: 1 })
        {
            uint cp = keySymbol[0];
            if (cp <= 0x00FF)
                // Latin-1 range: keysym == codepoint (X11 Appendix A)
                return cp;
            else
                // XKB Unicode keysym: 0x01000000 | codepoint (TDD §17.3)
                return 0x01000000u | cp;
        }

        // ── Tier 3: fallback by Key enum ─────────────────────────────────────────
        // Handles printable ASCII keys when KeySymbol is null (e.g. held Ctrl — no
        // character produced — but also handles plain letter keys as last resort).
        return FallbackFromKey(key);
    }

    /// <summary>
    /// Best-effort fallback: map printable Key enum values to their ASCII keysym.
    /// Only handles keys that have an unambiguous lowercase ASCII value.
    /// Returns <see langword="null"/> for keys with no clear mapping.
    /// </summary>
    private static uint? FallbackFromKey(Key key) => key switch
    {
        // Digits
        Key.D0 => (uint)'0',
        Key.D1 => (uint)'1',
        Key.D2 => (uint)'2',
        Key.D3 => (uint)'3',
        Key.D4 => (uint)'4',
        Key.D5 => (uint)'5',
        Key.D6 => (uint)'6',
        Key.D7 => (uint)'7',
        Key.D8 => (uint)'8',
        Key.D9 => (uint)'9',

        // Letters (lowercase keysym; X server + XTest handle shift-state separately
        // via explicit Shift_L/Shift_R press events sent before this key)
        Key.A => (uint)'a',
        Key.B => (uint)'b',
        Key.C => (uint)'c',
        Key.D => (uint)'d',
        Key.E => (uint)'e',
        Key.F => (uint)'f',
        Key.G => (uint)'g',
        Key.H => (uint)'h',
        Key.I => (uint)'i',
        Key.J => (uint)'j',
        Key.K => (uint)'k',
        Key.L => (uint)'l',
        Key.M => (uint)'m',
        Key.N => (uint)'n',
        Key.O => (uint)'o',
        Key.P => (uint)'p',
        Key.Q => (uint)'q',
        Key.R => (uint)'r',
        Key.S => (uint)'s',
        Key.T => (uint)'t',
        Key.U => (uint)'u',
        Key.V => (uint)'v',
        Key.W => (uint)'w',
        Key.X => (uint)'x',
        Key.Y => (uint)'y',
        Key.Z => (uint)'z',

        // Common punctuation (unshifted)
        Key.OemMinus    => (uint)'-',
        Key.OemPlus     => (uint)'=',
        Key.OemOpenBrackets => (uint)'[',
        Key.Oem6        => (uint)']',  // OemCloseBrackets
        Key.OemPipe     => (uint)'\\',
        Key.OemSemicolon => (uint)';',
        Key.OemQuotes   => (uint)'\'',
        Key.OemComma    => (uint)',',
        Key.OemPeriod   => (uint)'.',
        Key.OemQuestion => (uint)'/',
        Key.OemTilde    => (uint)'`',

        // Numpad
        Key.NumPad0 => (uint)'0',
        Key.NumPad1 => (uint)'1',
        Key.NumPad2 => (uint)'2',
        Key.NumPad3 => (uint)'3',
        Key.NumPad4 => (uint)'4',
        Key.NumPad5 => (uint)'5',
        Key.NumPad6 => (uint)'6',
        Key.NumPad7 => (uint)'7',
        Key.NumPad8 => (uint)'8',
        Key.NumPad9 => (uint)'9',
        Key.Multiply  => (uint)'*',
        Key.Add       => (uint)'+',
        Key.Subtract  => (uint)'-',
        Key.Decimal   => (uint)'.',
        Key.Divide    => (uint)'/',

        _ => null   // No mapping available — caller should skip sending
    };
}
