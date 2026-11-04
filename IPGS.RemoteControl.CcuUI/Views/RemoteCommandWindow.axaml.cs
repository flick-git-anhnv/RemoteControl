using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using IPGS.RemoteControl.CcuClient;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public class RemoteFileItem
    {
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public bool IsDirectory { get; set; }
        public bool IsSymbolicLink { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }

        public string Icon => (IsDirectory || IsSymbolicLink) ? "📁" : "📄";
        public Avalonia.Media.FontWeight FontWeight => (IsDirectory || IsSymbolicLink) ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
        public string TypeText => (IsDirectory || IsSymbolicLink) ? "Thư mục" : "Tệp tin";
        public string LastModifiedText => LastModified.ToString("dd/MM/yyyy HH:mm");
        
        public string SizeText
        {
            get
            {
                if (IsDirectory || IsSymbolicLink) return "";
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
        private static async Task<(string Output, string Error)> RunSshCommandAsync(
            SshClient ssh, string command, string sudoPassword, Action<string>? diag = null)
        {
            int sudoCount = SudoPattern.Matches(command).Count;
            bool hasSudo = sudoCount > 0;

            // FIX hồi quy S3: trước đây khi có sudo nhưng password rỗng thì chạy `sudo` TRẦN
            // (không có -S) → sudo cố mở /dev/tty để hỏi password → phiên SSH exec không có tty
            // → "sudo: no tty present and no askpass program specified". Nay: có sudo là LUÔN
            // thêm -S, và thiếu password thì báo lỗi rõ ràng ngay tại CCU.
            if (hasSudo && string.IsNullOrEmpty(sudoPassword))
                throw new InvalidOperationException(
                    "Lệnh có 'sudo' nhưng chưa có password sudo. Nhập vào ô \"Sudo password\" " +
                    "(hoặc đặt SSH password cho máy này trong cấu hình) rồi chạy lại.");

            // -S: đọc password từ stdin; -p '': không in prompt ra stderr
            string finalCmd = hasSudo ? SudoPattern.Replace(command, "$1$2sudo -S -p ''") : command;
            string bashCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus bash -c {ShQuote(finalCmd)}";

            // Chẩn đoán: bashCmd KHÔNG chứa password (password chỉ đi qua stdin) nên log an toàn.
            // F07: ẩn mặc định với người dùng cuối (lộ chi tiết kỹ thuật nội bộ) —
            // chỉ hiện khi bật env var IPGS_RC_DEBUG=1 (xem SshCommandHints.DiagEnabled).
            if (SshCommandHints.DiagEnabled)
                diag?.Invoke($"[debug] sudo={sudoCount}, cmd gửi đi: {bashCmd}");

            using var cmd = ssh.CreateCommand(bashCmd, Encoding.UTF8);
            if (!hasSudo)
            {
                await cmd.ExecuteAsync();
                return (cmd.Result ?? string.Empty, cmd.Error ?? string.Empty);
            }

            var execTask = cmd.ExecuteAsync();
            using (var input = await OpenInputStreamAsync(cmd))
            {
                // Mỗi sudo -S đọc đúng 1 dòng password từ stdin
                var passBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(sudoPassword + "\n", sudoCount)));
                await input.WriteAsync(passBytes);
                await input.FlushAsync();
            }
            await execTask;

            return (cmd.Result ?? string.Empty, cmd.Error ?? string.Empty);
        }

        /// <summary>
        /// SSH.NET chỉ cho tạo input stream KHI channel đã mở ("The input stream can be used only
        /// during execution"). <c>ExecuteAsync()</c> mở channel bất đồng bộ, nên gọi
        /// <c>CreateInputStream()</c> ngay sau đó có thể sớm hơn vài ms → thử lại ngắn thay vì
        /// để hỏng cả lệnh.
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

        private readonly string _sshHost;
        private readonly int _sshPort;
        private readonly string _sshUser;
        private readonly string _sshPassword;
        private SftpClient? _sftpClient;

        private readonly List<RemoteFileItem> _allRemoteFiles = new();
        private readonly ObservableCollection<RemoteFileItem> _displayRemoteFiles = new();

        public RemoteCommandWindow() : this(new ComputerProfile()) { }

        public RemoteCommandWindow(ComputerProfile prefill) : this(prefill, 0) { }

        public RemoteCommandWindow(ComputerProfile prefill, int initialTabIndex)
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

            var fileDataGrid = this.FindControl<DataGrid>("PART_FileDataGrid");
            if (fileDataGrid != null)
            {
                fileDataGrid.ItemsSource = _displayRemoteFiles;
                fileDataGrid.AddHandler(DragDrop.DragEnterEvent, OnFileDragEnter);
                fileDataGrid.AddHandler(DragDrop.DropEvent, OnFileDrop);
            }

            var txtFilter = this.FindControl<TextBox>("PART_TxtFileFilter");
            if (txtFilter != null)
            {
                txtFilter.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == nameof(TextBox.Text))
                    {
                        ApplyFileFilter();
                    }
                };
            }

            var tabControl = this.FindControl<TabControl>("PART_TabControl");
            if (tabControl != null && initialTabIndex > 0 && initialTabIndex < tabControl.ItemCount)
            {
                tabControl.SelectedIndex = initialTabIndex;
            }

            // Initial load of files
            _ = LoadDirectoryAsync($"/home/{_sshUser}");
        }

        private void ApplyFileFilter()
        {
            var txtFilter = this.FindControl<TextBox>("PART_TxtFileFilter");
            string filter = txtFilter?.Text?.Trim() ?? "";

            _displayRemoteFiles.Clear();
            var filtered = string.IsNullOrEmpty(filter)
                ? _allRemoteFiles
                : _allRemoteFiles.Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

            foreach (var item in filtered)
            {
                _displayRemoteFiles.Add(item);
            }
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
                // F03: chỉ set IsDropDownOpen=true khi view lọc nội bộ của AutoCompleteBox
                // còn RỖNG (chưa có TextChanged nào chạy population) → popup mở với 0 item
                // (vô hình) và property kẹt ở true nên gõ phím sau đó cũng không mở lại được.
                // Fix: đóng popup (reset trạng thái) → PopulateComplete() ép refresh view từ
                // ItemsSource theo SearchText hiện tại (rỗng = toàn bộ snippet, vì
                // MinimumPrefixLength=0 + FilterMode=Contains) → mở popup khi đã có item.
                combo.IsDropDownOpen = false;
                combo.PopulateComplete();
                combo.IsDropDownOpen = true;
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

            string sudoPassword = sudoPassBox?.Text ?? "";
            if (string.IsNullOrEmpty(sudoPassword)) sudoPassword = _sshPassword;

            string command = input?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(command))
            {
                SetStatus("Vui lòng nhập lệnh cần chạy.", true);
                return;
            }

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser))
            {
                SetStatus("Thiếu thông tin kết nối SSH (IP hoặc User).", true);
                return;
            }

            var btnRun = this.FindControl<Button>("PART_BtnRunCommand");
            if (btnRun != null) btnRun.IsEnabled = false;

            SetStatus("Đang thực thi lệnh qua SSH...", false);
            Log($"$ {command}");

            try
            {
                using var ssh = new SshClient(_sshHost, _sshPort, _sshUser, _sshPassword);
                await ssh.ConnectAsync(default);

                if (!ssh.IsConnected)
                {
                    SetStatus("Không thể kết nối SSH tới máy đích.", true);
                    return;
                }

                // S3: RunSshCommandAsync ghi password sudo vào STDIN, không nhúng vào command line
                var (output, error) = await RunSshCommandAsync(ssh, command, sudoPassword, Log);

                if (!string.IsNullOrEmpty(output))
                {
                    Log(output.TrimEnd());
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Log($"[STDERR]\n{error.TrimEnd()}");
                }

                SetStatus("Thực thi hoàn tất.", false, true);
                ssh.Disconnect();
            }
            catch (Exception ex)
            {
                Log($"[ERROR] {ex.Message}");
                SetStatus($"Lỗi: {ex.Message}", true);
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
                var console = this.FindControl<TextBox>("PART_LogConsole");
                if (console != null)
                {
                    console.Text += $"{text}\n";
                    console.CaretIndex = console.Text.Length;
                }
            });
        }

        private void SetStatus(string msg, bool isError = false, bool isSuccess = false)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var status = this.FindControl<TextBlock>("PART_StatusMsg");
                if (status != null)
                {
                    status.Foreground = isError ? Avalonia.Media.Brushes.Red : 
                                      (isSuccess ? Avalonia.Media.Brushes.Green : Avalonia.Media.Brushes.DarkOrange);
                    status.Text = msg;
                }
            });
        }

        // ==========================================
        // TAB 2: FILE MANAGEMENT (SFTP)
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

        private async Task LoadDirectoryAsync(string? path = null)
        {
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            if (string.IsNullOrEmpty(path))
            {
                path = txtPath?.Text?.Trim();
            }
            if (string.IsNullOrEmpty(path))
            {
                path = $"/home/{_sshUser}";
            }

            SetStatus($"Đang tải dữ liệu thư mục {path}...", false);

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
                        IsSymbolicLink = f.IsSymbolicLink,
                        Size = f.Length,
                        LastModified = f.LastWriteTime
                    })
                    .OrderByDescending(f => f.IsDirectory || f.IsSymbolicLink)
                    .ThenBy(f => f.Name)
                    .ToList();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (txtPath != null) txtPath.Text = path;
                    _allRemoteFiles.Clear();
                    _allRemoteFiles.AddRange(items);
                    ApplyFileFilter();
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
            if (currentPath == "/" || string.IsNullOrEmpty(currentPath)) return;

            currentPath = currentPath.TrimEnd('/');
            int lastSlash = currentPath.LastIndexOf('/');
            string parent = lastSlash <= 0 ? "/" : currentPath.Substring(0, lastSlash);
            
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

        public void OnFileGoClick(object? sender, RoutedEventArgs e)
        {
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            _ = LoadDirectoryAsync(txtPath?.Text);
        }

        public void OnOpenTerminalHereClick(object? sender, RoutedEventArgs e)
        {
            var txtPath = this.FindControl<TextBox>("PART_FileCurrentPath");
            string currentPath = txtPath?.Text?.Trim() ?? "/home";
            var tabControl = this.FindControl<TabControl>("PART_TabControl");
            if (tabControl != null) tabControl.SelectedIndex = 0;

            var input = this.FindControl<TextBox>("PART_CommandInput");
            if (input != null)
            {
                input.Text = $"cd {ShQuote(currentPath)} && ";
                input.Focus();
                input.CaretIndex = input.Text.Length;
            }
        }

        public void OnFileDataGridDoubleTapped(object? sender, TappedEventArgs e)
        {
            var grid = this.FindControl<DataGrid>("PART_FileDataGrid") ?? sender as DataGrid;
            var item = (sender as Control)?.DataContext as RemoteFileItem
                       ?? (e.Source as StyledElement)?.DataContext as RemoteFileItem
                       ?? (e.Source as Visual)?.FindAncestorOfType<DataGridRow>()?.DataContext as RemoteFileItem
                       ?? grid?.SelectedItem as RemoteFileItem;

            if (item != null && (item.IsDirectory || item.IsSymbolicLink))
            {
                e.Handled = true;
                string targetPath = !string.IsNullOrWhiteSpace(item.FullName)
                    ? item.FullName
                    : (this.FindControl<TextBox>("PART_FileCurrentPath")?.Text?.TrimEnd('/') + "/" + item.Name);
                _ = LoadDirectoryAsync(targetPath);
            }
        }

        public void OnFileDataGridKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var grid = this.FindControl<DataGrid>("PART_FileDataGrid") ?? sender as DataGrid;
                if (grid?.SelectedItem is RemoteFileItem item && (item.IsDirectory || item.IsSymbolicLink))
                {
                    e.Handled = true;
                    string targetPath = !string.IsNullOrWhiteSpace(item.FullName)
                        ? item.FullName
                        : (this.FindControl<TextBox>("PART_FileCurrentPath")?.Text?.TrimEnd('/') + "/" + item.Name);
                    _ = LoadDirectoryAsync(targetPath);
                }
            }
            else if (e.Key == Key.Back)
            {
                e.Handled = true;
                _ = NavigateUpAsync();
            }
            else if (e.Key == Key.Delete)
            {
                e.Handled = true;
                OnFileDeleteClick(sender, e);
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

        private static string SafeLocalName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(invalid.Contains(c) ? '_' : c);
            }
            return sb.ToString();
        }

        private async void OnFileDownloadClick(object? sender, RoutedEventArgs e)
        {
            var dataGrid = this.FindControl<DataGrid>("PART_FileDataGrid");
            var selectedList = dataGrid?.SelectedItems?.Cast<RemoteFileItem>().ToList();
            if (selectedList == null || selectedList.Count == 0)
            {
                if (dataGrid?.SelectedItem is RemoteFileItem single)
                    selectedList = new List<RemoteFileItem> { single };
                else
                {
                    SetStatus("Vui lòng chọn ít nhất 1 file để tải về.", true);
                    return;
                }
            }

            var items = selectedList.Where(i => !i.IsDirectory).ToList();
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
                        SuggestedFileName = SafeLocalName(item.Name)
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
                        string localPath = Path.Combine(localDir, SafeLocalName(item.Name));
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
            var dataGrid = this.FindControl<DataGrid>("PART_FileDataGrid");
            var selectedList = dataGrid?.SelectedItems?.Cast<RemoteFileItem>().ToList();
            if (selectedList == null || selectedList.Count == 0)
            {
                if (dataGrid?.SelectedItem is RemoteFileItem single)
                    selectedList = new List<RemoteFileItem> { single };
                else
                {
                    SetStatus("Vui lòng chọn file hoặc thư mục để xóa.", true);
                    return;
                }
            }

            var items = selectedList;
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
