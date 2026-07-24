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

            PART_BtnBrowseApp.Click += OnBrowseAppClick;
            PART_BtnDeploy.Click += OnDeployClick;
            if (this.FindControl<Button>("PART_BtnUninstall") is { } btnUninstall)
            {
                btnUninstall.Click += OnUninstallClick;
            }
            if (this.FindControl<Button>("PART_BtnShowAll") is { } btnShowAll)
            {
                btnShowAll.Click += (s, e) =>
                {
                    var autoComplete = this.FindControl<AutoCompleteBox>("PART_UninstallPackageName");
                    if (autoComplete != null)
                    {
                        autoComplete.IsDropDownOpen = true;
                        autoComplete.Focus();
                    }
                };
            }

            LoadInstalledPackagesAsync();
        }

        private async void LoadInstalledPackagesAsync()
        {
            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser)) return;

            try
            {
                using var ssh = new SshClient(_sshHost, _sshPort, _sshUser, _sshPassword);
                await Task.Run(() => ssh.Connect());
                if (ssh.IsConnected)
                {
                    var cmd = ssh.CreateCommand("dpkg-query -W -f='${binary:Package}\n'");
                    var result = await Task.Run(() => cmd.Execute());
                    var allPackages = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var packages = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(allPackages, p => 
                        p.Contains("kztek", StringComparison.OrdinalIgnoreCase) || 
                        p.Contains("remote", StringComparison.OrdinalIgnoreCase) || 
                        p.Contains("agent", StringComparison.OrdinalIgnoreCase) || 
                        p.Contains("kiosk", StringComparison.OrdinalIgnoreCase)));

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (this.FindControl<AutoCompleteBox>("PART_UninstallPackageName") is { } autoComplete)
                        {
                            autoComplete.ItemsSource = packages;
                        }
                    });
                }
            }
            catch
            {
                // Ignore background errors
            }
        }

        private async void OnBrowseAppClick(object? sender, RoutedEventArgs e)
        {
            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Chọn file ứng dụng (.deb, .sh, .run)",
                AllowMultiple = false
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count >= 1)
            {
                PART_AppInstallerPath.Text = files[0].Path.LocalPath;
            }
        }

        private async void OnDeployClick(object? sender, RoutedEventArgs e)
        {
            string sudoPass = string.IsNullOrEmpty(PART_SudoPassword.Text) ? _sshPassword : PART_SudoPassword.Text;
            string installerPath = PART_AppInstallerPath.Text?.Trim() ?? "";

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
                AppInstallerFile = installerPath
            };

            try
            {
                await _installService.InstallAsync(options, Log);

                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Green;
                PART_StatusMsg.Text = "🎉 Đã tải lên và cài đặt thành công!";

                // Lưu lại lựa chọn
                _profile.LastAppInstallerPath = installerPath;
                ComputerProfileStore.Instance.Save(_profile);
            }
            catch (Exception ex)
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "❌ Lỗi: " + ex.Message;
                Log("❌ LỖI: " + ex.Message);
            }
            finally
            {
                PART_BtnDeploy.IsEnabled = true;
                PART_BtnBrowseApp.IsEnabled = true;
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
                PART_StatusMsg.Text = "🎉 Đã gỡ cài đặt thành công!";
                if (txtPackageName != null) txtPackageName.Text = "";

                // Lưu lại lựa chọn
                _profile.LastUninstallPackage = packageName;
                ComputerProfileStore.Instance.Save(_profile);
            }
            catch (Exception ex)
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "❌ Lỗi: " + ex.Message;
                Log("❌ LỖI: " + ex.Message);
            }
            finally
            {
                PART_BtnDeploy.IsEnabled = true;
                PART_BtnBrowseApp.IsEnabled = true;
                if (this.FindControl<Button>("PART_BtnUninstall") is { } btnUninstall2) btnUninstall2.IsEnabled = true;
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
