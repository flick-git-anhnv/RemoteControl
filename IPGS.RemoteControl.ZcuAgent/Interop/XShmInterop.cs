using System.Runtime.InteropServices;

namespace IPGS.RemoteControl.ZcuAgent.Interop;

/// <summary>
/// P/Invoke for libXext.so.6 MIT-SHM extension and libc SysV shared-memory calls.
/// See TDD §9.2.
/// GOTCHA: XShmGetImage returns Bool (int) — check return value, it does NOT throw.
/// </summary>
internal static class XShm
{
    private const string LibXext = "libXext.so.6";
    private const string LibC    = "libc";

    // ── MIT-SHM ──────────────────────────────────────────────────────────

    /// <summary>Returns true if the server supports the MIT-SHM extension.</summary>
    [DllImport(LibXext)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool XShmQueryExtension(IntPtr display);

    /// <summary>
    /// Create an XImage backed by shared memory.
    /// <paramref name="data"/> should be set to the SHM address after calling <see cref="shmat"/>.
    /// </summary>
    [DllImport(LibXext)]
    public static extern IntPtr XShmCreateImage(
        IntPtr display, IntPtr visual, uint depth,
        int format, IntPtr data,
        IntPtr shmInfo,
        uint width, uint height);

    [DllImport(LibXext)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool XShmAttach(IntPtr display, IntPtr shmInfo);

    [DllImport(LibXext)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool XShmDetach(IntPtr display, IntPtr shmInfo);

    /// <summary>
    /// Capture the drawable into the SHM-backed XImage.
    /// Returns non-zero (true) on success. MUST check return value.
    /// </summary>
    [DllImport(LibXext)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool XShmGetImage(
        IntPtr display, IntPtr drawable, IntPtr ximage,
        int x, int y, ulong planeMask);

    // ── SysV shared memory (libc) ─────────────────────────────────────────

    /// <summary>Allocate a new shared memory segment. key=IPC_PRIVATE, flags=IPC_CREAT|0600.</summary>
    [DllImport(LibC)]
    public static extern int shmget(int key, UIntPtr size, int shmflg);

    /// <summary>Attach shared memory segment. Returns address pointer or -1 (IntPtr with all bits set) on failure.</summary>
    [DllImport(LibC)]
    public static extern IntPtr shmat(int shmid, IntPtr shmaddr, int shmflg);

    /// <summary>Detach shared memory from process address space.</summary>
    [DllImport(LibC)]
    public static extern int shmdt(IntPtr shmaddr);

    /// <summary>Control shared memory. cmd=IPC_RMID to mark for deletion.</summary>
    [DllImport(LibC)]
    public static extern int shmctl(int shmid, int cmd, IntPtr buf);

    // ── SHM constants ────────────────────────────────────────────────────
    public const int IPC_PRIVATE = 0;
    public const int IPC_CREAT   = 0x200;
    public const int IPC_RMID    = 0;
    /// <summary>Read+write permissions (0666 octal) so Xorg can attach even across UIDs.</summary>
    public const int SHM_MODE    = 0x1B6;
}

/// <summary>
/// Mirrors the C <c>XShmSegmentInfo</c> struct from <c>&lt;X11/extensions/XShm.h&gt;</c>.
/// See TDD §9.2.
/// <para>
/// GOTCHA (G020): In C client headers (<c>&lt;X11/X.h&gt;</c> without <c>_XSERVER64</c>),
/// <c>ShmSeg</c> (<c>XID</c>) is <c>unsigned long</c> — which is 8 bytes on 64-bit Linux (LP64)
/// and 4 bytes on 32-bit Linux (ILP32). Using <see cref="nuint"/> matches the exact C ABI
/// so <c>shmid</c>, <c>shmaddr</c>, and <c>readOnly</c> are never shifted by 4 bytes.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XShmSegmentInfo
{
    /// <summary>ShmSeg X resource ID (unsigned long in libXext — 8 bytes on LP64).</summary>
    public nuint  shmseg;
    /// <summary>SysV shared memory ID from shmget().</summary>
    public int    shmid;
    /// <summary>Virtual address of attached shared memory (from shmat()).</summary>
    public IntPtr shmaddr;
    /// <summary>Bool — set to 0 (read-write).</summary>
    public int    readOnly;
}
