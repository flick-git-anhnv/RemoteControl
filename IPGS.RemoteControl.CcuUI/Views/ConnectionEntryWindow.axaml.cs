using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;
using KztekComponentAvalonia.Controls;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// Cửa sổ chính phong cách VNC Viewer quản lý danh sách máy tính ZCU và lịch sử kết nối.
/// </summary>
public partial class ConnectionEntryWindow : Window
{
    private readonly IComputerProfileStore _store;
    private bool _showRecentOnly;

    // L7: chống "bão probe" theo keystroke — debounce search + hủy batch probe cũ khi có batch mới
    private CancellationTokenSource? _probeCts;
    private readonly DispatcherTimer _searchDebounceTimer;

    public ConnectionEntryWindow()
    {
        InitializeComponent();
        _store = ComputerProfileStore.Instance;

        // L7: gõ liên tục chỉ refresh 1 lần sau khi ngừng gõ ~300ms
        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            RefreshList();
        };

        Closed += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            try { _probeCts?.Cancel(); } catch { }
        };

        // Toolbar buttons
        if (this.FindControl<KzButton>("PART_BtnScanNetwork") is { } btnScan)
            btnScan.Click += OnScanNetworkClick;

        if (this.FindControl<KzButton>("PART_BtnMultiRemote") is { } btnMulti)
            btnMulti.Click += OnMultiRemoteClick;

        if (this.FindControl<KzButton>("PART_BtnAddComputer") is { } btnAdd)
            btnAdd.Click += OnAddComputerClick;

        // Tab buttons
        if (this.FindControl<KzButton>("PART_BtnTabAll") is { } btnAll)
            btnAll.Click += (_, _) => SwitchTab(recentOnly: false);

        if (this.FindControl<KzButton>("PART_BtnTabRecent") is { } btnRecent)
            btnRecent.Click += (_, _) => SwitchTab(recentOnly: true);

        // Search box — L7: debounce thay vì refresh + probe mỗi keystroke
        if (this.FindControl<KzTextBox>("PART_SearchBox") is { } searchBox)
        {
            searchBox.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == nameof(TextBox.Text))
                {
                    _searchDebounceTimer.Stop();
                    _searchDebounceTimer.Start();
                }
            };
        }

        // Quick connect button
        if (this.FindControl<KzButton>("PART_QuickConnectBtn") is { } btnQuick)
            btnQuick.Click += OnQuickConnectClick;

        // Double click on item in ListBox
        if (this.FindControl<ListBox>("PART_ComputerListBox") is { } listBox)
        {
            listBox.DoubleTapped += OnListBoxDoubleTapped;
        }

        RefreshList();
    }

    private void SwitchTab(bool recentOnly)
    {
        _showRecentOnly = recentOnly;

        if (this.FindControl<KzButton>("PART_BtnTabAll") is { } btnAll)
            btnAll.Classes.Set("kz-primary", !recentOnly);

        if (this.FindControl<KzButton>("PART_BtnTabRecent") is { } btnRecent)
            btnRecent.Classes.Set("kz-primary", recentOnly);

        RefreshList();
    }

    private void RefreshList()
    {
        var allProfiles = _store.GetAll();
        string filter = this.FindControl<KzTextBox>("PART_SearchBox")?.Text?.Trim() ?? "";

        IEnumerable<ComputerProfile> query = allProfiles;

        if (_showRecentOnly)
        {
            query = query.Where(p => p.LastConnectedAt.HasValue);
        }

        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(p =>
                (p.Name != null && p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (p.Host != null && p.Host.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (p.Notes != null && p.Notes.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }

        var resultList = query.ToList();

        if (this.FindControl<ListBox>("PART_ComputerListBox") is { } listBox)
        {
            listBox.ItemsSource = resultList;
        }

        if (this.FindControl<StackPanel>("PART_EmptyState") is { } emptyState)
        {
            emptyState.IsVisible = resultList.Count == 0;
        }

        // L7: hủy batch probe cũ trước khi chạy batch mới — batch cũ hoàn thành muộn
        // không được ghi đè trạng thái bằng kết quả lỗi thời.
        // Không Dispose CTS cũ ngay: task in-flight còn giữ token (tránh ObjectDisposedException).
        _probeCts?.Cancel();
        _probeCts = new CancellationTokenSource();
        _ = CheckAllStatusesAsync(resultList, _probeCts.Token);
    }

    private async Task CheckAllStatusesAsync(List<ComputerProfile> profiles, CancellationToken ct)
    {
        // L7: fire-and-forget PHẢI có try/catch — tránh unobserved task exception
        try
        {
            foreach (var profile in profiles)
            {
                profile.MarkChecking();
            }

            var tasks = profiles.Select(async profile =>
            {
                // Truyền uiDispatch để mutation CpuUsage/RamUsage/DiskUsage xảy ra trên UI thread
                // (param mới của ProbeAsync — xem bước 1.1)
                var result = await ComputerStatusChecker.ProbeAsync(
                    profile, ct, action => Dispatcher.UIThread.Post(action));

                if (ct.IsCancellationRequested) return; // batch đã bị thay thế — bỏ kết quả cũ

                Dispatcher.UIThread.Post(() =>
                {
                    if (!ct.IsCancellationRequested)
                        profile.ApplyStatusResult(result);
                });
            });

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // batch bị hủy do có batch mới / cửa sổ đóng — bình thường
        }
        catch (Exception)
        {
            // lỗi probe nền không được làm crash app; trạng thái giữ nguyên "checking/unknown"
        }
    }

    private async void OnAddComputerClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new ComputerEditWindow(new ComputerProfile());
        var updated = await dlg.ShowDialog<ComputerProfile?>(this);
        if (updated != null && dlg.IsSaved)
        {
            _store.Save(updated);
            RefreshList();
        }
    }

    public async void OnItemEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ComputerProfile profile })
        {
            var dlg = new ComputerEditWindow(new ComputerProfile
            {
                Id = profile.Id,
                Name = profile.Name,
                Host = profile.Host,
                Port = profile.Port,
                Token = profile.Token,
                Notes = profile.Notes,
                SshPort = profile.SshPort,
                SshUsername = profile.SshUsername,
                SshPassword = profile.SshPassword,
                MacAddress = profile.MacAddress,
                LastConnectedAt = profile.LastConnectedAt,
                CreatedAt = profile.CreatedAt
            });

            var updated = await dlg.ShowDialog<ComputerProfile?>(this);
            if (updated != null && dlg.IsSaved)
            {
                _store.Save(updated);
                RefreshList();
            }
        }
    }

    public async void OnItemWakeUpClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ComputerProfile profile })
        {
            if (string.IsNullOrWhiteSpace(profile.MacAddress))
            {
                var w1 = new Window { Title = "Lỗi Wake-on-LAN", Width = 400, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                w1.Content = new TextBlock { Text = "Máy tính này chưa được cấu hình địa chỉ MAC.\nVui lòng bấm 'Sửa' và điền địa chỉ MAC trước khi đánh thức.", Margin = new Avalonia.Thickness(20), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
                await w1.ShowDialog(this);
                return;
            }

            try
            {
                await IPGS.RemoteControl.CcuClient.Services.WakeOnLanService.SendMagicPacketAsync(profile.MacAddress);
                
                var w2 = new Window { Title = "Thành công", Width = 400, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                w2.Content = new TextBlock { Text = $"Đã gửi tín hiệu bật nguồn (Magic Packet) đến địa chỉ MAC {profile.MacAddress}.\nVui lòng chờ vài phút để máy tính khởi động.", Margin = new Avalonia.Thickness(20), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
                await w2.ShowDialog(this);
            }
            catch (Exception ex)
            {
                var w3 = new Window { Title = "Lỗi gửi tín hiệu", Width = 400, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                w3.Content = new TextBlock { Text = $"Không thể gửi gói tin Wake-on-LAN:\n{ex.Message}", Margin = new Avalonia.Thickness(20), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
                await w3.ShowDialog(this);
            }
        }
    }

    public async void OnItemSetupWizardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: ComputerProfile profile }) return;

        var dlg = new ZcuSetupWizardWindow(profile);
        var created = await dlg.ShowDialog<ComputerProfile?>(this);
        if (created != null)
        {
            RefreshList();
        }
    }

    public void OnItemKioskDeployClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: ComputerProfile profile }) return;

        var dlg = new KioskDeployWindow(profile);
        dlg.Show();
    }

    public void OnItemAppInstallClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: ComputerProfile profile }) return;

        var dlg = new RemoteAppInstallWindow(profile);
        dlg.Show();
    }

    public void OnItemFileManagerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: ComputerProfile profile }) return;
        var dlg = new FileManagerWindow(profile);
        dlg.Show();
    }

    private void OnItemHealthMonitorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ComputerProfile profile })
        {
            var win = new HealthMonitorWindow(profile);
            win.ShowDialog(this);
        }
    }

    private void OnItemCronJobClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ComputerProfile profile })
        {
            var win = new CronJobWindow(profile);
            win.ShowDialog(this);
        }
    }

    public void OnItemRunCommandClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: ComputerProfile profile }) return;

        var dlg = new RemoteCommandWindow(profile);
        dlg.Show();
    }

    public void OnItemDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ComputerProfile profile })
        {
            _store.Delete(profile.Id);
            RefreshList();
        }
    }

    public void OnItemConnectClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ComputerProfile profile })
        {
            StartRemoteControl(profile.Host, profile.Port, profile.Token, profile.Name, profile.SshReachable);
        }
    }

    private void OnListBoxDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (this.FindControl<ListBox>("PART_ComputerListBox") is { SelectedItem: ComputerProfile profile })
        {
            StartRemoteControl(profile.Host, profile.Port, profile.Token, profile.Name, profile.SshReachable);
        }
    }

    private void OnQuickConnectClick(object? sender, RoutedEventArgs e)
    {
        string host  = this.FindControl<KzTextBox>("PART_QuickHost")?.Text?.Trim()  ?? "";
        string portS = this.FindControl<KzTextBox>("PART_QuickPort")?.Text?.Trim()  ?? "17600";
        string token = this.FindControl<KzTextBox>("PART_QuickToken")?.Text?.Trim() ?? "";
        bool save    = this.FindControl<CheckBox>("PART_QuickSaveCheck")?.IsChecked ?? true;

        if (string.IsNullOrEmpty(host)) return;

        int port = int.TryParse(portS, out int p) ? p : 17600;

        if (save)
        {
            _store.RecordConnection(host, port, token);
        }

        StartRemoteControl(host, port, token, null, null);
    }

    private void OnScanNetworkClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new NetworkScanWindow();
        dlg.Closed += (_, _) => RefreshList();
        dlg.Show();
    }

    private void OnSelectionChanged(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("PART_ComputerListBox");
        if (listBox?.ItemsSource is IEnumerable<ComputerProfile> items)
        {
            int count = items.Count(p => p.IsSelected);
            var bulkBar = this.FindControl<Border>("PART_BulkActionBar");
            var bulkText = this.FindControl<TextBlock>("PART_BulkActionText");
            
            if (bulkBar != null) bulkBar.IsVisible = count > 0;
            if (bulkText != null) bulkText.Text = $"Đã chọn {count} máy tính";
        }
    }

    private void OnBulkActionClick(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("PART_ComputerListBox");
        if (listBox?.ItemsSource is IEnumerable<ComputerProfile> items)
        {
            var selectedProfiles = items.Where(p => p.IsSelected).ToList();
            if (selectedProfiles.Count > 0)
            {
                var bulkWin = new BulkActionWindow(selectedProfiles);
                bulkWin.Show();
            }
        }
    }

    private void OnMultiRemoteClick(object? sender, RoutedEventArgs e)
    {
        var allProfiles = _store.GetAll().ToList();
        var multiWin = new MultiRemoteWindow();
        if (allProfiles.Count > 0)
        {
            multiWin.AddSessions(allProfiles);
        }
        multiWin.Show();
    }

    private void StartRemoteControl(string host, int port, string token, string? name, bool? isSshReachable)
    {
        // Ghi nhận lịch sử kết nối
        _store.RecordConnection(host, port, token, name);

        // Hiển thị HD cài SSH nếu đã thăm dò và chắc chắn không kết nối được
        bool showSshHelp = isSshReachable == false;
        var screenWin = new RemoteScreenWindow(host, port, token, showSshHelp);

        screenWin.Closed += (_, _) =>
        {
            RefreshList();
        };
        screenWin.Show();
    }
}
