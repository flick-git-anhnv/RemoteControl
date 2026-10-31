# Bước 1.2 — Nới lỏng XDG_SESSION_TYPE Guard & Tích Hợp DPMS Anti-Blanking trong X11ScreenCapturer

- **Agent:** Senior Developer
- **Status:** done
- **Mục tiêu:** Cho phép ZcuAgent khởi động trong phiên systemd daemon headless và RDP; vô hiệu hóa triệt để cơ chế screensaver DPMS làm đen màn hình.

## Nội dung thực hiện
1. Sửa rào chắn trong `Program.cs`:
   - Chỉ chặn nghiêm ngặt khi `sessionType == "wayland"`.
   - Khi `sessionType` rỗng (thường gặp ở daemon không giao diện hoặc ssh/rdp), in thông báo Notice và tiếp tục dò tìm qua `X11DisplayResolver`.
2. Thêm P/Invoke trong `X11Interop.cs`:
   - `XSetScreenSaver(display, timeout, interval, prefer_blanking, allow_exposures)`
   - `XResetScreenSaver(display)`
3. Cập nhật `X11ScreenCapturer.Initialize()`:
   - Dùng `X11DisplayResolver.OpenAvailableDisplay` để mở Display khả dụng.
   - Gọi `XSetScreenSaver(_display, 0, 0, 0, 0)` và `XResetScreenSaver(_display)` ngay khi kết nối display thành công để triệt tiêu hiện tượng chụp ra ảnh đen.
