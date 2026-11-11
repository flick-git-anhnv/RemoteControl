# PLAN — WinAgent: điều khiển từ xa máy Windows từ CCU (2026-11-10)

## Mục tiêu
CCU hiện chỉ điều khiển máy ZCU Linux (ZcuAgent, X11). Thêm agent chạy trên Windows nói **cùng giao thức** để CCU điều khiển được máy Windows mà **không phải sửa CCU** (MVP).

## Quyết định thiết kế
| Chủ đề | Quyết định | Lý do |
|---|---|---|
| Chia sẻ mã | WinAgent **link file nguồn** từ ZcuAgent (giống cách ZcuAgent link `Protocol` của CcuClient), chưa tách `Agent.Core` | Không phải di chuyển file trong working tree đang có nhiều thay đổi; tách thật làm sau khi MVP chạy ổn |
| Tách nền tảng | `ClientSession`/`FileTransferHandler`/`TcpServer` chỉ dùng interface: `IScreenCapturer`, `IMouseInjector`, `IKeyboardInjector`, `IClipboardService`, `IMonitorProvider`, `IDesktopPlatformService`, `IMachineLoadSampler` (trong `ZcuAgent/Interfaces.cs`) | Xoá hết `xclip`/`xrandr`/`notify-send`/`/proc` khỏi phần dùng chung |
| Linux | Logic cũ chuyển nguyên văn vào `ZcuAgent/Platform/Linux*.cs`, `XrandrMonitorProvider.cs` | Hành vi Linux không đổi |
| Chụp màn hình | GDI `BitBlt(SRCCOPY\|CAPTUREBLT)` toàn bộ virtual screen vào DIB 32bpp top-down; vẽ con trỏ bằng `DrawIconEx` (BitBlt không chụp con trỏ) | Đơn giản, chạy mọi máy; DXGI để sau |
| DPI | PerMonitorV2 (manifest + `SetProcessDpiAwarenessContext`) | Tránh ảnh mờ/lệch toạ độ khi scale 125–150% |
| Chuột | `SendInput` absolute + `MOUSEEVENTF_VIRTUALDESK`, chuẩn hoá về tâm pixel `((2p+1)*65536)/(2*size)` | Chính xác từng pixel, đa màn hình |
| Phím | Keysym: phím đặc biệt→VK, ký tự in được→`KEYEVENTF_UNICODE` (trừ khi giữ Ctrl/Alt/Win thì dùng `VkKeyScanW`). Raw (evdev+8): scancode set 1 + cờ extended | Gõ đúng mọi layout/IME ở máy đích |
| Clipboard | Thread STA riêng + cửa sổ message-only làm chủ clipboard; text và `CF_HDROP` (+`Preferred DropEffect`) | `OpenClipboard(NULL)` làm `SetClipboardData` thất bại |
| JPEG | Dùng Skia (`JpegEncoder`); **không** link `TurboJpeg*` | `tjhandle`/`nuint` P/Invoke dựng cho Linux (`unsigned long` Windows = 4 byte) |
| Quyền | Chạy bằng Scheduled Task lúc đăng nhập, *Run with highest privileges* | UIPI chặn `SendInput` vào cửa sổ Administrator nếu agent chạy quyền thường |
| Phiên bản | HelloAck báo `WinAgent/1.2` | `IsAgentOutdated` của CCU chỉ đọc số sau dấu `/` |

## Phạm vi bản đầu (đã làm)
- Phase 1: refactor ZcuAgent qua interface (hành vi Linux giữ nguyên).
- Phase 2: `IPGS.RemoteControl.WinAgent` — capture GDI + con trỏ, chuột/phím, clipboard text + file, danh sách màn hình, chat (MessageBox), SysInfo, chạy ẩn + log file, script cài đặt `scripts/windows-agent/install-win-agent.ps1`.

## Chưa làm / để sau
- **Phase 3**: DXGI Desktop Duplication (nhanh hơn GDI), dirty rects.
- **Phase 4**: Windows Service (SYSTEM, Session 0) + helper trong phiên user (`SetThreadDesktop`, màn hình khoá/đăng nhập, `SendSAS` cho Ctrl+Alt+Del).
- **Phase 5**: CCU biết `OsType` của máy (hiện CCU không phân biệt), nút cài WinAgent từ CCU, đóng gói installer.
- Tách `Agent.Core` thật; `ClientSession` nhận `Stream` thay vì `TcpClient`.
- TurboJpeg trên Windows (sửa kiểu `nuint`→`UIntPtr`/`ulong` theo ABI).

## Kiểm chứng
- Đã có: biên dịch ZcuAgent + WinAgent 0 lỗi (môi trường cloud, không có NuGet → Skia được stub).
- **Chưa có**: chạy thật trên Windows; ZcuAgent chưa chạy lại sau refactor; chưa chạy bộ test.
- Cách kiểm tra trên máy dev:
  1. `dotnet build IPGS.RemoteControl.sln` và `dotnet test IPGS.RemoteControl.sln`.
  2. `dotnet publish IPGS.RemoteControl.WinAgent -c Release -r win-x64 --self-contained false -o publish/win-agent` (WinAgent chưa nằm trong `.sln`).
  3. Trên máy Windows: chép thư mục publish + `scripts/windows-agent/install-win-agent.ps1`, chạy PowerShell Admin → lấy token in ra.
  4. Từ CCU thêm máy (IP:17600 + token): xem hình, chuột, gõ chữ (có dấu), Ctrl+C/V text và file, đổi màn hình, đổi scale DPI 150%.
  5. Với ZCU Linux: `dotnet publish ... -r linux-x64` lại và kiểm tra một phiên như cũ (có thể cần `build.bat` nếu CCU nhúng binary agent).

## Rủi ro đã biết
- Cửa sổ Administrator không điều khiển được nếu agent không chạy quyền cao nhất.
- Màn hình khoá / UAC secure desktop / Ctrl+Alt+Del không hoạt động (Phase 4).
- GDI trên máy RDP/VM không GPU có thể chậm; ảnh đen với cửa sổ DRM/overlay phần cứng.
- Lỗi runtime P/Invoke chỉ lộ khi chạy thật trên Windows.
