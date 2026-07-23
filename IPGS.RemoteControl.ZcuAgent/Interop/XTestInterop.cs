using System.Runtime.InteropServices;

namespace IPGS.RemoteControl.ZcuAgent.Interop;

/// <summary>
/// P/Invoke for libXtst.so.6 — XTest extension for synthetic input injection.
/// See TDD §9.3.
/// GOTCHA: Always call XFlush(display) after XTestFakeButtonEvent so the event
/// is actually dispatched to the X server. TDD §9 gotcha note.
/// </summary>
internal static class XTest
{
    private const string Lib = "libXtst.so.6";

    /// <summary>
    /// Move the pointer to (<paramref name="x"/>, <paramref name="y"/>) on
    /// <paramref name="screen"/> (use -1 for current screen).
    /// <paramref name="delay"/> = 0 means immediate.
    /// </summary>
    [DllImport(Lib)]
    public static extern int XTestFakeMotionEvent(
        IntPtr display, int screen, int x, int y, ulong delay);

    /// <summary>
    /// Simulate a mouse button press or release.
    /// <paramref name="button"/>: 1=Left, 2=Middle, 3=Right, 4=WheelUp, 5=WheelDown.
    /// <paramref name="isPress"/>: true = press, false = release.
    /// Must call XFlush after to dispatch the event.
    /// </summary>
    [DllImport(Lib)]
    public static extern int XTestFakeButtonEvent(
        IntPtr display, uint button, bool isPress, ulong delay);

    /// <summary>
    /// Simulate a keyboard key press or release.
    /// <paramref name="keycode"/>: X11 keycode (8–255) obtained via
    /// <see cref="X11.XKeysymToKeycode"/>. Always call <see cref="X11.XFlush"/> after.
    /// Used for v1.1 keyboard injection. TDD §17.4.
    /// </summary>
    [DllImport(Lib)]
    public static extern int XTestFakeKeyEvent(
        IntPtr display, uint keycode, bool isPress, ulong delay);
}
