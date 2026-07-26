using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;
using KztekComponentAvalonia.Controls;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuUI.Views;

public class CronJobItem
{
    public string OriginalLine { get; set; } = "";
    public string Schedule { get; set; } = "";
    public string Command { get; set; } = "";
}

public partial class CronJobWindow : Window
{
    private readonly ComputerProfile _profile;
    public ObservableCollection<CronJobItem> Jobs { get; set; } = new();

    public CronJobWindow()
    {
        InitializeComponent();
        _profile = new ComputerProfile();
    }

    /// <summary>
    /// Bọc single-quote POSIX ('...' với ' bên trong → '\''): shell remote KHÔNG expand
    /// $VAR/backtick/! bên trong single-quote, và newline literal được giữ nguyên.
    /// KHÔNG dùng double-quote + escape thủ công — đó chính là nguyên nhân bug
    /// "echo \n literal phá toàn bộ crontab" (A1) và "$HOME bị expand sai" (Q13).
    /// </summary>
    private static string ShQuote(string value)
        => "'" + value.Replace("'", "'\\''") + "'";

    public CronJobWindow(ComputerProfile profile)
    {
        InitializeComponent();
        _profile = profile;

        var titleTxt = this.FindControl<TextBlock>("PART_TitleText");
        if (titleTxt != null)
            titleTxt.Text = $"Quản Lý Cron Jobs - {profile.DisplayName}";

        var jobList = this.FindControl<DataGrid>("PART_JobList");
        if (jobList != null)
        {
            jobList.ItemsSource = Jobs;
        }

        if (this.FindControl<KzButton>("PART_BtnRefresh") is { } btnRefresh) btnRefresh.Click += OnRefreshClick;
        if (this.FindControl<KzButton>("PART_BtnDelete") is { } btnDelete) btnDelete.Click += OnDeleteClick;
        if (this.FindControl<KzButton>("PART_BtnAdd") is { } btnAdd) btnAdd.Click += OnAddClick;
        if (this.FindControl<KzButton>("PART_BtnClose") is { } btnClose) btnClose.Click += (s, e) => Close();

        this.Opened += async (s, e) => await LoadJobsAsync();
    }

