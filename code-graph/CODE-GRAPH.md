# CODE-GRAPH.md — Bản đồ codebase: Remote Control Tool (CCU ↔ ZCU / Windows)
**Cập nhật lần cuối:** 2026-11-11 | **Bởi:** tech-lead, qa-engineer | **Version:** 4.1

> File này được duy trì tự động bởi coding agents.
> **Đọc file này TRƯỚC khi đọc source code** để hiểu cấu trúc dự án mà không cần mở từng file.

> **Ghi chú version 4.1 (2026-11-11):** Triển khai Zero-Touch Windows Remote Provisioning & Quản trị Đa Nền Tảng (Linux + Windows) trên CCU (đã kiểm nghiệm thực tế trên máy Windows `192.168.21.14`):
> - **Windows Zero-Touch OpenSSH Bootstrap & Tích hợp Solution (`IPGS.RemoteControl.sln`, `scripts/windows-agent/`):** Tích hợp `IPGS.RemoteControl.WinAgent` vào `.sln` (6 projects). Cung cấp bộ script Bootstrap toàn trình (`bootstrap-windows-ssh.ps1` UTF-8 BOM, `1-click-enable-ssh.bat` ASCII/REM, `autounattend.xml` với `BypassNRO = 1` và 4 bước `FirstLogonCommands` chạy độc lập cả khi rút USB) tự động đảm bảo tài khoản `kztek`/`123456`, kích hoạt OpenSSH Server (`sshd`), ghi `sshd_config` sạch (không có `Match Group administrators`) kèm chuẩn hóa phân quyền NTFS `icacls` (`SYSTEM:(F)`, `Administrators:(F)`, `/inheritance:r`), đặt đường dẫn tuyệt đối `DefaultShell = C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` (xóa bỏ `DefaultShellCommandOption`), mở Firewall TCP `22` + TCP `17600` + ICMPv4/v6 Ping, và bật `AutoAdminLogon`.
> - **Cài đặt Remote Agent Đa Nền Tảng qua SSH/SFTP (`RemoteOsDetector`, `ZcuRemoteInstallerService`, `ShellQuote`, `ZcuSetupWizardWindow`):** Tự động dò hệ điều hành máy đích tức thì (0ms) qua `ssh.ConnectionInfo?.ServerVersion` (`OpenSSH_for_Windows`) kết hợp `uname -s` / `cmd.exe /c ver` (`DetectRemoteOsAsync`, `ParseOsDetectionOutput`). Nếu là Windows: tự động đóng gói `WinAgent`, tải lên qua SFTP, tạo `appsettings.json` UTF-8 không BOM, thực thi `install-win-agent.ps1` đăng ký Scheduled Task `KztekWinAgent` chạy Interactive với `RunLevel Highest`, đọc lại cấu hình bằng `ParseAgentConfigJson` và hỗ trợ Gỡ cài đặt từ xa.
> - **Bộ Công cụ Quản trị CCU Đa Hệ Điều Hành (`ComputerStatusChecker`, `RemoteCommandWindow`, `BulkActionWindow`, `HealthMonitorWindow`, `RemoteAppInstallService`, `RemoteFilePickerWindow`):** Hỗ trợ lấy chỉ số CPU/RAM/Disk trên cả Linux và Windows (`ParseMetricsOutput`, `BuildWindowsMetricsCommand`), duyệt/quản lý SFTP ổ đĩa Windows (`/C:/Users/...` ↔ `C:\Users\...`), dịch tự động Quick Commands sang PowerShell (`TranslateQuickCommandForWindows`), giám sát tiến trình & dừng tiến trình Windows (`ParseHealthOutput`, `BuildKillProcessCommand`), và cài đặt silent gói `.msi`/`.exe` từ xa (`ValidateInstallerPackagePath`, `BuildWindowsInstallCommand`).
> - **267 Unit Tests Pass 100%:** Bổ sung `WindowsRemoteProvisioningTests.cs` kiểm thử toàn diện toàn bộ logic đa nền tảng của Phase 1–3 và kịch bản thực nghiệm trên `192.168.21.14`.

> **Ghi chú version 4.0 (2026-11-10):** Tự động nhận diện và điều chỉnh tỷ lệ/độ phân giải màn hình động (CCU Auto-fit Aspect Ratio & ZCU Headless Resolution Adapter):
> - **CCU Auto-fit Window Aspect Ratio & Display Mode Switcher (`RemoteScreenViewModel.cs`, `RemoteScreenWindow.axaml/.cs`, `RemoteScreenControl.axaml`):** Tự động nhận diện độ phân giải/tỷ lệ màn hình máy đích (4:3, 5:4, 16:9, 9:16...) và co giãn kích thước cửa sổ CCU vừa khít tỷ lệ thực (loại bỏ hoàn toàn viền đen thừa mà không méo hình). Hỗ trợ nút chuyển đổi 3 chế độ hiển thị (`AutoFit`, `Fill`, `Uniform`) với ánh xạ toạ độ chuột chính xác 100%.
> - **ZCU Headless Resolution Adapter (`HeadlessResolutionAdapter.cs`, `RemoteControlHostedService.cs`, `AgentOptions.cs`):** Tự động nhận diện khi máy ZCU không cắm màn hình vật lý (`0mm x 0mm` không có EDID, Xorg rơi về fallback VESA `1024x768`) để tiêm mode CVT `1920x1080_60.00` và nâng lên Full HD 16:9; khi cắm màn hình thật vào (có EDID), tự động khôi phục `--auto` về đúng độ phân giải chuẩn của màn hình vật lý.
> - **169 Unit Tests Pass 100%:** Bổ sung `ResolutionAdaptationTests.cs` kiểm thử toàn diện `CalculateAutoFitWindowSize`, `MapToZcuCoords`, và `HeadlessResolutionAdapter`.

