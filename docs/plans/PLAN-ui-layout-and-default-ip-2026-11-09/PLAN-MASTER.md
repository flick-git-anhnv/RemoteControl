# Plan: Tối ưu UI Form Thông tin máy tính & Đổi mặc định AllowedClientIPs về 0.0.0.0/0

**Mục tiêu:**
1. Đặt giá trị mặc định cho Mạng/IP Cho Phép Kết Nối (`AllowedClientIPs`) thành `0.0.0.0/0` trong ZcuSetupWizard và SshInstallerOptions.
2. Tái cấu trúc cửa sổ `ComputerEditWindow`: đưa thông tin máy tính & SSH lên trước cùng nút "Lấy lại Token từ máy", chuyển Token & Cổng TCP xuống dưới, thiết kế lại bố cục dạng 2 cột gọn gàng để vừa khít màn hình, không cần cuộn chuột.

**Workflow áp dụng:** WF-FASTTRACK (UI refinement & config adjustment)

**Tiến độ:**
- [x] Bước 1 (JD): Cập nhật default AllowedClientIPs thành `0.0.0.0/0` trong `ZcuRemoteInstallerService.cs`, `ZcuSetupWizardWindow.axaml`, `ZcuSetupWizardWindow.axaml.cs`
- [x] Bước 2 (JD): Thiết kế lại `ComputerEditWindow.axaml` (2 cột gọn gàng, SSH lên trước, kích thước vừa vặn không cuộn)
- [x] Bước 3 (TL): Review code & XAML structure
- [x] Bước 4 (UXR): UX-UI Reflection kiểm tra bố cục & luồng thao tác
- [x] Bước 5 (QAE): Smoke test & build check
