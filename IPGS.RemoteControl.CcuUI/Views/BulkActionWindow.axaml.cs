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
        private readonly List<ComputerProfile> _targets;
        private readonly ObservableCollection<BulkTaskResult> _results;

        public BulkActionWindow() : this(new List<ComputerProfile>()) { }

        public BulkActionWindow(List<ComputerProfile> targets)
        {
            InitializeComponent();
            _targets = targets;
            _results = new ObservableCollection<BulkTaskResult>(_targets.Select(p => new BulkTaskResult { Profile = p }));
            
            var listBox = this.FindControl<ListBox>("PART_ResultListBox");
            if (listBox != null) listBox.ItemsSource = _results;
        }

        private void OnSnippetSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            var input = this.FindControl<TextBox>("PART_CommandInput");
            if (combo?.SelectedItem is ComboBoxItem item && item.Content != null && input != null)
            {
                string text = item.Content.ToString() ?? "";
                if (text.Contains("Reboot")) input.Text = "sudo reboot";
                else if (text.Contains("Cập nhật IPGS App")) input.Text = "sudo apt update && sudo apt install -y ipgs-app";
                else if (text.Contains("Xóa log cũ")) input.Text = "sudo rm -rf /var/log/ipgs/*.log";
                
                combo.SelectedIndex = -1; // reset
            }
        }

        private async void OnRunCommandClick(object? sender, RoutedEventArgs e)
        {
            var input = this.FindControl<TextBox>("PART_CommandInput");
            string cmdToRun = input?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(cmdToRun))
            {
                return; // or show message
            }

            await ExecuteBulkActionAsync(async (taskInfo) => 
            {
                var profile = taskInfo.Profile;
                int sshPort = profile.SshPort > 0 ? profile.SshPort : 22;
                
                using var ssh = new SshClient(profile.Host, sshPort, profile.SshUsername ?? "", profile.SshPassword ?? "");
                ssh.Connect();

                string escapedSudoPass = (profile.SshPassword ?? "").Replace("'", "'\\''").Replace("\n", "").Replace("\r", "");
                string envCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus KIOSK_SUDO_PASS='{escapedSudoPass}'";
                
                using var cmd = ssh.CreateCommand($"{envCmd} bash -c '{cmdToRun.Replace("'", "'\\''")}'", Encoding.UTF8);
                cmd.Execute();
                
                string result = (cmd.Result ?? string.Empty) + "\n" + (cmd.Error ?? string.Empty);
                return result;
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
                    completed++;
                    Dispatcher.UIThread.Post(() => 
                    {
                        if (pb != null) pb.Value = completed;
                        if (textProgress != null) textProgress.Text = $"Đang xử lý: {completed}/{_results.Count}";
                    });
                }
            });

            await Task.WhenAll(tasks);

            if (btnRun != null) btnRun.IsEnabled = true;
            if (btnUpload != null) btnUpload.IsEnabled = true;
        }
    }
}
