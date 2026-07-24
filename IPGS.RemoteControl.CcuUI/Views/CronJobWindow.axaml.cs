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
                
                // Add new job
                // Escaping inner quotes might be needed, but for simple commands it's fine
                var cmd = ssh.CreateCommand($"(crontab -l 2>/dev/null; echo \"{newJob.Replace("\"", "\\\"")}\") | crontab -");
                cmd.Execute();
                
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
                
                string newCrontab = string.Join("\n", lines) + "\n";
                
                // create temp script to write back
                var cmdWrite = ssh.CreateCommand($"echo \"{newCrontab.Replace("\"", "\\\"").Replace("\n", "\\n")}\" | crontab -");
                cmdWrite.Execute();
                
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
