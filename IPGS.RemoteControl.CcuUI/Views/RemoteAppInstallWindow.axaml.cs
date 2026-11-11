using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using IPGS.RemoteControl.CcuClient;
using Renci.SshNet;
using System.Threading.Tasks;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public partial class RemoteAppInstallWindow : Window
    {
        private readonly RemoteAppInstallService _installService;

        private readonly string _sshHost;
        private readonly int _sshPort;
        private readonly string _sshUser;
        private readonly string _sshPassword;
        private readonly ComputerProfile _profile;
        // Khác rỗng khi file cài đặt được chọn TRÊN máy đích (không upload).
        private string _remoteInstallerPath = "";

        public RemoteAppInstallWindow() : this(new ComputerProfile())
        {
        }

        public RemoteAppInstallWindow(ComputerProfile prefill)
        {
            InitializeComponent();
            _installService = new RemoteAppInstallService();

            _sshHost = prefill.Host;
            _sshPort = prefill.SshPort > 0 ? prefill.SshPort : 22;
            _sshUser = prefill.SshUsername ?? "";
            _sshPassword = prefill.SshPassword ?? "";
            _profile = prefill;

            PART_TargetHostText.Text = string.IsNullOrWhiteSpace(_sshHost)
                ? "Đang cài đặt cho: —"
                : $"Đang cài đặt cho: {_sshUser}@{_sshHost}:{_sshPort}";

            if (!string.IsNullOrEmpty(_profile.LastAppInstallerPath))
            {
                PART_AppInstallerPath.Text = _profile.LastAppInstallerPath;
            }
            if (!string.IsNullOrEmpty(_profile.LastUninstallPackage))
            {
                var txtPackageName = this.FindControl<AutoCompleteBox>("PART_UninstallPackageName");
                if (txtPackageName != null)
                {
                    txtPackageName.Text = _profile.LastUninstallPackage;
                }
            }

            if (!string.IsNullOrEmpty(_profile.LastLaunchPackage))
            {
                PART_LaunchPackageName.Text = _profile.LastLaunchPackage;
            }

            PART_BtnBrowseApp.Click += OnBrowseAppClick;
            PART_BtnBrowseRemote.Click += OnBrowseRemoteClick;
            PART_BtnLaunch.Click += OnLaunchClick;
            PART_BtnShowAllLaunch.Click += (s, e) => ShowAppListFlyout(PART_BtnShowAllLaunch, PART_LaunchPackageName);
            PART_BtnDeploy.Click += OnDeployClick;
            if (this.FindControl<Button>("PART_BtnUninstall") is { } btnUninstall)
            {
                btnUninstall.Click += OnUninstallClick;
            }
            if (this.FindControl<Button>("PART_BtnShowAll") is { } btnShowAll)
            {
                btnShowAll.Click += (s, e) =>
                {
                    if (this.FindControl<AutoCompleteBox>("PART_UninstallPackageName") is { } autoComplete)
                        ShowAppListFlyout(btnShowAll, autoComplete);
                };
            }

            LoadInstalledPackagesAsync();
        }

        // Danh sách app trên máy đích: null = chưa tải xong; rỗng = tải lỗi/không có.
        private string[]? _installedApps;
        private Task? _loadAppsTask;

        // App = gói có shortcut .desktop (app giao diện) + gói kztek/ipgs/kiosk (app riêng có thể không có shortcut).
        private const string ListAppsCommand =
            "{ dpkg -S /usr/share/applications/*.desktop /usr/local/share/applications/*.desktop 2>/dev/null " +
            "| grep -v '^diversion' | cut -d: -f1 | tr ',' '\\n'; " +
            "dpkg-query -W -f='${binary:Package}\\n' | grep -iE 'kztek|ipgs|kiosk'; } " +
            "| sed 's/^ *//; s/ *$//; s/:.*$//' | grep -v '^$' | sort -u";

        private void LoadInstalledPackagesAsync() => _loadAppsTask = LoadInstalledAppsAsync();

        private async Task LoadInstalledAppsAsync()
        {
            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser)) { _installedApps = []; return; }

            string[] apps = [];
            try
            {
                apps = await Task.Run(() =>
                {
                    using var ssh = new SshClient(_sshHost, _sshPort, _sshUser, _sshPassword);
                    ssh.Connect();
                    var result = ssh.CreateCommand(ListAppsCommand).Execute();
                    return result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                });
            }
            catch
            {
                // Lỗi SSH → danh sách rỗng; nút ▼ sẽ báo không lấy được.
            }

            _installedApps = apps;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (this.FindControl<AutoCompleteBox>("PART_UninstallPackageName") is { } autoComplete)
                    autoComplete.ItemsSource = apps;
                PART_LaunchPackageName.ItemsSource = apps;
            });
        }

        /// <summary>
        /// Nút ▼: hiện toàn bộ app máy đích trong flyout (AutoCompleteBox chỉ lọc khi gõ chữ nên
        /// IsDropDownOpen=true không hiện gì). Chọn 1 dòng → điền vào ô tên package.
        /// </summary>
        private async void ShowAppListFlyout(Control anchor, AutoCompleteBox target)
        {
            if (_installedApps is null && _loadAppsTask is not null)
            {
                try { await _loadAppsTask; } catch { }
            }
            var apps = _installedApps ?? [];

            var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
            if (apps.Length == 0)
            {
                flyout.Content = new TextBlock
                {
                    Text = "Không lấy được danh sách app từ máy đích (kiểm tra SSH).",
                    Margin = new Avalonia.Thickness(8)
                };
            }
            else
            {
                var list = new ListBox { ItemsSource = apps, MaxHeight = 320, MinWidth = 360 };
                list.SelectionChanged += (_, _) =>
                {
                    if (list.SelectedItem is string pkg)
                    {
                        target.Text = pkg;
                        flyout.Hide();
                    }
                };
                flyout.Content = list;
            }
            flyout.ShowAt(anchor);
        }

        private async void OnBrowseAppClick(object? sender, RoutedEventArgs e)
        {
            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Chọn file ứng dụng (.deb, .sh, .run, .msi, .exe)",
                AllowMultiple = false
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count >= 1)
            {
                PART_AppInstallerPath.Text = files[0].Path.LocalPath;
                _remoteInstallerPath = "";
                // F04: người dùng vừa khắc phục nguyên nhân lỗi validation ("Vui lòng chọn
                // file cài đặt...") — xóa status đỏ cũ ngay, không chờ tới lần bấm Deploy sau.
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.SlateGray;
                PART_StatusMsg.Text = "";
            }
        }

        private async void OnBrowseRemoteClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Thiếu thông tin kết nối SSH.";
                return;
            }

            var picker = new RemoteFilePickerWindow(_sshHost, _sshPort, _sshUser, _sshPassword);
            var path = await picker.ShowDialog<string?>(this);
            if (string.IsNullOrEmpty(path)) return;

            _remoteInstallerPath = path;
            PART_AppInstallerPath.Text = "[Máy đích] " + path;
            PART_StatusMsg.Foreground = Avalonia.Media.Brushes.SlateGray;
            PART_StatusMsg.Text = "";
        }

        private async void OnLaunchClick(object? sender, RoutedEventArgs e)
        {
            string packageName = PART_LaunchPackageName.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Thiếu thông tin kết nối SSH.";
                return;
            }
            if (string.IsNullOrEmpty(packageName))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Vui lòng nhập tên package cần khởi động (Ví dụ: kztek-ipgsv4).";
                return;
            }

            PART_BtnLaunch.IsEnabled = false;
            PART_LogConsole.Text = "";
            PART_StatusMsg.Foreground = Avalonia.Media.Brushes.SlateGray;
            PART_StatusMsg.Text = "Đang gửi lệnh khởi động...";

            var options = new RemoteAppInstallOptions
            {
                Host = _sshHost,
                SshPort = _sshPort,
                Username = _sshUser,
                Password = _sshPassword,
                PackageName = packageName
            };

            try
            {
                await _installService.LaunchAsync(options, Log);
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Green;
                PART_StatusMsg.Text = "▶ Đã gửi lệnh khởi động.";

                _profile.LastLaunchPackage = packageName;
                ComputerProfileStore.Instance.Save(_profile);
            }
            catch (Exception ex)
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Lỗi: " + ex.Message;
                Log("LỖI: " + ex.Message);
            }
            finally
            {
                PART_BtnLaunch.IsEnabled = true;
            }
        }

        private async void OnDeployClick(object? sender, RoutedEventArgs e)
        {
            string sudoPass = string.IsNullOrEmpty(PART_SudoPassword.Text) ? _sshPassword : PART_SudoPassword.Text;
            bool isRemote = !string.IsNullOrEmpty(_remoteInstallerPath);
            string installerPath = isRemote ? _remoteInstallerPath : (PART_AppInstallerPath.Text?.Trim() ?? "");

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Thiếu thông tin kết nối SSH.";
                return;
            }

            if (string.IsNullOrEmpty(installerPath))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Vui lòng chọn file cài đặt trước khi bấm Bắt đầu.";
                return;
            }

            PART_BtnDeploy.IsEnabled = false;
            PART_BtnBrowseApp.IsEnabled = false;
            PART_BtnBrowseRemote.IsEnabled = false;
            PART_LogConsole.Text = "";
            PART_StatusMsg.Foreground = Avalonia.Media.Brushes.SlateGray;
            PART_StatusMsg.Text = "Đang kết nối và cài đặt...";

            var options = new RemoteAppInstallOptions
            {
                Host = _sshHost,
                SshPort = _sshPort,
                Username = _sshUser,
                Password = _sshPassword,
                SudoPassword = sudoPass,
                AppInstallerFile = isRemote ? "" : installerPath,
                RemoteInstallerPath = isRemote ? installerPath : ""
            };

            try
            {
                PART_ProgressPanel.IsVisible = true;
                ReportProgress(new InstallProgress(0, "Đang kết nối..."));
                await _installService.InstallAsync(options, Log, default, ReportProgress);
                ReportProgress(new InstallProgress(100, "Hoàn tất 100%"));

                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Green;
                PART_StatusMsg.Text = "Đã tải lên và cài đặt thành công!";

                // Lưu lại lựa chọn
                _profile.LastAppInstallerPath = installerPath;
                ComputerProfileStore.Instance.Save(_profile);
            }
            catch (Exception ex)
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Lỗi: " + ex.Message;
                Log("LỖI: " + ex.Message);
            }
            finally
            {
                PART_BtnDeploy.IsEnabled = true;
                PART_BtnBrowseApp.IsEnabled = true;
                PART_BtnBrowseRemote.IsEnabled = true;
                if (this.FindControl<Button>("PART_BtnUninstall") is { } btnUninstall) btnUninstall.IsEnabled = true;
            }
        }

        private async void OnUninstallClick(object? sender, RoutedEventArgs e)
        {
            string sudoPass = string.IsNullOrEmpty(PART_SudoPassword.Text) ? _sshPassword : PART_SudoPassword.Text;
            var txtPackageName = this.FindControl<AutoCompleteBox>("PART_UninstallPackageName");
            string packageName = txtPackageName?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Thiếu thông tin kết nối SSH.";
                return;
            }

            if (string.IsNullOrEmpty(packageName))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Vui lòng nhập tên package cần gỡ (Ví dụ: kztek-app).";
                return;
            }

            PART_BtnDeploy.IsEnabled = false;
            PART_BtnBrowseApp.IsEnabled = false;
            if (this.FindControl<Button>("PART_BtnUninstall") is { } btnUninstall) btnUninstall.IsEnabled = false;

            PART_LogConsole.Text = "";
            PART_StatusMsg.Foreground = Avalonia.Media.Brushes.SlateGray;
            PART_StatusMsg.Text = "Đang kết nối và gỡ cài đặt...";

            var options = new RemoteAppInstallOptions
            {
                Host = _sshHost,
                SshPort = _sshPort,
                Username = _sshUser,
                Password = _sshPassword,
                SudoPassword = sudoPass,
                PackageName = packageName
            };

            try
            {
                await _installService.UninstallAsync(options, Log);

                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Green;
                PART_StatusMsg.Text = "Đã gỡ cài đặt thành công!";
                if (txtPackageName != null) txtPackageName.Text = "";

                // Lưu lại lựa chọn
                _profile.LastUninstallPackage = packageName;
                ComputerProfileStore.Instance.Save(_profile);
            }
            catch (Exception ex)
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Lỗi: " + ex.Message;
                Log("LỖI: " + ex.Message);
            }
            finally
            {
                PART_BtnDeploy.IsEnabled = true;
                PART_BtnBrowseApp.IsEnabled = true;
                if (this.FindControl<Button>("PART_BtnUninstall") is { } btnUninstall2) btnUninstall2.IsEnabled = true;
            }
        }

        private void ReportProgress(InstallProgress p)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                PART_Progress.IsIndeterminate = p.Percent < 0;
                if (p.Percent >= 0) PART_Progress.Value = p.Percent;
                PART_ProgressText.Text = p.Text;
            });
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
