using IPGS.RemoteControl.ZcuAgent;
using Microsoft.Extensions.Logging;
using static IPGS.RemoteControl.WinAgent.Native.NativeMethods;

namespace IPGS.RemoteControl.WinAgent.Input;

/// <summary>
/// Bàn phím qua <c>SendInput</c>. Hai chế độ khớp hai message của giao thức:
/// <list type="bullet">
///   <item><b>KeyEvent (keysym X11):</b> phím điều khiển/chức năng → Virtual-Key. Ký tự in được → gửi dạng
///         <c>KEYEVENTF_UNICODE</c> (nhập đúng ký tự tiếng Việt/Unicode bất kể layout máy đích), TRỪ khi đang giữ
///         Ctrl/Alt/Win — lúc đó phải gửi Virtual-Key vì Ctrl + Unicode('c') không kích hoạt được Ctrl+C.</item>
///   <item><b>KeyEventRaw (keycode X11 = evdev + 8):</b> evdev &lt; 89 trùng scancode set 1 của PC; phím mở rộng
///         (mũi tên, Home/End, Ctrl/Alt phải, Win…) tra bảng riêng. Gửi bằng <c>KEYEVENTF_SCANCODE</c> nên bộ gõ
///         (UniKey/EVKey) trên máy đích hoạt động như bàn phím thật.</item>
/// </list>
/// Theo dõi phím đang giữ để <see cref="ReleaseAllKeys"/> nhả hết khi session kết thúc (tránh kẹt phím).
/// </summary>
internal sealed class Win32KeyboardInjector : IKeyboardInjector
{
    /// <summary>Phím đã gửi: Unicode (Code = codepoint) hoặc Virtual-Key (Code = VK, Ext = cờ extended).</summary>
    private readonly record struct Sent(bool Unicode, uint Code, bool Ext);

    // ── Bảng keysym X11 → (Virtual-Key, extended). Chỉ gồm các keysym mà CcuUI KeyboardMapper phát ra. ──
    private static readonly Dictionary<uint, (ushort Vk, bool Ext)> SpecialKeys = BuildSpecialKeys();

    private static Dictionary<uint, (ushort, bool)> BuildSpecialKeys()
    {
        var d = new Dictionary<uint, (ushort, bool)>
        {
            [0xFF08] = (0x08, false), // BackSpace
            [0xFF09] = (0x09, false), // Tab
            [0xFF0D] = (0x0D, false), // Return
            [0xFF13] = (0x13, false), // Pause
            [0xFF14] = (0x91, false), // Scroll_Lock
            [0xFF1B] = (0x1B, false), // Escape
            [0xFF61] = (0x2C, true),  // Print
            [0xFF63] = (0x2D, true),  // Insert
            [0xFFFF] = (0x2E, true),  // Delete
            [0xFF50] = (0x24, true),  // Home
            [0xFF51] = (0x25, true),  // Left
            [0xFF52] = (0x26, true),  // Up
            [0xFF53] = (0x27, true),  // Right
            [0xFF54] = (0x28, true),  // Down
            [0xFF55] = (0x21, true),  // Prior (PageUp)
            [0xFF56] = (0x22, true),  // Next (PageDown)
            [0xFF57] = (0x23, true),  // End
            [0xFF7F] = (0x90, true),  // Num_Lock
            [0xFFE5] = (0x14, false), // Caps_Lock
            [0xFFE1] = (0xA0, false), // Shift_L
            [0xFFE2] = (0xA1, false), // Shift_R
            [0xFFE3] = (0xA2, false), // Control_L
            [0xFFE4] = (0xA3, true),  // Control_R
            [0xFFE9] = (0xA4, false), // Alt_L
            [0xFFEA] = (0xA5, true),  // Alt_R
            [0xFFEB] = (0x5B, true),  // Super_L (Win trái)
            [0xFFEC] = (0x5C, true),  // Super_R (Win phải)
            [0xFF67] = (0x5D, true),  // Menu (Apps)
        };
        for (uint i = 0; i < 12; i++) d[0xFFBE + i] = ((ushort)(0x70 + i), false); // F1..F12
        return d;
    }

