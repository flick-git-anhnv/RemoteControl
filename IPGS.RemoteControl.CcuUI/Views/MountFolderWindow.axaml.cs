using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;
using KztekComponentAvalonia.Controls;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// Cài đặt &amp; mount thư mục share (CIFS/NFS) lên 1 hoặc nhiều máy Ubuntu cùng lúc.
/// </summary>
public partial class MountFolderWindow : Window
{
    private readonly List<ComputerProfile> _targets;
    private readonly RemoteMountService _service = new();

    public MountFolderWindow() : this(new List<ComputerProfile>()) { }

    public MountFolderWindow(List<ComputerProfile> targets)
    {
        InitializeComponent();
        _targets = targets;

        PART_TargetText.Text = targets.Count == 1
            ? $"Máy đích: {targets[0].DisplayName} ({targets[0].Host})"
            : $"Máy đích: {targets.Count} máy — " + string.Join(", ", targets.Take(6).Select(t => t.DisplayName)) + (targets.Count > 6 ? "…" : "");

        PART_Protocol.SelectionChanged += (_, _) => UpdateProtocolUi();
        PART_BtnMount.Click += (_, _) => _ = RunAsync(unmount: false);
        PART_BtnUnmount.Click += (_, _) => _ = RunAsync(unmount: true);
        PART_BtnSetupServer.Click += OnSetupServerClick;
        UpdateProtocolUi();
    }

    private bool IsNfs => PART_Protocol.SelectedIndex == 1;

    private void UpdateProtocolUi()
    {
        if (PART_Hint == null) return;
        PART_NfsServerBox.IsVisible = IsNfs;
        PART_UserLabel.IsVisible = PART_ShareUser.IsVisible = PART_PassLabel.IsVisible = PART_SharePass.IsVisible = !IsNfs;

        if (IsNfs)
        {
            PART_ShareLabel.Text = "Đường dẫn export:";
            PART_SharePath.Watermark = "/srv/share";
            PART_Hint.Text = "NFS dành cho Ubuntu ↔ Ubuntu: không cần user/mật khẩu, phân quyền theo dải IP máy khách. " +
                             "Nếu máy chủ chưa cài NFS, dùng khung 'Thiết lập máy chủ NFS' bên dưới trước, rồi bấm 'Cài & Mount' cho các máy khách.";
        }
        else
        {
            PART_ShareLabel.Text = "Tên share:";
            PART_SharePath.Watermark = "Images";
            PART_Hint.Text = "CIFS/SMB dùng cho thư mục share trên máy Windows (hoặc Ubuntu đã cài Samba). " +
                             "Tương đương dòng fstab: //IP/Tên_share /mnt/... cifs username=Guest,... — mật khẩu được lưu trong file /etc/ipgs-cifs/*.cred (quyền 600), không đặt trong fstab. " +
                             "Mount tự động khi truy cập (automount), nofail nên máy vẫn boot bình thường nếu máy chủ share tắt.";
        }
    }

    private RemoteMountOptions ReadOptions() => new()
    {
        Protocol = IsNfs ? MountProtocol.Nfs : MountProtocol.Cifs,
        ServerHost = PART_ServerHost.Text?.Trim() ?? "",
        SharePath = PART_SharePath.Text?.Trim() ?? "",
        MountPoint = PART_MountPoint.Text?.Trim() ?? "",
        ShareUsername = PART_ShareUser.Text?.Trim() ?? "",
        SharePassword = PART_SharePass.Text ?? "",
        ReadOnly = PART_ReadOnly.IsChecked == true
    };

    private static SshInstallerOptions Ssh(ComputerProfile p) => new()
    {
        Host = p.Host,
        SshPort = p.SshPort > 0 ? p.SshPort : 22,
        Username = p.SshUsername ?? "",
        Password = p.SshPassword ?? ""
    };

    private async Task RunAsync(bool unmount)
    {
        RemoteMountOptions opts = ReadOptions();
        try
        {
            RemoteMountService.ValidateMountPoint(opts.MountPoint);
        }
        catch (ArgumentException ex)
        {
            Log("❌ " + ex.Message);
            return;
        }
        if (!unmount && (opts.ServerHost.Length == 0 || opts.SharePath.Length == 0))
        {
            Log("❌ Nhập IP máy chủ và " + (IsNfs ? "đường dẫn export." : "tên share."));
            return;
        }

        SetBusy(true);
        PART_Log.Text = "";
        using var gate = new SemaphoreSlim(8);
        var tasks = _targets.Select(async p =>
        {
            await gate.WaitAsync();
            string name = p.DisplayName;
            try
            {
                if (string.IsNullOrWhiteSpace(p.SshUsername))
                    throw new InvalidOperationException("chưa cấu hình SSH user — bấm 'Sửa' máy để bổ sung.");

                Log($"🔄 [{name}] {(unmount ? "Đang gỡ mount" : "Đang cài & mount")}...");
                string result = unmount
                    ? await _service.UnmountAsync(Ssh(p), opts.MountPoint)
                    : await _service.MountAsync(Ssh(p), opts);
                Log($"✅ [{name}] {LastLines(result)}");
            }
            catch (Exception ex)
            {
                Log($"❌ [{name}] {LastLines(ex.Message)}");
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks);
        Log("── Hoàn tất ──");
        SetBusy(false);
    }

    private async void OnSetupServerClick(object? sender, RoutedEventArgs e)
    {
        string host = PART_ServerHost.Text?.Trim() ?? "";
        string exportPath = PART_SharePath.Text?.Trim() ?? "";
        var server = ComputerProfileStore.Instance.GetAll()
            .FirstOrDefault(p => p.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.SshUsername));
        if (server == null)
        {
            Log("❌ Không tìm thấy máy chủ có IP '" + host + "' (kèm SSH user) trong danh sách. Thêm máy chủ vào danh sách và nhập SSH trước.");
            return;
        }

        SetBusy(true);
        PART_Log.Text = "";
        try
        {
            Log($"🖥️ [{server.DisplayName}] Đang thiết lập máy chủ NFS...");
            string result = await _service.SetupNfsServerAsync(Ssh(server), exportPath, PART_AllowedClients.Text ?? "");
            Log($"✅ [{server.DisplayName}] {result}");
        }
        catch (Exception ex)
        {
            Log($"❌ [{server.DisplayName}] {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static string LastLines(string s)
    {
        var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" | ", lines.TakeLast(4));
    }

    private void SetBusy(bool busy)
    {
        PART_BtnMount.IsEnabled = PART_BtnUnmount.IsEnabled = PART_BtnSetupServer.IsEnabled = !busy;
    }

    private void Log(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            PART_Log.Text += $"[{DateTime.Now:HH:mm:ss}] {text}\n";
            PART_Log.CaretIndex = PART_Log.Text.Length;
        });
    }
}
