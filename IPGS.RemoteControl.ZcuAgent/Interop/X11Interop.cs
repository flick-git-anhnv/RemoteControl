using System.Runtime.InteropServices;

namespace IPGS.RemoteControl.ZcuAgent.Interop;

/// <summary>
/// P/Invoke declarations for libX11.so.6. See TDD §9.1.
/// GOTCHA: XInitThreads() MUST be called BEFORE XOpenDisplay if X11 is accessed
/// from multiple threads (capture thread + inject thread). See TDD §9 + GOTCHAS.md.
/// </summary>
internal static class X11
{
    private const string Lib = "libX11.so.6";

    /// <summary>
    /// Must be the very first X11 call when multiple threads will use X11.
    /// Returns non-zero on success.
    /// </summary>
    [DllImport(Lib)] public static extern IntPtr XInitThreads();

    [DllImport(Lib)] public static extern IntPtr XOpenDisplay(string? displayName);
    [DllImport(Lib)] public static extern int    XCloseDisplay(IntPtr display);

    [DllImport(Lib)] public static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport(Lib)] public static extern int    XDefaultScreen(IntPtr display);
    [DllImport(Lib)] public static extern int    XDisplayWidth(IntPtr display, int screen);
    [DllImport(Lib)] public static extern int    XDisplayHeight(IntPtr display, int screen);
    [DllImport(Lib)] public static extern IntPtr XDefaultVisual(IntPtr display, int screen);
    [DllImport(Lib)] public static extern uint   XDefaultDepth(IntPtr display, int screen);

    /// <summary>
    /// Fallback capture (slower than XShm). format: ZPixmap = 2.
    /// Returns IntPtr to an XImage struct, or IntPtr.Zero on failure.
    /// </summary>
    [DllImport(Lib)] public static extern IntPtr XGetImage(
        IntPtr display, IntPtr drawable,
        int x, int y, uint width, uint height,
        ulong planeMask, int format);

    [DllImport(Lib)] public static extern int XDestroyImage(IntPtr ximage);

    [DllImport(Lib)] public static extern int XSync(IntPtr display, bool discard);
    [DllImport(Lib)] public static extern int XFlush(IntPtr display);

    /// <summary>
    /// Queries the current geometry of a drawable directly from the X server (real round-trip,
    /// NOT a client-side cache read). Use this — not <see cref="XDisplayWidth"/> /
    /// <see cref="XDisplayHeight"/> — to detect live resolution changes after RandR
    /// rotate/resize while the display connection is open.
    /// <para>
    /// GOTCHA: <c>XDisplayWidth</c> / <c>XDisplayHeight</c> are Xlib client-side MACROS that
    /// read a cache populated ONLY at <c>XOpenDisplay()</c> time. They never issue a server
    /// request and therefore never reflect RandR changes made after the connection was opened.
    /// Always use <c>XGetGeometry</c> when you need the CURRENT root-window size at runtime.
    /// </para>
    /// Returns non-zero (Status) on success, 0 on error.
    /// Signature: <c>Status XGetGeometry(Display*, Drawable, Window*, int*, int*,
    /// unsigned int*, unsigned int*, unsigned int*, unsigned int*)</c>
    /// </summary>
    [DllImport(Lib)]
    public static extern int XGetGeometry(IntPtr display, IntPtr drawable,
        out IntPtr root, out int x, out int y,
        out uint width, out uint height, out uint borderWidth, out uint depth);

    /// <summary>
    /// Returns the keycode (8–255) for the given X11 keysym on the current keymap,
    /// or 0 if no physical key maps to that keysym.
    /// GOTCHA: return value 0 is NOT an error — it means the keysym simply has no key
    /// on this keyboard layout. Caller must log warning and skip, not throw.
    /// Man: XKeysymToKeycode(3). TDD §17.5.
    /// </summary>
    [DllImport(Lib)]
    public static extern byte XKeysymToKeycode(IntPtr display, uint keysym);

    // ── Error handling ────────────────────────────────────────────────────

    /// <summary>
    /// Callback delegate for a custom X error handler.
    /// Return 0 to suppress the default behaviour (which calls exit).
    /// IMPORTANT: store the delegate in a static field to prevent GC collection
    /// while the handler is installed.
    /// </summary>
    public delegate int XErrorHandler(IntPtr display, IntPtr errorEvent);

    /// <summary>
    /// Installs a custom X error handler and returns the previous one.
    /// Pass <c>null</c> to restore the default handler (which prints and calls exit).
    /// Use this to intercept async errors such as the BadAccess from XShmAttach
    /// on remote-display environments where MIT-SHM is unavailable.
    /// </summary>
    [DllImport(Lib)]
    public static extern IntPtr XSetErrorHandler(XErrorHandler? handler);

    // ── Constants ────────────────────────────────────────────────────────
    public const int    ZPixmap   = 2;
    /// <summary>X11 AllPlanes mask (~0UL on 64-bit Linux).</summary>
    public const ulong  AllPlanes = ulong.MaxValue;
}

/// <summary>
/// Partial layout of the C XImage struct — fields we actually need.
/// Must match the 64-bit X11 ABI (x86_64 Linux). Trailing function-pointer
/// fields are intentionally omitted (we never access them).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XImageHeader
{
    public int    Width;         // offset  0
    public int    Height;        // offset  4
    public int    XOffset;       // offset  8
    public int    Format;        // offset 12
    public IntPtr Data;          // offset 16  ← pointer to raw pixel bytes (BGRA/BGR on most displays)
    public int    ByteOrder;     // offset 24
    public int    BitmapUnit;    // offset 28
    public int    BitmapBitOrder;// offset 32
    public int    BitmapPad;     // offset 36
    public int    Depth;         // offset 40
    public int    BytesPerLine;  // offset 44  ← stride
    public int    BitsPerPixel;  // offset 48
    // ulong RedMask, GreenMask, BlueMask follow — not needed
}
