using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        if (this.FindControl<KzButton>("PART_BtnSetupWizard") is { } btnWizard)
            btnWizard.Click += OnSetupWizardClick;

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
    }

    private async void OnSetupWizardClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new ZcuSetupWizardWindow();
        var created = await dlg.ShowDialog<ComputerProfile?>(this);
        if (created != null)
        {
            RefreshList();
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
            StartRemoteControl(profile.Host, profile.Port, profile.Token, profile.Name);
        }
    }

    private void OnListBoxDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (this.FindControl<ListBox>("PART_ComputerListBox") is { SelectedItem: ComputerProfile profile })
        {
            StartRemoteControl(profile.Host, profile.Port, profile.Token, profile.Name);
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

        StartRemoteControl(host, port, token, null);
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

    private void StartRemoteControl(string host, int port, string token, string? name)
    {
        // Ghi nhận lịch sử kết nối
        _store.RecordConnection(host, port, token, name);

        var screenWin = new RemoteScreenWindow(host, port, token);

        screenWin.Closed += (_, _) =>
        {
            RefreshList();
        };
        screenWin.Show();
    }
}
