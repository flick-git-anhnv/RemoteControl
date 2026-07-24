namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// Trạng thái kết nối tổng hợp của một máy tính ZCU: có mở được cổng SSH và
/// cổng Agent Remote Control hay không. Chỉ dùng cho hiển thị UI (icon trạng
/// thái), KHÔNG lưu vào profiles.json.
/// </summary>
public enum ComputerConnectivityStatus
{
    /// <summary>Chưa kiểm tra lần nào.</summary>
    Unknown,

    /// <summary>Đang dò kết nối.</summary>
    Checking,

    /// <summary>Cả cổng SSH và Agent Remote Control đều mở.</summary>
    Online,

    /// <summary>Ít nhất một trong hai cổng (SSH / Agent) không kết nối được.</summary>
    Offline
}
