using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;

namespace IPGS.RemoteControl.CcuUI.Views
{
    /// <summary>
    /// Update ứng dụng hàng loạt: gửi thư mục DLL → backup → ghi đè → restart service (tự rollback nếu lỗi),
    /// kèm khôi phục bản backup cũ và restart service riêng.
    /// </summary>
    public partial class AppUpdateWindow : Window
    {
        private const string LatestBackupLabel = "Bản backup mới nhất";
        private static readonly string[] ActionButtons =
            { "PART_BtnUpdate", "PART_BtnRestart", "PART_BtnRestore", "PART_BtnScan", "PART_BtnLoadBackups", "PART_BtnBrowse" };

        private readonly List<ComputerProfile> _targets;
        private readonly ObservableCollection<BulkTaskResult> _results;
        private readonly RemoteAppUpdateService _service = new();

        public AppUpdateWindow() : this(new List<ComputerProfile>()) { }

        public AppUpdateWindow(List<ComputerProfile> targets)
        {
            InitializeComponent();
            _targets = targets;
            _results = new ObservableCollection<BulkTaskResult>(targets.Select(p => new BulkTaskResult { Profile = p }));
            this.FindControl<ListBox>("PART_ResultListBox")!.ItemsSource = _results;
            ResetBackupCombo(Array.Empty<string>());
        }

        // ── Helpers ─────────────────────────────────────────────────────────
        private string AppDirText => (this.FindControl<AutoCompleteBox>("PART_AppDir")?.Text ?? "").Trim();
        private string ServiceText => (this.FindControl<AutoCompleteBox>("PART_Service")?.Text ?? "").Trim();
        private int KeepBackups => (int)(this.FindControl<NumericUpDown>("PART_Keep")?.Value ?? 3);

        private RemoteAppUpdateOptions BuildOptions(ComputerProfile p)
        {
            if (string.IsNullOrWhiteSpace(p.SshUsername))
                throw new InvalidOperationException("Máy chưa cấu hình SSH user — bấm '✏️ Sửa' máy tính này để bổ sung rồi chạy lại.");
            return new RemoteAppUpdateOptions
            {
                Host = p.Host,
                SshPort = p.SshPort > 0 ? p.SshPort : 22,
                Username = p.SshUsername ?? "",
                Password = p.SshPassword ?? "",
                AppDir = AppDirText,
                ServiceName = ServiceText,
                KeepBackups = KeepBackups
            };
        }

        private void ResetBackupCombo(IEnumerable<string> names)
        {
            var combo = this.FindControl<ComboBox>("PART_BackupCombo");
            if (combo == null) return;
            var items = new List<string> { LatestBackupLabel };
            items.AddRange(names);
            combo.ItemsSource = items;
            combo.SelectedIndex = 0;
        }

        private void OnComboTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (sender is AutoCompleteBox combo)
            {
                combo.IsDropDownOpen = false;
                combo.PopulateComplete();
                combo.IsDropDownOpen = true;
            }
        }