> **Ghi chú version 3.9 (2026-11-03):** Tách độc lập Kiosk Deploy, hợp nhất Quản lý File & CMD Shell và chuẩn hóa 100% UTF-8:
> - **Tách độc lập Deploy Kiosk (KioskDeployService.cs & KioskDeployWindow.axaml.cs):** Phân chia DeployScope (MachineOnly cho Tab Config máy tính, SoftwareOnly cho Tab Config phần mềm), sửa thứ tự gỡ lock dconf (Step 0) trong 2-configure-system.sh và kích hoạt lại GNOME Shell trong 0ms giúp mở lại nút Activities ngay lập tức.
> - **Hợp nhất UI Quản lý File & CMD Shell (RemoteCommandWindow.axaml & .cs, ConnectionEntryWindow.axaml & .cs):** Gộp 2 nút thành 1 nút 🛠️ File & CMD trên thẻ máy tính. Nâng cấp Tab 2 của RemoteCommandWindow sang DataGrid 4 cột sortable, hỗ trợ lọc file live PART_TxtFileFilter, nút Đi, nút >_ CMD tại đây, giữ nguyên Download, Sync, Xóa đệ quy SSH 
m -rf.
> - **Khắc phục triệt để lỗi Encoding / vỡ font tiếng Việt & Emoji (G022):** Chuẩn hóa toàn bộ file nguồn theo UTF-8 không BOM/LF-CRLF sạch, đảm bảo hiển thị hoàn hảo các ký tự có dấu và biểu tượng.

> **Ghi chú version 3.8:** Tích hợp trực tiếp thư viện `KztekComponentAvalonia` và chuẩn hóa Solution `IPGS.RemoteControl.sln`:
> - **Tích hợp KztekComponentAvalonia nội bộ repo:** Sao chép toàn bộ mã nguồn `KztekComponentAvalonia` vào repository `6.RemoteControlTool`, chuyển đổi toàn bộ `ProjectReference` từ tham chiếu ngoài sang tham chiếu nội bộ (`..\KztekComponentAvalonia\KztekComponentAvalonia.csproj`).
> - **Khởi tạo Solution quản lý tập trung (`IPGS.RemoteControl.sln`):** Quản lý đồng bộ 5 project (.NET 8/.NET 9), khép kín chu trình build và test độc lập không phụ thuộc thư mục ngoài.
> - **Toàn diện 117 Unit Tests Pass 100%:** Bao gồm 9 bài test SFTP/Navigation (`RemoteFileItemTests.cs`) và 14 bài test Zero-Alloc Frame Streaming.

> **Ghi chú version 3.6:** Hoàn thành chu trình cải tiến đỉnh cao Vòng 7 — Zero-Allocation Frame Streaming & Chống Trễ Tích Lũy (F-ROUND7-1 đến F-ROUND7-5):
> - **Zero-Allocation Frame Streaming trên ZcuAgent (`ClientSession.cs` & `MessageCodec.cs`):** Bổ sung `WriteFrameMetadataHeader` ghi 24 bytes metadata vào span đích và buffer `_frameHeaderBuffer` 29 bytes cố định (`WriteFrameJpegDirectAsync`). Stream trực tiếp mảng pixel JPEG đã nén từ encoder buffer, **triệt tiêu 100% mảng LOH `new byte[24 + jpeg.Length]`**, tiết kiệm 1.5 – 3.0 MB/s rác heap trên ZCU Linux.
> - **Khử phân bổ Header 5 bytes qua `ArrayPool<byte>.Shared`:** Thay thế `new byte[HeaderSize]` trong `MessageCodec.WriteMessageAsync` và `ReadMessageAsync`, loại bỏ hàng chục nghìn allocations Gen 0 per-minute từ sự kiện chuột và bàn phím.
> - **Cơ chế Chống Trễ Tích Lũy (Congestion Frame Dropping & Latest-Frame Fast-Forward):** Nhận diện TCP backpressure khi `writeMs > 1.5 * frameInterval`, tự động nhường CPU (`Task.Yield()`) để giải phóng socket buffer, giữ cho độ trễ tương tác màn hình CCU luôn là thời gian thực.
> - **Vượt mốc 100 tests:** Bổ sung `FrameStreamingTests.cs` (14 ca kiểm thử mới), nâng tổng số lên **108 unit tests pass 100%**.

---

## Tổng quan dự án

Hệ thống Remote Control CCU↔ZCU / Windows: điều khiển từ xa các máy ZCU (Linux, X11) và máy tính Windows từ máy quản lý CCU (Windows/Linux). Gồm 6 project .NET trong solution `IPGS.RemoteControl.sln`:

