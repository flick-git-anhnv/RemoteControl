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
        /// <summary>
        /// S3: Chỉ match "sudo" ở VỊ TRÍ LỆNH (đầu chuỗi hoặc ngay sau ; &amp; | ( ) —
        /// không match chuỗi "sudo " nằm trong string literal giữa lệnh.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex SudoPattern =
            new(@"(^|[;&|(])(\s*)sudo(?=\s)", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Quote single-quote POSIX ('...' với ' bên trong → '\'') — như CronJobWindow.ShQuote.</summary>
        private static string ShQuote(string value)
            => "'" + value.Replace("'", "'\\''") + "'";

        /// <summary>
        /// S3: Chạy lệnh SSH. Password sudo được ghi vào STDIN của channel (mỗi lần sudo 1 dòng)
        /// thay vì nhúng vào command line — không còn lộ qua `ps -ef` / `/proc/*/environ` trên máy remote.
        /// Lệnh KHÔNG có sudo thì không pipe gì cả (tránh lệnh đọc stdin nhận nhầm password làm input).
        /// </summary>
        private static async Task<(string Output, string Error)> RunSshCommandAsync(SshClient ssh, string command, string sudoPassword)
        {
            int sudoCount = SudoPattern.Matches(command).Count;
            bool feedPassword = sudoCount > 0 && !string.IsNullOrEmpty(sudoPassword);

            // -S: đọc password từ stdin; -p '': không in prompt ra stderr
            string finalCmd = feedPassword ? SudoPattern.Replace(command, "$1$2sudo -S -p ''") : command;
            string bashCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus bash -c {ShQuote(finalCmd)}";

            using var cmd = ssh.CreateCommand(bashCmd, Encoding.UTF8);
            if (feedPassword)
            {
                var execTask = cmd.ExecuteAsync();
                using (var input = cmd.CreateInputStream())
                {
                    // Mỗi sudo -S đọc đúng 1 dòng password từ stdin
                    var passBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(sudoPassword + "\n", sudoCount)));
                    await input.WriteAsync(passBytes);
                }
                await execTask;
            }
            else
            {
                await cmd.ExecuteAsync();
            }

            return (cmd.Result ?? string.Empty, cmd.Error ?? string.Empty);
        }

        private readonly string _sshHost;
        private readonly int _sshPort;
        private readonly string _sshUser;
        private readonly string _sshPassword;
        private SftpClient? _sftpClient;

        public RemoteCommandWindow() : this(new ComputerProfile()) { }

        public RemoteCommandWindow(ComputerProfile prefill)
        {
            InitializeComponent();

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
                    "📋 Xem log hệ thống (syslog)"
                };
            }

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

            // Q17: LUÔN dispose — kể cả khi client đã tạo nhưng connect fail (IsConnected == false)
            this.Closed += (s, e) =>
            {
                var client = _sftpClient;
                _sftpClient = null;
                if (client == null) return;
                try { if (client.IsConnected) client.Disconnect(); } catch { /* đóng cửa sổ — bỏ qua lỗi mạng */ }
                try { client.Dispose(); } catch { }
            };

            var btnSync = this.FindControl<Button>("PART_BtnFileSync");
            if (btnSync != null) btnSync.Click += OnFileSyncClick;

            var fileListBox = this.FindControl<ListBox>("PART_FileListBox");
            if (fileListBox != null)
            {
                fileListBox.AddHandler(DragDrop.DragEnterEvent, OnFileDragEnter);
                fileListBox.AddHandler(DragDrop.DropEvent, OnFileDrop);
            }

            // Initial load of files
            _ = LoadDirectoryAsync($"/home/{_sshUser}");
        }

        // ==========================================
        // TAB 1: COMMAND EXECUTION
        // ==========================================
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
            }
        }

        private void OnSnippetComboTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (sender is AutoCompleteBox combo)
            {
                if (!combo.IsDropDownOpen)
                {
                    combo.IsDropDownOpen = true;
                }
            }
        }

        private async void OnRunCommandClick(object? sender, RoutedEventArgs e)
        {
            var sudoPassBox = this.FindControl<TextBox>("PART_SudoPassword");
            var input = this.FindControl<TextBox>("PART_CommandInput");
            
            var snippetCombo = this.FindControl<AutoCompleteBox>("PART_SnippetCombo");
            if (snippetCombo != null)
            {
                snippetCombo.Text = "";
                snippetCombo.SelectedItem = null;
            }

            string sudoPass = sudoPassBox != null && !string.IsNullOrEmpty(sudoPassBox.Text) ? sudoPassBox.Text : _sshPassword;
            string cmdToRun = input?.Text?.Trim() ?? "";

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
                await Task.Run(async () =>
                {
                    using var ssh = new SshClient(_sshHost, _sshPort, _sshUser, _sshPassword);
                    ssh.Connect();
                    if (!ssh.IsConnected)
                        throw new Exception("Không thể mở kết nối SSH tới máy đích.");

                    // S3: password đi qua stdin của channel — không nhúng vào command line
                    var (result, err) = await RunSshCommandAsync(ssh, cmdToRun, sudoPass);

                    result = result.TrimEnd();
                    if (!string.IsNullOrEmpty(result))
                    {
                        Log(result);
                    }

                    err = err.TrimEnd();
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
                
                var files = await Task.Run(() => 
                {
                    try {
                        return _sftpClient!.ListDirectory(path);
                    } catch {
                        return null;
                    }
                });

                if (files == null)
                {
                    SetStatus($"Lỗi: Không có quyền truy cập hoặc thư mục không tồn tại.", true, true);
                    return;
                }

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
            var options = new FilePickerOpenOptions
            {
                Title = "Chọn file để Upload",
                AllowMultiple = true
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0) return;

            await ProcessUploadFilesAsync(files.Select(f => f.Path.LocalPath));
        }

        private async Task ProcessUploadFilesAsync(IEnumerable<string> localPaths)
        {
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            string currentPath = txtPath?.Text?.Trim() ?? "/home";
            var pathList = localPaths.ToList();
            if (pathList.Count == 0) return;

            SetStatus($"Đang upload {pathList.Count} file...", false);

            try
            {
                await EnsureSftpConnectedAsync();

                foreach (var localPath in pathList)
                {
                    if (Directory.Exists(localPath)) continue; // skip folder upload for now
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

        private void OnFileDragEnter(object? sender, DragEventArgs e)
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null && files.Length > 0)
                e.DragEffects = DragDropEffects.Copy;
            else
                e.DragEffects = DragDropEffects.None;
        }

        private async void OnFileDrop(object? sender, DragEventArgs e)
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null && files.Length > 0)
            {
                await ProcessUploadFilesAsync(files.Select(f => f.Path.LocalPath));
            }
        }

        private async void OnFileSyncClick(object? sender, RoutedEventArgs e)
        {
            var folderOptions = new FolderPickerOpenOptions { Title = "Chọn thư mục máy cá nhân để đồng bộ lên máy đích" };
            var folders = await StorageProvider.OpenFolderPickerAsync(folderOptions);
            if (folders.Count == 0) return;

            string localDir = folders[0].Path.LocalPath;
            
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            string remoteDir = txtPath?.Text?.Trim() ?? "/home";

            SetStatus($"Đang đối chiếu file để đồng bộ từ {localDir}...", false);

            try
            {
                await EnsureSftpConnectedAsync();
                
                var localFiles = Directory.GetFiles(localDir, "*", SearchOption.TopDirectoryOnly);
                var remoteFiles = await Task.Run(() => _sftpClient!.ListDirectory(remoteDir));
                var remoteDict = remoteFiles.Where(f => !f.IsDirectory).ToDictionary(f => f.Name, f => f);

                int uploadCount = 0;
                foreach (var localFile in localFiles)
                {
                    var fileInfo = new FileInfo(localFile);
                    string fileName = fileInfo.Name;
                    bool shouldUpload = false;

                    if (remoteDict.TryGetValue(fileName, out var remoteFile))
                    {
                        // Compare size and LastWriteTime (simple check)
                        if (fileInfo.Length != remoteFile.Attributes.Size || fileInfo.LastWriteTimeUtc > remoteFile.LastWriteTime.ToUniversalTime())
                        {
                            shouldUpload = true;
                        }
                    }
                    else
                    {
                        shouldUpload = true;
                    }

                    if (shouldUpload)
                    {
                        string remotePath = remoteDir.TrimEnd('/') + "/" + fileName;
                        using var fs = new FileStream(localFile, FileMode.Open, FileAccess.Read);
                        await Task.Run(() => _sftpClient!.UploadFile(fs, remotePath, true));
                        uploadCount++;
                    }
                }

                SetStatus($"🎉 Đồng bộ hoàn tất! (Đã upload {uploadCount} file thay đổi)", false, true);
                await LoadDirectoryAsync(remoteDir);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Lỗi đồng bộ: {ex.Message}", true);
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

            // Q14: BẮT BUỘC xác nhận trước khi xóa — nêu rõ danh sách đường dẫn + cảnh báo rm -rf cho thư mục
            bool hasDirectory = items.Any(i => i.IsDirectory);
            var paths = items.Select(i => (i.IsDirectory ? "📁 " : "📄 ") + i.FullName).ToList();
            bool confirmed = await ConfirmDeleteDialog.ShowAsync(this, paths, hasDirectory);
            if (!confirmed) return;

            SetStatus($"Đang xóa {items.Count} mục...", false);

            var failures = new List<string>();
            try
            {
                await EnsureSftpConnectedAsync();

                foreach (var item in items)
                {
                    await Task.Run(() =>
                    {
                        if (item.IsDirectory)
                        {
                            // SftpClient.DeleteDirectory chỉ xóa được thư mục rỗng — fallback rm -rf qua SSH.
                            try { _sftpClient!.DeleteDirectory(item.FullName); }
                            catch
                            {
                                // Q14: fallback rm -rf phải báo lỗi nếu thất bại, không nuốt im lặng
                                try
                                {
                                    using var ssh = new SshClient(_sshHost, _sshPort, _sshUser, _sshPassword);
                                    ssh.Connect();
                                    using var cmd = ssh.CreateCommand($"rm -rf {ShQuote(item.FullName)}");
                                    cmd.Execute();
                                    string err = (cmd.Error ?? "").Trim();
                                    if (cmd.ExitStatus != 0)
                                        failures.Add($"{item.FullName}: {(string.IsNullOrEmpty(err) ? $"rm -rf exit {cmd.ExitStatus}" : err)}");
                                    ssh.Disconnect();
                                }
                                catch (Exception sshEx)
                                {
                                    failures.Add($"{item.FullName}: {sshEx.Message}");
                                }
                            }
                        }
                        else
                        {
                            try { _sftpClient!.DeleteFile(item.FullName); }
                            catch (Exception fileEx) { failures.Add($"{item.FullName}: {fileEx.Message}"); }
                        }
                    });
                }

                if (failures.Count == 0)
                {
                    SetStatus("🎉 Đã xóa thành công!", false, true);
                }
                else
                {
                    SetStatus($"⚠️ Xóa xong nhưng {failures.Count}/{items.Count} mục bị lỗi (xem log).", true);
                    Log("❌ Các mục xóa thất bại:\n" + string.Join("\n", failures));
                }
                await LoadDirectoryAsync(currentPath);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Lỗi xóa file: {ex.Message}", true);
            }
        }
    }
}
