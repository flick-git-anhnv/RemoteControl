using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Interop;

/// <summary>
/// Chiếm quyền sở hữu selection CLIPBOARD của X11 và phục vụ nhiều định dạng cùng lúc — việc <c>xclip</c>
/// không làm được (mỗi lần chỉ 1 target). Dùng để đặt "file đã copy" lên clipboard ZCU sao cho dán (Ctrl+V)
/// được trong mọi trình quản lý file: <c>text/uri-list</c> (Qt/PCManFM-Qt, Thunar) và
/// <c>x-special/gnome-copied-files</c> (Nautilus, Nemo, Caja), kèm đường dẫn dạng text.
/// <para>
/// Mỗi lần <see cref="SetFiles"/> thay thế chủ sở hữu cũ. Chủ sở hữu tự kết thúc khi ứng dụng khác chiếm
/// clipboard (SelectionClear) hoặc khi <see cref="Stop"/>.
/// </para>
/// </summary>
internal sealed class X11ClipboardOwner : IDisposable
{
    private const string Lib = "libX11.so.6";

    [DllImport(Lib)] private static extern IntPtr XOpenDisplay(string? name);
    [DllImport(Lib)] private static extern int    XCloseDisplay(IntPtr d);
    [DllImport(Lib)] private static extern IntPtr XDefaultRootWindow(IntPtr d);
    [DllImport(Lib)] private static extern IntPtr XCreateSimpleWindow(IntPtr d, IntPtr parent, int x, int y,
        uint w, uint h, uint border, ulong borderPx, ulong bg);
    [DllImport(Lib)] private static extern int    XDestroyWindow(IntPtr d, IntPtr w);
    [DllImport(Lib)] private static extern IntPtr XInternAtom(IntPtr d, string name, bool onlyIfExists);
    [DllImport(Lib)] private static extern IntPtr XGetAtomName(IntPtr d, IntPtr atom);
    [DllImport(Lib)] private static extern int    XFree(IntPtr p);
    [DllImport(Lib)] private static extern int    XSetSelectionOwner(IntPtr d, IntPtr sel, IntPtr owner, IntPtr time);
    [DllImport(Lib)] private static extern IntPtr XGetSelectionOwner(IntPtr d, IntPtr sel);
    [DllImport(Lib)] private static extern int    XPending(IntPtr d);
    [DllImport(Lib)] private static extern int    XNextEvent(IntPtr d, IntPtr ev);
    [DllImport(Lib)] private static extern int    XFlush(IntPtr d);
    [DllImport(Lib)] private static extern int    XChangeProperty(IntPtr d, IntPtr w, IntPtr prop, IntPtr type,
        int format, int mode, byte[] data, int nelements);
    [DllImport(Lib)] private static extern int    XSendEvent(IntPtr d, IntPtr w, bool propagate, long mask, IntPtr ev);

    private const int SelectionClear   = 29;
    private const int SelectionRequest = 30;
    private const int SelectionNotify  = 31;
    private const int PropModeReplace  = 0;
    private static readonly IntPtr XA_ATOM = (IntPtr)4;

    [StructLayout(LayoutKind.Sequential)]
    private struct XSelectionRequestEvent
    {
        public int type; public ulong serial; public int send_event; public IntPtr display;
        public IntPtr owner, requestor, selection, target, property, time;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XSelectionEvent
    {
        public int type; public ulong serial; public int send_event; public IntPtr display;
        public IntPtr requestor, selection, target, property, time;
    }

    private readonly ILogger _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Thread? _thread;

    public X11ClipboardOwner(ILogger logger) => _logger = logger;

    /// <summary>Đặt danh sách file (đường dẫn tuyệt đối) lên clipboard như thể vừa "Copy" trong trình quản lý file.</summary>
    public bool SetFiles(IReadOnlyList<string> absolutePaths)
    {
        if (absolutePaths.Count == 0) return false;

        var uris = absolutePaths.Select(p => new Uri(p).AbsoluteUri).ToList();
        var targets = new Dictionary<string, byte[]>
        {
            ["text/uri-list"]                  = Encoding.UTF8.GetBytes(string.Join("\r\n", uris) + "\r\n"),
            ["x-special/gnome-copied-files"]   = Encoding.UTF8.GetBytes("copy\n" + string.Join("\n", uris)),
            ["text/plain"]                     = Encoding.UTF8.GetBytes(string.Join("\n", absolutePaths)),
            ["UTF8_STRING"]                    = Encoding.UTF8.GetBytes(string.Join("\n", absolutePaths)),
            ["STRING"]                         = Encoding.UTF8.GetBytes(string.Join("\n", absolutePaths)),
        };

        lock (_gate)
        {
            Stop();
            var started = new ManualResetEventSlim(false);
            var ok = false;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _thread = new Thread(() => Run(targets, token, started, v => ok = v))
            { IsBackground = true, Name = "X11ClipboardOwner" };
            _thread.Start();
            started.Wait(TimeSpan.FromSeconds(3));
            return ok;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _thread?.Join(TimeSpan.FromSeconds(2));
            _cts = null; _thread = null;
        }
    }

