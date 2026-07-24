using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using IPGS.RemoteControl.CcuClient;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public partial class KioskDeployWindow : Window
    {
        private readonly KioskDeployService _deployService;

        // Thông tin SSH lấy từ hồ sơ máy tính đã lưu (nhập ở "Thêm/Sửa máy tính") —
        // không còn ô nhập SSH riêng trong cửa sổ này để tránh trùng lặp dữ liệu.
        private readonly string _sshHost;
        private readonly int _sshPort;
        private readonly string _sshUser;
        private readonly string _sshPassword;

        public KioskDeployWindow() : this(new ComputerProfile())
        {
        }

        public KioskDeployWindow(ComputerProfile prefill)
        {
            InitializeComponent();
            _deployService = new KioskDeployService();

            _sshHost = prefill.Host;
            _sshPort = prefill.SshPort > 0 ? prefill.SshPort : 22;
            _sshUser = prefill.SshUsername ?? "";
            _sshPassword = prefill.SshPassword ?? "";

            PART_TargetHostText.Text = string.IsNullOrWhiteSpace(_sshHost)
                ? "Đang deploy cho: —"
                : $"Đang deploy cho: {_sshUser}@{_sshHost}:{_sshPort}";

            if (!string.IsNullOrWhiteSpace(_sshUser))
            {
                PART_KioskUser.Text = _sshUser;
            }

            PART_BtnDeploy.Click += OnDeployClick;
        }

        private async void OnDeployClick(object? sender, RoutedEventArgs e)
        {
            string sudoPass = string.IsNullOrEmpty(PART_SudoPassword.Text) ? _sshPassword : PART_SudoPassword.Text;
            string kioskUser = PART_KioskUser.Text?.Trim() ?? "";
            string appExec = PART_AppExec.Text?.Trim() ?? "kioskapp";

            // Tab 1 — Config máy tính
            bool hideTopBar = PART_ChkHideTopBar.IsChecked == true;
            bool hideActivities = PART_ChkHideActivities.IsChecked == true;
            bool hideWorkspace = PART_ChkHideWorkspace.IsChecked == true;
            bool hideDash = PART_ChkHideDash.IsChecked == true;
            bool hideDockIcons = PART_ChkHideDockIcons.IsChecked == true;
            bool installUnclutter = PART_ChkInstallUnclutter.IsChecked == true;
            bool hideKeyboard = PART_ChkHideKeyboard.IsChecked == true;
            bool hotCorner = PART_ChkHotCorner.IsChecked == true;
            bool blockSleep = PART_ChkBlockSleep.IsChecked == true;
            bool initialSetup = PART_ChkInitialSetup.IsChecked == true;
            bool autologin = PART_ChkAutologin.IsChecked == true;
            bool lockWorkspace = PART_ChkLockWorkspace.IsChecked == true;

            // Tab 2 — Config phần mềm
            bool swUpdate = PART_ChkSwUpdate.IsChecked == true;
            bool autostart = PART_ChkAutostart.IsChecked == true;

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Thiếu IP/SSH user — vào 'Sửa' máy tính để bổ sung thông tin SSH.";
                return;
            }

            PART_BtnDeploy.IsEnabled = false;
            PART_LogConsole.Text = "";
            PART_StatusMsg.Foreground = Avalonia.Media.Brushes.SlateGray;
            PART_StatusMsg.Text = "Đang deploy...";

            var options = new KioskDeployOptions
            {
                Host = _sshHost,
                SshPort = _sshPort,
                Username = _sshUser,
                Password = _sshPassword,
                SudoPassword = sudoPass,
                KioskUser = kioskUser,
                AppExec = appExec,
                HideTopBar = hideTopBar,
                HideActivities = hideActivities,
                HideWorkspaceSwitcher = hideWorkspace,
                HideDash = hideDash,
                InstallUnclutter = installUnclutter,
                HideVirtualKeyboard = hideKeyboard,
                DisableHotCorner = hotCorner,
                DisableDockIcons = hideDockIcons,
                BlockSleep = blockSleep,
                SkipInitialSetup = initialSetup,
                EnableAutologin = autologin,
                LockSingleWorkspace = lockWorkspace,
                DisableSoftwareUpdate = swUpdate,
                EnableAutostart = autostart
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
