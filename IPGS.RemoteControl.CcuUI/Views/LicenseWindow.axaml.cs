using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using IPGS.RemoteControl.CcuUI.Services;
using KztekComponentAvalonia.Controls;
using System.Threading.Tasks;

namespace IPGS.RemoteControl.CcuUI.Views;

public partial class LicenseWindow : Window
{
    public LicenseWindow()
    {
        InitializeComponent();

        if (this.FindControl<KzTextBox>("PART_HardwareId") is { } hwIdBox)
            hwIdBox.Text = LicenseManagerService.HardwareId;

        if (this.FindControl<KzButton>("PART_BtnCopyHwId") is { } btnCopy)
            btnCopy.Click += OnCopyHwIdClick;

        if (this.FindControl<KzButton>("PART_BtnActivate") is { } btnActivate)
            btnActivate.Click += OnActivateClick;
    }

    private async void OnCopyHwIdClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(LicenseManagerService.HardwareId);
            SetStatus("Đã sao chép Hardware ID vào bộ nhớ đệm.", Brushes.Green);
        }
    }

    private async void OnActivateClick(object? sender, RoutedEventArgs e)
    {
        var keyInput = this.FindControl<KzTextBox>("PART_LicenseKeyInput")?.Text?.Trim();
        if (string.IsNullOrEmpty(keyInput))
        {
            SetStatus("Vui lòng nhập License Key.", Brushes.Red);
            return;
        }

        if (LicenseManagerService.ApplyLicense(keyInput))
        {
            // Activate success
            SetStatus("Kích hoạt thành công! Ứng dụng sẽ khởi động lại...", Brushes.Green);
            await Task.Delay(1500);

            // Open main window
            var mainWin = new ConnectionEntryWindow();
            mainWin.Show();
            this.Close();
        }
        else
        {
            SetStatus(LicenseManagerService.LastError, Brushes.Red);
        }
    }

    private void SetStatus(string message, IBrush color)
    {
        if (this.FindControl<TextBlock>("PART_StatusMsg") is { } statusMsg)
        {
            statusMsg.Text = message;
            statusMsg.Foreground = color;
        }
    }
}
