using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using IPGS.RemoteControl.CcuClient;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public partial class KioskDeployWindow : Window
    {
        private readonly KioskDeployService _deployService;

        private readonly string _sshHost;
        private readonly int _sshPort;
        private readonly string _sshUser;
        private readonly string _sshPassword;

        // ── Task A: nạp danh sách app exec từ ZCU ─────────────────────────
        // Ánh xạ display-text → exec-command + trạng thái verified.
        // null = chưa nạp (dùng default hardcoded hoặc user nhập tay).
        private sealed record LoadedApp(string DisplayText, string ExecCommand, bool ExistsOnSystem);
        private List<LoadedApp>? _loadedApps;
        private CancellationTokenSource? _loadAppsCts;

        // Danh sách gợi ý mặc định — hiển thị ngay khi chưa nạp từ ZCU
        private static readonly IReadOnlyList<string> _defaultExecSuggestions =
            new[] { "ipgskioskavalonia", "kioskapp", "ipgs-kiosk-app" };

        public KioskDeployWindow() : this(new ComputerProfile())
        {
        }

        public KioskDeployWindow(ComputerProfile prefill)
        {
            InitializeComponent();
            _deployService = new KioskDeployService();

            _sshHost     = prefill.Host;
            _sshPort     = prefill.SshPort > 0 ? prefill.SshPort : 22;
            _sshUser     = prefill.SshUsername ?? "";
            _sshPassword = prefill.SshPassword ?? "";

            PART_TargetHostText.Text = string.IsNullOrWhiteSpace(_sshHost)
                ? "Đang deploy cho: —"
                : $"Đang deploy cho: {_sshUser}@{_sshHost}:{_sshPort}";

            if (!string.IsNullOrWhiteSpace(_sshUser))
                PART_KioskUser.Text = _sshUser;

            // Nạp gợi ý mặc định cho App exec (trước khi user bấm 🔄)
            PART_AppExec.ItemsSource = _defaultExecSuggestions;
            PART_AppExec.Text = _defaultExecSuggestions[0];

            // Wire up events
            PART_BtnDeploy.Click       += OnDeployClick;
            PART_BtnLoadApps.Click     += OnLoadAppsClick;
            PART_BtnSelectAll.Click    += (_, _) => SetAllConfigMachineCheckboxes(true);
            PART_BtnDeselectAll.Click  += (_, _) => SetAllConfigMachineCheckboxes(false);
        }

        // ── Task A: Nạp danh sách ứng dụng từ ZCU ────────────────────────

        private async void OnLoadAppsClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_LoadAppsStatus.Foreground = Brushes.Red;
                PART_LoadAppsStatus.Text = "Thiếu thông tin SSH — vào 'Sửa' máy tính để bổ sung.";
                return;
            }

            // Huỷ lần nạp trước nếu đang chạy
            _loadAppsCts?.Cancel();
            _loadAppsCts = new CancellationTokenSource();
            var ct = _loadAppsCts.Token;

            PART_BtnLoadApps.IsEnabled = false;
            PART_LoadAppsStatus.Foreground = Brushes.SlateGray;
            PART_LoadAppsStatus.Text = "⏳ Đang kết nối SSH và quét danh sách ứng dụng trên máy ZCU...";

            try
            {
                var apps = await _deployService.LoadKioskAppsAsync(
                    _sshHost, _sshPort, _sshUser, _sshPassword, ct);

                if (apps.Count == 0)
                {
                    PART_LoadAppsStatus.Foreground = Brushes.DarkOrange;
                    PART_LoadAppsStatus.Text =
                        "Không tìm thấy ứng dụng nào trên máy ZCU. " +
                        "Bạn có thể nhập tay lệnh vào ô bên trái.";
                    return;
                }

                // Xây dựng display text: recommended → "⭐ exec (tên — khuyến nghị)"
                _loadedApps = apps.Select(a =>
                {
                    string display;
                    if (a.IsRecommended)
                    {
                        display = $"⭐ {a.ExecCommand}";
                        if (!string.IsNullOrEmpty(a.Name))
                            display += $" ({a.Name})";
                        display += " — khuyến nghị";
                    }
                    else if (!string.IsNullOrEmpty(a.Name))
                        display = $"{a.Name} — {a.ExecCommand}";
                    else
                        display = a.ExecCommand;

                    // Đánh dấu binary không tồn tại để user biết (F12 prevention)
                    if (!a.ExistsOnSystem)
                        display += " ⚠️ chưa cài";

                    return new LoadedApp(display, a.ExecCommand, a.ExistsOnSystem);
                }).ToList();

                PART_AppExec.ItemsSource = _loadedApps.Select(x => x.DisplayText).ToList();

                // Tự chọn entry recommended đầu tiên đã cài (ExistsOnSystem=true)
                var bestMatch = _loadedApps.FirstOrDefault(a => a.ExistsOnSystem && a.ExistsOnSystem)
                    ?? _loadedApps.First();
                PART_AppExec.Text = bestMatch.DisplayText;

                int notFound = _loadedApps.Count(a => !a.ExistsOnSystem);
                PART_LoadAppsStatus.Foreground = Brushes.SeaGreen;
                PART_LoadAppsStatus.Text =
                    $"✅ Tìm thấy {apps.Count} ứng dụng" +
                    (notFound > 0 ? $" ({notFound} chưa cài trên máy, đánh dấu ⚠️)" : "") +
                    ". Chọn từ danh sách hoặc nhập tay lệnh vào ô trên.";
            }
            catch (OperationCanceledException)
            {
                PART_LoadAppsStatus.Text = "";
            }
            catch (Exception ex)
            {
                PART_LoadAppsStatus.Foreground = Brushes.Red;
                PART_LoadAppsStatus.Text = $"❌ Lỗi khi nạp danh sách: {ex.Message}";
            }
            finally
            {
                PART_BtnLoadApps.IsEnabled = true;
            }
        }

        /// <summary>
        /// Lấy lệnh exec thực sự từ ComboBox (ưu tiên lookup trong _loadedApps để có lệnh sạch).
        /// Nếu user nhập tay → dùng trực tiếp. Fallback = "ipgskioskavalonia".
        /// </summary>
        private string GetAppExec()
        {
            string text = PART_AppExec.Text?.Trim() ?? "";

            // Nếu đã nạp từ ZCU — tra theo display text để lấy exec command thật
            if (_loadedApps != null)
            {
                var match = _loadedApps.FirstOrDefault(a => a.DisplayText == text);
                if (match != null) return match.ExecCommand;
            }

            // User nhập tay hoặc dùng gợi ý mặc định — giá trị là lệnh trực tiếp
            return string.IsNullOrEmpty(text) ? "ipgskioskavalonia" : text;
        }

        // ── Task B: Chọn tất cả / Bỏ chọn tất cả ─────────────────────────

        /// <summary>
        /// Toggle tất cả 13 checkbox trong Tab "Config máy tính".
        /// Ghi chú: "Cài unclutter" là 1 chiều về mặt hiệu ứng trên máy (bỏ tick không gỡ cài),
        /// nhưng checkbox vẫn bị đổi để tránh tái cài khi deploy lại.
        /// </summary>
        private void SetAllConfigMachineCheckboxes(bool value)
        {
            // Cột ① Ẩn giao diện GNOME
            PART_ChkHideTopBar.IsChecked     = value;
            PART_ChkHideActivities.IsChecked = value;
            PART_ChkHideWorkspace.IsChecked  = value;
            PART_ChkHideDash.IsChecked         = value;
            PART_ChkHideUbuntuDock.IsChecked   = value;
            PART_ChkHideDesktopIcons.IsChecked = value;
            // Cột ② Hành vi máy / màn hình
            PART_ChkInstallUnclutter.IsChecked = value;
            PART_ChkHideKeyboard.IsChecked     = value;
            PART_ChkHotCorner.IsChecked        = value;
            PART_ChkBlockSleep.IsChecked       = value;
            PART_ChkInitialSetup.IsChecked     = value;
            PART_ChkAutologin.IsChecked        = value;
            PART_ChkLockWorkspace.IsChecked    = value;
            PART_ChkLockdownShell.IsChecked    = value;
        }

        // ── Deploy ─────────────────────────────────────────────────────────

        private async void OnDeployClick(object? sender, RoutedEventArgs e)
        {
            string sudoPass  = string.IsNullOrEmpty(PART_SudoPassword.Text) ? _sshPassword : PART_SudoPassword.Text;
            string kioskUser = PART_KioskUser.Text?.Trim() ?? "";
            string appExec   = GetAppExec();

            // F12 prevention: cảnh báo nếu binary không được kiểm chứng
            string? execWarning = null;
            if (_loadedApps != null)
            {
                var loaded = _loadedApps.FirstOrDefault(a => a.ExecCommand == appExec);
                if (loaded == null)
                    execWarning = $"⚠️ Lệnh '{appExec}' chưa được kiểm chứng trên máy ZCU — bấm '🔄 Nạp DS' để kiểm tra trước khi deploy.";
                else if (!loaded.ExistsOnSystem)
                    execWarning = $"⚠️ Lệnh '{appExec}' không tìm thấy trên máy ZCU — autostart sẽ đăng ký nhưng app sẽ không chạy được (binary chưa được cài, F12).";
            }

            // Tab 1 — Config máy tính
            bool hideTopBar      = PART_ChkHideTopBar.IsChecked == true;
            bool hideActivities  = PART_ChkHideActivities.IsChecked == true;
            bool hideWorkspace   = PART_ChkHideWorkspace.IsChecked == true;
            bool hideDash        = PART_ChkHideDash.IsChecked == true;
            bool hideUbuntuDock    = PART_ChkHideUbuntuDock.IsChecked == true;
            bool hideDesktopIcons  = PART_ChkHideDesktopIcons.IsChecked == true;
            bool installUnclutter = PART_ChkInstallUnclutter.IsChecked == true;
            bool hideKeyboard    = PART_ChkHideKeyboard.IsChecked == true;
            bool hotCorner       = PART_ChkHotCorner.IsChecked == true;
            bool blockSleep      = PART_ChkBlockSleep.IsChecked == true;
            bool initialSetup    = PART_ChkInitialSetup.IsChecked == true;
            bool autologin       = PART_ChkAutologin.IsChecked == true;
            bool lockWorkspace   = PART_ChkLockWorkspace.IsChecked == true;
            bool lockdownShell   = PART_ChkLockdownShell.IsChecked == true;

            // Tab 2 — Config phần mềm
            bool swUpdate  = PART_ChkSwUpdate.IsChecked == true;
            bool autostart = PART_ChkAutostart.IsChecked == true;
            bool watchdog  = PART_ChkWatchdog.IsChecked == true;

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_StatusMsg.Foreground = Brushes.Red;
                PART_StatusMsg.Text = "Thiếu IP/SSH user — vào 'Sửa' máy tính để bổ sung thông tin SSH.";
                return;
            }

            PART_BtnDeploy.IsEnabled = false;
            PART_LogConsole.Text = "";

            // Hiển thị cảnh báo exec (nếu có) TRƯỚC khi bắt đầu deploy
            if (execWarning != null)
            {
                PART_StatusMsg.Foreground = Brushes.DarkOrange;
                PART_StatusMsg.Text = execWarning;
            }
            else
            {
                PART_StatusMsg.Foreground = Brushes.SlateGray;
                PART_StatusMsg.Text = "Đang deploy...";
            }

            var options = new KioskDeployOptions
            {
                Host                  = _sshHost,
                SshPort               = _sshPort,
                Username              = _sshUser,
                Password              = _sshPassword,
                SudoPassword          = sudoPass,
                KioskUser             = kioskUser,
                AppExec               = appExec,
                HideTopBar            = hideTopBar,
                HideActivities        = hideActivities,
                HideWorkspaceSwitcher = hideWorkspace,
                HideDash              = hideDash,
                InstallUnclutter      = installUnclutter,
                HideVirtualKeyboard   = hideKeyboard,
                DisableHotCorner      = hotCorner,
                DisableUbuntuDock     = hideUbuntuDock,
                DisableDesktopIcons   = hideDesktopIcons,
                BlockSleep            = blockSleep,
                SkipInitialSetup      = initialSetup,
                EnableAutologin       = autologin,
                LockSingleWorkspace   = lockWorkspace,
                LockdownShell         = lockdownShell,
                DisableSoftwareUpdate = swUpdate,
                EnableAutostart       = autostart,
                EnableWatchdog        = watchdog,
            };

            try
            {
                await _deployService.DeployAsync(options, Log);

                PART_StatusMsg.Foreground = Brushes.Green;
                PART_StatusMsg.Text = "🎉 Deploy hoàn tất! Nhớ RESTART máy kiosk để áp dụng autologin + autostart.";
            }
            catch (Exception ex)
            {
                PART_StatusMsg.Foreground = Brushes.Red;
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