    /// <summary>VK của Ctrl/Alt/Win (kể cả L/R) — đang giữ một trong số này thì ký tự phải gửi dạng Virtual-Key.</summary>
    private static readonly HashSet<uint> CommandModifierVks =
        [0x11, 0xA2, 0xA3, 0x12, 0xA4, 0xA5, 0x5B, 0x5C];

    /// <summary>evdev ≥ 96 (phím mở rộng) → scancode set 1 (phần sau tiền tố E0).</summary>
    private static readonly Dictionary<int, byte> ExtendedScan = new()
    {
        [96]  = 0x1C, [97]  = 0x1D, [98]  = 0x35, [99]  = 0x37, [100] = 0x38,
        [102] = 0x47, [103] = 0x48, [104] = 0x49, [105] = 0x4B, [106] = 0x4D,
        [107] = 0x4F, [108] = 0x50, [109] = 0x51, [110] = 0x52, [111] = 0x53,
        [125] = 0x5B, [126] = 0x5C, [127] = 0x5D,
    };
    private const int EvdevPause = 119;
    private const int MaxPlainEvdev = 88;

    private readonly ILogger<Win32KeyboardInjector> _logger;
    private readonly Dictionary<uint, Sent> _downKeysyms = new();
    private readonly HashSet<byte> _downRaw = new();
    private readonly HashSet<uint> _downCommandMods = new();
    private long _lastErrorLogTick;
    private bool _initialized;
    private bool _disposed;

    public Win32KeyboardInjector(ILogger<Win32KeyboardInjector> logger) => _logger = logger;

    public void Initialize() => _initialized = true;

    // ── keysym mode ───────────────────────────────────────────────────────

    public void SendKey(uint keysym, bool isDown)
    {
        if (!_initialized || _disposed) return;

        Sent sent;
        if (!isDown && _downKeysyms.TryGetValue(keysym, out var remembered))
            sent = remembered; // nhả đúng kiểu đã nhấn, kể cả khi trạng thái Ctrl đã đổi giữa chừng
        else if (!TryResolve(keysym, out sent))
        {
            _logger.LogDebug("KeyboardInjector: keysym 0x{Keysym:X} không có ánh xạ Windows — bỏ qua", keysym);
            return;
        }

        if (isDown) _downKeysyms[keysym] = sent; else _downKeysyms.Remove(keysym);

        if (!sent.Unicode && CommandModifierVks.Contains(sent.Code))
        {
            if (isDown) _downCommandMods.Add(sent.Code); else _downCommandMods.Remove(sent.Code);
        }

        Emit(sent, isDown);
    }

    private bool TryResolve(uint keysym, out Sent sent)
    {
        sent = default;

        if (SpecialKeys.TryGetValue(keysym, out var special))
        {
            sent = new Sent(false, special.Vk, special.Ext);
            return true;
        }

        uint cp;
        if (keysym is >= 0x20 and <= 0x7E or >= 0xA0 and <= 0xFF) cp = keysym;           // ASCII / Latin-1: keysym == codepoint
        else if (keysym is >= 0x01000000 and <= 0x0110FFFF)       cp = keysym & 0x00FFFFFF; // Unicode keysym
        else return false;

        if (cp is >= 0xD800 and <= 0xDFFF) return false;

        // Ctrl/Alt/Win đang giữ → cần Virtual-Key thật (Ctrl+C, Alt+Tab…).
        if (_downCommandMods.Count > 0 && cp <= 0xFFFF)
        {
            var scan = VkKeyScanW((char)cp);
            if (scan != -1)
            {
                sent = new Sent(false, (uint)(scan & 0xFF), false);
                return true;
            }
        }

        sent = new Sent(true, cp, false);
        return true;
    }

