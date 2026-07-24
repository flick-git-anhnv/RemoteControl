namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// Đại diện cho thông tin một máy tính ZCU trong danh sách đã lưu / lịch sử kết nối.
/// </summary>
public sealed class ComputerProfile
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

    /// <summary>Thời điểm kết nối gần nhất (null nếu chưa từng kết nối).</summary>
    public DateTimeOffset? LastConnectedAt { get; set; }

    /// <summary>Thời điểm tạo hồ sơ.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Tên hiển thị (Tên gợi nhớ nếu có, ngược lại là Host:Port).</summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : $"{Host}:{Port}";
}