    private void SetStatus(string msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TextBlock>("PART_StatusMsg") is { } statusMsg)
                statusMsg.Text = msg;
        });
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await LoadJobsAsync();
    }

    private async Task LoadJobsAsync()
    {
        SetStatus("Đang tải danh sách Cron Job...");
        Jobs.Clear();
        
        try
        {
            await Task.Run(() =>
            {
                using var ssh = new SshClient(_profile.Host, _profile.SshPort > 0 ? _profile.SshPort : 22, 
                    !string.IsNullOrWhiteSpace(_profile.SshUsername) ? _profile.SshUsername : "kztek", _profile.SshPassword ?? "");
                ssh.Connect();
                
                var cmd = ssh.CreateCommand("crontab -l");
                string result = cmd.Execute();
                
                Dispatcher.UIThread.Post(() =>
                {
                    if (string.IsNullOrWhiteSpace(result) || result.Contains("no crontab"))
                    {
                        SetStatus("Không có cron job nào.");
                        return;
                    }

                    var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string tLine = line.Trim();
                        // Ignore comments and env vars
                        if (tLine.StartsWith("#") || string.IsNullOrWhiteSpace(tLine) || tLine.Contains("=")) 
                            continue;

                        var parts = tLine.Split(new[] { ' ' }, 6, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 6)
                        {
                            Jobs.Add(new CronJobItem
                            {
                                OriginalLine = line,
                                Schedule = string.Join(" ", parts.Take(5)),
                                Command = parts[5]
                            });
                        }
                    }
                    SetStatus($"Tải thành công {Jobs.Count} jobs.");
                });
                
                ssh.Disconnect();
            });
        }
        catch (Exception ex)
        {
            SetStatus($"Lỗi tải cron jobs: {ex.Message}");
        }
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        string min = this.FindControl<KzTextBox>("PART_TxtMin")?.Text?.Trim() ?? "*";
        string hr = this.FindControl<KzTextBox>("PART_TxtHour")?.Text?.Trim() ?? "*";
        string day = this.FindControl<KzTextBox>("PART_TxtDay")?.Text?.Trim() ?? "*";
        string mon = this.FindControl<KzTextBox>("PART_TxtMonth")?.Text?.Trim() ?? "*";
        string dow = this.FindControl<KzTextBox>("PART_TxtDow")?.Text?.Trim() ?? "*";
        string cmdTxt = this.FindControl<KzTextBox>("PART_TxtCommand")?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(cmdTxt))
        {
            SetStatus("Lệnh không được để trống!");
            return;
        }

        string newJob = $"{min} {hr} {day} {mon} {dow} {cmdTxt}";
        SetStatus("Đang thêm job...");

        try
        {
            await Task.Run(() =>
            {
                using var ssh = new SshClient(_profile.Host, _profile.SshPort > 0 ? _profile.SshPort : 22, 
                    !string.IsNullOrWhiteSpace(_profile.SshUsername) ? _profile.SshUsername : "kztek", _profile.SshPassword ?? "");
                ssh.Connect();
                
                // Append job qua printf + single-quote: giữ nguyên văn $VAR/backtick trong
                // lệnh cron (Q13), không phụ thuộc hành vi echo của từng shell.
                var cmd = ssh.CreateCommand($"(crontab -l 2>/dev/null; printf '%s\\n' {ShQuote(newJob)}) | crontab -");
                string output = cmd.Execute();
                if (cmd.ExitStatus != 0)
                    throw new InvalidOperationException($"crontab trả về lỗi (exit {cmd.ExitStatus}): {cmd.Error} {output}".Trim());
                
                ssh.Disconnect();
            });

            SetStatus("Đã thêm thành công!");
            await LoadJobsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Lỗi thêm cron job: {ex.Message}");
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<DataGrid>("PART_JobList")?.SelectedItem is not CronJobItem selectedJob)
        {
            SetStatus("Vui lòng chọn một job để xóa.");
            return;
        }

        SetStatus("Đang xóa job...");
        
        try
        {
            await Task.Run(() =>
            {
                using var ssh = new SshClient(_profile.Host, _profile.SshPort > 0 ? _profile.SshPort : 22, 
                    !string.IsNullOrWhiteSpace(_profile.SshUsername) ? _profile.SshUsername : "kztek", _profile.SshPassword ?? "");
                ssh.Connect();
                
                var cmdList = ssh.CreateCommand("crontab -l");
                string result = cmdList.Execute();
                
                var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                lines.Remove(selectedJob.OriginalLine);
                
                // Ghi lại crontab qua printf + single-quote (newline literal bên trong
                // single-quote được shell giữ nguyên; $VAR/backtick không bị expand).
                // Bug cũ (A1): echo "...\n..." in nguyên văn chuỗi \n → crontab mới thành
                // 1 dòng duy nhất → cron parse fail → MỌI job còn lại chết.
                string newCrontab = string.Join("\n", lines);
                SshCommand cmdWrite = lines.Count == 0
                    ? ssh.CreateCommand("crontab -r 2>/dev/null || true")
                    : ssh.CreateCommand($"printf '%s\\n' {ShQuote(newCrontab)} | crontab -");
                string writeOutput = cmdWrite.Execute();
                if (cmdWrite.ExitStatus != 0)
                    throw new InvalidOperationException($"crontab trả về lỗi (exit {cmdWrite.ExitStatus}): {cmdWrite.Error} {writeOutput}".Trim());
                
                ssh.Disconnect();
            });

            SetStatus("Đã xóa thành công!");
            await LoadJobsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Lỗi xóa cron job: {ex.Message}");
        }
    }
}
