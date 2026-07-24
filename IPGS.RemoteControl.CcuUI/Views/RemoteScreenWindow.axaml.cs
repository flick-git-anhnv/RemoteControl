using Avalonia.Controls;
using Avalonia.Interactivity;
using IPGS.RemoteControl.CcuClient;
using IPGS.RemoteControl.CcuUI.ViewModels;

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
        // Gracefully disconnect before the window is destroyed.
        await _vm.DisconnectAsync();
        _vm.Dispose();
    }

    // ── Toolbar button handlers ──────────────────────────────────────────────

    private async void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        await _vm.DisconnectAsync();
    }
}