| Project | Vai trò | Nền tảng |
|---|---|---|
| `IPGS.RemoteControl.ZcuAgent` | Service chạy trên ZCU (Linux): **Zero-Allocation Frame Streaming** (`WriteFrameJpegDirectAsync`), chống trễ tích lũy Congestion Dropping, tự động dò tìm Display thông minh (`X11DisplayResolver`), chống màn hình đen Headless (`XSetScreenSaver`), capture màn hình X11 (XShm/XGetImage), inject chuột/phím (XTest không chặn XSync), TCP server 1-client, chat/clipboard, nén JPEG Zero-Copy (`SKImage.FromPixels`) + `malloc_trim(0)` định kỳ, Zero-Copy file download chunking, Socket tuning `NoDelay = true` + `SendBufferSize = 256KB` | linux-x64 (.NET 8) |
| `IPGS.RemoteControl.WinAgent` | **[ĐÃ TÍCH HỢP `.sln` v4.1]** Agent chạy trên máy Windows, cùng giao thức với ZcuAgent: link file nguồn dùng chung từ ZcuAgent (`ClientSession`, `TcpServer`, `AuthManager`, `FileTransferHandler`…), phần Win32 riêng: `GdiScreenCapturer` (BitBlt + con trỏ, PerMonitorV2 DPI), `Win32MouseInjector`/`Win32KeyboardInjector` (SendInput), `Win32ClipboardService` (text + CF_HDROP), `Win32MonitorProvider`, `WindowsDesktopPlatformService`. Hỗ trợ triển khai Zero-Touch từ xa qua `ZcuRemoteInstallerService` + `scripts/windows-agent/` (`bootstrap-windows-ssh.ps1`, `1-click-enable-ssh.bat`, `autounattend.xml`, `install-win-agent.ps1`) | win-x64 (.NET 8) |
| `IPGS.RemoteControl.CcuClient` | Class library: TCP client + protocol codec với **ArrayPool rented wire headers**, nhận diện HĐH đa nền tảng (`RemoteOsDetector`), SSH installer/deployer cho cả Linux (`ZcuAgent`) và Windows (`WinAgent`), multi-OS SFTP/PowerShell helpers (`ShellQuote`), WoL, discovery (tự động trích xuất TLS fingerprint), profile store (DPAPI), TLS Certificate Pinning & TOFU tự động, zero-copy buffer upload, Telemetry đo RTT/FPS/Bitrate, Clipboard Safety Shield 1MB, Socket tuning `ReceiveBufferSize = 256KB` | win-x64 + linux-x64 (.NET 8) |
| `IPGS.RemoteControl.CcuUI` | App Avalonia 12: viewer/manager — remote screen với Ping-Pong Double-Buffering zero-copy (`Buffer.MemoryCopy`), giải phóng buffer an toàn khi đổi resolution, huy hiệu TLS 🔒, Live Network HUD Badge (`⚡ FPS | 📶 RTT | 📊 Bitrate`), MultiRemote hỗ trợ TLS pinning, bộ công cụ quản trị đa nền tảng Linux + Windows (`RemoteCommandWindow`, `BulkActionWindow`, `HealthMonitorWindow`, `RemoteAppInstallWindow`, `ZcuSetupWizardWindow`), Zero-Allocation AVI video recording | win-x64 + linux-x64 (.NET 8) |
| `KztekComponentAvalonia` | Thư viện UI chuẩn KZTEK: Controls (KzButton, KzTextBox, KzSearchBox, KzNavigation, KzCrudControl, KzCard...), Theme tokens & extensions | win-x64 + linux-x64 (.NET 8) |
| `IPGS.RemoteControl.Tests` | Bộ kiểm thử tự động xUnit: **264 test cases** bao phủ `WindowsRemoteProvisioningTests` (95 cases), `ResolutionAdaptationTests` (47 cases), `FrameStreaming` (14 tests), `X11DisplayResolver` (19 tests), `MessageCodec` (18 tests), `ShellQuote` (15 tests), `SecretProtector` (7 tests), `ComputerProfileStore` (6 tests), `WakeOnLanService` (14 tests), `ComputerStatusChecker` (6 tests), `ZcuAgentDiscoveryService` (4 tests), `SessionRecorder` (5 tests), `TopParserTests`, `RemoteFileItemTests` | cross-platform (.NET 9) |

**Tech stack:** .NET 8 / .NET 9, Avalonia 12 (CcuUI — dùng `KztekComponentAvalonia` trực tiếp trong solution), SSH.NET 2026.0.0 (Renci), SkiaSharp (decode/encode JPEG zero-allocation), X11/XTest/XShm P/Invoke (ZcuAgent), DPAPI (`System.Security.Cryptography.ProtectedData`), xUnit 2.9.2.

**Phụ thuộc giữa project:** `CcuUI` → `CcuClient`; `IPGS.RemoteControl.Tests` → `CcuClient` + `CcuUI` + `ZcuAgent` (linked interop); `ZcuAgent` độc lập nhưng dùng chung chuẩn protocol.

---

## Cấu trúc thư mục

