using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;
using KztekComponentAvalonia.Controls;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace IPGS.RemoteControl.CcuUI.Views;

public class SftpFileItem
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastWriteTime { get; set; }

    public string Type => IsDirectory ? "Thư mục" : "File";
    public string SizeString => IsDirectory ? "" : FormatSize(Size);
    public string LastModifiedString => LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatSize(long bytes)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        if (bytes == 0)
            return "0 B";
        long bytesAbsolute = Math.Abs(bytes);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytesAbsolute, 1024)));
        double num = Math.Round(bytesAbsolute / Math.Pow(1024, place), 1);
        return (Math.Sign(bytes) * num).ToString() + " " + suf[place];
    }
}

public partial class FileManagerWindow : Window
{
    private readonly ComputerProfile _profile;
    private SftpClient? _sftpClient;
    private readonly List<SftpFileItem> _currentFiles = new();
    public ObservableCollection<SftpFileItem> Files { get; set; } = new();

    // L6: guard chống race giữa Closed→Dispose và background task đang dùng _sftpClient.
    // Acquire/Release + Close đều chạy trên UI thread (trước Task.Run / sau await) → không cần lock.
    private int _activeOps;
    private bool _closeRequested;

    /// <summary>L6: Lấy client để thao tác. Trả null nếu cửa sổ đang đóng / chưa kết nối.</summary>
    private SftpClient? AcquireClient()
    {
        var client = _sftpClient;
        if (_closeRequested || client == null || !client.IsConnected) return null;
        _activeOps++;
        return client;
    }

    /// <summary>L6: Trả client sau thao tác — nếu cửa sổ đã yêu cầu đóng và đây là op cuối → dispose.</summary>
    private void ReleaseClient()
    {
        _activeOps--;
        if (_activeOps <= 0 && _closeRequested)
            DisposeClient();
    }

    private void DisposeClient()
    {
        var client = _sftpClient;
        _sftpClient = null;
        if (client == null) return;
        try { if (client.IsConnected) client.Disconnect(); } catch { /* đang đóng — bỏ qua lỗi mạng */ }
        try { client.Dispose(); } catch { }
    }

    public FileManagerWindow()
    {
        InitializeComponent();
        _profile = new ComputerProfile();
    }

    public FileManagerWindow(ComputerProfile profile)
    {
        InitializeComponent();
        _profile = profile;
        
        var titleTxt = this.FindControl<TextBlock>("PART_TitleText");
        if (titleTxt != null)
            titleTxt.Text = $"Quản Lý File Chuyên Sâu - {profile.DisplayName}";

        var fileList = this.FindControl<DataGrid>("PART_FileList");
        if (fileList != null)
        {
            fileList.ItemsSource = Files;
            fileList.DoubleTapped += OnFileListDoubleTapped;
            fileList.AddHandler(DragDrop.DropEvent, OnFileDrop);
        }

        if (this.FindControl<KzButton>("PART_BtnUp") is { } btnUp) btnUp.Click += OnUpClick;
        if (this.FindControl<KzButton>("PART_BtnGo") is { } btnGo) btnGo.Click += OnGoClick;
        if (this.FindControl<KzButton>("PART_BtnRefresh") is { } btnRefresh) btnRefresh.Click += OnRefreshClick;
        if (this.FindControl<KzButton>("PART_BtnUpload") is { } btnUpload) btnUpload.Click += OnUploadClick;
        if (this.FindControl<KzButton>("PART_BtnDelete") is { } btnDelete) btnDelete.Click += OnDeleteClick;
        if (this.FindControl<KzButton>("PART_BtnSync") is { } btnSync) btnSync.Click += OnSyncClick;

        if (this.FindControl<KzTextBox>("PART_TxtFilter") is { } txtFilter)
        {
            txtFilter.TextChanged += (s, e) => ApplyFilter(txtFilter.Text);
        }

        this.Opened += async (s, e) => await ConnectAndLoadAsync();
        this.Closed += (s, e) => Disconnect();
    }