    private void Emit(Sent sent, bool isDown)
    {
        var up = isDown ? 0u : KEYEVENTF_KEYUP;

        if (sent.Unicode)
        {
            if (sent.Code > 0xFFFF)
            {
                var v = sent.Code - 0x10000;
                var hi = (ushort)(0xD800 + (v >> 10));
                var lo = (ushort)(0xDC00 + (v & 0x3FF));
                Send([InputSender.Key(0, hi, KEYEVENTF_UNICODE | up), InputSender.Key(0, lo, KEYEVENTF_UNICODE | up)]);
            }
            else
            {
                Send([InputSender.Key(0, (ushort)sent.Code, KEYEVENTF_UNICODE | up)]);
            }
            return;
        }

        var vk = (ushort)sent.Code;
        var scanCode = (ushort)MapVirtualKeyW(vk, MAPVK_VK_TO_VSC);
        Send([InputSender.Key(vk, scanCode, (sent.Ext ? KEYEVENTF_EXTENDEDKEY : 0u) | up)]);
    }

    // ── scan mode ─────────────────────────────────────────────────────────

    public void SendRawKey(byte keycode, bool isDown)
    {
        if (!_initialized || _disposed || keycode < 8) return;

        if (!TryResolveRaw(keycode, out var vk, out var scan, out var ext))
        {
            _logger.LogDebug("KeyboardInjector: keycode X11 {Code} không có ánh xạ scancode Windows — bỏ qua", keycode);
            return;
        }

        if (isDown) _downRaw.Add(keycode); else _downRaw.Remove(keycode);
        EmitRaw(vk, scan, ext, isDown);
    }

    private static bool TryResolveRaw(byte keycode, out ushort vk, out ushort scan, out bool ext)
    {
        vk = 0; scan = 0; ext = false;
        var evdev = keycode - 8;

        if (evdev == EvdevPause) { vk = 0x13; return true; } // Pause không có scancode đơn → dùng VK_PAUSE
        if (ExtendedScan.TryGetValue(evdev, out var es)) { scan = es; ext = true; return true; }
        if (evdev is >= 1 and <= MaxPlainEvdev) { scan = (ushort)evdev; return true; }
        return false;
    }

    private void EmitRaw(ushort vk, ushort scan, bool ext, bool isDown)
    {
        var up = isDown ? 0u : KEYEVENTF_KEYUP;
        if (vk != 0)
            Send([InputSender.Key(vk, (ushort)MapVirtualKeyW(vk, MAPVK_VK_TO_VSC), up)]);
        else
            Send([InputSender.Key(0, scan, KEYEVENTF_SCANCODE | (ext ? KEYEVENTF_EXTENDEDKEY : 0u) | up)]);
    }

    // ── nhả hết phím ──────────────────────────────────────────────────────

    public void ReleaseAllKeys()
    {
        if (!_initialized || _disposed || (_downKeysyms.Count == 0 && _downRaw.Count == 0)) return;

        _logger.LogInformation("KeyboardInjector: releasing {Count} stuck key(s) on session end",
            _downKeysyms.Count + _downRaw.Count);

        foreach (var code in _downRaw.ToArray())
            if (TryResolveRaw(code, out var vk, out var scan, out var ext)) EmitRaw(vk, scan, ext, isDown: false);
        _downRaw.Clear();

        foreach (var sent in _downKeysyms.Values.ToArray())
            Emit(sent, isDown: false);
        _downKeysyms.Clear();
        _downCommandMods.Clear();
    }

    private void Send(INPUT[] inputs)
    {
        if (InputSender.TrySend(inputs, out var err)) return;

        var now = Environment.TickCount64;
        if (now - _lastErrorLogTick < 5_000) return;
        _lastErrorLogTick = now;
        _logger.LogWarning(
            "SendInput(bàn phím) thất bại, lỗi Win32 {Err}{Hint}", err,
            err == 5 ? " (ACCESS_DENIED — UIPI: cửa sổ đích có quyền cao hơn agent; chạy agent bằng quyền Administrator)" : "");
    }

    public void Dispose()
    {
        if (_disposed) return;
        ReleaseAllKeys();
        _disposed = true;
    }
}
