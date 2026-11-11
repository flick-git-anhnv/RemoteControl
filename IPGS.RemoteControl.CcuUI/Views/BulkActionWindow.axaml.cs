using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public class BulkTaskResult : INotifyPropertyChanged
    {
        public ComputerProfile Profile { get; set; } = null!;
        public string ComputerName => Profile.Name ?? "Unknown ZCU";
        public string HostAddress => $"{Profile.Host}:{(Profile.SshPort > 0 ? Profile.SshPort : 22)}";

        private string _statusText = "Đang chờ...";
        public string StatusText
        {
            get => _statusText;
            set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
        }

        private string _statusColor = "#94A3B8"; // Slate
        public string StatusColor
        {
            get => _statusColor;
            set { if (_statusColor != value) { _statusColor = value; OnPropertyChanged(); } }
        }

        private string _statusIcon = "·";
        public string StatusIcon
        {
            get => _statusIcon;
            set { if (_statusIcon != value) { _statusIcon = value; OnPropertyChanged(); } }
        }

        private string _output = "";
        public string Output
        {
            get => _output;
            set { if (_output != value) { _output = value; OnPropertyChanged(); } }
        }

        public void SetRunning()
        {
            StatusText = "Đang chạy...";
            StatusColor = "#3B82F6"; // Blue
            StatusIcon = "…";
        }

        public void SetSuccess(string output = "")
        {
            StatusText = "Thành công";
            StatusColor = "#10B981"; // Emerald
            StatusIcon = "✓";
            if (!string.IsNullOrEmpty(output)) Output = output.Trim();
        }

        public void SetError(string error)
        {
            StatusText = "Lỗi";
            StatusColor = "#EF4444"; // Red
            StatusIcon = "✗";
            Output = error;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class BulkActionWindow : Window
    {
        /// <summary>
        /// S3: Chạy lệnh SSH đa nền tảng (Linux &amp; Windows).
        /// - Trên máy đích Windows: tự động nhận diện OS qua <see cref="RemoteOsDetector.DetectRemoteOsAsync"/>,
        ///   ánh xạ Quick Command sang PowerShell qua <see cref="RemoteCommandWindow.TranslateQuickCommandForWindows"/>
        ///   và chạy trực tiếp trên Windows.
        /// - Trên máy đích Linux: giữ nguyên cơ chế <c>env DISPLAY=:0 ... bash -c</c> và truyền password sudo qua STDIN.
        /// </summary>
        private static Task<(string Output, string Error)> RunSshCommandAsync(SshClient ssh, string command, string sudoPassword)
            => RemoteCommandWindow.RunSshCommandAsync(ssh, command, sudoPassword);

        /// <summary>
        /// F05: validate cấu hình SSH của máy TRƯỚC khi tạo SshClient — SshClient ném raw
        /// ArgumentException tiếng Anh ("The value cannot be an empty string... Parameter
        /// 'username'") mà người dùng cuối không hiểu. Thay bằng thông báo tiếng Việt chỉ rõ
        /// cách khắc phục.
        /// </summary>
        private static void ValidateSshProfile(ComputerProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.SshUsername))
                throw new InvalidOperationException(
                    "Máy chưa cấu hình SSH user — bấm 'Sửa' máy tính này để bổ sung SSH username/password rồi chạy lại.");
        }

        private readonly List<ComputerProfile> _targets;
        private readonly ObservableCollection<BulkTaskResult> _results;

        public BulkActionWindow() : this(new List<ComputerProfile>()) { }

        public BulkActionWindow(List<ComputerProfile> targets)
        {
            InitializeComponent();
            _targets = targets;

            var snippetCombo = this.FindControl<AutoCompleteBox>("PART_SnippetCombo");
            if (snippetCombo != null)
            {
                snippetCombo.ItemsSource = new string[]
                {
                    "Khởi động lại (Reboot)",
                    "Tắt máy (Shutdown)",
                    "Kiểm tra RAM & Ổ cứng",
                    "Dọn dẹp cache (apt clean)",
                    "Xem Top Process",
                    "Cập nhật APT (apt update)",
                    "Cập nhật & Nâng cấp (apt update & upgrade)",
                    "Kiểm tra IP & Mạng",
                    "Khởi động lại SSH (Restart SSH)",
                    "Xem log hệ thống (syslog)",
                    "Cập nhật Remote App",
                    "Xóa log cũ"
                };
            }
            
            _results = new ObservableCollection<BulkTaskResult>(_targets.Select(p => new BulkTaskResult { Profile = p }));
            
            var listBox = this.FindControl<ListBox>("PART_ResultListBox");
            if (listBox != null) listBox.ItemsSource = _results;
        }

        private void OnSnippetSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var combo = sender as AutoCompleteBox;
            var input = this.FindControl<TextBox>("PART_CommandInput");
            if (combo?.SelectedItem is string text && input != null)
            {
                if (text.Contains("Reboot")) input.Text = "sudo reboot";
                else if (text.Contains("Shutdown")) input.Text = "sudo poweroff";
                else if (text.Contains("RAM & Ổ cứng")) input.Text = "free -m && echo '' && df -h /";
                else if (text.Contains("apt clean")) input.Text = "sudo apt clean && sudo apt autoremove -y";
                else if (text.Contains("Top Process")) input.Text = "top -bn1 | head -n 15";
                else if (text.Contains("apt update & upgrade")) input.Text = "sudo apt update && sudo apt upgrade -y";
                else if (text.Contains("apt update")) input.Text = "sudo apt update";
                else if (text.Contains("IP & Mạng")) input.Text = "ip a && ping -c 4 8.8.8.8";
                else if (text.Contains("Restart SSH")) input.Text = "sudo systemctl restart ssh";
                else if (text.Contains("syslog")) input.Text = "tail -n 50 /var/log/syslog";
                else if (text.Contains("Cập nhật Remote App")) input.Text = "sudo apt update && sudo apt install -y remote-app";
                else if (text.Contains("Xóa log cũ")) input.Text = "sudo rm -rf /var/log/ipgs/*.log";
            }
        }

        private void OnSnippetComboTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (sender is AutoCompleteBox combo)
            {
                // F03: xem giải thích tại RemoteCommandWindow.OnSnippetComboTapped —
                // popup mở khi view nội bộ còn rỗng sẽ vô hình + kẹt IsDropDownOpen=true.
                combo.IsDropDownOpen = false;
                combo.PopulateComplete();
                combo.IsDropDownOpen = true;
            }
        }

        private async void OnRunCommandClick(object? sender, RoutedEventArgs e)
        {
            var input = this.FindControl<TextBox>("PART_CommandInput");
            
            var snippetCombo = this.FindControl<AutoCompleteBox>("PART_SnippetCombo");
            if (snippetCombo != null)
            {
                snippetCombo.Text = "";
                snippetCombo.SelectedItem = null;
            }

            string cmdToRun = input?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(cmdToRun))
            {
                return; // or show message
            }

            await ExecuteBulkActionAsync(async (taskInfo) => 
            {
                var profile = taskInfo.Profile;
                ValidateSshProfile(profile); // F05
                int sshPort = profile.SshPort > 0 ? profile.SshPort : 22;

                using var ssh = new SshClient(profile.Host, sshPort, profile.SshUsername ?? "", profile.SshPassword ?? "");
                ssh.Connect();

                // S3: password sudo đi qua stdin của channel, không nhúng vào command line / env
                try
                {
                    var (output, error) = await RunSshCommandAsync(ssh, cmdToRun, profile.SshPassword ?? "");
                    return output + "\n" + error;
                }
                catch (Exception ex) when (SshCommandHints.IsShutdownCommand(cmdToRun)
                                           && SshCommandHints.IsConnectionDropped(ex))
                {
                    // F07: reboot/shutdown làm SSH ngắt là kết quả mong đợi — máy này coi như
                    // thành công kèm ghi chú, không hiển thị ❌ Lỗi đỏ. Lỗi thật khác (sai
                    // password sudo, thiếu SSH user...) không rơi vào filter này → vẫn SetError.
                    return SshCommandHints.ShutdownInfoMessage;
                }
            });
        }

        private async void OnUploadFileClick(object? sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions
            {
                Title = "Chọn file để Upload lên tất cả máy",
                AllowMultiple = true
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0) return;

            var localPaths = files.Select(f => f.Path.LocalPath).ToList();
            // Thư mục đích: người dùng nhập, bỏ trống → /home/<ssh user> của từng máy
            // (trước đây cố định "/home" → user thường không có quyền ghi).
            string customDir = (this.FindControl<TextBox>("PART_RemoteDir")?.Text ?? "").Trim().TrimEnd('/');

            await ExecuteBulkActionAsync(async (taskInfo) =>
            {
                var profile = taskInfo.Profile;
                ValidateSshProfile(profile); // F05
                int sshPort = profile.SshPort > 0 ? profile.SshPort : 22;

                using var sftp = new SftpClient(profile.Host, sshPort, profile.SshUsername ?? "", profile.SshPassword ?? "");
                sftp.Connect();

                string defaultDir = !string.IsNullOrWhiteSpace(sftp.WorkingDirectory)
                    ? ShellQuote.NormalizeSftpPath(sftp.WorkingDirectory)
                    : $"/home/{profile.SshUsername}";
                string remoteDir = customDir.Length > 0 ? ShellQuote.NormalizeSftpPath(customDir) : defaultDir;

                var sb = new StringBuilder();
                foreach (var localPath in localPaths)
                {
                    string remotePath = ShellQuote.CombineSftpPath(remoteDir, Path.GetFileName(localPath));
                    using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read);
                    sftp.UploadFile(fs, remotePath, true);
                    sb.AppendLine($"Đã upload thành công tới: {remotePath}");
                }
                return await Task.FromResult(sb.ToString());
            });
        }

        private async void OnInstallAppClick(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Chọn file ứng dụng (.deb, .sh, .run, .msi, .exe) để cài lên tất cả máy",
                AllowMultiple = false
            });
            if (files.Count == 0) return;
            string installer = files[0].Path.LocalPath;

            await ExecuteBulkActionAsync(async (taskInfo) =>
            {
                var profile = taskInfo.Profile;
                ValidateSshProfile(profile);
                var log = new StringBuilder();
                await new RemoteAppInstallService().InstallAsync(new RemoteAppInstallOptions
                {
                    Host = profile.Host,
                    SshPort = profile.SshPort > 0 ? profile.SshPort : 22,
                    Username = profile.SshUsername ?? "",
                    Password = profile.SshPassword ?? "",
                    SudoPassword = profile.SshPassword ?? "",
                    AppInstallerFile = installer
                }, msg => { lock (log) log.AppendLine(msg); });
                lock (log) return log.ToString();
            });
        }

        private void OnUpdateAppClick(object? sender, RoutedEventArgs e)
        {
            new AppUpdateWindow(_targets).Show();
        }

        private async void OnInstallAgentClick(object? sender, RoutedEventArgs e)
        {
            await ExecuteBulkActionAsync(async (taskInfo) =>
            {
                var profile = taskInfo.Profile;
                ValidateSshProfile(profile);

                // Giữ token cũ nếu máy đã có; chưa có thì sinh ngẫu nhiên và lưu lại vào hồ sơ.
                string token = profile.Token;
                if (string.IsNullOrWhiteSpace(token))
                    token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                int agentPort = profile.Port > 0 ? profile.Port : 17600;

                var log = new StringBuilder();
                await new ZcuRemoteInstallerService().InstallZcuAgentAsync(new SshInstallerOptions
                {
                    Host = profile.Host,
                    SshPort = profile.SshPort > 0 ? profile.SshPort : 22,
                    Username = profile.SshUsername ?? "",
                    Password = profile.SshPassword ?? "",
                    AgentPort = agentPort,
                    AgentToken = token
                }, (msg, _) => { lock (log) log.AppendLine(msg); });

                profile.Token = token;
                profile.Port = agentPort;
                ComputerProfileStore.Instance.Save(profile);

                // G028: agent vừa cài lại có thể mang chứng chỉ TLS khác → ghim lại vân tay theo agent hiện tại.
                var fp = await ZcuAgentDiscoveryService.ProbeTlsFingerprintAsync(profile.Host, agentPort);
                if (ComputerProfileStore.Instance.ReplaceTlsFingerprint(profile.Host, agentPort, fp))
                    profile.TlsCertFingerprint = fp is null ? null : fp.ToUpperInvariant();
                lock (log) log.AppendLine(fp is null
                    ? "Chưa đọc được vân tay TLS — đã bỏ ghim cũ, lần kết nối đầu tiên sẽ tự ghim lại."
                    : $"Đã ghim vân tay TLS mới: {fp[..Math.Min(16, fp.Length)]}…");
                lock (log) return log.ToString();
            });
        }

        private void OnMountFolderClick(object? sender, RoutedEventArgs e)
        {
            new MountFolderWindow(_targets).Show();
        }

        private async void OnRestartAgentClick(object? sender, RoutedEventArgs e)
        {
            await ExecuteBulkActionAsync(async (taskInfo) =>
            {
                var profile = taskInfo.Profile;
                ValidateSshProfile(profile);
                string status = await new ZcuRemoteInstallerService().RestartAgentAsync(new SshInstallerOptions
                {
                    Host = profile.Host,
                    SshPort = profile.SshPort > 0 ? profile.SshPort : 22,
                    Username = profile.SshUsername ?? "",
                    Password = profile.SshPassword ?? ""
                });
                if (status != "active")
                    throw new InvalidOperationException($"Restart xong nhưng service ở trạng thái '{status}'.");
                return "Remote Agent đã restart, trạng thái: active";
            });
        }

        private async Task ExecuteBulkActionAsync(Func<BulkTaskResult, Task<string>> action)
        {
            var actionButtons = new[] { "PART_BtnRunCommand", "PART_BtnUploadFile", "PART_BtnInstallApp", "PART_BtnInstallAgent", "PART_BtnRestartAgent" }
                .Select(n => this.FindControl<Button>(n)).Where(b => b != null).ToList();
            var panelProgress = this.FindControl<StackPanel>("PART_ProgressPanel");
            var pb = this.FindControl<ProgressBar>("PART_ProgressBar");
            var textProgress = this.FindControl<TextBlock>("PART_ProgressText");

            foreach (var b in actionButtons) b!.IsEnabled = false;
            // Giới hạn số máy chạy song song để không nghẽn mạng khi cài/upload file lớn
            using var gate = new System.Threading.SemaphoreSlim(8);

            if (panelProgress != null) panelProgress.IsVisible = true;
            if (pb != null) { pb.Value = 0; pb.Maximum = _results.Count; }

            int completed = 0;

            var tasks = _results.Select(async result =>
            {
                await gate.WaitAsync();
                Dispatcher.UIThread.Post(() => result.SetRunning());

                try
                {
                    string output = await Task.Run(() => action(result));
                    Dispatcher.UIThread.Post(() => result.SetSuccess(output));
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => result.SetError(ex.Message));
                }
                finally
                {
                    gate.Release();
                    // Q15: nhiều task song song cùng tăng biến đếm — phải Interlocked, dùng giá trị trả về
                    int done = System.Threading.Interlocked.Increment(ref completed);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (pb != null) pb.Value = done;
                        if (textProgress != null) textProgress.Text = $"Đang xử lý: {done}/{_results.Count}";
                    });
                }
            });

            await Task.WhenAll(tasks);

            foreach (var b in actionButtons) b!.IsEnabled = true;
        }
    }
}
