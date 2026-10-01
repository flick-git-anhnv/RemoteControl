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
    private HashSet<string> _remoteNames = new(StringComparer.Ordinal);

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
            _remoteNames = new HashSet<string>(files.Select(x => x.Name), StringComparer.Ordinal);
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
                var name = Path.GetFileName(path);

                // Máy đích đã có file cùng tên → hỏi, không tự đổi tên âm thầm.
                var overwrite = false;
                if (_remoteNames.Contains(name))
                {
                    switch (await AskConflictAsync(name))
                    {
                        case ConflictChoice.Skip: continue;
                        case ConflictChoice.Overwrite: overwrite = true; break;
                        // KeepBoth: để agent tự đổi tên (vd "a (1).txt")
                    }
                }

                SetStatus($"Đang gửi {name}…");
                await _client.UploadFileAsync(path, new Progress<(long Done, long Total)>(ReportProgress), overwrite: overwrite);
            }
            SetStatus($"Đã gửi xong {files.Count} file.");
            await _client.RequestFileListAsync();
        }
        catch (Exception ex) { SetStatus("Lỗi: " + ex.Message); }
        finally { SetBusy(false); }
    }

    private enum ConflictChoice { Overwrite, KeepBoth, Skip }

    /// <summary>Hộp thoại khi file đã tồn tại trên máy đích: Ghi đè / Giữ cả hai (đổi tên) / Bỏ qua.</summary>
    private async Task<ConflictChoice> AskConflictAsync(string name)
    {
        var result = ConflictChoice.Skip; // đóng hộp thoại = bỏ qua (an toàn)
        var dlg = new Window
        {
            Title = "File đã tồn tại",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        Button Make(string text, string bg, ConflictChoice choice)
        {
            var b = new Button
            {
                Content = text, Padding = new Avalonia.Thickness(14, 6),
                Background = Avalonia.Media.Brush.Parse(bg), Foreground = Avalonia.Media.Brushes.White,
            };
            b.Click += (_, _) => { result = choice; dlg.Close(); };
            return b;
        }

        dlg.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20), Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Máy đích đã có file “{name}”.\nBạn muốn làm gì?",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 14,
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Children =
                    {
                        Make("Ghi đè", "#F05922", ConflictChoice.Overwrite),
                        Make("Giữ cả hai", "#251C53", ConflictChoice.KeepBoth),
                        Make("Bỏ qua", "#4A3F8C", ConflictChoice.Skip),
                    },
                },
            },
        };
        await dlg.ShowDialog(this);
        return result;
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