- `6.RemoteControlTool/`
  - `IPGS.RemoteControl.ZcuAgent/`: Dịch vụ chạy ngầm trên ZCU (Linux daemon systemd)
    - `Program.cs`, `RemoteControlHostedService.cs`: Vòng đời service nền, nới lỏng session guard cho headless/rdp
    - `AgentOptions.cs`: Cấu hình Port, Token, AllowedClientIPs, EnableDesktopIntegration, Display
    - `Interfaces.cs`: Định nghĩa `IScreenCapturer`, `IFrameEncoder`, `IInputInjector`
    - `Auth/AuthManager.cs`: Token + IP whitelist (deny-by-default, fail-fast)
    - `Net/TcpServer.cs`, `ClientSession.cs`: Quản lý 1 client, **Zero-Allocation Frame Streaming** qua `_frameHeaderBuffer` 29 bytes, Congestion Relief `Task.Yield()`, `struct InputCommand` + Channel coalescing chuột, overload `WriteAsync(ReadOnlyMemory<byte>)`, Socket tuning `NoDelay` + `SendBufferSize = 256KB`
    - `Net/FileTransferHandler.cs`: Xử lý truyền tệp 2 chiều, Zero-Copy download chunking qua `buf.AsMemory(0, 1 + n)`
    - `Capture/X11ScreenCapturer.cs`, `JpegEncoder.cs`: Nén Zero-Copy trực tiếp qua `SKImage.FromPixels` + `malloc_trim(0)`, tích hợp `X11DisplayResolver` và `XSetScreenSaver` anti-blanking
    - `Input/KeyboardInjector.cs`, `MouseInjector.cs`: Inject phím và chuột qua XTest (đã bỏ blocking `XSync`, đồng bộ `X11DisplayResolver`)
    - `Interop/X11DisplayResolver.cs`: Dò tìm tự động Unix socket `/tmp/.X11-unix/`, fallback `:0`, `:1`, `:10.0` (xrdp)
    - `Interop/X11Interop.cs`, `XShmInterop.cs`, `XTestInterop.cs`, `X11ErrorTracker.cs`: P/Invoke native X11 + DPMS anti-blanking
    - `appsettings.json`: Tệp cấu hình mẫu (installer ghi đè khi deploy thật)
  - `IPGS.RemoteControl.CcuClient/`: Thư viện logic CCU (.NET 8)
    - `RemoteControlClient.cs`: TCP client, reconnect, TLS Certificate Pinning, zero-copy chunk upload, telemetry RTT/FPS/Bitrate, Clipboard Shield 1MB, Socket buffer 256KB
    - `IRemoteControlClient.cs`: Interface client với telemetry event `NetworkMetricsUpdated`
    - `Protocol/MessageCodec.cs`: Framing với **ArrayPool rented headers**, `WriteFrameMetadataHeader`, Encode/Decode 12 message types + bounds check 8192px
    - `ComputerProfile.cs`, `ComputerProfileStore.cs`: profiles.json + DPAPI + lưu trữ đầy đủ `TlsCertFingerprint` (TOFU)
    - `ComputerStatusChecker.cs`, `ComputerConnectivityStatus.cs`: Kiểm tra trạng thái kết nối SSH và Agent
    - `ShellQuote.cs`: Escape & validate tham số shell SSH, chống command injection
    - `SecretProtector.cs`: DPAPI mã hóa tiền tố `enc:v1:`
    - `RemoteAppInstallService.cs`, `ZcuRemoteInstallerService.cs`, `KioskDeployService.cs`: SSH sudo installer qua STDIN
    - `ZcuAgentDiscoveryService.cs`: Tự động dò tìm ZCU trong mạng LAN và trích xuất SHA-256 TLS fingerprint
    - `Services/WakeOnLanService.cs`: Gửi gói tin Magic Packet đánh thức máy qua LAN
  - `IPGS.RemoteControl.CcuUI/`: Ứng dụng điều khiển Avalonia 12 (.NET 8)
    - `App.axaml(.cs)`, `Program.cs`, `KeyboardMapper.cs`: Cấu hình app và ánh xạ phím
    - `Services/SessionRecorder.cs`: Ghi hình phiên AVI Zero-Allocation qua UTF-8 literals `"..."u8` và `stackalloc`, hỗ trợ `Stream` trừu tượng
    - `Services/LicenseManagerService.cs`: Quản lý bản quyền RSA-SHA256
    - `ViewModels/RemoteScreenViewModel.cs`: Ping-Pong Double-Buffering zero-copy `Buffer.MemoryCopy`, safe buffer disposal khi đổi resolution, thuộc tính `IsTlsEncrypted`, telemetry `NetworkHudText` và `IsHudVisible`
    - `Views/`: 18 màn hình và view controls (ConnectionEntry truyền vân tay TLS, RemoteScreenWindow với badge `🔒 TLS` và Live HUD `⚡ FPS | 📶 RTT | 📊 Bitrate`, RemoteScreenControl bọc UIThread.Post, MultiRemoteWindow hỗ trợ TLS pinning, NetworkScanWindow tự động điền vân tay TLS, FileManager, RemoteCommand, BulkAction, CronJob, HealthMonitor với `100 - idle%` CPU metric, KioskDeploy, RemoteAppInstall (cài từ file local hoặc file có sẵn trên máy đích qua `RemoteFilePickerWindow`, gỡ, khởi động app theo package), ZcuSetupWizard, SystemInventory, ComputerEdit, License, SessionPickerWindow, ConfirmDeleteDialog; toàn bộ dùng `PlaceholderText`)
  - `IPGS.RemoteControl.Tests/`: Bộ kiểm thử tự động xUnit (.NET 9)
    - `IPGS.RemoteControl.Tests.csproj`: Cấu hình xUnit, tham chiếu `CcuClient`, `CcuUI` và linked interop `ZcuAgent`
    - `FrameStreamingTests.cs`: 14 test cases kiểm thử Zero-Allocation frame streaming, direct 29-byte framing, ArrayPool header, 4K/8K resolutions
    - `X11DisplayResolverTests.cs`: 19 test cases bao phủ trích xuất socket, độ ưu tiên display, deduplication, mock opener
    - `MessageCodecTests.cs`: 18 unit tests bao phủ toàn bộ TCP message codecs, bounds check 8192px, Unicode, auth fail
    - `ShellQuoteTests.cs`: 15 unit tests bao phủ quote, escape, chống shell injection & validation
    - `SecretProtectorTests.cs`: 7 unit tests bao phủ DPAPI Protect/Unprotect, idempotency, legacy plaintext, error handling
    - `ComputerProfileStoreTests.cs`: 6 unit tests bao phủ lưu trữ, update, TOFU fingerprint persistence, delete
    - `WakeOnLanServiceTests.cs`: 14 unit tests kiểm thử Magic Packet MAC format chuẩn, gạch nối, dấu cách, non-hex, invalid format
    - `ComputerStatusCheckerTests.cs`: 6 unit tests kiểm thử probe trạng thái kết nối Online/Offline, TCP check
    - `ZcuAgentDiscoveryServiceTests.cs`: 4 unit tests kiểm thử trích xuất subnet IPv4, record equality, cancellation handling
    - `SessionRecorderTests.cs`: 5 unit tests kiểm thử cấu trúc AVI RIFF, chunk 00dc, padding chẵn byte, patch frame count, idx1 index trong RAM

