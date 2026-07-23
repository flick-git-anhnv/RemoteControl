using Avalonia.Controls;
using Avalonia.Interactivity;
using KztekComponentAvalonia.Controls;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// Cửa sổ khởi động của app IPGS.RemoteControl.CcuUI độc lập.
/// Người dùng nhập IP/Port/Token rồi nhấn "Kết nối" để mở
/// <see cref="RemoteScreenWindow"/>.
/// </summary>
/// <remarks>
/// UX flow: ConnectionEntryWindow (MainWindow) ẩn đi khi mở RemoteScreenWindow,
/// hiện lại khi RemoteScreenWindow đóng — cho phép kết nối lại mà không cần
/// khởi động lại app.
/// </remarks>
public partial class ConnectionEntryWindow : Window
{
    public ConnectionEntryWindow()
    {
        InitializeComponent();

        if (this.FindControl<KzButton>("PART_BtnExit") is { } btnExit)
            btnExit.Click += (_, _) => Close();

        if (this.FindControl<KzButton>("PART_BtnConnect") is { } btnConnect)
            btnConnect.Click += OnConnectClick;
    }

    private void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        string host  = this.FindControl<KzTextBox>("PART_Host")?.Text?.Trim()  ?? "";
        string portS = this.FindControl<KzTextBox>("PART_Port")?.Text?.Trim()  ?? "17600";
        string token = this.FindControl<KzTextBox>("PART_Token")?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(host)) return;   // host là bắt buộc

        int port = int.TryParse(portS, out int p) ? p : 17600;

        var screenWin = new RemoteScreenWindow(host, port, token);

        // Ẩn cửa sổ nhập liệu khi đang điều khiển — hiện lại khi remote window đóng.
        this.Hide();
        screenWin.Closed += (_, _) => this.Show();
        screenWin.Show();
    }
}
