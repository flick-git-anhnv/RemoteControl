using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Interop;

/// <summary>
/// Centralised X11 async-error handler for the entire ZCU agent process.
/// <para>
/// X11 error events are asynchronous: the X server may send them after the offending
/// request has already returned. Xlib's built-in handler logs to stderr then calls
/// <c>exit()</c>, killing the process with no diagnostic information. This class installs
/// one global handler that:
/// <list type="bullet">
///   <item>Logs every error (error_code / request_code / minor_code / serial) so we can
///         diagnose silent failures such as XTest commands being rejected on an SSH-hijacked
///         display.</item>
///   <item>Sets <see cref="ShmErrorOccurred"/> so <c>X11ScreenCapturer.TryInitSHM</c> can
///         detect a <c>BadAccess</c> from <c>XShmAttach</c> and fall back gracefully.</item>
/// </list>
/// </para>
/// <para>
/// Call <see cref="Install"/> exactly once before any X call that may produce async errors.
/// The handler MUST remain active for the process lifetime — restoring the default handler
/// would re-enable Xlib's <c>exit()</c> behaviour.
/// </para>
/// </summary>
internal static class X11ErrorTracker
{
    // Strong GC root — Xlib holds only a raw function pointer, not a GC reference.
    // Without this field the delegate would be collected and Xlib would call a
    // dangling pointer on the next error.
    private static readonly X11.XErrorHandler _handler = OnX11Error;

    private static ILogger? _logger;
    private static volatile bool _installed;

    /// <summary>
    /// Set to <c>true</c> by the handler when an X error attributable to a MIT-SHM
    /// request arrives (see <see cref="ShmMajorOpcode"/>). Reset to <c>false</c> by the
    /// caller immediately before the operation being guarded (e.g. <c>XShmAttach</c>),
    /// then checked after <c>XSync</c>.
    /// </summary>
    public static volatile bool ShmErrorOccurred;

    /// <summary>
    /// Major opcode of the MIT-SHM extension (the <c>request_code</c> its requests carry
    /// in XErrorEvents). Set once by <c>X11ScreenCapturer.Initialize</c> via
    /// <c>XQueryExtension("MIT-SHM")</c>.
    /// <para>
    /// Audit L8: without this scoping, ANY X error on ANY display connection in the
    /// process (e.g. an XTest BadAccess on the injector connection, raised concurrently
    /// on the receive thread) set <see cref="ShmErrorOccurred"/> and caused a
    /// false-positive fallback from SHM to the much slower XGetImage during a
    /// mid-session <c>TryInitSHM</c> re-init (resolution change).
    /// While the opcode is unknown (-1), the handler stays conservative and flags every
    /// error — preserving the original crash-safe SHM fallback behaviour.
    /// </para>
    /// </summary>
    public static volatile int ShmMajorOpcode = -1;

    // ── XErrorEvent memory layout (x86_64 Linux, Xlib ABI) ──────────────────
    //
    // Matches <X11/Xlib.h> on a 64-bit little-endian system:
    //   int type          → offset  0 (4 bytes)
    //   [4-byte pad]      → offset  4 (compiler/ABI alignment before pointer)
    //   Display *display  → offset  8 (8 bytes)
    //   XID resourceid    → offset 16 (unsigned long, 8 bytes)
    //   unsigned long serial → offset 24 (8 bytes)
    //   unsigned char error_code   → offset 32
    //   unsigned char request_code → offset 33
    //   unsigned char minor_code   → offset 34
    //
    [StructLayout(LayoutKind.Sequential)]
    private struct XErrorEvent
    {
        public int    Type;          // offset  0
        private int   _pad0;         // offset  4 — padding to align the pointer below
        public IntPtr Display;       // offset  8
        public nuint  ResourceId;    // offset 16 (XID = unsigned long on 64-bit)
        public nuint  Serial;        // offset 24
        public byte   ErrorCode;     // offset 32
        public byte   RequestCode;   // offset 33
        public byte   MinorCode;     // offset 34
    }

    /// <summary>
    /// Install the global X11 error handler.  Idempotent — safe to call more than once;
    /// only the first call has any effect.
    /// </summary>
    /// <param name="logger">
    ///   Logger to write error details to.  When <c>null</c> (e.g. very early in startup),
    ///   errors are written to <see cref="Console.Error"/> instead.
    /// </param>
    public static void Install(ILogger? logger = null)
    {
        // Always update the logger reference so the first real logger wins over null.
        if (logger is not null)
            _logger = logger;

        if (_installed) return;
        _installed = true;
        X11.XSetErrorHandler(_handler);
    }

    // Called by Xlib on any async X error (any display connection in the process).
    // Rules: NEVER throw, NEVER call back into Xlib (re-entrant crash risk).
    private static int OnX11Error(IntPtr display, IntPtr errorEventPtr)
    {
        try
        {
            var ev  = Marshal.PtrToStructure<XErrorEvent>(errorEventPtr);

            // Audit L8: only flag SHM fallback for errors produced by MIT-SHM requests
            // (request_code == extension major opcode). Errors from other requests —
            // e.g. XTest on the injector's display connection, arriving concurrently on
            // another thread — must NOT poison a TryInitSHM re-init in progress.
            // If the opcode is not (yet) known, stay conservative: flag every error so
            // the original crash-safe fallback behaviour is preserved.
            var shmOpcode = ShmMajorOpcode;
            if (shmOpcode < 0 || ev.RequestCode == shmOpcode)
                ShmErrorOccurred = true;

            var msg = $"[X11Error] error_code={ev.ErrorCode} request_code={ev.RequestCode} " +
                      $"minor_code={ev.MinorCode} serial={ev.Serial} resourceid=0x{ev.ResourceId:X}";

            if (_logger is not null)
                _logger.LogWarning("{Msg}", msg);
            else
                Console.Error.WriteLine(msg);
        }
        catch
        {
            // Could not parse the event — be conservative so a real XShmAttach BadAccess
            // is never missed (would otherwise re-enable the historical crash scenario).
            ShmErrorOccurred = true;
            // Silently ignore otherwise — we must not throw from an Xlib C callback.
        }

        // Return 0 to suppress Xlib's default handler (which would call exit()).
        return 0;
    }
}