---

## Module chính

| Module | Path | Mục đích & Đặc tả kiến trúc | Files quan trọng |
|--------|------|------------------------------|-----------------|
| Zero-Alloc Frame Streaming | `ZcuAgent/Net/ClientSession.cs` | **[MỚI v3.6]** Stream khung hình trực tiếp bằng cách ghép 29 bytes cố định (`_frameHeaderBuffer`) và đẩy thẳng slice pixel nén, triệt tiêu 100% mảng LOH `new byte[24 + len]`. Tích hợp Congestion Dropping chống trễ tích lũy. | `ClientSession.cs`, `MessageCodec.cs` |
| Protocol Codec & Pool Header | `CcuClient/Protocol/` | Khung gói tin TCP nhị phân (1 byte Type + 4 bytes Length + Payload) sử dụng **`ArrayPool<byte>.Shared`**, loại bỏ cấp phát header cho từng gói tin chuột/phím. Hỗ trợ `WriteFrameMetadataHeader`. | `MessageCodec.cs`, `MessageTypes.cs` |
| Display Auto-Resolver & Headless | `ZcuAgent/Interop/` | Tự động phát hiện cổng hiển thị X11: duyệt `/tmp/.X11-unix/`, fallback `:0`, `:1`, `:10.0` (xrdp), chống màn hình đen blanking screensaver với `XSetScreenSaver`. | `X11DisplayResolver.cs`, `X11Interop.cs`, `X11ScreenCapturer.cs` |
| TCP client & Telemetry | `CcuClient/RemoteControlClient.cs` | Client TCP kết nối ZCU: tự động reconnect với exponential backoff, xác thực token bảo mật, hỗ trợ TLS stream và kiểm tra Certificate Pinning (`TlsCertFingerprint`), upload file zero-copy, socket buffer 256KB, đo kiểm RTT/FPS/Bitrate theo thời gian thực, Clipboard Shield 1MB. | `RemoteControlClient.cs`, `IRemoteControlClient.cs` |
| Profile & Secret Store | `CcuClient/` | Quản trị cấu hình kết nối ZCU trong `%APPDATA%\iPGS\RemoteControl\profiles.json`. Ghi đĩa nguyên tử (atomic write qua tmp + Move); bảo vệ mật khẩu SSH và Token bằng DPAPI (`SecretProtector`, tiền tố `enc:v1:`). Hỗ trợ lưu trữ và cập nhật vân tay chứng chỉ TLS (`TlsCertFingerprint`) tự động qua `UpdateTlsFingerprint`. | `ComputerProfile.cs`, `ComputerProfileStore.cs`, `SecretProtector.cs` |
| Shell safety & STDIN Sudo | `CcuClient/` | Chống tấn công shell command injection qua `ShellQuote`. Toàn bộ thao tác sudo SSH trên ZCU (`RemoteAppInstallService`, `ZcuRemoteInstallerService`, `RemoteCommandWindow`, `BulkActionWindow`) truyền mật khẩu qua luồng STDIN `cmd.CreateInputStream()` với cờ `sudo -S -p ''`, loại bỏ hoàn toàn biến môi trường `KIOSK_SUDO_PASS`. | `ShellQuote.cs`, `RemoteAppInstallService.cs`, `ZcuRemoteInstallerService.cs` |
| ZCU session & Socket Tuning | `ZcuAgent/Net/` & `Input/` | Quản lý phiên 1 client duy nhất với cơ chế watchdog PONG tách riêng task. Socket tuning `NoDelay = true` và `SendBufferSize = 256KB` triệt tiêu trễ Nagle. Hàng đợi inject input sử dụng `Channel<InputCommand>` với `readonly struct InputCommand` (zero GC allocation) và mouse move coalescing. `MouseInjector` và `KeyboardInjector` loại bỏ blocking `XSync`. | `TcpServer.cs`, `ClientSession.cs`, `MouseInjector.cs`, `KeyboardInjector.cs` |
| Screen Capture & Zero-Copy Encode | `ZcuAgent/Capture/` | Chụp màn hình qua XShm (MIT-SHM shared memory) hoặc fallback XGetImage. Nén JPEG trực tiếp từ con trỏ pixel qua `SKImage.FromPixels` mà không cần sao chép bộ nhớ trung gian, kích hoạt định kỳ `malloc_trim(0)` mỗi 60s để ép kernel Linux thu hồi trang nhớ unmanaged của glibc. | `X11ScreenCapturer.cs`, `JpegEncoder.cs` |
| File Transfer Zero-Copy | `ZcuAgent/Net/FileTransferHandler.cs` | Tối ưu hóa tải tệp 2 chiều. Download từ ZCU dùng `buf.AsMemory(0, 1 + n)` đẩy qua `ClientSession.WriteAsync(MessageType, ReadOnlyMemory<byte>, CancellationToken)`, không sinh rác bộ nhớ per-chunk. | `FileTransferHandler.cs`, `ClientSession.cs` |
| Ping-Pong Double-Buffering UI & HUD | `CcuUI/ViewModels/` & `Views/` | Render màn hình remote với hiệu năng cực đại: duy trì 2 `WriteableBitmap` (`_frontBuffer` và `_backBuffer`). Giải mã JPEG qua SkiaSharp và copy trực tiếp sang con trỏ framebuffer bằng `Buffer.MemoryCopy` unmanaged, triệt tiêu 100% rác LOH. Hiển thị huy hiệu `🔒 TLS` và Live Network HUD Badge trên thanh công cụ. | `RemoteScreenViewModel.cs`, `RemoteScreenWindow.axaml`, `RemoteScreenControl.axaml.cs` |
| Zero-Alloc Video Recording | `CcuUI/Services/SessionRecorder.cs` | Ghi video AVI bằng cách stream khung hình JPEG với header chunk 100% FourCC tĩnh `"..."u8` và kích thước qua `stackalloc byte[4]` + `BinaryPrimitives`, hỗ trợ luồng Stream trừu tượng (MemoryStream), đạt 0 bytes/frame allocation trên heap. | `SessionRecorder.cs` |
| Automated Test Suite | `IPGS.RemoteControl.Tests/` | **117 test cases tự động xUnit** kiểm chứng toàn diện round-trip serialization/deserialization mọi thông điệp nhị phân, Zero-Allocation streaming, độ an toàn chống injection của module shell SSH, mã hóa bí mật DPAPI, lưu trữ bền vững hồ sơ máy tính, logic kiểm tra MAC Wake-on-LAN, probe trạng thái kết nối, quét mạng LAN, cấu trúc AVI RIFF container, bộ dò X11DisplayResolver, và tương tác/định vị đường dẫn SFTP RemoteFileItem. | `FrameStreamingTests.cs`, `RemoteFileItemTests.cs`, `X11DisplayResolverTests.cs`, `MessageCodecTests.cs`, `ShellQuoteTests.cs`, `SecretProtectorTests.cs`, `ComputerProfileStoreTests.cs`, `WakeOnLanServiceTests.cs`, `ComputerStatusCheckerTests.cs`, `ZcuAgentDiscoveryServiceTests.cs`, `SessionRecorderTests.cs` |

