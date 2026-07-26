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

        private string _statusIcon = "⏳";
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
            StatusIcon = "🔄";
        }

        public void SetSuccess(string output = "")
        {
            StatusText = "Thành công";
            StatusColor = "#10B981"; // Emerald
            StatusIcon = "✅";
            if (!string.IsNullOrEmpty(output)) Output = output.Trim();
        }

        public void SetError(string error)
        {
            StatusText = "Lỗi";
            StatusColor = "#EF4444"; // Red
            StatusIcon = "❌";
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
        /// S3: Chỉ match "sudo" ở VỊ TRÍ LỆNH (đầu chuỗi hoặc ngay sau ; &amp; | ( ) —
        /// không match chuỗi "sudo " nằm trong string literal giữa lệnh.
        /// (Helper cục bộ — ShellQuote bên CcuClient là internal, xem Handoff bước 1.1.)
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex SudoPattern =
            new(@"(^|[;&|(])(\s*)sudo(?=\s)", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Quote single-quote POSIX ('...' với ' bên trong → '\'').</summary>
        private static string ShQuote(string value)
            => "'" + value.Replace("'", "'\\''") + "'";

        /// <summary>
        /// S3: Chạy lệnh SSH. Password sudo ghi vào STDIN của channel (mỗi sudo 1 dòng)
        /// thay vì nhúng vào command line — không lộ qua `ps -ef` / `/proc/*/environ` trên máy remote.
        /// Lệnh không có sudo thì không pipe password (tránh lệnh đọc stdin nhận nhầm password).
        /// </summary>
        private static async Task<(string Output, string Error)> RunSshCommandAsync(SshClient ssh, string command, string sudoPassword)
        {
            int sudoCount = SudoPattern.Matches(command).Count;
            bool hasSudo = sudoCount > 0;

            // FIX hồi quy S3: trước đây khi có sudo nhưng password rỗng thì chạy `sudo` TRẦN
            // (không có -S) → sudo cố mở /dev/tty để hỏi password → phiên SSH exec không có tty
            // → "sudo: no tty present and no askpass program specified". Nay: có sudo là LUÔN
            // thêm -S, và thiếu password thì báo lỗi rõ ràng ngay tại CCU.
            if (hasSudo && string.IsNullOrEmpty(sudoPassword))
                throw new InvalidOperationException(
                    "Lệnh có 'sudo' nhưng máy này chưa có password sudo/SSH trong cấu hình — " +
                    "bổ sung password cho máy rồi chạy lại.");

            string finalCmd = hasSudo ? SudoPattern.Replace(command, "$1$2sudo -S -p ''") : command;
            string bashCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus bash -c {ShQuote(finalCmd)}";

            using var cmd = ssh.CreateCommand(bashCmd, Encoding.UTF8);
            if (!hasSudo)
            {
                await cmd.ExecuteAsync();
                return (cmd.Result ?? string.Empty, cmd.Error ?? string.Empty);
            }

            var execTask = cmd.ExecuteAsync();
            using (var input = await OpenInputStreamAsync(cmd))
            {
                var passBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(sudoPassword + "\n", sudoCount)));
                await input.WriteAsync(passBytes);
                await input.FlushAsync();
            }
            await execTask;

            return (cmd.Result ?? string.Empty, cmd.Error ?? string.Empty);
        }

        /// <summary>
        /// SSH.NET chỉ cho tạo input stream KHI channel đã mở ("The input stream can be used only
        /// during execution"). <c>ExecuteAsync()</c> mở channel bất đồng bộ nên gọi
        /// <c>CreateInputStream()</c> ngay sau đó có thể sớm hơn vài ms → thử lại ngắn.
        /// </summary>
        private static async Task<Stream> OpenInputStreamAsync(SshCommand cmd)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return cmd.CreateInputStream();
                }
                catch (InvalidOperationException) when (attempt < 40)
                {
                    await Task.Delay(25);
                }
            }
        }

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
                    "Máy chưa cấu hình SSH user — bấm '✏️ Sửa' máy tính này để bổ sung SSH username/password rồi chạy lại.");
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
                    "🔄 Khởi động lại (Reboot)",
                    "🔌 Tắt máy (Shutdown)",
                    "💾 Kiểm tra RAM & Ổ cứng",
                    "🧹 Dọn dẹp cache (apt clean)",
                    "📊 Xem Top Process",
                    "🌐 Cập nhật APT (apt update)",
                    "🌐 Cập nhật & Nâng cấp (apt update & upgrade)",
                    "🔍 Kiểm tra IP & Mạng",
                    "🛠 Khởi động lại SSH (Restart SSH)",
                    "📋 Xem log hệ thống (syslog)",
                    "💾 Cập nhật Remote App",
                    "🧹 Xóa log cũ"
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
                var (output, error) = await RunSshCommandAsync(ssh, cmdToRun, profile.SshPassword ?? "");
                return output + "\n" + error;
            });
        }

        private async void OnUploadFileClick(object? sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions
            {
                Title = "Chọn file để Upload lên tất cả máy",
                AllowMultiple = false
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0) return;
            
            string localPath = files[0].Path.LocalPath;
            string fileName = Path.GetFileName(localPath);
            string remoteDir = "/home"; // Default remote directory for bulk upload

            // Ask user for remote path (Ideally we would have a dialog, but for simplicity we upload to /home)
            // Can be expanded later.

            await ExecuteBulkActionAsync(async (taskInfo) => 
            {
                var profile = taskInfo.Profile;
                ValidateSshProfile(profile); // F05
                int sshPort = profile.SshPort > 0 ? profile.SshPort : 22;
                string remotePath = $"{remoteDir}/{fileName}";

                using var sftp = new SftpClient(profile.Host, sshPort, profile.SshUsername ?? "", profile.SshPassword ?? "");
                sftp.Connect();

                using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read);
                sftp.UploadFile(fs, remotePath, true);
                
                return $"Đã upload thành công tới: {remotePath}";
            });
        }

        private async Task ExecuteBulkActionAsync(Func<BulkTaskResult, Task<string>> action)
        {
            var btnRun = this.FindControl<Button>("PART_BtnRunCommand");
            var btnUpload = this.FindControl<Button>("PART_BtnUploadFile");
            var panelProgress = this.FindControl<StackPanel>("PART_ProgressPanel");
            var pb = this.FindControl<ProgressBar>("PART_ProgressBar");
            var textProgress = this.FindControl<TextBlock>("PART_ProgressText");

            if (btnRun != null) btnRun.IsEnabled = false;
            if (btnUpload != null) btnUpload.IsEnabled = false;
            
            if (panelProgress != null) panelProgress.IsVisible = true;
            if (pb != null) { pb.Value = 0; pb.Maximum = _results.Count; }

            int completed = 0;

            var tasks = _results.Select(async result => 
            {
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

            if (btnRun != null) btnRun.IsEnabled = true;
            if (btnUpload != null) btnUpload.IsEnabled = true;
        }
    }
}
