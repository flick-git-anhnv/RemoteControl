using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using System.IO;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using IPGS.RemoteControl.CcuClient;
using IPGS.RemoteControl.CcuUI.ViewModels;
using IPGS.RemoteControl.CcuUI.Services;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// Remote-desktop window that connects to a ZCU, displays the live screen
/// and forwards mouse events.
/// <para>
/// Usage from IPGSUseCam:
/// <code>
///   var win = new RemoteScreenWindow("192.168.1.100", RemoteControlConstants.DefaultPort, "my-token");
///   win.Show();
/// </code>
/// </para>
/// </summary>
public partial class RemoteScreenWindow : Window
{
    private readonly RemoteScreenViewModel _vm;
    private readonly string _host;
    private readonly int    _port;
    private readonly string _token;
    private SessionRecorder? _recorder;

    // ── F02: phát hiện agent phiên bản cũ + timeout chờ SysInfoResp ──────────
    /// <summary>Phiên bản agent tối thiểu hỗ trợ nhóm Enterprise (SysInfo/Privacy/Chat/Clipboard).</summary>
    private static readonly Version MinAgentVersion = new(1, 1);
    private const int SysInfoTimeoutMs = 10_000;
    private bool _agentVersionWarned;
    private CancellationTokenSource? _sysInfoTimeoutCts;

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and shows the remote-control window.
    /// The window connects automatically when it first opens.
    /// </summary>
    /// <param name="host">ZCU hostname or IP address.</param>
    /// <param name="port">TCP port (default <see cref="RemoteControlConstants.DefaultPort"/> = 17600).</param>
    /// <param name="token">Shared secret token (must match ZcuAgent appsettings.json).</param>
    public RemoteScreenWindow(string host, int port, string token, bool showSshHelp = false)
    {
        _host  = host;
        _port  = port;
        _token = token;

        _vm = new RemoteScreenViewModel
        {
            ShowSshHelp = showSshHelp
        };

        InitializeComponent();

        DataContext = _vm;
        Title = $"Remote Control — {host}:{port}";

        _vm.Client.SysInfoReceived += OnSysInfoReceived;
        _vm.Client.ClipboardDataReceived += OnClipboardDataReceived;
        _vm.Client.MonitorListReceived += OnMonitorListReceived;
        _vm.Client.ClipboardFileListReceived += OnClipboardFileListReceived;
        RemoteScreen.PasteStatus += s => Dispatcher.UIThread.Post(() => SetStatusTitle(s));
        _vm.Client.FrameReceived += OnFrameReceived;
        _vm.Client.StateChanged += OnClientStateChanged;
    }

    // ── Window lifecycle ─────────────────────────────────────────────────────

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Start connection loop immediately; CcuClient fires StateChanged events
        // back to the ViewModel which updates the UI via Dispatcher.UIThread.Post.
        await _vm.ConnectAsync(_host, _port, _token);
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // A3: unsubscribe TRƯỚC, disconnect XONG rồi mới dispose recorder — thứ tự cũ
        // (dispose recorder trước DisconnectAsync) để frame từ receive-thread vẫn tới
        // trong khoảng đó và đập vào recorder đã dispose.
        _vm.Client.FrameReceived   -= OnFrameReceived;
        _vm.Client.SysInfoReceived -= OnSysInfoReceived;
        _vm.Client.ClipboardDataReceived -= OnClipboardDataReceived;
        _vm.Client.MonitorListReceived -= OnMonitorListReceived;
        _vm.Client.ClipboardFileListReceived -= OnClipboardFileListReceived;
        _vm.Client.StateChanged  -= OnClientStateChanged;
        _sysInfoTimeoutCts?.Cancel();
        _fileWindow?.Close();

        // Gracefully disconnect before the window is destroyed.
        await _vm.DisconnectAsync();

