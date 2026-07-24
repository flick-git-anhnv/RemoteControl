using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// Đại diện cho thông tin một máy tính ZCU trong danh sách đã lưu / lịch sử kết nối.
/// </summary>
public sealed class ComputerProfile : INotifyPropertyChanged
{
    /// <summary>Mã ID duy nhất của máy tính (Guid string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Tên gợi nhớ (ví dụ: "ZCU Máy Trạm P01").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Địa chỉ IP hoặc hostname của ZCU.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Cổng TCP kết nối (mặc định 17600).</summary>
    public int Port { get; set; } = 17600;

    /// <summary>Shared-secret token xác thực.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Ghi chú bổ sung (tùy chọn).</summary>
    public string? Notes { get; set; }

    /// <summary>Cổng SSH (mặc định 22) — dùng cho Deploy Kiosk / Cài ZCU từ xa.</summary>
    public int SshPort { get; set; } = 22;

    /// <summary>Username SSH (tùy chọn) — dùng cho Deploy Kiosk / Cài ZCU từ xa.</summary>
    public string? SshUsername { get; set; }

    /// <summary>Password SSH (tùy chọn) — dùng cho Deploy Kiosk / Cài ZCU từ xa.</summary>
    public string? SshPassword { get; set; }

    /// <summary>Thời điểm kết nối gần nhất (null nếu chưa từng kết nối).</summary>
    public DateTimeOffset? LastConnectedAt { get; set; }

    /// <summary>Thời điểm tạo hồ sơ.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Tên hiển thị (Tên gợi nhớ nếu có, ngược lại là Host:Port).</summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : $"{Host}:{Port}";

    // ── Trạng thái kết nối runtime (chỉ dùng cho UI, KHÔNG lưu vào profiles.json) ──

    private ComputerConnectivityStatus _status = ComputerConnectivityStatus.Unknown;

    /// <summary>Trạng thái tổng hợp SSH + Agent Remote Control, cập nhật bởi <see cref="ComputerStatusChecker"/>.</summary>
    [JsonIgnore]
    public ComputerConnectivityStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Kết quả dò cổng SSH gần nhất (null nếu chưa kiểm tra).</summary>
    [JsonIgnore]
    public bool? SshReachable { get; private set; }

    /// <summary>Kết quả dò cổng Agent Remote Control gần nhất (null nếu chưa kiểm tra).</summary>
    [JsonIgnore]
    public bool? AgentReachable { get; private set; }

    /// <summary>Chú thích trạng thái hiển thị dạng tooltip cho icon trạng thái tổng hợp.</summary>
    [JsonIgnore]
    public string StatusTooltip => Status switch
    {
        ComputerConnectivityStatus.Checking => "🔄 Đang kiểm tra kết nối SSH & Agent Service...",
        ComputerConnectivityStatus.Online =>
            "🟢 SSH: kết nối được · Agent Service: đang chạy",
        ComputerConnectivityStatus.Offline =>
            $"🔴 SSH ({SshPort}): {(SshReachable == true ? "OK" : "Không kết nối được")} · " +
            $"Agent Service ({Port}): {(AgentReachable == true ? "OK" : "Không kết nối được")}",
        _ => "⚪ Chưa kiểm tra kết nối"
    };

    /// <summary>Tooltip riêng cho icon trạng thái SSH.</summary>
    [JsonIgnore]
    public string SshStatusTooltip => SshReachable switch
    {
        true => $"🟢 SSH ({SshPort}): kết nối được",
        false => $"🔴 SSH ({SshPort}): không kết nối được",
        null => Status == ComputerConnectivityStatus.Checking ? "🔄 Đang kiểm tra SSH..." : "⚪ Chưa kiểm tra SSH"
    };

    /// <summary>Tooltip riêng cho icon trạng thái Agent Remote Control (đã cài & đang chạy).</summary>
    [JsonIgnore]
    public string AgentStatusTooltip => AgentReachable switch
    {
        true => $"🟢 Remote Agent ({Port}): đã cài đặt & đang chạy",
        false => $"🔴 Remote Agent ({Port}): chưa cài đặt hoặc không kết nối được",
        null => Status == ComputerConnectivityStatus.Checking ? "🔄 Đang kiểm tra Remote Agent..." : "⚪ Chưa kiểm tra Remote Agent"
    };

    /// <summary>Đánh dấu đang dò kết nối — gọi trước khi bắt đầu <see cref="ComputerStatusChecker.ProbeAsync"/>.</summary>
    public void MarkChecking()
    {
        SshReachable = null;
        AgentReachable = null;
        Status = ComputerConnectivityStatus.Checking;
        OnPropertyChanged(nameof(SshReachable));
        OnPropertyChanged(nameof(AgentReachable));
        OnPropertyChanged(nameof(StatusTooltip));
        OnPropertyChanged(nameof(SshStatusTooltip));
        OnPropertyChanged(nameof(AgentStatusTooltip));
    }

    /// <summary>Áp kết quả dò kết nối — PHẢI gọi trên UI thread (raise PropertyChanged).</summary>
    public void ApplyStatusResult(ComputerStatusProbeResult result)
    {
        SshReachable = result.SshReachable;
        AgentReachable = result.AgentReachable;
        Status = result.Status;
        OnPropertyChanged(nameof(SshReachable));
        OnPropertyChanged(nameof(AgentReachable));
        OnPropertyChanged(nameof(StatusTooltip));
        OnPropertyChanged(nameof(SshStatusTooltip));
        OnPropertyChanged(nameof(AgentStatusTooltip));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
