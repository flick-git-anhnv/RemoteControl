using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using IPGS.RemoteControl.ZcuAgent;
using Microsoft.Extensions.Logging;
using static IPGS.RemoteControl.WinAgent.Native.NativeMethods;

namespace IPGS.RemoteControl.WinAgent.Platform;

/// <summary>
/// Clipboard Windows: chữ (<c>CF_UNICODETEXT</c>) và danh sách file (<c>CF_HDROP</c>).
/// <para>
/// Mọi thao tác chạy trên MỘT luồng STA riêng có cửa sổ ẩn (message-only): <c>SetClipboardData</c> chỉ hoạt động
/// khi clipboard được mở bởi một cửa sổ hợp lệ (<c>OpenClipboard(NULL)</c> làm <c>SetClipboardData</c> thất bại),
/// và cửa sổ phải sống cùng luồng với việc dùng nó.
/// </para>
/// <para>
/// Đọc rẻ: <c>GetClipboardSequenceNumber</c> không đổi thì trả lại kết quả đã đọc, không mở clipboard — tránh
/// tranh chấp với ứng dụng khác khi ClientSession poll mỗi 2 giây.
/// </para>
/// </summary>
internal sealed class Win32ClipboardService : IClipboardService, IDisposable
{
    private const int OpenRetries = 10;
    private const int OpenRetryDelayMs = 20;
    /// <summary>Bỏ qua clipboard chữ lớn hơn mức này (byte) — ClientSession cũng từ chối &gt; 262144 ký tự.</summary>
    private const long MaxTextBytes = 1L * 1024 * 1024;
    private const int MaxFiles = 1_000;

    private readonly ILogger<Win32ClipboardService> _logger;
    private readonly BlockingCollection<Action> _jobs = new();
    private readonly Thread _thread;
    private IntPtr _hwnd;
    private uint _dropEffectFormat;

    // Chỉ đụng tới trên luồng clipboard.
    private long _cacheSeq = -1;
    private string? _cacheText;
    private List<string>? _cacheFiles;
    private uint _ownedSeq;

