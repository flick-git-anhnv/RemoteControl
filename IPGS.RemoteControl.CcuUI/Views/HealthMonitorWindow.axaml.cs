using System;
using System.Collections.ObjectModel;
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
    private bool _isClosed;

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
        }

        string host = _profile.Host;
        int port = _profile.SshPort > 0 ? _profile.SshPort : 22;
        string username = !string.IsNullOrWhiteSpace(_profile.SshUsername) ? _profile.SshUsername : "kztek";
        string password = _profile.SshPassword ?? "";

        var newClient = new SshClient(host, port, username, password);
        newClient.Connect();
        _sshClient = newClient;
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

                // 1. RAM Usage
                using var ramCmd = ssh.CreateCommand("free -m");
                string ramResult = ramCmd.Execute();

                // 2. Disk Usage
                using var diskCmd = ssh.CreateCommand("df -h /");
                string diskResult = diskCmd.Execute();

                // 3. Top Processes & CPU
                // top -b -n 1 returns batch mode output once.
                using var topCmd = ssh.CreateCommand("top -b -n 1 | head -n 15");
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