        private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Chọn thư mục chứa file update",
                AllowMultiple = false
            });
            if (folders.Count == 0) return;
            this.FindControl<TextBox>("PART_FolderPath")!.Text = folders[0].Path.LocalPath;
        }

        // ── Quét app/service & backup từ máy đầu tiên ───────────────────────
        private async void OnScanClick(object? sender, RoutedEventArgs e)
        {
            if (_targets.Count == 0) return;
            SetBusy(true);
            try
            {
                var o = BuildOptions(_targets[0]);
                o.AppDir = "/opt/none/none";
                var res = await _service.ScanAsync(o);
                this.FindControl<AutoCompleteBox>("PART_AppDir")!.ItemsSource = res.AppDirs;
                this.FindControl<AutoCompleteBox>("PART_Service")!.ItemsSource = res.Services;
                _results[0].SetSuccess($"Quét xong: {res.AppDirs.Count} thư mục app, {res.Services.Count} service. Bấm vào ô để chọn.");
            }
            catch (Exception ex) { _results[0].SetError(ex.Message); }
            finally { SetBusy(false); }
        }

        private async void OnLoadBackupsClick(object? sender, RoutedEventArgs e)
        {
            if (_targets.Count == 0) return;
            SetBusy(true);
            try
            {
                var names = await _service.ListBackupsAsync(BuildOptions(_targets[0]));
                ResetBackupCombo(names);
                _results[0].SetSuccess($"Tìm thấy {names.Count} bản backup trên {_targets[0].Name}.");
            }
            catch (Exception ex) { _results[0].SetError(ex.Message); }
            finally { SetBusy(false); }
        }

        // ── Actions hàng loạt ───────────────────────────────────────────────
        private async void OnUpdateClick(object? sender, RoutedEventArgs e)
        {
            string folder = (this.FindControl<TextBox>("PART_FolderPath")?.Text ?? "").Trim();
            if (AppDirText.Length == 0)
            {
                _results.FirstOrDefault()?.SetError("Chưa nhập/chọn thư mục ứng dụng cần update.");
                return;
            }

            string? tarball = null;
            try
            {
                SetBusy(true);
                tarball = await Task.Run(() => RemoteAppUpdateService.PackFolder(folder));
            }
            catch (Exception ex)
            {
                _results.FirstOrDefault()?.SetError(ex.Message);
                SetBusy(false);
                return;
            }

            try
            {
                await RunBulkAsync(p =>
                {
                    var o = BuildOptions(p);
                    o.TarballPath = tarball;
                    return log => _service.UpdateAsync(o, log);
                });
            }
            finally { try { File.Delete(tarball); } catch { /* file tạm */ } }
        }

        private async void OnRestoreClick(object? sender, RoutedEventArgs e)
        {
            if (AppDirText.Length == 0)
            {
                _results.FirstOrDefault()?.SetError("Chưa nhập/chọn thư mục ứng dụng cần khôi phục.");
                return;
            }
            string sel = this.FindControl<ComboBox>("PART_BackupCombo")?.SelectedItem as string ?? LatestBackupLabel;
            string backup = sel == LatestBackupLabel ? "" : sel;

            await RunBulkAsync(p =>
            {
                var o = BuildOptions(p);
                o.BackupName = backup;
                return log => _service.RestoreAsync(o, log);
            });
        }

        private async void OnRestartClick(object? sender, RoutedEventArgs e)
        {
            if (ServiceText.Length == 0)
            {
                _results.FirstOrDefault()?.SetError("Chưa chọn service để restart.");
                return;
            }
            // Restart chỉ cần service; AppDir được dùng để validate nên đặt giá trị hợp lệ nếu để trống.
            await RunBulkAsync(p =>
            {
                var o = BuildOptions(p);
                if (o.AppDir.Length == 0) o.AppDir = "/opt/none/none";
                return log => _service.RestartServiceAsync(o, log);
            });
        }

        private void SetBusy(bool busy)
        {
            foreach (var n in ActionButtons)
                if (this.FindControl<Button>(n) is { } b) b.IsEnabled = !busy;
        }

        private async Task RunBulkAsync(Func<ComputerProfile, Func<Action<string>, Task<string>>> prepare)
        {
            var panel = this.FindControl<StackPanel>("PART_ProgressPanel");
            var pb = this.FindControl<ProgressBar>("PART_ProgressBar");
            var text = this.FindControl<TextBlock>("PART_ProgressText");

            SetBusy(true);
            foreach (var r in _results) { r.StatusText = "Đang chờ..."; r.StatusColor = "#94A3B8"; r.StatusIcon = "⏳"; r.Output = ""; }
            if (panel != null) panel.IsVisible = true;
            if (pb != null) { pb.Value = 0; pb.Maximum = Math.Max(1, _results.Count); }
            if (text != null) text.Text = $"Đang xử lý: 0/{_results.Count}";

            using var gate = new SemaphoreSlim(8);
            int completed = 0;

            var tasks = _results.Select(async result =>
            {
                await gate.WaitAsync();
                Dispatcher.UIThread.Post(() => result.SetRunning());
                var log = new StringBuilder();
                try
                {
                    var run = prepare(result.Profile);
                    string output = await Task.Run(() => run(msg =>
                    {
                        lock (log) log.AppendLine(msg);
                        string snapshot; lock (log) snapshot = log.ToString();
                        Dispatcher.UIThread.Post(() => result.Output = snapshot);
                    }));
                    Dispatcher.UIThread.Post(() => result.SetSuccess(output));
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => result.SetError(ex.Message));
                }
                finally
                {
                    gate.Release();
                    int done = Interlocked.Increment(ref completed);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (pb != null) pb.Value = done;
                        if (text != null) text.Text = $"Đang xử lý: {done}/{_results.Count}";
                    });
                }
            });

            await Task.WhenAll(tasks);
            SetBusy(false);
        }
    }
}
