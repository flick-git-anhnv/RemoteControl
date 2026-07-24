using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public partial class NetworkScanWindow : Window
    {
        private readonly ObservableCollection<NetworkScanFoundItem> _found = new();
        private CancellationTokenSource? _scanCts;

        public NetworkScanWindow()
        {
            InitializeComponent();

            var subnets = ZcuAgentDiscoveryService.GetLocalSubnetBases();
            PART_SubnetBase.Text = subnets.Count > 0 ? subnets[0] : "192.168.1.";

            PART_ListFound.ItemsSource = _found;
            _found.CollectionChanged += (_, _) => UpdateEmptyHint();
            UpdateEmptyHint();

            PART_BtnScan.Click += OnScanClick;
            PART_BtnClose.Click += (_, _) => Close();
        }

        private void UpdateEmptyHint()
        {
            PART_EmptyHint.IsVisible = _found.Count == 0;
        }

        private async void OnScanClick(object? sender, RoutedEventArgs e)
        {
            if (_scanCts != null)
            {
                _scanCts.Cancel();
                return;
            }

            string subnetBase = PART_SubnetBase.Text?.Trim() ?? "";
            if (subnetBase.Length == 0)
            {
                PART_StatusText.Text = "Vui lòng nhập dải mạng (VD: 192.168.1.)";
                return;
            }
            if (!subnetBase.EndsWith(".")) subnetBase += ".";

            if (!int.TryParse(PART_Port.Text?.Trim(), out int port)) port = 17600;

            _found.Clear();
            PART_ProgressBar.Value = 0;
            PART_StatusText.Text = "Đang quét...";
            PART_BtnScan.Content = "⏹ Dừng quét";
            PART_BtnScan.Classes.Set("kz-primary", false);

            _scanCts = new CancellationTokenSource();
            var ct = _scanCts.Token;

            try
            {
                var progress = new Progress<int>(done =>
                {
                    PART_ProgressBar.Value = done;
                    PART_StatusText.Text = $"Đã quét {done}/254 — tìm thấy {_found.Count} máy";
                });

                await ZcuAgentDiscoveryService.ScanAsync(
                    subnetBase,
                    port,
                    found => Dispatcher.UIThread.Post(() =>
                    {
                        if (!_found.Any(f => f.Host.Equals(found.Host, StringComparison.OrdinalIgnoreCase)))
                        {
                            bool alreadyInList = ComputerProfileStore.Instance.GetAll()
                                .Any(p => p.Host.Equals(found.Host, StringComparison.OrdinalIgnoreCase));
                            _found.Add(new NetworkScanFoundItem(found, alreadyInList));
                        }
                    }),
                    progress,
                    ct);

                PART_StatusText.Text = $"✅ Hoàn tất — tìm thấy {_found.Count} máy đã cài ZcuAgent";
            }
            catch (OperationCanceledException)
            {
                PART_StatusText.Text = $"⏹ Đã dừng quét — tìm thấy {_found.Count} máy";
            }
            catch (Exception ex)
            {
                PART_StatusText.Text = $"❌ Lỗi khi quét: {ex.Message}";
            }
            finally
            {
                _scanCts?.Dispose();
                _scanCts = null;
                PART_BtnScan.Content = "🔍 Bắt đầu quét";
                PART_BtnScan.Classes.Set("kz-primary", true);
            }
        }

        private async void OnAddFoundClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control { Tag: NetworkScanFoundItem item }) return;

            var found = item.Source;
            var existing = ComputerProfileStore.Instance.GetAll()
                .FirstOrDefault(p => p.Host.Equals(found.Host, StringComparison.OrdinalIgnoreCase));

            var profileToEdit = existing != null
                ? new ComputerProfile
                {
                    Id = existing.Id,
                    Name = existing.Name,
                    Host = existing.Host,
                    Port = existing.Port,
                    Token = existing.Token,
                    Notes = existing.Notes,
                    SshPort = existing.SshPort,
                    SshUsername = existing.SshUsername,
                    SshPassword = existing.SshPassword,
                    LastConnectedAt = existing.LastConnectedAt,
                    CreatedAt = existing.CreatedAt
                }
                : new ComputerProfile
                {
                    Name = found.ServerName,
                    Host = found.Host,
                    Port = found.Port
                };

            var dlg = new ComputerEditWindow(profileToEdit);
            var saved = await dlg.ShowDialog<ComputerProfile?>(this);
            if (saved != null && dlg.IsSaved)
            {
                ComputerProfileStore.Instance.Save(saved);
                item.IsAdded = true;
            }
        }
    }
}