    public Win32ClipboardService(ILogger<Win32ClipboardService> logger)
    {
        _logger = logger;
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = "WinAgent-Clipboard" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    // ── IClipboardService ─────────────────────────────────────────────────

    public async Task<string?> GetTextAsync(CancellationToken ct)
    {
        try { return (await RunAsync(Refresh).WaitAsync(ct)).Text; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger.LogDebug(ex, "Đọc clipboard (text) thất bại"); return null; }
    }

    public async Task<List<string>?> GetFilePathsAsync(CancellationToken ct)
    {
        try { return (await RunAsync(Refresh).WaitAsync(ct)).Files; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger.LogDebug(ex, "Đọc clipboard (file) thất bại"); return null; }
    }

    public async Task SetTextAsync(string text, CancellationToken ct)
    {
        try
        {
            var ok = await RunAsync(() => SetUnicodeText(text)).WaitAsync(ct);
            if (!ok) _logger.LogWarning("Không đặt được text lên clipboard (clipboard đang bị ứng dụng khác giữ?)");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger.LogWarning(ex, "Không đặt được text lên clipboard"); }
    }

    public bool SetFiles(IReadOnlyList<string> absolutePaths)
    {
        try
        {
            var task = RunAsync(() => SetDropFiles(absolutePaths));
            return task.Wait(TimeSpan.FromSeconds(5)) && task.Result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đặt được file lên clipboard");
            return false;
        }
    }

    public void ReleaseFiles()
    {
        try
        {
            _ = RunAsync(() =>
            {
                // Chỉ dọn nếu clipboard vẫn là của ta; người dùng đã Copy thứ khác thì giữ nguyên.
                if (_ownedSeq != 0 && GetClipboardSequenceNumber() == _ownedSeq && OpenWithRetry(_hwnd))
                {
                    try { EmptyClipboard(); } finally { CloseClipboard(); }
                }
                _ownedSeq = 0;
                return true;
            });
        }
        catch (Exception ex) { _logger.LogDebug(ex, "ReleaseFiles thất bại"); }
    }

    // ── Luồng clipboard ───────────────────────────────────────────────────

    private void ThreadMain()
    {
        try
        {
            _hwnd = CreateWindowExW(0, "STATIC", string.Empty, 0, 0, 0, 0, 0,
                HWND_MESSAGE, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
                _logger.LogError("Không tạo được cửa sổ ẩn cho clipboard (lỗi Win32 {Err}) — clipboard sẽ không đồng bộ",
                    Marshal.GetLastWin32Error());
            _dropEffectFormat = RegisterClipboardFormatW("Preferred DropEffect");

            while (!_jobs.IsAddingCompleted)
            {
                if (_jobs.TryTake(out var job, 50))
                {
                    try { job(); } catch (Exception ex) { _logger.LogDebug(ex, "Clipboard job lỗi"); }
                }
                while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                {
                    TranslateMessage(ref msg);
                    DispatchMessageW(ref msg);
                }
            }
        }
        finally
        {
            if (_hwnd != IntPtr.Zero) DestroyWindow(_hwnd);
        }
    }

    private Task<T> RunAsync<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _jobs.Add(() =>
            {
                try { tcs.SetResult(work()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
        }
        catch (InvalidOperationException)
        {
            tcs.SetException(new ObjectDisposedException(nameof(Win32ClipboardService)));
        }
        return tcs.Task;
    }

    // ── Đọc ───────────────────────────────────────────────────────────────

    private (string? Text, List<string>? Files) Refresh()
    {
        var seq = GetClipboardSequenceNumber();
        if (seq == _cacheSeq) return (_cacheText, _cacheFiles);

        if (!OpenWithRetry(IntPtr.Zero))
            return (_cacheText, _cacheFiles); // chưa cập nhật mốc → lần poll sau thử lại

        string? text = null;
        List<string>? files = null;
        try
        {
            if (IsClipboardFormatAvailable(CF_HDROP)) files = ReadDropFiles();
            if (files is null && IsClipboardFormatAvailable(CF_UNICODETEXT)) text = ReadUnicodeText();
        }
        finally { CloseClipboard(); }

        _cacheSeq = seq;
        _cacheText = text;
        _cacheFiles = files;
        return (text, files);
    }

    private static string? ReadUnicodeText()
    {
        var h = GetClipboardData(CF_UNICODETEXT);
        if (h == IntPtr.Zero) return null;
        if ((long)GlobalSize(h) > MaxTextBytes) return null;

        var p = GlobalLock(h);
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUni(p); }
        finally { GlobalUnlock(h); }
    }

    private static List<string>? ReadDropFiles()
    {
        var h = GetClipboardData(CF_HDROP);
        if (h == IntPtr.Zero) return null;

        var count = DragQueryFileW(h, 0xFFFFFFFF, null, 0);
        var paths = new List<string>();
        for (uint i = 0; i < count && paths.Count < MaxFiles; i++)
        {
            var len = DragQueryFileW(h, i, null, 0);
            if (len == 0) continue;
            var sb = new StringBuilder((int)len + 1);
            DragQueryFileW(h, i, sb, len + 1);
            var path = sb.ToString();
            if (File.Exists(path) || Directory.Exists(path)) paths.Add(Path.GetFullPath(path));
        }
        return paths.Count > 0 ? paths : null;
    }

    // ── Ghi ───────────────────────────────────────────────────────────────

    private bool SetUnicodeText(string text)
    {
        if (!OpenWithRetry(_hwnd)) return false;
        try
        {
            EmptyClipboard();
            var h = AllocGlobal((text.Length + 1) * 2, p =>
            {
                Marshal.Copy(text.ToCharArray(), 0, p, text.Length);
                Marshal.WriteInt16(p, text.Length * 2, 0);
            });
            if (h == IntPtr.Zero) return false;
            if (SetClipboardData(CF_UNICODETEXT, h) == IntPtr.Zero) { GlobalFree(h); return false; }
            return true; // quyền sở hữu bộ nhớ đã chuyển cho hệ thống
        }
        finally { CloseClipboard(); }
    }

    /// <summary>CF_HDROP = DROPFILES (20 byte) + danh sách đường dẫn UTF-16, mỗi chuỗi kết thúc bằng NUL, thêm 1 NUL cuối.</summary>
    private bool SetDropFiles(IReadOnlyList<string> paths)
    {
        var list = string.Concat(paths.Select(p => p + "\0")) + "\0";
        const int headerSize = 20;

        if (!OpenWithRetry(_hwnd)) return false;
        try
        {
            EmptyClipboard();

            var hDrop = AllocGlobal(headerSize + list.Length * 2, p =>
            {
                Marshal.WriteInt32(p, 0, headerSize);                 // pFiles
                Marshal.WriteInt32(p, 16, 1);                         // fWide = TRUE (pt, fNC giữ 0)
                Marshal.Copy(list.ToCharArray(), 0, p + headerSize, list.Length);
            });
            if (hDrop == IntPtr.Zero) return false;
            if (SetClipboardData(CF_HDROP, hDrop) == IntPtr.Zero) { GlobalFree(hDrop); return false; }

            // "Copy" chứ không phải "Cut": dán xong file nguồn (trong thư mục nhận) vẫn còn.
            if (_dropEffectFormat != 0)
            {
                var hEffect = AllocGlobal(4, p => Marshal.WriteInt32(p, 1)); // DROPEFFECT_COPY
                if (hEffect != IntPtr.Zero && SetClipboardData(_dropEffectFormat, hEffect) == IntPtr.Zero)
                    GlobalFree(hEffect);
            }
            return true;
        }
        finally
        {
            CloseClipboard();
            _ownedSeq = GetClipboardSequenceNumber();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IntPtr AllocGlobal(int bytes, Action<IntPtr> fill)
    {
        var h = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)(uint)bytes);
        if (h == IntPtr.Zero) return IntPtr.Zero;
        var p = GlobalLock(h);
        if (p == IntPtr.Zero) { GlobalFree(h); return IntPtr.Zero; }
        try { fill(p); }
        finally { GlobalUnlock(h); }
        return h;
    }

    /// <summary>Clipboard là tài nguyên dùng chung: ứng dụng khác có thể đang giữ → thử lại vài lần.</summary>
    private static bool OpenWithRetry(IntPtr owner)
    {
        for (var i = 0; i < OpenRetries; i++)
        {
            if (OpenClipboard(owner)) return true;
            Thread.Sleep(OpenRetryDelayMs);
        }
        return false;
    }

    public void Dispose()
    {
        if (_jobs.IsAddingCompleted) return;
        _jobs.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(1));
    }
}