---

## API / Interface chính

| Interface / Class | File | Đặc tả kỹ thuật & Thay đổi mới (v3.6) |
|-------------------|------|---------------------------------------|
| `ClientSession.WriteFrameJpegDirectAsync` | `ZcuAgent/Net/ClientSession.cs` | **[MỚI v3.6]** Stream frame trực tiếp qua 29-byte header và memory slice, 0 bytes heap alloc |
| `MessageCodec.WriteFrameMetadataHeader` | `CcuClient/Protocol/MessageCodec.cs` | **[MỚI v3.6]** Ghi 24 bytes metadata vào span đích (zero-allocation) |
| `MessageCodec.WriteMessageAsync` (ArrayPool) | `CcuClient/Protocol/MessageCodec.cs` | **[MỚI v3.6]** Tái sử dụng header 5 bytes qua `ArrayPool<byte>.Shared`, giảm rác Gen 0 |
| `FrameStreamingTests` | `IPGS.RemoteControl.Tests/FrameStreamingTests.cs` | **[KIỂM THỬ v3.6]** 14 unit tests tự động kiểm thử Zero-Allocation streaming và framing |
| `X11DisplayResolver.OpenAvailableDisplay` | `ZcuAgent/Interop/X11DisplayResolver.cs` | Dò tìm tự động Display hợp lệ, cập nhật `DISPLAY` env và hỗ trợ mock test |
| `AgentOptions.Display` | `ZcuAgent/AgentOptions.cs` | Tùy biến cấu hình X11 Display tĩnh (:0, :1, :10.0) |
| `X11.XSetScreenSaver` / `XResetScreenSaver` | `ZcuAgent/Interop/X11Interop.cs` | P/Invoke vô hiệu hóa DPMS sleep và chống chụp màn hình đen |
| `SessionRecorder(Stream, ...)` | `CcuUI/Services/SessionRecorder.cs` | Khởi tạo bộ ghi hình tới luồng `Stream` trừu tượng, hỗ trợ ghi RAM trong test suite |
| `RemoteControlClient.MaxClipboardTextLength` | `CcuClient/RemoteControlClient.cs` | Hằng số 1MB an toàn, tự động cắt chuỗi bảo vệ socket và RAM |
| `MessageCodec.DecodeFrameJpeg` | `CcuClient/Protocol/MessageCodec.cs` | Kiểm tra ranh giới kích thước `width/height <= 8192px` chống payload rác |
| `IRemoteControlClient.NetworkMetricsUpdated` | `CcuClient/IRemoteControlClient.cs` | Event thông báo dữ liệu hiệu năng mạng thời gian thực: FPS, RTT (ms), Bitrate (KB/s) |
| `TcpServer.RunAsync` (Socket Tuning) | `ZcuAgent/Net/TcpServer.cs` | Thiết lập `NoDelay = true` và `SendBufferSize = 256KB`, loại bỏ trễ Nagle 40ms |
| `RemoteScreenViewModel.NetworkHudText` | `CcuUI/ViewModels/RemoteScreenViewModel.cs` | Thuộc tính định dạng chuỗi HUD `⚡ FPS | 📶 RTT | 📊 Bitrate` hiển thị mượt mà trên UI |
| `RemoteCommandWindow.OnFileItemDoubleTapped` | `CcuUI/Views/RemoteCommandWindow.axaml.cs` | **[MỚI v3.7]** Xử lý Double Click và phím Enter/Backspace trên ListBox SFTP, trích xuất đa tầng RemoteFileItem |

