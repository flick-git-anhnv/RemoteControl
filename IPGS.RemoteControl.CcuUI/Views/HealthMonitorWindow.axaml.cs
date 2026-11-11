using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
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

public class AcceleratorItem
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Util { get; set; } = "";
    public string Freq { get; set; } = "";
    public string Temp { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed record HealthParseResult(
    string? Uptime,
    double? CpuPercent,
    double? RamUsedMb,
    double? RamTotalMb,
    string? DiskUsed,
    string? DiskTotal,
    double? DiskPercent,
    IReadOnlyList<ProcessItem> Processes);

public partial class HealthMonitorWindow : Window
{
    // Script dò GPU/NPU/VPU chạy trên máy khách (POSIX sh). Mỗi dòng: KIND|tên|tải%|MHz|°C|ghi chú
    // Gửi qua base64 để tránh lỗi quote khi chạy bằng SSH exec.
    private const string AcceleratorProbeScript = """
emit() { echo "$1|$2|${3:--}|${4:--}|${5:--}|${6:--}"; }
devname() { n=$(lspci -s "$1" 2>/dev/null | cut -d: -f3- | sed 's/^ *//'); [ -n "$n" ] && echo "$n" || echo "$2"; }
dfinfo() {
  for p in /sys/class/devfreq/*; do
    [ -d "$p" ] || continue
    basename "$p" | grep -Eqi "$1" || continue
    l=$(cat "$p/load" 2>/dev/null); u=${l%%@*}
    g=$(cat "$p/governor" 2>/dev/null)
    # governor cố định xung nhịp thì "load" không phản ánh tải thật (hay kẹt 100%) -> bỏ
    case "$g" in performance|userspace|powersave) u= ;; esac
    fr=$(cat "$p/cur_freq" 2>/dev/null); [ -n "$fr" ] && fr=$((fr/1000000))
    echo "$(basename "$p")|$u|$fr|$g"
  done
}
# GPU NVIDIA
if command -v nvidia-smi >/dev/null 2>&1; then
  nvidia-smi --query-gpu=name,utilization.gpu,clocks.gr,temperature.gpu,memory.used,memory.total --format=csv,noheader,nounits 2>/dev/null | while IFS=, read -r n u f t mu mt; do
    emit GPU "$n" "$u" "$f" "$t" "VRAM $mu/$mt MB"
  done
fi
# GPU AMD / Intel / ARM (DRM)
for c in /sys/class/drm/card[0-9]; do
  d=$c/device; [ -d "$d" ] || continue
  drv=$(basename "$(readlink -f "$d/driver" 2>/dev/null)")
  [ "$drv" = nvidia ] && continue
  # cổng DRM của NPU/VPU (vd rknpu) không phải GPU -> để mục NPU/VPU xử lý
  echo "$drv" | grep -Eqi 'npu|vpu' && continue
  n=$(devname "$(basename "$(readlink -f "$d")")" "$drv")
  u=$(cat "$d/gpu_busy_percent" 2>/dev/null)
  f=$(cat "$c/gt_cur_freq_mhz" 2>/dev/null)
  [ -n "$f" ] || f=$(sed -n 's/^[0-9]*: *\([0-9]*\)Mhz \*.*/\1/p' "$d/pp_dpm_sclk" 2>/dev/null)
  t=$(cat "$d"/hwmon/hwmon*/temp1_input 2>/dev/null | head -n1); [ -n "$t" ] && t=$((t/1000))
  vu=$(cat "$d/mem_info_vram_used" 2>/dev/null); vt=$(cat "$d/mem_info_vram_total" 2>/dev/null)
  note=; [ -n "$vt" ] && [ -n "$vu" ] && note="VRAM $((vu/1048576))/$((vt/1048576)) MB"
  if [ -z "$u" ]; then
    x=$(dfinfo 'gpu|mali' | head -n1)
    if [ -n "$x" ]; then u=$(echo "$x" | cut -d'|' -f2); [ -n "$f" ] || f=$(echo "$x" | cut -d'|' -f3); fi
  fi
  emit GPU "$n" "$u" "$f" "$t" "$note"
done
# NPU
for a in /sys/class/accel/accel[0-9]*; do
  [ -d "$a/device" ] || continue
  drv=$(basename "$(readlink -f "$a/device/driver" 2>/dev/null)")
  n=$(devname "$(basename "$(readlink -f "$a/device")")" "$drv")
  b1=$(cat "$a/device/npu_busy_time_us" 2>/dev/null)
  f=$(cat "$a/device/npu_current_frequency_mhz" 2>/dev/null)
  u=
  if [ -n "$b1" ]; then
    sleep 1; b2=$(cat "$a/device/npu_busy_time_us" 2>/dev/null)
    u=$(( (b2-b1)/10000 )); [ "$u" -gt 100 ] && u=100
  fi
  emit NPU "$n" "$u" "$f" "" "driver $drv"
done
rkl=$(cat /sys/kernel/debug/rknpu/load 2>/dev/null || sudo -n cat /sys/kernel/debug/rknpu/load 2>/dev/null)
npu=$(dfinfo 'npu')
if [ -n "$rkl" ]; then
  # tải thật của NPU Rockchip: lấy core bận nhất
  u=$(echo "$rkl" | grep -o '[0-9]*%' | tr -d % | sort -n | tail -n1)
  f=$(echo "$npu" | head -n1 | cut -d'|' -f3)
  emit NPU "Rockchip NPU" "$u" "$f" "" "rknpu (tải thật)"
elif [ -n "$npu" ]; then
  echo "$npu" | while IFS='|' read -r nm u f g; do
    note="devfreq"; [ -n "$g" ] && note="devfreq, governor $g"; [ -z "$u" ] && note="$note (không đo được tải)"
    emit NPU "$nm" "$u" "$f" "" "$note"
  done
fi
for dv in /dev/hailo* /dev/apex_*; do
  [ -e "$dv" ] && emit NPU "$(basename "$dv")" "" "" "" "phát hiện thiết bị, chưa đo tải"
done
# VPU
vpu=$(dfinfo 'vpu|vdec|venc|rkvdec|hantro|codec|mfc')
if [ -n "$vpu" ]; then
  echo "$vpu" | while IFS='|' read -r nm u f g; do
    note="devfreq"; [ -n "$g" ] && note="devfreq, governor $g"; [ -z "$u" ] && note="$note (không đo được tải)"
    emit VPU "$nm" "$u" "$f" "" "$note"
  done
else
  for dv in /dev/mpp_service /dev/vpu_service; do
    [ -e "$dv" ] && emit VPU "$(basename "$dv")" "" "" "" "phát hiện thiết bị, chưa đo tải"
  done
  for v in /sys/class/video4linux/*/name; do cat "$v" 2>/dev/null; done | grep -Ei 'dec|enc|vpu|hantro|rkvdec|cedrus|codec|mfc|venus|coda' | sort -u | while IFS= read -r nm; do
    emit VPU "$nm" "" "" "" "phát hiện thiết bị, chưa đo tải"
  done
fi
exit 0
""";

    private readonly ComputerProfile _profile;
    private DispatcherTimer? _timer;
    private bool _isConnecting;

    // Q18: giữ 1 kết nối SSH xuyên suốt thay vì mở kết nối mới mỗi tick 5s (chậm + nặng cho ZCU)
    private SshClient? _sshClient;
    private RemoteOsType? _detectedOs;
    private bool _isClosed;

    /// <summary>
    /// Câu lệnh PowerShell thu thập đầy đủ sức khỏe máy đích Windows theo 5 section chuẩn:
    /// <c>===UPTIME===</c>, <c>===CPU===</c>, <c>===MEM===</c>, <c>===DISK===</c>, <c>===PROCS===</c>.
    /// </summary>
    public const string WindowsHealthProbeCommand =
        "powershell.exe -NoProfile -NonInteractive -Command \"" +
        "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
        "Write-Output '===UPTIME==='; " +
        "$os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue; " +
        "if ($null -ne $os -and $null -ne $os.LastBootUpTime) { " +
        "$up = (Get-Date) - $os.LastBootUpTime; " +
        "Write-Output ([string]::Format('{0}d {1:00}:{2:00}:{3:00}', [int]$up.TotalDays, $up.Hours, $up.Minutes, $up.Seconds)) " +
        "}; " +
        "Write-Output '===CPU==='; " +
        "$cpu = (Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue | Measure-Object -Property LoadPercentage -Average).Average; " +
        "if ($null -eq $cpu) { $cpu = 0 }; " +
        "Write-Output ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.0}', $cpu)); " +
        "Write-Output '===MEM==='; " +
        "if ($null -ne $os -and $os.TotalVisibleMemorySize -gt 0) { " +
        "$totMb = [int][math]::Round($os.TotalVisibleMemorySize / 1024.0); " +
        "$freeMb = [int][math]::Round($os.FreePhysicalMemory / 1024.0); " +
        "$usedMb = $totMb - $freeMb; " +
        "Write-Output ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, 'Mem: {0} {1} {2}', $totMb, $usedMb, $freeMb)) " +
        "}; " +
        "Write-Output '===DISK==='; " +
        "$d = Get-CimInstance Win32_LogicalDisk -Filter 'DeviceID=''C:''' -ErrorAction SilentlyContinue; " +
        "if ($null -ne $d -and $d.Size -gt 0) { " +
        "$totG = $d.Size / 1GB; $usedG = ($d.Size - $d.FreeSpace) / 1GB; $freeG = $d.FreeSpace / 1GB; $pct = [int][math]::Round(($usedG / $totG) * 100); " +
        "Write-Output 'Filesystem Size Used Avail Use% Mounted'; " +
        "Write-Output ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, 'C: {0:0.0}G {1:0.0}G {2:0.0}G {3}% C:', $totG, $usedG, $freeG, $pct)) " +
        "}; " +
        "Write-Output '===PROCS==='; " +
        "$totRamBytes = if ($null -ne $os -and $os.TotalVisibleMemorySize -gt 0) { $os.TotalVisibleMemorySize * 1024.0 } else { 1.0 }; " +
        "Get-Process -ErrorAction SilentlyContinue | Sort-Object WorkingSet64 -Descending | Select-Object -First 15 | ForEach-Object { " +
        "$memPct = ($_.WorkingSet64 / $totRamBytes) * 100.0; " +
        "$cpuSec = if ($null -ne $_.CPU) { $_.CPU } else { 0.0 }; " +
        "Write-Output ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0}|windows|{1:0.0}|{2:0.0}|{3}', $_.Id, $cpuSec, $memPct, $_.ProcessName)) " +
        "}\"";

    /// <summary>
    /// Phân tích đầu ra theo section (<c>===UPTIME===</c>, <c>===CPU===</c>, <c>===MEM===</c>,
    /// <c>===DISK===</c>, <c>===PROCS===</c>) từ máy đích Windows/Linux.
    /// </summary>
    public static HealthParseResult ParseHealthOutput(string? output, int maxProcesses = MaxProcessRows)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new HealthParseResult(null, null, null, null, null, null, null, Array.Empty<ProcessItem>());
        }

        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string currentSection = string.Empty;

        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("===", StringComparison.Ordinal) && line.EndsWith("===", StringComparison.Ordinal) && line.Length > 6)
            {
                currentSection = line.Substring(3, line.Length - 6).Trim();
                if (!sections.ContainsKey(currentSection))
                    sections[currentSection] = new List<string>();
                continue;
            }

            if (!string.IsNullOrEmpty(currentSection))
            {
                sections[currentSection].Add(line);
            }
        }

        string? uptime = sections.TryGetValue("UPTIME", out var upLines) && upLines.Count > 0
            ? upLines[0]
            : null;

        double? cpuPercent = null;
        if (sections.TryGetValue("CPU", out var cpuLines) && cpuLines.Count > 0)
        {
            string cLine = cpuLines[0].TrimEnd('%').Replace(',', '.');
            if (double.TryParse(cLine, NumberStyles.Float, CultureInfo.InvariantCulture, out double cVal))
            {
                cpuPercent = Math.Clamp(cVal, 0.0, 100.0);
            }
            else
            {
                cpuPercent = TopParser.Parse(string.Join("\n", cpuLines)).TotalCpuPercent;
            }
        }

        double? ramUsedMb = null;
        double? ramTotalMb = null;
        if (sections.TryGetValue("MEM", out var memLines) && memLines.Count > 0)
        {
            var memLine = memLines.FirstOrDefault(l => l.StartsWith("Mem:", StringComparison.OrdinalIgnoreCase)) ?? memLines[0];
            var parts = memLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0].StartsWith("Mem", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(parts[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double tot)
                    && double.TryParse(parts[2].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double used))
                {
                    ramTotalMb = tot;
                    ramUsedMb = used;
                }
            }
            else if (parts.Length >= 2
                     && double.TryParse(parts[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double used2)
                     && double.TryParse(parts[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double tot2))
            {
                ramUsedMb = used2;
                ramTotalMb = tot2;
            }
        }

        string? diskUsed = null;
        string? diskTotal = null;
        double? diskPercent = null;
        if (sections.TryGetValue("DISK", out var diskLines) && diskLines.Count > 0)
        {
            string dataLine = diskLines.Count >= 2 && diskLines[0].StartsWith("Filesystem", StringComparison.OrdinalIgnoreCase)
                ? diskLines[1]
                : diskLines[0];
            var parts = dataLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5 && parts[4].EndsWith('%'))
            {
                diskTotal = parts[1];
                diskUsed = parts[2];
                if (double.TryParse(parts[4].TrimEnd('%').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double dp))
                    diskPercent = dp;
            }
            else if (parts.Length >= 3 && parts[2].EndsWith('%'))
            {
                diskUsed = parts[0];
                diskTotal = parts[1];
                if (double.TryParse(parts[2].TrimEnd('%').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double dp))
                    diskPercent = dp;
            }
        }

        var processes = new List<ProcessItem>();
        if (sections.TryGetValue("PROCS", out var procLines))
        {
            foreach (var pLine in procLines)
            {
                if (processes.Count >= maxProcesses) break;
                if (pLine.StartsWith("PID", StringComparison.OrdinalIgnoreCase)) continue;

                if (pLine.Contains('|'))
                {
                    var pParts = pLine.Split('|', 5);
                    if (pParts.Length >= 5)
                    {
                        processes.Add(new ProcessItem
                        {
                            Pid = pParts[0].Trim(),
                            User = pParts[1].Trim(),
                            Cpu = pParts[2].Trim(),
                            Mem = pParts[3].Trim(),
                            Command = pParts[4].Trim()
                        });
                    }
                }
                else
                {
                    var pParts = pLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (pParts.Length >= 5)
                    {
                        processes.Add(new ProcessItem
                        {
                            Pid = pParts[0],
                            User = pParts[1],
                            Cpu = pParts[2],
                            Mem = pParts[3],
                            Command = string.Join(" ", pParts.Skip(4))
                        });
                    }
                }
            }
        }

        return new HealthParseResult(uptime, cpuPercent, ramUsedMb, ramTotalMb, diskUsed, diskTotal, diskPercent, processes);
    }

    /// <summary>
    /// Tạo lệnh dừng tiến trình theo PID cho hệ điều hành đích (Windows dùng <c>Stop-Process -Id &lt;pid&gt; -Force</c>,
    /// Linux dùng <c>LC_ALL=C kill -&lt;sig&gt; &lt;pid&gt;</c>).
    /// </summary>
    public static string BuildKillProcessCommand(int pid, string sig, RemoteOsType osType)
    {
        if (osType == RemoteOsType.Windows)
        {
            return $"powershell.exe -NoProfile -NonInteractive -Command \"Stop-Process -Id {pid} -Force -ErrorAction Stop\"";
        }
        return $"LC_ALL=C kill -{sig} {pid}";
    }

    public ObservableCollection<ProcessItem> Processes { get; set; } = new();
    public ObservableCollection<AcceleratorItem> Accelerators { get; set; } = new();

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
        if (this.FindControl<DataGrid>("PART_AcceleratorList") is { } accelList)
            accelList.ItemsSource = Accelerators;

        if (this.FindControl<KzButton>("PART_BtnRefresh") is { } btnRefresh) btnRefresh.Click += OnRefreshClick;
        if (this.FindControl<KzButton>("PART_BtnClose") is { } btnClose) btnClose.Click += (s, e) => Close();

        // End task (như Task Manager): nút, menu chuột phải, phím Delete
        if (processList != null)
        {
            processList.SelectionChanged += (_, _) => UpdateEndTaskButtons();
            processList.KeyDown += async (_, ev) =>
            {
                if (ev.Key != Avalonia.Input.Key.Delete) return;
                ev.Handled = true;
                await EndSelectedProcessAsync(force: false);
            };
        }
        if (this.FindControl<KzButton>("PART_BtnEndTask") is { } btnEnd) btnEnd.Click += async (_, _) => await EndSelectedProcessAsync(force: false);
        if (this.FindControl<KzButton>("PART_BtnKillTask") is { } btnKill) btnKill.Click += async (_, _) => await EndSelectedProcessAsync(force: true);
        if (this.FindControl<MenuItem>("PART_MenuEndTask") is { } miEnd) miEnd.Click += async (_, _) => await EndSelectedProcessAsync(force: false);
        if (this.FindControl<MenuItem>("PART_MenuKillTask") is { } miKill) miKill.Click += async (_, _) => await EndSelectedProcessAsync(force: true);
        UpdateEndTaskButtons();

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
        _isClosed = true;

        // Q18: nếu đang có refresh in-flight thì finally của RefreshMetricsAsync sẽ dispose
        // (cả Closed lẫn finally đều chạy trên UI thread → không race).
        if (!_isConnecting)
            DisposeSshClient();
    }

    private void DisposeSshClient()
    {
        var client = _sshClient;
        _sshClient = null;
        _detectedOs = null;
        if (client == null) return;
        try { if (client.IsConnected) client.Disconnect(); } catch { /* đang đóng — bỏ qua lỗi mạng */ }
        try { client.Dispose(); } catch { }
    }

    /// <summary>Q18: đảm bảo có kết nối SSH sống — chỉ tạo/connect lại khi chưa có hoặc đã rớt.</summary>
    private SshClient EnsureSshConnected()
    {
        var client = _sshClient;
        if (client != null && client.IsConnected) return client;

        // Kết nối cũ rớt → dọn trước khi tạo mới
        if (client != null)
        {
            try { client.Dispose(); } catch { }
            _sshClient = null;
            _detectedOs = null;
        }

        string host = _profile.Host;
        int port = _profile.SshPort > 0 ? _profile.SshPort : 22;
        string username = !string.IsNullOrWhiteSpace(_profile.SshUsername) ? _profile.SshUsername : "kztek";
        string password = _profile.SshPassword ?? "";

        var newClient = new SshClient(host, port, username, password);
        newClient.Connect();
        _sshClient = newClient;
        _detectedOs = RemoteOsDetector.DetectRemoteOs(newClient);
        return newClient;
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
        if (_isConnecting || _isClosed) return;
        _isConnecting = true;
        SetStatus($"Đang tải dữ liệu lúc {DateTime.Now:HH:mm:ss}...");

        try
        {
            await Task.Run(() =>
            {
                // Q18: tái sử dụng kết nối; chỉ reconnect khi rớt
                var ssh = EnsureSshConnected();
                var osType = _detectedOs ?? RemoteOsDetector.DetectRemoteOs(ssh);
                _detectedOs = osType;

                if (osType == RemoteOsType.Windows)
                {
                    using var winCmd = ssh.CreateCommand(WindowsHealthProbeCommand);
                    winCmd.CommandTimeout = TimeSpan.FromSeconds(10);
                    string winOut = winCmd.Execute() ?? string.Empty;
                    var parsedWin = ParseHealthOutput(winOut, MaxProcessRows);

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_isClosed) return;
                        ApplyParsedHealthResult(parsedWin);
                        UpdateAccelerators("");
                        string uptimeSuffix = !string.IsNullOrWhiteSpace(parsedWin.Uptime) ? $" (Uptime: {parsedWin.Uptime})" : "";
                        SetStatus($"Cập nhật lần cuối: {DateTime.Now:HH:mm:ss}{uptimeSuffix}");
                    });
                    return;
                }

                // 1. RAM Usage
                using var ramCmd = ssh.CreateCommand("LC_ALL=C free -m");
                string ramResult = ramCmd.Execute();

                // 2. Disk Usage
                using var diskCmd = ssh.CreateCommand("LC_ALL=C df -hP /");
                string diskResult = diskCmd.Execute();

                // 3. Top Processes & CPU
                // top -b -n 1 returns batch mode output once.
                using var topCmd = ssh.CreateCommand(TopParser.DefaultTopCommand);
                string topResult = topCmd.Execute();

                // 4. GPU / NPU / VPU (nếu có) — lỗi ở đây không được làm hỏng các chỉ số chính
                string accelResult = "";
                try
                {
                    string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(AcceleratorProbeScript.Replace("\r\n", "\n")));
                    using var accelCmd = ssh.CreateCommand($"echo {b64} | base64 -d | sh");
                    accelCmd.CommandTimeout = TimeSpan.FromSeconds(10);
                    accelResult = accelCmd.Execute();
                }
                catch { /* máy không hỗ trợ / timeout — bỏ qua */ }

                Dispatcher.UIThread.Post(() =>
                {
                    // Q18: guard — refresh in-flight không được post UI vào window đã đóng
                    if (_isClosed) return;
                    ParseAndUpdateUI(ramResult, diskResult, topResult);
                    UpdateAccelerators(accelResult);
                    SetStatus($"Cập nhật lần cuối: {DateTime.Now:HH:mm:ss}");
                });
            });
        }
        catch (Exception ex)
        {
            // Kết nối có thể đã hỏng — dispose để tick sau tự reconnect
            DisposeSshClient();
            if (!_isClosed)
                SetStatus($"Lỗi kết nối: {ex.Message}");
        }
        finally
        {
            _isConnecting = false;
            // Q18: cửa sổ đã đóng trong lúc refresh chạy → dispose tại đây (op cuối cùng)
            if (_isClosed)
                DisposeSshClient();
        }
    }

    private void ApplyParsedHealthResult(HealthParseResult result)
    {
        if (result.CpuPercent.HasValue)
        {
            if (this.FindControl<TextBlock>("PART_CpuText") is { } cpuTxt)
                cpuTxt.Text = $"{result.CpuPercent.Value:F1}%";
            if (this.FindControl<ProgressBar>("PART_CpuBar") is { } cpuBar)
                cpuBar.Value = result.CpuPercent.Value;
        }

        if (result.RamUsedMb.HasValue && result.RamTotalMb.HasValue && result.RamTotalMb.Value > 0)
        {
            double used = result.RamUsedMb.Value;
            double total = result.RamTotalMb.Value;
            double pct = Math.Clamp(used / total * 100.0, 0.0, 100.0);
            if (this.FindControl<TextBlock>("PART_RamText") is { } ramTxt)
                ramTxt.Text = $"{used:0} / {total:0} MB";
            if (this.FindControl<TextBlock>("PART_RamPercentText") is { } ramPctTxt)
                ramPctTxt.Text = $"{pct:0.0}%";
            if (this.FindControl<ProgressBar>("PART_RamBar") is { } ramBar)
                ramBar.Value = pct;
        }

        if (!string.IsNullOrEmpty(result.DiskUsed) && !string.IsNullOrEmpty(result.DiskTotal))
        {
            if (this.FindControl<TextBlock>("PART_DiskText") is { } diskTxt)
                diskTxt.Text = $"{result.DiskUsed} / {result.DiskTotal}";
            if (result.DiskPercent.HasValue)
            {
                if (this.FindControl<TextBlock>("PART_DiskPercentText") is { } diskPctTxt)
                    diskPctTxt.Text = $"{result.DiskPercent.Value:0}%";
                if (this.FindControl<ProgressBar>("PART_DiskBar") is { } diskBar)
                    diskBar.Value = result.DiskPercent.Value;
            }
        }

        var grid = this.FindControl<DataGrid>("PART_ProcessList");
        string? selectedPid = (grid?.SelectedItem as ProcessItem)?.Pid;

        Processes.Clear();
        foreach (var p in result.Processes)
        {
            Processes.Add(p);
        }

        if (grid != null && selectedPid != null)
            grid.SelectedItem = Processes.FirstOrDefault(x => x.Pid == selectedPid);
        UpdateEndTaskButtons();
    }

    private static string WithUnit(string v, string unit)
    {
        v = v.Trim();
        if (v.Length == 0 || v == "-" || v.StartsWith("[")) return "--";
        return v + unit;
    }

    private void UpdateAccelerators(string output)
    {
        Accelerators.Clear();
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Split('|', 6);
            if (p.Length < 6 || p[0] is not ("GPU" or "NPU" or "VPU")) continue;
            Accelerators.Add(new AcceleratorItem
            {
                Kind = p[0],
                Name = p[1].Trim(),
                Util = WithUnit(p[2], "%"),
                Freq = WithUnit(p[3], " MHz"),
                Temp = WithUnit(p[4], " °C"),
                Note = p[5].Trim() == "-" ? "" : p[5].Trim()
            });
        }

        bool any = Accelerators.Count > 0;
        if (this.FindControl<Control>("PART_AcceleratorPanel") is { } panel)
            panel.IsVisible = any;
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
                    if (this.FindControl<ProgressBar>("PART_RamBar") is { } ramBar)
                        ramBar.Value = total > 0 ? used / total * 100 : 0;
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
                    diskPctTxt.Text = parts[4];
                if (this.FindControl<ProgressBar>("PART_DiskBar") is { } diskBar
                    && double.TryParse(parts[4].TrimEnd('%'), out double diskPct))
                    diskBar.Value = diskPct;
            }
        }

        // 3. Parse Top (CPU & Processes) using TopParser (hỗ trợ locale vi_VN dấu phẩy và steady-state mẫu 2)
        var parsedTop = TopParser.Parse(topStr, MaxProcessRows);
        if (parsedTop.TotalCpuPercent.HasValue)
        {
            if (this.FindControl<TextBlock>("PART_CpuText") is { } cpuTxt)
                cpuTxt.Text = $"{parsedTop.TotalCpuPercent.Value:F1}%";
            if (this.FindControl<ProgressBar>("PART_CpuBar") is { } cpuBar)
                cpuBar.Value = parsedTop.TotalCpuPercent.Value;
        }

        // Giữ dòng đang chọn (theo PID) qua mỗi lần làm mới 5s — nếu không, chọn để End task sẽ bị mất.
        var grid = this.FindControl<DataGrid>("PART_ProcessList");
        string? selectedPid = (grid?.SelectedItem as ProcessItem)?.Pid;

        Processes.Clear();
        foreach (var p in parsedTop.Processes)
        {
            Processes.Add(new ProcessItem
            {
                Pid = p.Pid,
                User = p.User,
                Cpu = p.Cpu,
                Mem = p.Mem,
                Command = p.Command
            });
        }

        if (grid != null && selectedPid != null)
            grid.SelectedItem = Processes.FirstOrDefault(x => x.Pid == selectedPid);
        UpdateEndTaskButtons();
    }

    // ── End task ────────────────────────────────────────────────────────────

    private const int MaxProcessRows = 15;

    private void UpdateEndTaskButtons()
    {
        bool hasSel = this.FindControl<DataGrid>("PART_ProcessList")?.SelectedItem is ProcessItem;
        if (this.FindControl<KzButton>("PART_BtnEndTask") is { } b1) b1.IsEnabled = hasSel;
        if (this.FindControl<KzButton>("PART_BtnKillTask") is { } b2) b2.IsEnabled = hasSel;
        if (this.FindControl<MenuItem>("PART_MenuEndTask") is { } m1) m1.IsEnabled = hasSel;
        if (this.FindControl<MenuItem>("PART_MenuKillTask") is { } m2) m2.IsEnabled = hasSel;
    }

    /// <summary>
    /// Kết thúc tiến trình đang chọn trên máy đích: kill -TERM (đóng an toàn) hoặc kill -KILL (buộc dừng).
    /// Không đủ quyền (tiến trình của user khác/root) → thử lại bằng sudo, mật khẩu SSH ghi qua STDIN.
    /// </summary>
    private async Task EndSelectedProcessAsync(bool force)
    {
        if (_isClosed) return;
        if (this.FindControl<DataGrid>("PART_ProcessList")?.SelectedItem is not ProcessItem p) return;
        if (!int.TryParse(p.Pid, out int pid) || pid <= 1)
        {
            SetStatus($"PID không hợp lệ: {p.Pid}");
            return;
        }
        if (!await ConfirmEndTaskAsync(p, force)) return;

        string sig = force ? "KILL" : "TERM";
        SetStatus($"Đang {(force ? "buộc dừng" : "kết thúc")} PID {pid} ({p.Command})...");
        try
        {
            string result = await Task.Run(() => KillRemoteProcess(pid, sig));
            SetStatus(result);
        }
        catch (Exception ex)
        {
            SetStatus($"Không dừng được PID {pid}: {ex.Message}");
        }
        await RefreshMetricsAsync();
    }

    private async Task<bool> ConfirmEndTaskAsync(ProcessItem p, bool force)
    {
        string action = force ? "Buộc dừng" : "Kết thúc";
        string text = $"{action} tiến trình \"{p.Command}\" (PID {p.Pid}, người dùng {p.User}) trên máy đích?";
        if (force)
            text += "\n\nBuộc dừng không cho tiến trình lưu dữ liệu — chỉ dùng khi nó bị treo.";
        if (p.User == "root")
            text += "\n\nĐây là tiến trình hệ thống (root) — dừng có thể làm máy đích hoạt động sai.";
        if (p.Command.Contains("IPGS.Remote", StringComparison.OrdinalIgnoreCase)
            || p.Command.Contains("ZcuAgent", StringComparison.OrdinalIgnoreCase))
            text += "\n\nĐây có thể là Remote Agent — dừng nó sẽ ngắt điều khiển từ xa (service tự bật lại sau vài giây).";

        bool confirmed = false;
        var dlg = new Window
        {
            Title = $"{action} tiến trình",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var btnCancel = new KzButton { Content = "Hủy" };
        btnCancel.Classes.Add("kz-outline");
        var btnOk = new KzButton { Content = action };
        btnOk.Classes.Add("kz-danger");
        btnCancel.Click += (_, _) => dlg.Close();
        btnOk.Click += (_, _) => { confirmed = true; dlg.Close(); };

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        buttons.Children.Add(btnCancel);
        buttons.Children.Add(btnOk);

        var root = new StackPanel { Margin = new Avalonia.Thickness(24, 20), Spacing = 18 };
        root.Children.Add(new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        root.Children.Add(buttons);
        dlg.Content = root;

        await dlg.ShowDialog(this);
        return confirmed;
    }

    /// <summary>Chạy trên thread nền. Dùng kết nối SSH riêng để không đụng kết nối của vòng làm mới 5s.</summary>
    private string KillRemoteProcess(int pid, string sig)
    {
        string host = _profile.Host;
        int port = _profile.SshPort > 0 ? _profile.SshPort : 22;
        string username = !string.IsNullOrWhiteSpace(_profile.SshUsername) ? _profile.SshUsername : "kztek";
        string password = _profile.SshPassword ?? "";

        using var ssh = new SshClient(host, port, username, password);
        ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
        ssh.Connect();
        try
        {
            var osType = _detectedOs ?? RemoteOsDetector.DetectRemoteOs(ssh);
            if (osType == RemoteOsType.Windows)
            {
                using var winKill = ssh.CreateCommand(BuildKillProcessCommand(pid, sig, RemoteOsType.Windows));
                winKill.Execute();
                if (winKill.ExitStatus == 0)
                    return $"Đã dừng tiến trình PID {pid} trên Windows (Stop-Process -Force).";

                string err = (winKill.Error ?? winKill.Result ?? "").Trim();
                return $"Không dừng được PID {pid}: {(string.IsNullOrEmpty(err) ? $"exit {winKill.ExitStatus}" : err)}";
            }

            using (var cmd = ssh.CreateCommand(BuildKillProcessCommand(pid, sig, RemoteOsType.Linux)))
            {
                cmd.Execute();
                if (cmd.ExitStatus == 0)
                    return $"Đã gửi {sig} tới PID {pid}.";
                if (!cmd.Error.Contains("not permitted", StringComparison.OrdinalIgnoreCase))
                    return $"Không dừng được PID {pid}: {cmd.Error.Trim()}";
            }

            // Không đủ quyền → sudo; mật khẩu ghi vào STDIN (không lộ trên command line) — như RemoteAppInstallService.
            using var sudo = ssh.CreateCommand($"LC_ALL=C sudo -S -p '' kill -{sig} {pid}");
            var async = sudo.BeginExecute();
            System.IO.Stream? input = null;
            for (int i = 0; i < 20 && input == null; i++)
            {
                try { input = sudo.CreateInputStream(); }
                catch { System.Threading.Thread.Sleep(50); }
            }
            if (input != null)
            {
                using (input)
                {
                    var bytes = Encoding.UTF8.GetBytes(password + "\n");
                    input.Write(bytes, 0, bytes.Length);
                    input.Flush();
                }
            }
            sudo.EndExecute(async);
            return sudo.ExitStatus == 0
                ? $"Đã gửi {sig} tới PID {pid} (sudo)."
                : $"Không dừng được PID {pid} (sudo): {sudo.Error.Trim()}";
        }
        finally
        {
            try { ssh.Disconnect(); } catch { }
        }
    }
}
