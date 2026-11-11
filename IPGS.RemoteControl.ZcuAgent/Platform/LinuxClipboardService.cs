using System.Diagnostics;
using IPGS.RemoteControl.ZcuAgent.Interop;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Platform;

/// <summary>
/// Clipboard X11 qua <c>xclip</c> (chữ, danh sách file đọc từ TARGETS) và <see cref="X11ClipboardOwner"/>
/// (đặt file lên clipboard). Logic chuyển nguyên từ ClientSession/FileTransferHandler.
/// </summary>
internal sealed class LinuxClipboardService : IClipboardService
{
    private readonly ILogger<LinuxClipboardService> _logger;
    private readonly object _ownerGate = new();
    private X11ClipboardOwner? _owner;

    public LinuxClipboardService(ILogger<LinuxClipboardService> logger) => _logger = logger;

    /// <summary>Đọc text clipboard ZCU qua <c>xclip -o</c>. Trả null nếu rỗng/không phải text/xclip lỗi.</summary>
    public Task<string?> GetTextAsync(CancellationToken ct) => RunXclipOutAsync(null, ct);

    /// <summary>
    /// Nếu clipboard ZCU đang chứa file (Copy trong Nautilus/PCManFM/Thunar…) → danh sách đường dẫn tuyệt đối
    /// (chỉ mục file:// cục bộ); ngược lại null. Đọc TARGETS trước để không tốn thêm tiến trình khi là chữ.
    /// </summary>
    public async Task<List<string>?> GetFilePathsAsync(CancellationToken ct)
    {
        var targets = await RunXclipOutAsync("TARGETS", ct);
        if (targets is null) return null;

        string? target = null;
        foreach (var t in targets.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (t == "x-special/gnome-copied-files") { target = t; break; }
            if (t == "text/uri-list") target = t;
        }
        if (target is null) return null;

        var raw = await RunXclipOutAsync(target, ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var paths = new List<string>();
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (line.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(line, UriKind.Absolute, out var uri) && uri.IsFile)
                paths.Add(uri.LocalPath);
        return paths.Count > 0 ? paths : null;
    }

    /// <summary><c>xclip -selection clipboard -o [-t target]</c>; null nếu lỗi/rỗng/quá 2 giây.</summary>
    private async Task<string?> RunXclipOutAsync(string? target, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("xclip")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };
            psi.ArgumentList.Add("-selection");
            psi.ArgumentList.Add("clipboard");
            psi.ArgumentList.Add("-o");
            if (target is not null) { psi.ArgumentList.Add("-t"); psi.ArgumentList.Add(target); }

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                var text = await proc.StandardOutput.ReadToEndAsync(cts.Token);
                await proc.WaitForExitAsync(cts.Token);
                return proc.ExitCode == 0 ? text : null;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
                return null;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "xclip -o failed");
            return null;
        }
    }

    /// <summary>
    /// Push text into the X11 clipboard via <c>xclip</c>, async and fully disposed (audit L2).
    /// GOTCHA: xclip daemonizes (forks a child that owns the selection) — the parent we
    /// spawn exits almost immediately after stdin closes, but if it has not exited within
    /// the grace period we simply release our handle WITHOUT killing it: killing the tree
    /// would drop the clipboard content we just set.
    /// </summary>
    public async Task SetTextAsync(string text, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("xclip")
            {
                RedirectStandardInput = true,
                UseShellExecute       = false,
            };
            psi.ArgumentList.Add("-selection");
            psi.ArgumentList.Add("clipboard");

            using var proc = Process.Start(psi);
            if (proc is null) return;

            await proc.StandardInput.WriteAsync(text.AsMemory(), ct);
            proc.StandardInput.Close();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("xclip did not exit within grace period " +
                                 "(likely daemonized) — releasing handle without killing");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "failed to set clipboard via xclip");
        }
    }

    public bool SetFiles(IReadOnlyList<string> absolutePaths)
    {
        lock (_ownerGate)
        {
            _owner ??= new X11ClipboardOwner(_logger);
            return _owner.SetFiles(absolutePaths);
        }
    }

    public void ReleaseFiles()
    {
        lock (_ownerGate)
        {
            _owner?.Dispose();
            _owner = null;
        }
    }
}