    private void Run(Dictionary<string, byte[]> targets, CancellationToken ct, ManualResetEventSlim started, Action<bool> setOk)
    {
        var dpy = XOpenDisplay(null);
        if (dpy == IntPtr.Zero) { _logger.LogWarning("ClipboardOwner: XOpenDisplay thất bại"); started.Set(); return; }

        IntPtr win = IntPtr.Zero;
        var evBuf = Marshal.AllocHGlobal(192); // sizeof(XEvent) = 24 long
        try
        {
            win = XCreateSimpleWindow(dpy, XDefaultRootWindow(dpy), 0, 0, 1, 1, 0, 0, 0);
            var clipboard = XInternAtom(dpy, "CLIPBOARD", false);
            var targetsAtom = XInternAtom(dpy, "TARGETS", false);
            var atomToName = new Dictionary<IntPtr, string>();
            var supported = new List<IntPtr> { targetsAtom };
            foreach (var name in targets.Keys)
            {
                var a = XInternAtom(dpy, name, false);
                atomToName[a] = name;
                supported.Add(a);
            }

            XSetSelectionOwner(dpy, clipboard, win, IntPtr.Zero);
            XFlush(dpy);
            var ok = XGetSelectionOwner(dpy, clipboard) == win;
            setOk(ok);
            started.Set();
            if (!ok) { _logger.LogWarning("ClipboardOwner: không chiếm được CLIPBOARD"); return; }

            while (!ct.IsCancellationRequested)
            {
                if (XPending(dpy) == 0) { Thread.Sleep(20); continue; }
                XNextEvent(dpy, evBuf);
                var type = Marshal.ReadInt32(evBuf);

                if (type == SelectionClear) return; // app khác đã copy → hết nhiệm vụ

                if (type != SelectionRequest) continue;
                var req = Marshal.PtrToStructure<XSelectionRequestEvent>(evBuf);
                var prop = req.property == IntPtr.Zero ? req.target : req.property;
                var reply = IntPtr.Zero; // property của phản hồi; 0 = từ chối

                if (req.selection == clipboard)
                {
                    if (req.target == targetsAtom)
                    {
                        var bytes = new byte[supported.Count * 8];
                        for (var i = 0; i < supported.Count; i++)
                            BitConverter.GetBytes(supported[i].ToInt64()).CopyTo(bytes, i * 8);
                        XChangeProperty(dpy, req.requestor, prop, XA_ATOM, 32, PropModeReplace, bytes, supported.Count);
                        reply = prop;
                    }
                    else if (atomToName.TryGetValue(req.target, out var name))
                    {
                        var data = targets[name];
                        XChangeProperty(dpy, req.requestor, prop, req.target, 8, PropModeReplace, data, data.Length);
                        reply = prop;
                    }
                }

                var notify = new XSelectionEvent
                {
                    type = SelectionNotify, display = dpy, requestor = req.requestor,
                    selection = req.selection, target = req.target, property = reply, time = req.time,
                };
                var nPtr = Marshal.AllocHGlobal(192);
                try
                {
                    Marshal.Copy(new byte[192], 0, nPtr, 192); // zero toàn bộ XEvent
                    Marshal.StructureToPtr(notify, nPtr, false);
                    XSendEvent(dpy, req.requestor, false, 0, nPtr);
                    XFlush(dpy);
                }
                finally { Marshal.FreeHGlobal(nPtr); }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ClipboardOwner: lỗi vòng lặp");
            started.Set();
        }
        finally
        {
            Marshal.FreeHGlobal(evBuf);
            if (win != IntPtr.Zero) XDestroyWindow(dpy, win);
            XCloseDisplay(dpy);
        }
    }

    public void Dispose() => Stop();
}