        _recorder?.Dispose();
        _recorder = null;
        _vm.Dispose();
    }

    // ── Phase 6 Handlers ──────────────────────────────────────────────────

    private void OnFrameReceived(object? sender, FrameReceivedEventArgs e)
    {
        // Chạy trên thread nhận TCP của CcuClient — đọc field 1 lần vào local để không
        // đua với UI thread gán _recorder = null (SessionRecorder tự thread-safe thêm 1 lớp).
        var recorder = _recorder;
        if (recorder == null) return;

        // ZCU đổi độ phân giải giữa chừng → AVI header (width/height cố định) sẽ sai,
        // video hỏng. Dừng ghi ngay và báo người dùng thay vì ghi tiếp file rác.
        if (e.Width > 0 && e.Height > 0 &&
            (e.Width != recorder.Width || e.Height != recorder.Height))
        {
            _recorder = null;
            recorder.Dispose();
            Dispatcher.UIThread.Post(() =>
            {
                if (this.FindControl<ToggleButton>("BtnRecord") is { } btn)
                {
                    btn.IsChecked = false;
                    btn.Content = "🔴 Record";
                }
                Title = $"Remote Control — {_host}:{_port} (⏹ Đã dừng ghi hình: độ phân giải ZCU thay đổi)";
            });
            return;
        }

        recorder.AddFrame(e.JpegData.Span);
    }

    private void OnSysInfoReceived(object? sender, string json)
    {
        // F02: response đã tới → hủy timeout đang chờ (nếu có)
        _sysInfoTimeoutCts?.Cancel();
        Dispatcher.UIThread.Post(() =>
        {
            var win = new SystemInventoryWindow();
            win.LoadFromJson(json);
            win.Show();
        });
    }

    // ── F02: cảnh báo agent phiên bản cũ (fail âm thầm SysInfo/Privacy/Chat/Clipboard) ──

    // ── File Copy trên ZCU → clipboard Windows ───────────────────────────────

    private string _titleBase => $"Remote Control — {_host}:{_port}";
    private void SetStatusTitle(string? status)
        => Title = string.IsNullOrEmpty(status) ? _titleBase : $"{_titleBase}  |  {status}";

    private string? _lastClipFileJson;
    private int _clipFilesBusy;

    /// <summary>
    /// Người dùng vừa Copy file trong trình quản lý file của ZCU → tải các file về thư mục tạm rồi đặt vào
    /// clipboard Windows, để Ctrl+V trong Explorer dán ra được. Mỗi danh sách chỉ tải một lần.
    /// </summary>
    private async void OnClipboardFileListReceived(object? sender, string json)
    {
        if (json == _lastClipFileJson) return;
        if (Interlocked.CompareExchange(ref _clipFilesBusy, 1, 0) != 0) return;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray()
                .Select(e => (Path: e.GetProperty("path").GetString()!, Name: e.GetProperty("name").GetString()!))
                .ToList();
            if (items.Count == 0) return;
            _lastClipFileJson = json;

            var root = Path.Combine(Path.GetTempPath(), "IPGS-Remote", "clip");
            CleanOldTempDirs(root);
            var dir = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            var local = new List<string>();
            for (var i = 0; i < items.Count; i++)
            {
                var dest = Path.Combine(dir, Path.GetFileName(items[i].Name));
                var idx = i;
                var progress = new Progress<(long Done, long Total)>(p => SetStatusTitle(
                    $"Đang tải {items[idx].Name} từ máy đích ({idx + 1}/{items.Count}) — {(p.Total <= 0 ? 0 : p.Done * 100 / p.Total)}%"));
                await _vm.Client.DownloadRemotePathAsync(items[i].Path, dest, progress);
                local.Add(dest);
            }

            var top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard is { } clipboard)
            {
                var storage = new List<IStorageItem>();
                foreach (var p in local)
                    if (await top.StorageProvider.TryGetFileFromPathAsync(p) is { } f) storage.Add(f);
                await clipboard.SetFilesAsync(storage);
                SetStatusTitle($"Đã chép {storage.Count} file từ máy đích vào clipboard — dán (Ctrl+V) trong Explorer");
            }
        }
        catch (Exception ex)
        {
            SetStatusTitle("⚠ Không lấy được file từ máy đích: " + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _clipFilesBusy, 0);
            _ = Task.Delay(8000).ContinueWith(_ => Dispatcher.UIThread.Post(() => SetStatusTitle(null)));
        }
    }

    private static void CleanOldTempDirs(string root)
    {
        try
        {
            if (!Directory.Exists(root)) return;
            foreach (var d in Directory.GetDirectories(root))
                if (Directory.GetCreationTimeUtc(d) < DateTime.UtcNow.AddDays(-1)) Directory.Delete(d, true);
        }
        catch { /* file đang được dùng — bỏ qua */ }
    }

    // ── Multi-monitor ────────────────────────────────────────────────────────

    private bool _suppressMonitorChange;

    private void OnMonitorListReceived(object? sender, string json)
    {
        List<(int Index, string Name, int W, int H)> monitors = [];
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
                monitors.Add((el.GetProperty("index").GetInt32(), el.GetProperty("name").GetString() ?? "?",
                              el.GetProperty("w").GetInt32(), el.GetProperty("h").GetInt32()));
        }
        catch { return; }

        Dispatcher.UIThread.Post(() =>
        {
            var cmb = this.FindControl<ComboBox>("CmbMonitor");
            if (cmb is null) return;

            _suppressMonitorChange = true;
            cmb.Items.Clear();
            if (monitors.Count >= 2)
            {
                cmb.Items.Add(new ComboBoxItem { Content = "🖥 Tất cả màn hình", Tag = (byte)0xFF });
                foreach (var m in monitors)
                    cmb.Items.Add(new ComboBoxItem { Content = $"🖥 {m.Index + 1}: {m.Name} ({m.W}×{m.H})", Tag = (byte)m.Index });
                cmb.SelectedIndex = 0;
            }
            cmb.IsVisible = monitors.Count >= 2;
            _suppressMonitorChange = false;
        });
    }

    private async void OnMonitorChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressMonitorChange) return;
        if (sender is ComboBox { SelectedItem: ComboBoxItem { Tag: byte idx } })
            await _vm.Client.SelectMonitorAsync(idx);
    }

    private void OnClientStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        // Mỗi lần (tái) kết nối: hỏi danh sách màn hình — chỉ agent ≥ 1.2 mới trả lời.
        if (e.Current == ConnectionState.Streaming && !IsAgentOlderThan(_vm.Client.ServerName, new Version(1, 2)))
            _ = _vm.Client.RequestMonitorsAsync();

        if (e.Current != ConnectionState.Streaming || _agentVersionWarned) return;

        var serverName = _vm.Client.ServerName;
        if (!IsAgentOutdated(serverName)) return;

        _agentVersionWarned = true;
        Dispatcher.UIThread.Post(() => ShowInfoDialog(
            "Agent phiên bản cũ",
            $"Agent trên máy đích báo phiên bản \"{serverName}\" — cũ hơn phiên bản tối thiểu " +
            $"ZcuAgent/{MinAgentVersion} mà client này cần.\n\n" +
            "Các tính năng SysInfo / Privacy / Chat / Sync Clipboard sẽ KHÔNG hoạt động " +
            "(agent cũ bỏ qua yêu cầu mà không báo lỗi).\n\n" +
            "Khuyến nghị: cập nhật Remote Agent qua nút \"⚡ Cài remote\" của máy này."));
    }

    /// <summary>"ZcuAgent/1.1" → 1.1; không parse được (agent lạ/quá cũ) → coi là outdated.</summary>
    private static bool IsAgentOutdated(string serverName)
    {
        var idx = serverName.LastIndexOf('/');
        if (idx < 0 || idx == serverName.Length - 1) return true;
        return !Version.TryParse(serverName[(idx + 1)..], out var v) || v < MinAgentVersion;
    }

    private void ShowInfoDialog(string title, string message)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 13,
                    },
                }
            }
        };
        var btnOk = new Button
        {
            Content = "Đã hiểu",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Padding = new Avalonia.Thickness(24, 6),
        };
        btnOk.Click += (_, _) => dlg.Close();
        ((StackPanel)dlg.Content!).Children.Add(btnOk);
        dlg.ShowDialog(this);
    }

    /// <summary>Clipboard ZCU→CCU: agent đẩy text khi nội dung clipboard trên ZCU đổi → đặt vào clipboard máy này.</summary>
    private void OnClipboardDataReceived(object? sender, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                    await clipboard.SetTextAsync(text);
            }
            catch { /* clipboard máy này đang bị tiến trình khác giữ — bỏ qua lần này */ }
        });
    }

    // X11 keysym: XK_Control_L, XK_Alt_L, XK_Delete
    private const uint KeysymControlL = 0xFFE3;
    private const uint KeysymAltL     = 0xFFE9;
    private const uint KeysymDelete   = 0xFFFF;

    /// <summary>Gửi tổ hợp Ctrl+Alt+Del qua giao thức (hệ điều hành máy này không bắt được vì không đi qua bàn phím vật lý).</summary>
    private async void OnCtrlAltDelClick(object? sender, RoutedEventArgs e)
    {
        var c = _vm.Client;
        await c.SendKeyEventAsync(KeysymControlL, true);
        await c.SendKeyEventAsync(KeysymAltL, true);
        await c.SendKeyEventAsync(KeysymDelete, true);
        await c.SendKeyEventAsync(KeysymDelete, false);
        await c.SendKeyEventAsync(KeysymAltL, false);
        await c.SendKeyEventAsync(KeysymControlL, false);
    }

    private FileTransferWindow? _fileWindow;

    private void OnFilesClick(object? sender, RoutedEventArgs e)
    {
        if (IsAgentOlderThan(_vm.Client.ServerName, new Version(1, 2)))
        {
            ShowInfoDialog("Agent chưa hỗ trợ",
                "Truyền file cần ZcuAgent 1.2 trở lên. Cập nhật agent qua nút \"⚡ Cài remote\" của máy này.");
            return;
        }
        if (_fileWindow is { IsVisible: true }) { _fileWindow.Activate(); return; }

        _fileWindow = new FileTransferWindow(_vm.Client);
        _fileWindow.Closed += (_, _) => _fileWindow = null;
        _fileWindow.Show(this);
    }

    private void OnScanModeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn) return;
        var on = btn.IsChecked ?? false;

        if (on && IsAgentOlderThan(_vm.Client.ServerName, new Version(1, 2)))
        {
            btn.IsChecked = false;
            ShowInfoDialog("Agent chưa hỗ trợ",
                "Chế độ phím vật lý cần ZcuAgent 1.2 trở lên. Cập nhật agent qua nút \"⚡ Cài remote\" của máy này.");
            return;
        }

        _vm.ReleaseAllDownKeys();
        _vm.UseScanMode = on;
    }

    private static bool IsAgentOlderThan(string serverName, Version min)
    {
        var idx = serverName.LastIndexOf('/');
        return idx < 0 || !Version.TryParse(serverName[(idx + 1)..], out var v) || v < min;
    }

    private async void OnPrivacyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            await _vm.Client.SetPrivacyModeAsync(btn.IsChecked ?? false);
        }
    }

    private async void OnSysInfoClick(object? sender, RoutedEventArgs e)
    {
        // F02: request-response duy nhất trong nhóm Enterprise — nếu agent không trả
        // SysInfoResp trong SysInfoTimeoutMs (agent cũ bỏ qua âm thầm, hoặc nghẽn),
        // báo người dùng thay vì im lặng mãi mãi.
        _sysInfoTimeoutCts?.Cancel();
        var cts = new CancellationTokenSource();
        _sysInfoTimeoutCts = cts;

        await _vm.Client.RequestSysInfoAsync();

        try
        {
            await Task.Delay(SysInfoTimeoutMs, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // SysInfoResp đã tới (hoặc cửa sổ đóng) — không cần cảnh báo
        }

        ShowInfoDialog(
            "Không nhận được phản hồi SysInfo",
            $"Đã gửi yêu cầu SysInfo nhưng không nhận được phản hồi sau {SysInfoTimeoutMs / 1000} giây.\n\n" +
            "Nguyên nhân thường gặp:\n" +
            "  • Agent trên máy đích là phiên bản cũ chưa hỗ trợ SysInfo — cập nhật qua \"⚡ Cài remote\".\n" +
            "  • Agent đang quá tải hoặc kết nối không ổn định — thử lại sau.");
    }

    private async void OnRecordClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            if (btn.IsChecked == true)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                    {
                        Title = "Lưu video ghi hình",
                        DefaultExtension = "avi",
                        SuggestedFileName = $"RemoteSession_{DateTime.Now:yyyyMMdd_HHmmss}.avi",
                        FileTypeChoices = new[]
                        {
                            new Avalonia.Platform.Storage.FilePickerFileType("AVI Video") { Patterns = new[] { "*.avi" } }
                        }
                    });

                    if (file != null && file.TryGetLocalPath() is string path)
                    {
                        int sw = _vm.Client.ScreenWidth, sh = _vm.Client.ScreenHeight;
                        if (sw <= 0 || sh <= 0)
                        {
                            // Chưa nhận frame nào → chưa biết độ phân giải, AVI header sẽ sai.
                            btn.IsChecked = false;
                            return;
                        }
                        _recorder = new SessionRecorder(path, sw, sh, 15);
                        btn.Content = "⏹️ Stop";
                        return;
                    }
                }

                btn.IsChecked = false;
            }
            else
            {
                // A3: gán null TRƯỚC rồi mới Dispose — OnFrameReceived (receive-thread)
                // đọc snapshot field nên frame đang bay hoặc bị bỏ qua (null) hoặc được
                // SessionRecorder (đã thread-safe) nuốt êm, không còn ObjectDisposedException.
                var rec = _recorder;
                _recorder = null;
                rec?.Dispose();
                btn.Content = "🔴 Record";
            }
        }
    }

    private async void OnClipboardSyncClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            string? text = await clipboard.TryGetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                await _vm.Client.SendClipboardTextAsync(text);
            }
        }
    }

    private void OnChatSendClick(object? sender, RoutedEventArgs e)
    {
        var input = this.FindControl<TextBox>("PART_ChatInput");
        if (input != null && !string.IsNullOrWhiteSpace(input.Text))
        {
            _ = _vm.Client.SendChatMessageAsync(input.Text);
            input.Text = "";
        }
    }

    private void OnChatKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            OnChatSendClick(sender, new RoutedEventArgs());
        }
    }

    // ── Toolbar button handlers ──────────────────────────────────────────────

    private async void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        await _vm.DisconnectAsync();
    }
}
