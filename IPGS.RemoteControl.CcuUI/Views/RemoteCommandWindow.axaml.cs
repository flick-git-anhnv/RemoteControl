using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using IPGS.RemoteControl.CcuClient;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public class RemoteFileItem
    {
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }

        public string Icon => IsDirectory ? "📁" : "📄";
        public Avalonia.Media.FontWeight FontWeight => IsDirectory ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
        
        public string SizeText
        {
            get
            {
                if (IsDirectory) return "";
                if (Size < 1024) return $"{Size} B";
                if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
                if (Size < 1024 * 1024 * 1024) return $"{Size / (1024.0 * 1024.0):F1} MB";
                return $"{Size / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }
    }

    public partial class RemoteCommandWindow : Window
    {
        private readonly string _sshHost;
        private readonly int _sshPort;
        private readonly string _sshUser;
        private readonly string _sshPassword;
        private SftpClient? _sftpClient;

        public RemoteCommandWindow() : this(new ComputerProfile()) { }

        public RemoteCommandWindow(ComputerProfile prefill)
        {
            InitializeComponent();

            _sshHost = prefill.Host;
            _sshPort = prefill.SshPort > 0 ? prefill.SshPort : 22;
            _sshUser = prefill.SshUsername ?? "";
            _sshPassword = prefill.SshPassword ?? "";

            var targetText = this.FindControl<TextBlock>("PART_TargetHostText");
            if (targetText != null)
            {
                targetText.Text = string.IsNullOrWhiteSpace(_sshHost)
                    ? "Đang kết nối tới: —"
                    : $"Đang kết nối tới: {_sshUser}@{_sshHost}:{_sshPort}";
            }

            // Command events
            var btnRun = this.FindControl<Button>("PART_BtnRunCommand");
            if (btnRun != null) btnRun.Click += OnRunCommandClick;

            // File events
            var btnRefresh = this.FindControl<Button>("PART_BtnFileRefresh");
            if (btnRefresh != null) btnRefresh.Click += async (s, e) => await LoadDirectoryAsync();

            var btnUpDir = this.FindControl<Button>("PART_BtnFileUpDir");
            if (btnUpDir != null) btnUpDir.Click += async (s, e) => await NavigateUpAsync();

            var btnUpload = this.FindControl<Button>("PART_BtnFileUpload");
            if (btnUpload != null) btnUpload.Click += OnFileUploadClick;

            var btnDownload = this.FindControl<Button>("PART_BtnFileDownload");
            if (btnDownload != null) btnDownload.Click += OnFileDownloadClick;

            var btnDelete = this.FindControl<Button>("PART_BtnFileDelete");
            if (btnDelete != null) btnDelete.Click += OnFileDeleteClick;

            this.Closed += (s, e) => 
            {
                if (_sftpClient != null && _sftpClient.IsConnected)
                {
                    _sftpClient.Disconnect();
                    _sftpClient.Dispose();
                }
            };

            // Initial load of files
            _ = LoadDirectoryAsync($"/home/{_sshUser}");
        }

        // ==========================================
        // TAB 1: COMMAND EXECUTION
        // ==========================================
        private async void OnRunCommandClick(object? sender, RoutedEventArgs e)
        {
            var sudoPassBox = this.FindControl<TextBox>("PART_SudoPassword");
            string sudoPass = sudoPassBox != null && !string.IsNullOrEmpty(sudoPassBox.Text) ? sudoPassBox.Text : _sshPassword;
            
            var commandInput = this.FindControl<TextBox>("PART_CommandInput");
            string cmdToRun = commandInput?.Text?.Trim() ?? "";

            var statusMsg = this.FindControl<TextBlock>("PART_StatusMsg");
            var btnRun = this.FindControl<Button>("PART_BtnRunCommand");
            var logConsole = this.FindControl<TextBox>("PART_LogConsole");

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                SetStatus("Thiếu thông tin kết nối SSH.", true);
                return;
            }

            if (string.IsNullOrEmpty(cmdToRun))
            {
                SetStatus("Vui lòng nhập lệnh cần chạy.", true);
                return;
            }

            if (btnRun != null) btnRun.IsEnabled = false;
            if (logConsole != null) logConsole.Text = "";
            
            SetStatus("Đang kết nối và thực thi...", false);

            try
            {
                await Task.Run(() =>
                {
                    using var ssh = new SshClient(_sshHost, _sshPort, _sshUser, _sshPassword);
                    ssh.Connect();
                    if (!ssh.IsConnected)
                        throw new Exception("Không thể mở kết nối SSH tới máy đích.");

                    string escapedSudoPass = sudoPass
                        .Replace("'", "'\\''")
                        .Replace("\n", "")
                        .Replace("\r", "");
                    
                    string envCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus KIOSK_SUDO_PASS='{escapedSudoPass}'";
                    
                    using var cmd = ssh.CreateCommand($"{envCmd} bash -c '{cmdToRun.Replace("'", "'\\''")}'", Encoding.UTF8);
                    
                    cmd.Execute();
                    
                    string result = (cmd.Result ?? string.Empty).TrimEnd();
                    if (!string.IsNullOrEmpty(result))
                    {
                        Log(result);
                    }

                    string err = (cmd.Error ?? string.Empty).TrimEnd();
                    if (!string.IsNullOrEmpty(err))
                    {
                        Log("[STDERR / WARNING]:\n" + err);
                    }
                    
                    ssh.Disconnect();
                });

                SetStatus("🎉 Lệnh đã thực thi xong!", false, true);
            }
            catch (Exception ex)
            {
                SetStatus("❌ Lỗi: " + ex.Message, true);
                Log("❌ LỖI: " + ex.Message);
            }
            finally
            {
                if (btnRun != null) btnRun.IsEnabled = true;
            }
        }

        private void Log(string text)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (string.IsNullOrEmpty(text)) return;
                var logConsole = this.FindControl<TextBox>("PART_LogConsole");
                if (logConsole != null)
                {
                    logConsole.Text += $"[{DateTime.Now:HH:mm:ss}]\n{text}\n\n";
                    logConsole.CaretIndex = logConsole.Text.Length;
                }
            });
        }

        private void SetStatus(string msg, bool isError, bool isSuccess = false)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var statusMsg = this.FindControl<TextBlock>("PART_StatusMsg");
                if (statusMsg != null)
                {
                    statusMsg.Text = msg;
                    if (isError) statusMsg.Foreground = Avalonia.Media.Brushes.Red;
                    else if (isSuccess) statusMsg.Foreground = Avalonia.Media.Brushes.Green;
                    else statusMsg.Foreground = Avalonia.Media.Brushes.SlateGray;
                }
            });
        }

        // ==========================================
        // TAB 2: FILE TRANSFER
        // ==========================================
        
        private async Task EnsureSftpConnectedAsync()
        {
            if (_sftpClient == null)
            {
                _sftpClient = new SftpClient(_sshHost, _sshPort, _sshUser, _sshPassword);
            }
            if (!_sftpClient.IsConnected)
            {
                await Task.Run(() => _sftpClient.Connect());
            }
        }

        private async Task LoadDirectoryAsync(string? newPath = null)
        {
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            string path = newPath ?? txtPath?.Text?.Trim() ?? "/home";

            if (string.IsNullOrEmpty(path)) path = "/home";

            SetStatus($"Đang tải danh sách file tại {path}...", false);

            try
            {
                await EnsureSftpConnectedAsync();
                
                var files = await Task.Run(() => _sftpClient!.ListDirectory(path));

                var items = files
                    .Where(f => f.Name != "." && f.Name != "..")
                    .Select(f => new RemoteFileItem
                    {
                        Name = f.Name,
                        FullName = f.FullName,
                        IsDirectory = f.IsDirectory,
                        Size = f.Length,
                        LastModified = f.LastWriteTime
                    })
                    .OrderByDescending(f => f.IsDirectory)
                    .ThenBy(f => f.Name)
                    .ToList();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (txtPath != null) txtPath.Text = path;
                    var listBox = this.FindControl<ListBox>("PART_FileListBox");
                    if (listBox != null) listBox.ItemsSource = items;
                    SetStatus($"Đã tải {items.Count} mục.", false, true);
                });
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Lỗi đọc thư mục: {ex.Message}", true);
            }
        }

        private async Task NavigateUpAsync()
        {
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            string currentPath = txtPath?.Text?.Trim() ?? "/";
            if (currentPath == "/") return;

            string parent = Path.GetDirectoryName(currentPath)?.Replace("\\", "/") ?? "/";
            if (string.IsNullOrEmpty(parent)) parent = "/";
            
            await LoadDirectoryAsync(parent);
        }

        public void OnFileCurrentPathKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
                _ = LoadDirectoryAsync(txtPath?.Text);
            }
        }

        public void OnFileItemDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Control c && c.DataContext is RemoteFileItem item)
            {
                if (item.IsDirectory)
                {
                    _ = LoadDirectoryAsync(item.FullName);
                }
            }
        }

        private async void OnFileUploadClick(object? sender, RoutedEventArgs e)
        {
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            string currentPath = txtPath?.Text?.Trim() ?? "/home";

            var options = new FilePickerOpenOptions
            {
                Title = "Chọn file để Upload",
                AllowMultiple = true
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0) return;

            SetStatus($"Đang upload {files.Count} file...", false);

            try
            {
                await EnsureSftpConnectedAsync();

                foreach (var file in files)
                {
                    string localPath = file.Path.LocalPath;
                    string fileName = Path.GetFileName(localPath);
                    string remotePath = currentPath.TrimEnd('/') + "/" + fileName;

                    using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read);
                    await Task.Run(() => _sftpClient!.UploadFile(fs, remotePath, true));
                }

                SetStatus("🎉 Upload thành công!", false, true);
                await LoadDirectoryAsync(currentPath);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Lỗi upload: {ex.Message}", true);
            }
        }

        private async void OnFileDownloadClick(object? sender, RoutedEventArgs e)
        {
            var listBox = this.FindControl<ListBox>("PART_FileListBox");
            if (listBox?.SelectedItems == null || listBox.SelectedItems.Count == 0)
            {
                SetStatus("Vui lòng chọn ít nhất 1 file để tải về.", true);
                return;
            }

            var items = listBox.SelectedItems.Cast<RemoteFileItem>().Where(i => !i.IsDirectory).ToList();
            if (items.Count == 0)
            {
                SetStatus("Chỉ có thể tải về file, không thể tải cả thư mục.", true);
                return;
            }

            try
            {
                await EnsureSftpConnectedAsync();

                if (items.Count == 1)
                {
                    var item = items[0];
                    var options = new FilePickerSaveOptions
                    {
                        Title = "Lưu file",
                        SuggestedFileName = item.Name
                    };

                    var file = await StorageProvider.SaveFilePickerAsync(options);
                    if (file == null) return;

                    SetStatus($"Đang download {item.Name}...", false);
                    string localPath = file.Path.LocalPath;
                    
                    using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write);
                    await Task.Run(() => _sftpClient!.DownloadFile(item.FullName, fs));
                }
                else
                {
                    var folderOptions = new FolderPickerOpenOptions { Title = "Chọn thư mục lưu nhiều file" };
                    var folders = await StorageProvider.OpenFolderPickerAsync(folderOptions);
                    if (folders.Count == 0) return;

                    string localDir = folders[0].Path.LocalPath;
                    SetStatus($"Đang download {items.Count} file...", false);

                    foreach (var item in items)
                    {
                        string localPath = Path.Combine(localDir, item.Name);
                        using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write);
                        await Task.Run(() => _sftpClient!.DownloadFile(item.FullName, fs));
                    }
                }

                SetStatus("🎉 Download thành công!", false, true);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Lỗi download: {ex.Message}", true);
            }
        }

        private async void OnFileDeleteClick(object? sender, RoutedEventArgs e)
        {
            var listBox = this.FindControl<ListBox>("PART_FileListBox");
            if (listBox?.SelectedItems == null || listBox.SelectedItems.Count == 0)
            {
                SetStatus("Vui lòng chọn file hoặc thư mục để xóa.", true);
                return;
            }

            var items = listBox.SelectedItems.Cast<RemoteFileItem>().ToList();
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            string currentPath = txtPath?.Text?.Trim() ?? "/home";

            // Ideally we should ask for confirmation, but for now we proceed.
            // Since Avalonia MessageBox isn't built-in easily without a package, we just delete.
            SetStatus($"Đang xóa {items.Count} mục...", false);

            try
            {
                await EnsureSftpConnectedAsync();

                foreach (var item in items)
                {
                    await Task.Run(() => 
                    {
                        if (item.IsDirectory)
                        {
                            // simple delete directory might fail if not empty, but SftpClient.DeleteDirectory requires empty.
                            // To force delete we could run a ssh command "rm -rf", but let's try SFTP first.
                            try { _sftpClient!.DeleteDirectory(item.FullName); }
                            catch 
                            {
                                // fallback to rm -rf via SSH
                                using var ssh = new SshClient(_sshHost, _sshPort, _sshUser, _sshPassword);
                                ssh.Connect();
                                using var cmd = ssh.CreateCommand($"rm -rf '{item.FullName.Replace("'", "'\\''")}'");
                                cmd.Execute();
                                ssh.Disconnect();
                            }
                        }
                        else
                        {
                            _sftpClient!.DeleteFile(item.FullName);
                        }
                    });
                }

                SetStatus("🎉 Đã xóa thành công!", false, true);
                await LoadDirectoryAsync(currentPath);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Lỗi xóa file: {ex.Message}", true);
            }
        }
    }
}