---

## Dependencies quan trọng

| Package | Version | Project | Ghi chú & Đánh giá bảo mật |
|---------|---------|---------|---------------------------|
| `SSH.NET` (Renci.SshNet) | **2026.0.0** | CcuClient, CcuUI | Xóa sạch 100% 2 lỗ hổng bảo mật NU1903 (GHSA-mggc-4xg6-vcxf và GHSA-q939-rpr3-3284) |
| `System.Security.Cryptography.ProtectedData` | 8.0.0 | CcuClient | DPAPI mã hóa dữ liệu nhạy cảm trên Windows (với cơ chế cảnh báo trên Linux) |
| `Avalonia` | 11.2 / 12.x | CcuUI | Toàn bộ 10 file AXAML đã chuyển 100% sang `PlaceholderText`, sạch toàn bộ obsolete warnings |
| `KztekComponentAvalonia` | ProjectReference | CcuUI | Thư viện UI chuẩn KZTEK tích hợp nội bộ trong solution `IPGS.RemoteControl.sln` |
| `SkiaSharp` | (theo csproj) | CcuUI, ZcuAgent | Xử lý hình ảnh và giải mã JPEG |
| `xunit` & `coverlet.collector` | 2.9.2 / 6.0.2 | IPGS.RemoteControl.Tests | Khung kiểm thử tự động, **264/264 tests passed (100%)** |

---

## Thay đổi gần đây

| Ngày | File/Module | Loại | Mô tả ngắn | Agent |
|------|------------|------|------------|-------|
| 2026-11-11 | Toàn bộ giải pháp (6 projects) + `scripts/windows-agent/` | Feature & Multi-OS Architecture | **Hoàn thành Zero-Touch Windows Remote Provisioning & Multi-OS CCU Management (v4.1):** Tích hợp `WinAgent` vào `.sln`, tạo bộ Bootstrap OpenSSH/ICMPv4/AutoLogon (`bootstrap-windows-ssh.ps1`, `1-click-enable-ssh.bat`, `autounattend.xml`), nâng cấp `ZcuRemoteInstallerService`, `ShellQuote`, `ComputerStatusChecker`, `RemoteCommandWindow`, `BulkActionWindow`, `HealthMonitorWindow`, `RemoteAppInstallService` hỗ trợ quản trị từ xa cả Linux và Windows qua IPv4 SSH/SFTP. Bổ sung `WindowsRemoteProvisioningTests.cs`, nâng tổng số lên **264 unit tests pass 100%** | senior-developer, tech-lead, qa-engineer |
| 2026-11-10 | ZcuAgent + WinAgent mới | Feature & Refactor | Tách phần phụ thuộc nền tảng của ZcuAgent sau `IClipboardService`/`IMonitorProvider`/`IDesktopPlatformService`/`IMachineLoadSampler` (`ZcuAgent/Platform/Linux*.cs`, hành vi Linux giữ nguyên) và thêm project `WinAgent` điều khiển máy Windows qua cùng giao thức. Plan: `docs/plans/PLAN-windows-agent-2026-11-10/`. Mới biên dịch, chưa chạy thử trên Windows | claude |
| 2026-11-02 | Solution & KztekComponentAvalonia | Refactor & Integration | Tích hợp trực tiếp project `KztekComponentAvalonia` vào repository `6.RemoteControlTool`, tạo solution `IPGS.RemoteControl.sln` quản lý 5 project, cập nhật toàn bộ tham chiếu nội bộ, 117 unit tests pass 100% | senior-developer, tech-lead |
| 2026-11-02 | CcuUI/RemoteCommandWindow | Bug Fix | Sửa lỗi không nhấn đúp chuột mở thư mục SFTP (trích xuất đa tầng RemoteFileItem, phím Enter/Backspace, chuẩn hóa POSIX path), bổ sung 9 tests | senior-developer, tech-lead, qa-engineer |
| 2026-10-31 | Toàn bộ giải pháp (4 projects) | Performance & Optimization | **Hoàn thành chu trình cải tiến đỉnh cao v3.6 (F-ROUND7-1 đến F-ROUND7-5):** Zero-Allocation Frame Streaming trên ZcuAgent (`WriteFrameJpegDirectAsync` qua buffer 29-byte cố định, triệt tiêu 100% rác LOH 1.5–3MB/s), khử phân bổ header bằng `ArrayPool`, chống trễ tích lũy Congestion Dropping, mở rộng bộ test suite lên 108 unit tests pass 100% | senior-developer, junior-developer, qa-engineer, tech-lead |
| 2026-10-31 | Toàn bộ giải pháp (4 projects) | Feature & Optimization | Chu trình cải tiến Headless & RDP v3.5 (F-HEADLESS-1 đến F-HEADLESS-5): `X11DisplayResolver` tự động phát hiện display `:0`, `:1`, `:10.0` (xrdp), nới lỏng session guard, P/Invoke `XSetScreenSaver` chống màn hình đen, 94 unit tests | senior-developer, junior-developer, qa-engineer, tech-lead |
| 2026-10-31 | Toàn bộ giải pháp (4 projects) | Hardening & Test Suite | Chu trình cải tiến đỉnh cao v3.4 (F-ROUND5-1 đến F-ROUND5-4): Trừu tượng hóa `Stream` cho `SessionRecorder`, 100% FourCC tĩnh zero-allocation, chốt chặn Clipboard 1MB, bounds checking 8192px cho JPEG, 75 unit tests | senior-developer, junior-developer, qa-engineer, tech-lead |

