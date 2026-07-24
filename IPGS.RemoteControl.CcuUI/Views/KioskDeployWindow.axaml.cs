using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using IPGS.RemoteControl.CcuClient;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public partial class KioskDeployWindow : Window
    {
        private readonly KioskDeployService _deployService;

        public KioskDeployWindow() : this(null)
        {
        }

        public KioskDeployWindow(ComputerProfile? prefill)
        {
            InitializeComponent();
            _deployService = new KioskDeployService();

            PART_BtnTestSsh.Click += OnTestSshClick;
            PART_BtnDeploy.Click += OnDeployClick;

            if (prefill != null)
            {
                PART_SshHost.Text = prefill.Host;
                if (prefill.SshPort > 0) PART_SshPort.Text = prefill.SshPort.ToString();
                if (!string.IsNullOrWhiteSpace(prefill.SshUsername))
                {
                    PART_SshUser.Text = prefill.SshUsername;
                    PART_KioskUser.Text = prefill.SshUsername;
                }
                if (!string.IsNullOrWhiteSpace(prefill.SshPassword)) PART_SshPassword.Text = prefill.SshPassword;
            }
        }

        private async void OnTestSshClick(object? sender, RoutedEventArgs e)
        {
            string host = PART_SshHost.Text?.Trim() ?? "";
            if (!int.TryParse(PART_SshPort.Text?.Trim(), out int port)) port = 22;
            string user = PART_SshUser.Text?.Trim() ?? "";
            string pass = PART_SshPassword.Text ?? "";

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user))
            {
                PART_StatusMsg.Text = "Vui lòng nhập đầy đủ IP máy kiosk và SSH user.";
                return;
            }

            PART_BtnTestSsh.IsEnabled = false;
            PART_StatusMsg.Text = "Đang thử kết nối SSH...";
            Log("🔄 Đang thử kết nối SSH tới " + host + ":" + port + "...");

            var options = new KioskDeployOptions
            {
                Host = host,
                SshPort = port,
                Username = user,
                Password = pass
            };

            bool success = await _deployService.TestSshConnectionAsync(options);
            PART_BtnTestSsh.IsEnabled = true;

            if (success)
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Green;
                PART_StatusMsg.Text = "✅ Kết nối SSH thành công!";
                Log("✅ Kết nối SSH thành công tới " + host);
            }
            else
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "❌ Kết nối SSH thất bại. Kiểm tra IP/Username/Password.";
                Log("❌ Kết nối SSH thất bại.");
            }
        }

        private async void OnDeployClick(object? sender, RoutedEventArgs e)
        {
            string host = PART_SshHost.Text?.Trim() ?? "";
            if (!int.TryParse(PART_SshPort.Text?.Trim(), out int port)) port = 22;
            string user = PART_SshUser.Text?.Trim() ?? "";
            string pass = PART_SshPassword.Text ?? "";
            string sudoPass = string.IsNullOrEmpty(PART_SudoPassword.Text) ? pass : PART_SudoPassword.Text;
            string kioskUser = PART_KioskUser.Text?.Trim() ?? "";
            string appExec = PART_AppExec.Text?.Trim() ?? "ipgskioskavalonia";

            bool runStep1 = PART_ChkStep1.IsChecked == true;
            bool runStep2 = PART_ChkStep2.IsChecked == true;
            string toggleMode = PART_ToggleHide.IsChecked == true ? "hide"
                : PART_ToggleShow.IsChecked == true ? "show"
                : "";

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Vui lòng nhập đầy đủ IP, SSH user và SSH password.";
                return;
            }

            if (!runStep1 && !runStep2 && string.IsNullOrEmpty(toggleMode))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Chọn ít nhất 1 hành động (Script 1, Script 2, hoặc Ẩn/Hiện Top Bar).";
                return;
            }

            PART_BtnDeploy.IsEnabled = false;
            PART_BtnTestSsh.IsEnabled = false;
            PART_LogConsole.Text = "";
            PART_StatusMsg.Foreground = Avalonia.Media.Brushes.SlateGray;
            PART_StatusMsg.Text = "Đang deploy...";

            var options = new KioskDeployOptions
            {
                Host = host,
                SshPort = port,
                Username = user,
                Password = pass,
                SudoPassword = sudoPass,
                KioskUser = kioskUser,
                AppExec = appExec,
                RunInstallSoftware = runStep1,
                RunConfigureSystem = runStep2,
                ToggleMode = toggleMode
            };

            try
            {
                await _deployService.DeployAsync(options, Log);

                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Green;
                PART_StatusMsg.Text = "🎉 Deploy hoàn tất! Nhớ RESTART máy kiosk để áp dụng autologin + autostart.";
            }
            catch (Exception ex)
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "❌ Deploy thất bại: " + ex.Message;
                Log("❌ LỖI: " + ex.Message);
            }
            finally
            {
                PART_BtnDeploy.IsEnabled = true;
                PART_BtnTestSsh.IsEnabled = true;
            }
        }

        private void Log(string text)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                PART_LogConsole.Text += $"[{DateTime.Now:HH:mm:ss}] {text}\n";
                PART_LogConsole.CaretIndex = PART_LogConsole.Text.Length;
            });
        }
    }
}
