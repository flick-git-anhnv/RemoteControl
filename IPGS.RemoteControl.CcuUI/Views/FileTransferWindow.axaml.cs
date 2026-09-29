using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// Truyền file hai chiều qua chính kết nối remote (không cần SSH): gửi file lên thư mục nhận của
/// máy đích và tải file trong thư mục đó về. Cần ZcuAgent ≥ 1.2.
/// </summary>
public partial class FileTransferWindow : Window
{
    private sealed record RemoteFile(string Name, long Size)
    {
        public override string ToString() => $"{Name}   ({FormatSize(Size)})";
    }

    private readonly RemoteControlClient _client;
    private bool _busy;

    // XAML loader cần constructor không tham số (Avalonia designer/runtime loader).
    public FileTransferWindow() : this(new RemoteControlClient()) { }

    public FileTransferWindow(RemoteControlClient client)
    {
        _client = client;
        InitializeComponent();
        _client.FileListReceived += OnFileListReceived;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await _client.RequestFileListAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _client.FileListReceived -= OnFileListReceived;
    }

    // ── Danh sách file trên máy đích ─────────────────────────────────────────

    private void OnFileListReceived(object? sender, string json)
    {
        var files = new List<RemoteFile>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
                files.Add(new RemoteFile(el.GetProperty("name").GetString() ?? "?", el.GetProperty("size").GetInt64()));
        }
        catch { return; }

        Dispatcher.UIThread.Post(() =>
        {
            FileList.ItemsSource = files;
            SetStatus(files.Count == 0
                ? "Thư mục nhận trên máy đích đang trống."
                : $"{files.Count} file trong thư mục nhận của máy đích.");
        });
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) => await _client.RequestFileListAsync();

    // ── Upload ───────────────────────────────────────────────────────────────

    private async void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Chọn file gửi lên máy đích",
            AllowMultiple = true,
        });
        if (files.Count == 0) return;

        SetBusy(true);
        try
        {
            foreach (var f in files)
            {
                if (f.TryGetLocalPath() is not { } path) continue;
                SetStatus($"Đang gửi {Path.GetFileName(path)}…");
                await _client.UploadFileAsync(path, new Progress<(long Done, long Total)>(ReportProgress));
            }
            SetStatus($"Đã gửi xong {files.Count} file.");
            await _client.RequestFileListAsync();
        }
        catch (Exception ex) { SetStatus("Lỗi: " + ex.Message); }
        finally { SetBusy(false); }
    }

    // ── Download ─────────────────────────────────────────────────────────────

    private async void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || FileList.SelectedItem is not RemoteFile item) { SetStatus("Chọn một file trong danh sách trước."); return; }

        var dest = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Lưu file về máy này",
            SuggestedFileName = item.Name,
        });
        if (dest?.TryGetLocalPath() is not { } localPath) return;

        SetBusy(true);
        try
        {
            SetStatus($"Đang tải {item.Name}…");
            await _client.DownloadFileAsync(item.Name, localPath, new Progress<(long Done, long Total)>(ReportProgress));
            SetStatus($"Đã tải xong: {localPath}");
        }
        catch (Exception ex) { SetStatus("Lỗi: " + ex.Message); }
        finally { SetBusy(false); }
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    private void ReportProgress((long Done, long Total) p)
        => Dispatcher.UIThread.Post(() => TransferProgress.Value = p.Total <= 0 ? 0 : p.Done * 100.0 / p.Total);

    private void SetStatus(string text) => StatusText.Text = text;

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BtnUpload.IsEnabled = BtnDownload.IsEnabled = BtnRefresh.IsEnabled = !busy;
        if (!busy) TransferProgress.Value = 0;
    }

    private static string FormatSize(long bytes)
    {
        string[] suf = ["B", "KB", "MB", "GB"];
        double v = bytes; var i = 0;
        while (v >= 1024 && i < suf.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {suf[i]}";
    }
}