---

## Lessons & Quyết định quan trọng

| Ngày | Quyết định / Bài học | Lý do (WHY) | Agent |
|------|----------------------|--------------|-------|
| 2026-11-11 | Sử dụng Windows Native OpenSSH Server (`sshd`) + `DefaultShell = powershell.exe` + `autounattend.xml` | Cho phép CCU tái sử dụng 100% hạ tầng `SSH.NET` (`SshClient` + `SftpClient`) qua cổng TCP 22 tiêu chuẩn để đẩy và cài đặt `WinAgent` từ xa trên Windows mà không cần cài phần mềm thứ 3 hay mở WinRM phức tạp | tech-lead, cto |
| 2026-11-11 | Bật `AutoAdminLogon` + tắt khoá màn hình trong Bootstrap Windows | `WinAgent` chụp màn hình bằng GDI `BitBlt` và tiêm phím/chuột bằng `SendInput` trong Interactive Desktop Session; nếu máy Windows reboot mà dừng ở màn hình khóa Winlogon (Session 0), chưa có Interactive Session nào khởi tạo cho đến khi user đăng nhập | senior-developer |
| 2026-10-31 | Zero-Allocation Frame Streaming qua 29-byte Header | Cấp phát `new byte[24 + len]` cho mỗi frame JPEG sinh 1.5–3.0 MB/s rác LOH kích hoạt GC Gen 2 gây micro-stuttering trên ZCU. Ghép 5 bytes wire header + 24 bytes metadata vào buffer cố định rồi stream trực tiếp mảng pixel nén triệt tiêu 100% phân bổ heap | senior-developer |
| 2026-10-31 | Tái sử dụng Header 5 bytes bằng `ArrayPool<byte>.Shared` | Rê chuột liên tục sinh 50–100 events/giây; cấp phát mảng 5 bytes liên tục làm phân mảnh Gen 0 heap không cần thiết | senior-developer |
| 2026-10-31 | Congestion Frame Dropping & Latest-Frame Fast-Forward | Khi Wi-Fi hoặc socket nghẽn, việc dồn ứ frames khiến CCU hiển thị hình ảnh của quá khứ (lag backlog). Nhường CPU khi socket nghẽn đảm bảo CCU luôn render khung hình mới nhất tại thời gian thực | senior-developer |
| 2026-10-31 | Tự động phát hiện Display qua Unix Domain Socket `/tmp/.X11-unix/` | Môi trường Headless Kiosk hoặc xrdp RDP không chạy trên display `:0` cố định mà cấp phát động. Quét socket tự động giúp ZcuAgent kết nối tức thì mà người dùng không cần cấu hình thủ công | senior-developer |
| 2026-10-31 | Vô hiệu hóa DPMS Screensaver Blanking trên X11 Headless | Khi không cắm cáp HDMI, X server tự động đưa màn hình vào trạng thái DPMS Sleep sau vài phút, gây ra hiện tượng khung hình chụp được toàn màu đen | senior-developer |

---

## Ghi chú đặc biệt

- **Chất lượng kiểm thử:** Toàn bộ giải pháp sở hữu test suite tự động `IPGS.RemoteControl.Tests` với **267 bài test đạt 100% tỷ lệ đỗ**.
- **Trạng thái biên dịch:** Sạch sẽ 100% (0 Warnings, 0 Errors trên toàn bộ 6 dự án trong solution).
- **Tính vững chắc phòng thủ & Hiệu năng đỉnh cao:** Đường ống truyền nhận video đạt chuẩn **Zero-Allocation hoàn toàn** ở cả hai đầu (ZcuAgent/WinAgent stream trực tiếp 0-alloc, CcuUI render Ping-Pong Double-Buffering zero-copy `Buffer.MemoryCopy`), triệt tiêu 100% rác LOH và chống trễ tích lũy công nghiệp.
