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

    public ConnectionEntryWindow()
    {
        InitializeComponent();
        _store = ComputerProfileStore.Instance;

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

        // Search box
        if (this.FindControl<KzTextBox>("PART_SearchBox") is { } searchBox)
        {
            searchBox.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == nameof(TextBox.Text))
                {
                    RefreshList();
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

        _ = CheckAllStatusesAsync(resultList);
    }

    private async Task CheckAllStatusesAsync(List<ComputerProfile> profiles)
    {
        foreach (var profile in profiles)
        {
            profile.MarkChecking();
        }

        var tasks = profiles.Select(async profile =>
        {
            var result = await ComputerStatusChecker.ProbeAsync(profile);
            Dispatcher.UIThread.Post(() => profile.ApplyStatusResult(result));
        });

        await Task.WhenAll(tasks);
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
