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
        _recorder?.Dispose();
        // Gracefully disconnect before the window is destroyed.
        await _vm.DisconnectAsync();
        _vm.Dispose();
    }

    // ── Phase 6 Handlers ──────────────────────────────────────────────────

    private void OnFrameReceived(object? sender, FrameReceivedEventArgs e)
    {
        if (_recorder != null)
        {
            _recorder.AddFrame(e.JpegData.Span);
        }
    }

    private void OnSysInfoReceived(object? sender, string json)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var win = new SystemInventoryWindow();
            win.LoadFromJson(json);
            win.Show();
        });
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
        await _vm.Client.RequestSysInfoAsync();
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
                        _recorder = new SessionRecorder(path, _vm.Client.ScreenWidth, _vm.Client.ScreenHeight, 15);
                        btn.Content = "⏹️ Stop";
                        return;
                    }
                }

                btn.IsChecked = false;
            }
            else
            {
                _recorder?.Dispose();
                _recorder = null;
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
