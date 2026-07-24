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
        if (this.FindControl<KzTextBox>("PART_MacAddress") is { } macTxt) macTxt.Text = profile.MacAddress;

        if (this.FindControl<KzTextBox>("PART_SshUser") is { } sshUserTxt) sshUserTxt.Text = profile.SshUsername;
        if (this.FindControl<TextBox>("PART_SshPassword") is { } sshPassTxt) sshPassTxt.Text = profile.SshPassword;
        if (this.FindControl<KzTextBox>("PART_SshPort") is { } sshPortTxt) sshPortTxt.Text = profile.SshPort.ToString();

        if (this.FindControl<KzButton>("PART_BtnCancel") is { } btnCancel)
            btnCancel.Click += (_, _) => Close();

        if (this.FindControl<KzButton>("PART_BtnSave") is { } btnSave)
            btnSave.Click += OnSaveClick;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        string name  = this.FindControl<KzTextBox>("PART_Name")?.Text?.Trim()  ?? "";
        string host  = this.FindControl<KzTextBox>("PART_Host")?.Text?.Trim()  ?? "";
        string portS = this.FindControl<KzTextBox>("PART_Port")?.Text?.Trim()  ?? "17600";
        string token = this.FindControl<KzTextBox>("PART_Token")?.Text?.Trim() ?? "";
        string notes = this.FindControl<KzTextBox>("PART_Notes")?.Text?.Trim() ?? "";

        string sshUser = this.FindControl<KzTextBox>("PART_SshUser")?.Text?.Trim() ?? "";
        string sshPass = this.FindControl<TextBox>("PART_SshPassword")?.Text ?? "";
        string sshPortS = this.FindControl<KzTextBox>("PART_SshPort")?.Text?.Trim() ?? "22";

        if (string.IsNullOrWhiteSpace(host))
        {
            // Host IP là bắt buộc
            return;
        }

        int port = int.TryParse(portS, out int p) ? p : 17600;
        int sshPort = int.TryParse(sshPortS, out int sp) ? sp : 22;

        Profile.Name = name;
        Profile.Host = host;
        Profile.Port = port;
        Profile.Token = token;
        Profile.Notes = notes;
        Profile.MacAddress = this.FindControl<KzTextBox>("PART_MacAddress")?.Text?.Trim() ?? "";

        Profile.SshUsername = sshUser;
        Profile.SshPassword = sshPass;
        Profile.SshPort = sshPort;

        IsSaved = true;
        Close(Profile);
    }
}
