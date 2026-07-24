namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// Interface quản lý lưu trữ danh sách máy tính ZCU và lịch sử kết nối.
/// </summary>
public interface IComputerProfileStore
{
    /// <summary>Lấy danh sách tất cả máy tính đã lưu.</summary>
    IReadOnlyList<ComputerProfile> GetAll();

    /// <summary>Lấy thông tin máy tính theo ID.</summary>
    ComputerProfile? GetById(string id);

    /// <summary>Thêm mới hoặc cập nhật thông tin máy tính.</summary>
    ComputerProfile Save(ComputerProfile profile);

    /// <summary>Xóa máy tính khỏi danh sách theo ID.</summary>
    bool Delete(string id);

    /// <summary>
    /// Ghi nhận lịch sử kết nối tới host:port. 
    /// Nếu đã có trong danh sách thì cập nhật LastConnectedAt, nếu chưa có thì tạo mới profile.
    /// </summary>
    ComputerProfile RecordConnection(string host, int port, string token, string? name = null);
}
