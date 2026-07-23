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
        ref XShmSegmentInfo shmInfo,
        uint width, uint height);

    [DllImport(LibXext)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool XShmAttach(IntPtr display, ref XShmSegmentInfo shmInfo);

    [DllImport(LibXext)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool XShmDetach(IntPtr display, ref XShmSegmentInfo shmInfo);

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
    /// <summary>Owner read+write (0600 octal).</summary>
    public const int SHM_MODE    = 0x180;
}

/// <summary>
/// Mirrors the C <c>XShmSegmentInfo</c> struct from <c>&lt;X11/extensions/XShm.h&gt;</c>.
/// See TDD §9.2.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XShmSegmentInfo
{
    /// <summary>ShmSeg X resource ID (XID — 32-bit even on 64-bit).</summary>
    public int    shmseg;
    /// <summary>SysV shared memory ID from shmget().</summary>
    public int    shmid;
    /// <summary>Virtual address of attached shared memory (from shmat()).</summary>
    public IntPtr shmaddr;
    /// <summary>Bool — set to 0 (read-write).</summary>
    public int    readOnly;
}