    private void SetStatus(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TextBlock>("PART_StatusMsg") is { } statusTxt)
            {
                statusTxt.Text = message;
            }
        });
    }

    private async Task ConnectAndLoadAsync()
    {
        SetStatus("Đang kết nối SFTP...");
        try
        {
            var client = await Task.Run(() =>
            {
                string host = _profile.Host;
                int port = _profile.SshPort > 0 ? _profile.SshPort : 22;
                string username = !string.IsNullOrWhiteSpace(_profile.SshUsername) ? _profile.SshUsername : "kztek";
                string password = _profile.SshPassword ?? "";

                var sftp = new SftpClient(host, port, username, password);
                sftp.Connect();
                return sftp;
            });

            // L6: nếu user đóng cửa sổ trong lúc đang connect → dispose ngay, không giữ lại
            if (_closeRequested)
            {
                try { client.Disconnect(); } catch { }
                try { client.Dispose(); } catch { }
                return;
            }
            _sftpClient = client;

            SetStatus("Đã kết nối. Đang tải danh sách file...");
            await LoadDirectoryAsync("/");
        }
        catch (Exception ex)
        {
            SetStatus($"Lỗi kết nối SFTP: {ex.Message}");
        }
    }

    private void Disconnect()
    {
        // L6: chỉ dispose ngay khi không còn op nào đang chạy; ngược lại op cuối cùng
        // sẽ dispose trong ReleaseClient() — tránh NRE/ObjectDisposedException trên thread pool.
        _closeRequested = true;
        if (_activeOps <= 0)
            DisposeClient();
    }

    private async Task LoadDirectoryAsync(string path)
    {
        var sftp = AcquireClient();
        if (sftp == null)
        {
            SetStatus("Lỗi: Chưa kết nối SFTP.");
            return;
        }

        SetStatus($"Đang tải danh mục: {path}");

        if (this.FindControl<KzTextBox>("PART_TxtPath") is { } pathTxt)
        {
            pathTxt.Text = path;
        }

        try
        {
            var files = await Task.Run(() =>
            {
                try {
                    return sftp.ListDirectory(path).ToList();
                } catch {
                    return null;
                }
            });

            if (files == null)
            {
                SetStatus($"Lỗi: Không có quyền truy cập hoặc thư mục không tồn tại.");
                return;
            }
            
            Dispatcher.UIThread.Post(() =>
            {
                _currentFiles.Clear();
                foreach (var file in files.OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name))
                {
                    if (file.Name == "." || file.Name == "..") continue;

                    _currentFiles.Add(new SftpFileItem
                    {
                        Name = file.Name,
                        FullName = file.FullName,
                        IsDirectory = file.IsDirectory,
                        Size = file.Length,
                        LastWriteTime = file.LastWriteTime
                    });
                }
                
                var filterText = this.FindControl<KzTextBox>("PART_TxtFilter")?.Text ?? "";
                ApplyFilter(filterText);
            });
        }
        catch (Exception ex)
        {
            SetStatus($"Lỗi khi tải thư mục: {ex.Message}");
        }
        finally
        {
            ReleaseClient();
        }
    }

    private void ApplyFilter(string? filter)
    {
        Files.Clear();
        var lowerFilter = filter?.ToLowerInvariant() ?? "";
        
        foreach (var item in _currentFiles)
        {
            if (string.IsNullOrEmpty(lowerFilter) || item.Name.ToLowerInvariant().Contains(lowerFilter))
            {
                Files.Add(item);
            }
        }
        
        SetStatus($"Đã tải xong {_currentFiles.Count} mục (Hiển thị {Files.Count}).");
    }

    private async void OnUpClick(object? sender, RoutedEventArgs e)
    {
        string currentPath = this.FindControl<KzTextBox>("PART_TxtPath")?.Text ?? "/";
        if (currentPath == "/") return;
        
        string parentPath = Path.GetDirectoryName(currentPath.TrimEnd('/'))?.Replace('\\', '/') ?? "/";
        if (string.IsNullOrWhiteSpace(parentPath)) parentPath = "/";
        
        await LoadDirectoryAsync(parentPath);
    }

    private async void OnGoClick(object? sender, RoutedEventArgs e)
    {
        string currentPath = this.FindControl<KzTextBox>("PART_TxtPath")?.Text ?? "/";
        await LoadDirectoryAsync(currentPath);
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        string currentPath = this.FindControl<KzTextBox>("PART_TxtPath")?.Text ?? "/";
        await LoadDirectoryAsync(currentPath);
    }

    private async void OnFileListDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var fileList = this.FindControl<DataGrid>("PART_FileList");
        if (fileList?.SelectedItem is SftpFileItem item && item.IsDirectory)
        {
            await LoadDirectoryAsync(item.FullName);
        }
    }

    private async void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Chọn File để Upload",
            AllowMultiple = true
        });

        if (files.Count > 0)
        {
            // L6: acquire SAU khi await picker — user có thể đã đóng cửa sổ trong lúc chọn file
            var sftp = AcquireClient();
            if (sftp == null)
            {
                SetStatus("Lỗi: Chưa kết nối SFTP.");
                return;
            }

            string currentPath = this.FindControl<KzTextBox>("PART_TxtPath")?.Text ?? "/";
            if (!currentPath.EndsWith("/")) currentPath += "/";

            SetStatus($"Đang upload {files.Count} file...");

            try
            {
                await Task.Run(async () =>
                {
                    foreach (var file in files)
                    {
                        using var stream = await file.OpenReadAsync();
                        string remotePath = currentPath + file.Name;
                        sftp.UploadFile(stream, remotePath);
                    }
                });

                SetStatus($"Đã upload thành công {files.Count} file.");
                await LoadDirectoryAsync(currentPath);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi khi upload: {ex.Message}");
            }
            finally
            {
                ReleaseClient();
            }
        }
    }

    private async void OnSyncClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Chọn thư mục nội bộ để đồng bộ lên máy đích"
        });

        if (folders.Count > 0)
        {
            // L6: acquire SAU khi await picker — user có thể đã đóng cửa sổ trong lúc chọn thư mục
            var sftp = AcquireClient();
            if (sftp == null)
            {
                SetStatus("Lỗi: Chưa kết nối SFTP.");
                return;
            }

            string localDir = folders[0].Path.LocalPath;
            string remoteDir = this.FindControl<KzTextBox>("PART_TxtPath")?.Text ?? "/";
            if (!remoteDir.EndsWith("/")) remoteDir += "/";

            SetStatus($"Đang đối chiếu file để đồng bộ từ {localDir}...");

            try
            {
                var localFiles = Directory.GetFiles(localDir, "*", SearchOption.TopDirectoryOnly);
                var remoteFiles = await Task.Run(() => sftp.ListDirectory(remoteDir));
                var remoteDict = remoteFiles.Where(f => !f.IsDirectory).ToDictionary(f => f.Name, f => f);

                int uploadCount = 0;
                await Task.Run(() =>
                {
                    foreach (var localFile in localFiles)
                    {
                        var fileInfo = new FileInfo(localFile);
                        bool shouldUpload = true;

                        if (remoteDict.TryGetValue(fileInfo.Name, out var remoteFile))
                        {
                            // If size is same, skip (very basic sync logic, can improve timestamp later)
                            if (remoteFile.Length == fileInfo.Length)
                            {
                                shouldUpload = false;
                            }
                        }

                        if (shouldUpload)
                        {
                            using var stream = File.OpenRead(localFile);
                            sftp.UploadFile(stream, remoteDir + fileInfo.Name);
                            uploadCount++;
                        }
                    }
                });

                SetStatus($"Đồng bộ hoàn tất. Đã upload {uploadCount}/{localFiles.Length} file.");
                await LoadDirectoryAsync(remoteDir);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi khi đồng bộ: {ex.Message}");
            }
            finally
            {
                ReleaseClient();
            }
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        var fileList = this.FindControl<DataGrid>("PART_FileList");
        if (fileList == null || fileList.SelectedItems.Count == 0)
        {
            SetStatus("Vui lòng chọn ít nhất 1 file/thư mục để xóa.");
            return;
        }

        var itemsToDelete = fileList.SelectedItems.Cast<SftpFileItem>().ToList();
        string currentPath = this.FindControl<KzTextBox>("PART_TxtPath")?.Text ?? "/";

        // Q14: BẮT BUỘC xác nhận trước khi xóa — nêu rõ danh sách đường dẫn sẽ xóa
        bool hasDirectory = itemsToDelete.Any(i => i.IsDirectory);
        var paths = itemsToDelete.Select(i => (i.IsDirectory ? "📁 " : "📄 ") + i.FullName).ToList();
        bool confirmed = await ConfirmDeleteDialog.ShowAsync(this, paths, hasDirectory);
        if (!confirmed) return;

        // L6: acquire SAU dialog xác nhận
        var sftp = AcquireClient();
        if (sftp == null)
        {
            SetStatus("Lỗi: Chưa kết nối SFTP.");
            return;
        }

        SetStatus($"Đang xóa {itemsToDelete.Count} mục...");

        var failures = new List<string>();
        try
        {
            await Task.Run(() =>
            {
                foreach (var item in itemsToDelete)
                {
                    // Q14: không nuốt lỗi im lặng — gom lại và hiển thị cho user
                    try
                    {
                        if (item.IsDirectory)
                        {
                            // SFTP DeleteDirectory chỉ xóa được thư mục RỖNG (không hỗ trợ đệ quy)
                            sftp.DeleteDirectory(item.FullName);
                        }
                        else
                        {
                            sftp.DeleteFile(item.FullName);
                        }
                    }
                    catch (Exception itemEx)
                    {
                        string hint = item.IsDirectory ? " (SFTP chỉ xóa được thư mục rỗng)" : "";
                        failures.Add($"{item.FullName}: {itemEx.Message}{hint}");
                    }
                }
            });

            if (failures.Count == 0)
                SetStatus("Đã xóa xong.");
            else
                SetStatus($"⚠️ Xóa xong nhưng {failures.Count}/{itemsToDelete.Count} mục bị lỗi: {failures[0]}" +
                          (failures.Count > 1 ? $" (+{failures.Count - 1} lỗi khác)" : ""));

            await LoadDirectoryAsync(currentPath);
        }
        catch (Exception ex)
        {
            SetStatus($"Lỗi khi xóa: {ex.Message}");
        }
        finally
        {
            ReleaseClient();
        }
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?.ToList();
        if (files != null && files.Count > 0)
        {
            var sftp = AcquireClient();
            if (sftp == null) return;

            string currentPath = this.FindControl<KzTextBox>("PART_TxtPath")?.Text ?? "/";
            if (!currentPath.EndsWith("/")) currentPath += "/";

            SetStatus($"Đang upload {files.Count} file từ kéo thả...");

            try
            {
                await Task.Run(async () =>
                {
                    foreach (var file in files)
                    {
                        if (file is IStorageFile storageFile)
                        {
                            using var stream = await storageFile.OpenReadAsync();
                            string remotePath = currentPath + storageFile.Name;
                            sftp.UploadFile(stream, remotePath);
                        }
                    }
                });

                SetStatus($"Đã upload thành công {files.Count} file (kéo/thả).");
                await LoadDirectoryAsync(currentPath);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi khi upload kéo/thả: {ex.Message}");
            }
            finally
            {
                ReleaseClient();
            }
        }
    }
}
