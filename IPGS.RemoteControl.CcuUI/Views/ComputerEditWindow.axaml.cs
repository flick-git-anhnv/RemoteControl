using Avalonia.Controls;
using Avalonia.Interactivity;
using IPGS.RemoteControl.CcuClient;
using KztekComponentAvalonia.Controls;

namespace IPGS.RemoteControl.CcuUI.Views;

public partial class ComputerEditWindow : Window
{
    public ComputerProfile Profile { get; private set; }
    public bool IsSaved { get; private set; }

    public ComputerEditWindow() : this(new ComputerProfile())
    {
    }

    public ComputerEditWindow(ComputerProfile profile)
    {
        InitializeComponent();
        Profile = profile;

        if (this.FindControl<KzTextBox>("PART_Name") is { } nameTxt) nameTxt.Text = profile.Name;
        if (this.FindControl<KzTextBox>("PART_Host") is { } hostTxt) hostTxt.Text = profile.Host;
        if (this.FindControl<KzTextBox>("PART_Port") is { } portTxt) portTxt.Text = profile.Port.ToString();
        if (this.FindControl<KzTextBox>("PART_Token") is { } tokenTxt) tokenTxt.Text = profile.Token;
        if (this.FindControl<KzTextBox>("PART_Notes") is { } notesTxt) notesTxt.Text = profile.Notes;
        if (this.FindControl<TextBox>("PART_MacAddress") is { } macTxt) macTxt.Text = profile.MacAddress;

        if (this.FindControl<KzTextBox>("PART_SshUser") is { } sshUserTxt) sshUserTxt.Text = profile.SshUsername;
        if (this.FindControl<KzTextBox>("PART_SshPassword") is { } sshPassTxt) sshPassTxt.Text = profile.SshPassword;
        if (this.FindControl<KzTextBox>("PART_SshPort") is { } sshPortTxt) sshPortTxt.Text = profile.SshPort.ToString();

        if (this.FindControl<KzButton>("PART_BtnCancel") is { } btnCancel)
            btnCancel.Click += (_, _) => Close();

        if (this.FindControl<KzButton>("PART_BtnFetchToken") is { } btnFetch)
            btnFetch.Click += OnFetchTokenClick;

        if (this.FindControl<KzButton>("PART_BtnSave") is { } btnSave)
            btnSave.Click += OnSaveClick;
    }

    private async void OnFetchTokenClick(object? sender, RoutedEventArgs e)
    {
        var msg = this.FindControl<TextBlock>("PART_FetchTokenMsg");
        void Show(string text, bool ok)
        {
            if (msg == null) return;
            msg.Text = text;
            msg.Foreground = ok ? Avalonia.Media.Brushes.Green : Avalonia.Media.Brushes.Red;
            msg.IsVisible = true;
        }

        string host = this.FindControl<KzTextBox>("PART_Host")?.Text?.Trim() ?? "";
        string user = this.FindControl<KzTextBox>("PART_SshUser")?.Text?.Trim() ?? "";
        string pass = this.FindControl<KzTextBox>("PART_SshPassword")?.Text ?? "";
        int sshPort = int.TryParse(this.FindControl<KzTextBox>("PART_SshPort")?.Text?.Trim(), out int sp) ? sp : 22;

        if (host.Length == 0 || user.Length == 0)
        {
            Show("Cần nhập IP và SSH user/password trước.", false);
            return;
        }

        if (sender is KzButton btn) btn.IsEnabled = false;
        Show("Đang đọc cấu hình qua SSH...", true);
        try
        {
            var result = await new ZcuRemoteInstallerService().ReadAgentConfigAsync(new SshInstallerOptions
            {
                Host = host, SshPort = sshPort, Username = user, Password = pass
            });

            if (result is { } cfg)
            {
                if (this.FindControl<KzTextBox>("PART_Token") is { } tokenTxt) tokenTxt.Text = cfg.Token;
                if (this.FindControl<KzTextBox>("PART_Port") is { } portTxt) portTxt.Text = cfg.Port.ToString();
                Show("✅ Đã lấy Token & Cổng. Bấm 'Lưu' để ghi lại.", true);
            }
            else
            {
                Show("Không tìm thấy cấu hình Agent trên máy (chưa cài remote?). Dùng '⚡ Cài remote' để cài/đặt token mới.", false);
            }
        }
        catch (Exception ex)
        {
            Show("❌ Lỗi SSH: " + ex.Message, false);
        }
        finally
        {
            if (sender is KzButton b) b.IsEnabled = true;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        string name  = this.FindControl<KzTextBox>("PART_Name")?.Text?.Trim()  ?? "";
        string host  = this.FindControl<KzTextBox>("PART_Host")?.Text?.Trim()  ?? "";
        string portS = this.FindControl<KzTextBox>("PART_Port")?.Text?.Trim()  ?? "17600";
        string token = this.FindControl<KzTextBox>("PART_Token")?.Text?.Trim() ?? "";
        string notes = this.FindControl<KzTextBox>("PART_Notes")?.Text?.Trim() ?? "";

        string sshUser = this.FindControl<KzTextBox>("PART_SshUser")?.Text?.Trim() ?? "";
        string sshPass = this.FindControl<KzTextBox>("PART_SshPassword")?.Text ?? "";
        string sshPortS = this.FindControl<KzTextBox>("PART_SshPort")?.Text?.Trim() ?? "22";

        // Không return khi host rỗng — cho phép lưu thông tin bộ phận (ví dụ chỉ lưu MAC)

        int port = int.TryParse(portS, out int p) ? p : 17600;
        int sshPort = int.TryParse(sshPortS, out int sp) ? sp : 22;

        Profile.Name = name;
        Profile.Host = host;
        Profile.Port = port;
        Profile.Token = token;
        Profile.Notes = notes;
        Profile.MacAddress = this.FindControl<TextBox>("PART_MacAddress")?.Text?.Trim() ?? "";

        Profile.SshUsername = sshUser;
        Profile.SshPassword = sshPass;
        Profile.SshPort = sshPort;

        IsSaved = true;
        Close(Profile);
    }
}
