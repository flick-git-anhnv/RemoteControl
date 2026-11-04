# STEP 2.1: Hợp Nhất UI Quản Lý File & CMD Shell (Phương Án A)

## Mục tiêu
- Nâng cấp `RemoteCommandWindow.axaml` (Tab 2 SFTP):
  - Chuyển `ListBox` sang `DataGrid` 4 cột (Tên File, Loại, Kích thước, Ngày sửa) hỗ trợ sắp xếp.
  - Thêm ô lọc live `PART_TxtFileFilter` và nút `Đi` (`PART_BtnFileGo`).
  - Thêm nút `>_ Terminal tại đây` trên thanh công cụ SFTP.
- Nâng cấp `RemoteCommandWindow.axaml.cs`:
  - Thêm constructor hỗ trợ `initialTabIndex` (0 = CMD, 1 = File).
  - Tích hợp mô hình đếm tham chiếu `AcquireSftpClient()` / `ReleaseSftpClient()` chống crash khi đóng cửa sổ.
  - Giữ nguyên các ưu điểm: `Download` (single & multi), `Sync` (so sánh kích thước + thời gian sửa), `Delete` (fallback `rm -rf` qua SSH).
- Cập nhật `ConnectionEntryWindow.axaml` và `.cs`:
  - Gộp 2 nút `📁 Quản lý File` và `>_ CMD Shell` thành 1 nút `🛠️ File & CMD`.
