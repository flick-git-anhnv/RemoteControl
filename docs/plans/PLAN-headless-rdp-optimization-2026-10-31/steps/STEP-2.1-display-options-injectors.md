# Bước 2.1 — Thêm Cấu Hình Display Trong AgentOptions & Cập Nhật Mouse/Keyboard Injectors

- **Agent:** Senior Developer
- **Status:** done
- **Mục tiêu:** Cho phép người dùng tùy biến cổng hiển thị trong cấu hình và đồng bộ hóa injectors.

## Nội dung thực hiện
1. Bổ sung trường `public string Display { get; set; } = string.Empty;` vào `AgentOptions.cs`.
2. Cập nhật `MouseInjector.cs`:
   - Inject `IOptions<AgentOptions>? options = null`.
   - Sử dụng `X11DisplayResolver.OpenAvailableDisplay(_options?.Value?.Display, _logger)` thay vì ép cứng `XOpenDisplay(null)`.
3. Cập nhật `KeyboardInjector.cs`:
   - Inject `IOptions<AgentOptions>? options = null`.
   - Sử dụng `X11DisplayResolver.OpenAvailableDisplay(_options?.Value?.Display, _logger)`.
4. Đảm bảo toàn bộ hệ thống capturer và injectors chạy đồng bộ trên cùng Display đã phân giải.
