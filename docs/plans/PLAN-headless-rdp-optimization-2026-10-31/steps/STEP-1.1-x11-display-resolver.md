# Bước 1.1 — Tạo X11DisplayResolver Tự Động Dò Tìm Display :0, :1, :10.0 (xrdp)

- **Agent:** Senior Developer
- **Status:** done
- **Mục tiêu:** Xây dựng module `X11DisplayResolver` tự động phát hiện cổng X11 Display khả dụng trong môi trường Headless (không cắm màn hình), Kiosk và phiên ảo xrdp.

## Nội dung thực hiện
1. Tạo class `X11DisplayResolver.cs` trong `IPGS.RemoteControl.ZcuAgent/Interop/`.
2. Hỗ trợ phương thức trích xuất `ParseDisplayFromSocketName` từ socket Unix domain `/tmp/.X11-unix/X*` (ví dụ `X0` -> `:0`, `X10` -> `:10`).
3. Hiện thực hóa `GetCandidateDisplays` với độ ưu tiên giảm dần:
   - Preferred display do người dùng cấu hình
   - Biến môi trường hệ thống `DISPLAY`
   - Danh sách sockets trong `/tmp/.X11-unix/`
   - Danh sách fallback mặc định (`:0`, `:1`, `:10.0`, `:10`, `:20.0`)
4. Hỗ trợ delegate `displayOpener` trong `OpenAvailableDisplay` phục vụ kiểm thử đơn vị tự động trên Windows.
5. Tự động thiết lập `Environment.SetEnvironmentVariable("DISPLAY", resolved)` khi kết nối thành công.
