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
        _vm.Client.StateChanged    -= OnClientStateChanged;
        _sysInfoTimeoutCts?.Cancel();

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

    private void OnClientStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
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
