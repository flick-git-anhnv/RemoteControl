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
        private sealed record LoadedApp(string DisplayText, string ExecCommand, bool ExistsOnSystem, bool IsRecommended);
        private List<LoadedApp>? _loadedApps;
        private CancellationTokenSource? _loadAppsCts;

        // KHÔNG có danh sách gợi ý hardcoded: các tên như "ipgskioskavalonia"/"kioskapp"
        // không đảm bảo tồn tại trên máy ZCU đích — chọn nhầm sẽ deploy autostart trỏ tới
        // binary không tồn tại (F12). Danh sách CHỈ đến từ '🔄 Nạp DS' (quét thật qua SSH).

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

            // App exec để TRỐNG — chỉ điền sau khi nạp danh sách thật từ ZCU
            PART_AppExec.ItemsSource = System.Array.Empty<string>();
            PART_AppExec.Text = "";

            // Wire up events
            // F21: 2 nút Deploy độc lập theo tab thay vì 1 nút chung — xem OnDeployMachineClick/
            // OnDeploySoftwareClick.
            PART_BtnDeployMachine.Click  += OnDeployMachineClick;
            PART_BtnDeploySoftware.Click += OnDeploySoftwareClick;
            PART_BtnResetColor.Click   += OnResetColorClick;
            PART_BtnLoadApps.Click     += OnLoadAppsClick;
            PART_BtnSelectAll.Click    += (_, _) => SetAllConfigMachineCheckboxes(true);
            PART_BtnDeselectAll.Click  += (_, _) => SetAllConfigMachineCheckboxes(false);

            // Tự động bấm '🔄 Nạp DS' ngay khi mở cửa sổ (nếu đã có đủ thông tin SSH)
            Opened += OnWindowOpened;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            Opened -= OnWindowOpened;

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_LoadAppsStatus.Foreground = Brushes.DarkOrange;
                PART_LoadAppsStatus.Text =
                    "Chưa có thông tin SSH — vào 'Sửa' máy tính để bổ sung, rồi bấm '🔄 Nạp DS'.";
                return;
            }

            OnLoadAppsClick(this, new RoutedEventArgs());
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

                    return new LoadedApp(display, a.ExecCommand, a.ExistsOnSystem, a.IsRecommended);
                }).ToList();

                PART_AppExec.ItemsSource = _loadedApps.Select(x => x.DisplayText).ToList();

                // F23: chỉ tự chọn entry vừa ĐÃ CÀI THẬT trên máy VỪA được đánh dấu
                // "khuyến nghị" (tên/lệnh chứa ipgs/kiosk) — trước đây chọn đại bất kỳ app
                // nào tồn tại (kể cả tiện ích hệ thống như "Software & Updates") khi máy
                // chưa cài app kiosk thật, khiến autostart trỏ sai (verify thật: máy ZCU
                // sau khi gỡ ZcuAgent, không còn app nào "khuyến nghị" → tự chọn nhầm
                // 'software-properties-gtk --open-tab=4'). Không có match phù hợp → để
                // trống, bắt user chọn/nhập tay.
                var bestMatch = _loadedApps.FirstOrDefault(a => a.ExistsOnSystem && a.IsRecommended);
                PART_AppExec.Text = bestMatch?.DisplayText ?? "";

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
        /// Nếu user nhập tay → dùng trực tiếp. KHÔNG có fallback hardcoded —
        /// rỗng nghĩa là chưa chọn, deploy sẽ bị chặn.
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

            // User nhập tay — giá trị là lệnh trực tiếp
            return text;
        }

        // ── Task B: Chọn tất cả / Bỏ chọn tất cả ─────────────────────────

        /// <summary>
        /// Toggle tất cả 14 checkbox trong Tab "Config máy tính".
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
            PART_ChkFirewall.IsChecked         = value;
        }

        // ── F22: Reset màu màn hình ─────────────────────────────────────────

        private async void OnResetColorClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_ResetColorStatus.Foreground = Brushes.Red;
                PART_ResetColorStatus.Text = "Thiếu IP/SSH user — vào 'Sửa' máy tính để bổ sung thông tin SSH.";
                return;
            }

            PART_BtnResetColor.IsEnabled = false;
            PART_ResetColorStatus.Foreground = Brushes.SlateGray;
            PART_ResetColorStatus.Text = "⏳ Đang tắt Night Light + reset gamma/brightness qua SSH...";

            try
            {
                string result = await _deployService.ResetDisplayColorAsync(_sshHost, _sshPort, _sshUser, _sshPassword);
                PART_ResetColorStatus.Foreground = Brushes.SeaGreen;
                PART_ResetColorStatus.Text = string.IsNullOrWhiteSpace(result)
                    ? "✅ Đã tắt Night Light + reset gamma/brightness về mặc định."
                    : $"✅ Xong: {result}";
            }
            catch (Exception ex)
            {
                PART_ResetColorStatus.Foreground = Brushes.Red;
                PART_ResetColorStatus.Text = $"❌ Lỗi: {ex.Message}";
            }
            finally
            {
                PART_BtnResetColor.IsEnabled = true;
            }
        }

        // ── Deploy ─────────────────────────────────────────────────────────
        //
        // F21: 2 nút Deploy độc lập theo tab (tool này còn dùng để deploy autostart
        // cho app KHÁC, không muốn mỗi lần đổi app phải chạy lại toàn bộ config máy tính):
        //   - "Deploy Config máy tính" (Tab 1): chạy 1-install-software.sh +
        //     2-configure-system.sh. KHÔNG bắt buộc chọn App exec — nếu App exec đang
        //     trống, tự tắt Autostart/Watchdog cho LẦN CHẠY NÀY (không throw chặn deploy),
        //     vì 2 mục đó cần App exec hợp lệ mới áp dụng đúng (F12).
        //   - "Deploy Config phần mềm" (Tab 2): CHỈ chạy 2-configure-system.sh (bỏ qua
        //     1-install-software.sh — không liên quan autostart/watchdog, tránh mất thời
        //     gian cài lại extension GNOME không cần thiết). Vẫn bắt buộc App exec nếu
        //     Autostart hoặc Watchdog đang được tick, vì đây là mục đích chính của nút này.

        private async void OnDeployMachineClick(object? sender, RoutedEventArgs e)
            => await RunDeployAsync(isMachineTab: true, PART_BtnDeployMachine, PART_StatusMsgMachine);

        private async void OnDeploySoftwareClick(object? sender, RoutedEventArgs e)
            => await RunDeployAsync(isMachineTab: false, PART_BtnDeploySoftware, PART_StatusMsgSoftware);

        private async System.Threading.Tasks.Task RunDeployAsync(bool isMachineTab, Button deployButton, TextBlock tabStatusMsg)
        {
            string sudoPass  = string.IsNullOrEmpty(PART_SudoPassword.Text) ? _sshPassword : PART_SudoPassword.Text;
            string kioskUser = PART_KioskUser.Text?.Trim() ?? "";
            string appExec   = GetAppExec();

            // Tab 2 — Config phần mềm (đọc trước để biết Autostart/Watchdog có cần App exec không)
            bool swUpdate  = PART_ChkSwUpdate.IsChecked == true;
            bool autostart = PART_ChkAutostart.IsChecked == true;
            bool watchdog  = PART_ChkWatchdog.IsChecked == true;

            bool needsAppExec = autostart || watchdog;

            if (string.IsNullOrWhiteSpace(appExec) && needsAppExec)
            {
                if (isMachineTab)
                {
                    // F21: Tab "Config máy tính" không bắt buộc App exec — tự tắt
                    // autostart/watchdog cho lần deploy này thay vì chặn toàn bộ.
                    autostart = false;
                    watchdog = false;
                    tabStatusMsg.Foreground = Brushes.DarkOrange;
                    tabStatusMsg.Text = "ℹ️ Chưa chọn App exec — bỏ qua Autostart/Watchdog cho lần deploy này (chỉ áp dụng cấu hình máy).";
                }
                else
                {
                    tabStatusMsg.Foreground = Brushes.Red;
                    tabStatusMsg.Text = _loadedApps == null
                        ? "Chưa chọn App exec — bấm '🔄 Nạp DS' để nạp danh sách ứng dụng từ máy ZCU, hoặc nhập tay lệnh."
                        : "Chưa chọn App exec — chọn một mục trong danh sách vừa nạp, hoặc nhập tay lệnh.";
                    return;
                }
            }

            // F12 prevention: cảnh báo nếu binary không được kiểm chứng
            string? execWarning = null;
            if (needsAppExec && !string.IsNullOrWhiteSpace(appExec) && _loadedApps != null)
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
            bool firewall        = PART_ChkFirewall.IsChecked == true;
            // F28: index 0 = Ubuntu/GNOME (mặc định), 1 = Lubuntu/LXQt.
            bool isLubuntu       = PART_CmbOsFamily.SelectedIndex == 1;

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                tabStatusMsg.Foreground = Brushes.Red;
                tabStatusMsg.Text = "Thiếu IP/SSH user — vào 'Sửa' máy tính để bổ sung thông tin SSH.";
                return;
            }

            PART_BtnDeployMachine.IsEnabled = false;
            PART_BtnDeploySoftware.IsEnabled = false;
            PART_LogConsole.Text = "";
            PART_StatusMsg.Text = "";

            // Hiển thị cảnh báo exec (nếu có) TRƯỚC khi bắt đầu deploy — không ghi đè
            // thông báo "bỏ qua Autostart/Watchdog" đã set ở trên nếu chưa có execWarning mới.
            if (execWarning != null)
            {
                tabStatusMsg.Foreground = Brushes.DarkOrange;
                tabStatusMsg.Text = execWarning;
            }
            else if (string.IsNullOrEmpty(tabStatusMsg.Text))
            {
                tabStatusMsg.Foreground = Brushes.SlateGray;
                tabStatusMsg.Text = "Đang deploy...";
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
                // F21: chỉ chạy 1-install-software.sh khi deploy từ Tab "Config máy tính".
                RunInstallSoftware    = isMachineTab,
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
                EnableFirewall        = firewall,
                IsLubuntu             = isLubuntu,
            };

            try
            {
                await _deployService.DeployAsync(options, Log);

                tabStatusMsg.Foreground = Brushes.Green;
                tabStatusMsg.Text = "🎉 Deploy hoàn tất! Nhớ RESTART máy kiosk để áp dụng autologin + autostart.";
            }
            catch (Exception ex)
            {
                tabStatusMsg.Foreground = Brushes.Red;
                tabStatusMsg.Text = "❌ Deploy thất bại: " + ex.Message;
                Log("❌ LỖI: " + ex.Message);
            }
            finally
            {
                PART_BtnDeployMachine.IsEnabled = true;
                PART_BtnDeploySoftware.IsEnabled = true;
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
