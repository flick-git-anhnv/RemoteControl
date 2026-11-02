# CONTEXT HINTS — remote-control-review-improve
Task: Review toàn diện codebase & đề xuất cải tiến IPGS Remote Control (P1)

## Module liên quan chính:
- `ZcuAgent/Net/ClientSession.cs`: Capture loop, adaptive FPS, message dispatch, buffer reuse, resource cleanup
- `ZcuAgent/Capture/X11ScreenCapturer.cs`: XShm/XGetImage, screen size synchronization, X11 error handling
- `CcuClient/RemoteControlClient.cs`: TCP connection, TLS upgrade, reconnect loop, heartbeat, thread marshaling
- `CcuClient/ShellQuote.cs` & `SecretProtector.cs`: An toàn thực thi shell SSH và mã hóa DPAPI profiles
- `CcuUI/Views/RemoteScreenControl.axaml.cs`: Render WriteableBitmap, clipboard hook, direct file/folder paste
- `CcuUI/Views/HealthMonitorWindow.axaml.cs`: Giám sát GPU/NPU/VPU và phần cứng Rockchip/Intel

## Skill & Command khuyến nghị:
- `security-audit-stride`: Đánh giá rủi ro bảo mật (STRIDE) cho transport, token, SSH sudo
- `verify-pr`: Chốt chặn chất lượng kiểm tra build, tests và đồng bộ tài liệu

## UNCERTAIN / Watch out:
- Khả năng rò rỉ unmanaged memory (SkiaSharp / WriteableBitmap / X11 SHM) khi chạy remote liên tục
- Tranh chấp luồng giữa UI Thread và TCP Background Task khi cập nhật clipboard/frame
- An toàn khi truyền file trực tiếp và xử lý quyền root qua SSH STDIN
