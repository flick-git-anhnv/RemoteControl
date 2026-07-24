using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;
using KztekComponentAvalonia.Controls;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuUI.Views;

public class ProcessItem
{
    public string Pid { get; set; } = "";
    public string User { get; set; } = "";
    public string Cpu { get; set; } = "";
    public string Mem { get; set; } = "";
    public string Command { get; set; } = "";
}

public partial class HealthMonitorWindow : Window
{
    private readonly ComputerProfile _profile;
    private DispatcherTimer? _timer;
    private bool _isConnecting;

    public ObservableCollection<ProcessItem> Processes { get; set; } = new();

    public HealthMonitorWindow()
    {
        InitializeComponent();
        _profile = new ComputerProfile();
    }

    public HealthMonitorWindow(ComputerProfile profile)
    {
        InitializeComponent();
        _profile = profile;

        var titleTxt = this.FindControl<TextBlock>("PART_TitleText");
        if (titleTxt != null)
            titleTxt.Text = $"Dashboard Giám Sát - {profile.DisplayName}";

        var processList = this.FindControl<DataGrid>("PART_ProcessList");
        if (processList != null)
        {
            processList.ItemsSource = Processes;
        }

        if (this.FindControl<KzButton>("PART_BtnRefresh") is { } btnRefresh) btnRefresh.Click += OnRefreshClick;
        if (this.FindControl<KzButton>("PART_BtnClose") is { } btnClose) btnClose.Click += (s, e) => Close();

        this.Opened += OnWindowOpened;
        this.Closed += OnWindowClosed;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += async (s, ev) => await RefreshMetricsAsync();
        
        // Start immediately
        _ = RefreshMetricsAsync();
        _timer.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _timer?.Stop();
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await RefreshMetricsAsync();
    }

    private void SetStatus(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TextBlock>("PART_StatusMsg") is { } statusTxt)
            {
                statusTxt.Text = message;
            }
        });
    }

    private async Task RefreshMetricsAsync()
    {
        if (_isConnecting) return;
        _isConnecting = true;
        SetStatus($"Đang tải dữ liệu lúc {DateTime.Now:HH:mm:ss}...");

        try
        {
            string host = _profile.Host;
            int port = _profile.SshPort > 0 ? _profile.SshPort : 22;
            string username = !string.IsNullOrWhiteSpace(_profile.SshUsername) ? _profile.SshUsername : "kztek";
            string password = _profile.SshPassword ?? "";

            await Task.Run(() =>
            {
                using var ssh = new SshClient(host, port, username, password);
                ssh.Connect();

                // 1. RAM Usage
                var ramCmd = ssh.CreateCommand("free -m");
                string ramResult = ramCmd.Execute();
                
                // 2. Disk Usage
                var diskCmd = ssh.CreateCommand("df -h /");
                string diskResult = diskCmd.Execute();
                
                // 3. Top Processes & CPU
                // top -b -n 1 returns batch mode output once.
                var topCmd = ssh.CreateCommand("top -b -n 1 | head -n 15");
                string topResult = topCmd.Execute();

                ssh.Disconnect();

                Dispatcher.UIThread.Post(() =>
                {
                    ParseAndUpdateUI(ramResult, diskResult, topResult);
                    SetStatus($"Cập nhật lần cuối: {DateTime.Now:HH:mm:ss}");
                });
            });
        }
        catch (Exception ex)
        {
            SetStatus($"Lỗi kết nối: {ex.Message}");
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private void ParseAndUpdateUI(string ramStr, string diskStr, string topStr)
    {
        // 1. Parse RAM
        // Mem:           7964        1502        3585         324        2876        5806
        var ramLines = ramStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var memLine = ramLines.FirstOrDefault(l => l.StartsWith("Mem:"));
        if (memLine != null)
        {
            var parts = memLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                if (double.TryParse(parts[1], out double total) && double.TryParse(parts[2], out double used))
                {
                    if (this.FindControl<TextBlock>("PART_RamText") is { } ramTxt)
                        ramTxt.Text = $"{used} / {total} MB";
                    if (this.FindControl<TextBlock>("PART_RamPercentText") is { } ramPctTxt)
                        ramPctTxt.Text = $"{(used / total * 100):0.0}%";
                }
            }
        }

        // 2. Parse Disk
        // Filesystem      Size  Used Avail Use% Mounted on
        // /dev/sda1        50G   20G   28G  42% /
        var diskLines = diskStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (diskLines.Length >= 2)
        {
            var parts = diskLines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5)
            {
                if (this.FindControl<TextBlock>("PART_DiskText") is { } diskTxt)
                    diskTxt.Text = $"{parts[2]} / {parts[1]}";
                if (this.FindControl<TextBlock>("PART_DiskPercentText") is { } diskPctTxt)
                    diskPctTxt.Text = $"{parts[4]} Used";
            }
        }

        // 3. Parse Top (CPU & Processes)
        // %Cpu(s):  5.0 us,  2.0 sy,  0.0 ni, 93.0 id,  0.0 wa,  0.0 hi,  0.0 si,  0.0 st
        // PID USER      PR  NI    VIRT    RES    SHR S  %CPU %MEM     TIME+ COMMAND
        var topLines = topStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var cpuLine = topLines.FirstOrDefault(l => l.StartsWith("%Cpu"));
        if (cpuLine != null)
        {
            // Simple parsing to get idle or user usage. 
            // e.g. %Cpu(s):  5.0 us
            var match = Regex.Match(cpuLine, @"([\d\.]+)\s*us");
            if (match.Success)
            {
                if (this.FindControl<TextBlock>("PART_CpuText") is { } cpuTxt)
                    cpuTxt.Text = $"{match.Groups[1].Value}%";
            }
        }

        int pidIndex = Array.FindIndex(topLines, l => l.Contains("PID") && l.Contains("USER"));
        if (pidIndex >= 0)
        {
            Processes.Clear();
            for (int i = pidIndex + 1; i < topLines.Length && i < pidIndex + 6; i++)
            {
                var pParts = topLines[i].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (pParts.Length >= 12)
                {
                    Processes.Add(new ProcessItem
                    {
                        Pid = pParts[0],
                        User = pParts[1],
                        Cpu = pParts[8],
                        Mem = pParts[9],
                        Command = pParts[11]
                    });
                }
                else if (pParts.Length >= 11) // sometimes COMMAND is empty or shifted
                {
                    Processes.Add(new ProcessItem
                    {
                        Pid = pParts[0],
                        User = pParts[1],
                        Cpu = pParts[8],
                        Mem = pParts[9],
                        Command = pParts[10]
                    });
                }
            }
        }
    }
}
