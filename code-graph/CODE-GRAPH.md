# CODE-GRAPH.md — Bản đồ codebase: Remote Control Tool (CCU ↔ ZCU)
**Cập nhật lần cuối:** 2026-10-31 | **Bởi:** documentation-writer | **Version:** 3.4

> File này được duy trì tự động bởi coding agents.
> **Đọc file này TRƯỚC khi đọc source code** để hiểu cấu trúc dự án mà không cần mở từng file.

> **Ghi chú version 3.4:** Hoàn thành chu trình cải tiến đỉnh cao Vòng 5 (F-ROUND5-1 đến F-ROUND5-4):
> - Tối ưu hóa toàn diện `SessionRecorder.cs`: Hỗ trợ constructor nhận `Stream` trừu tượng (cho phép ghi vào `MemoryStream` phục vụ kiểm thử trong RAM độc lập đĩa cứng); chuyển đổi 100% FourCC ("RIFF", "AVI ", "LIST", "hdrl", "avih", "strl", "strh", "vids", "MJPG", "strf", "movi", "idx1", "00dc") sang UTF-8 byte literals `"..."u8` zero-allocation.
> - Clipboard Safety Shield: Áp dụng ngưỡng cắt an toàn `MaxClipboardTextLength = 1MB` trong `RemoteControlClient.cs`, triệt tiêu nguy cơ tràn rác LOH và nghẽn luồng socket khi copy dữ liệu khổng lồ.
> - Boundary Hardening: Kiểm soát chặt chẽ kích thước khung hình `width/height <= 8192` trong `MessageCodec.DecodeFrameJpeg`, bảo vệ Skia pipeline khỏi payload rác.
> - Mở rộng Test Suite: Thêm `SessionRecorderTests` (5 tests) và mở rộng `MessageCodecTests` (thêm 5 tests), nâng tổng số lên **75 unit tests pass 100%**.

---

## Tổng quan dự án

Hệ thống Remote Control CCU↔ZCU: điều khiển từ xa các máy ZCU (Linux, X11) từ máy quản lý CCU (Windows/Linux). Gồm 4 project .NET:

| Project | Vai trò | Nền tảng |
|---|---|---|
| `IPGS.RemoteControl.ZcuAgent` | Service chạy trên ZCU (Linux): capture màn hình X11 (XShm/XGetImage), inject chuột/phím (XTest không chặn XSync), TCP server 1-client, chat/clipboard, nén JPEG Zero-Copy (`SKImage.FromPixels`) + `malloc_trim(0)` định kỳ, Zero-Copy file download chunking, Socket tuning `NoDelay = true` + `SendBufferSize = 256KB` | linux-x64 (.NET 8) |
| `IPGS.RemoteControl.CcuClient` | Class library: TCP client + protocol codec, SSH installer/deployer (STDIN sudo an toàn), WoL, discovery (tự động trích xuất TLS fingerprint), profile store (DPAPI), TLS Certificate Pinning & TOFU tự động, zero-copy buffer upload, Telemetry đo RTT/FPS/Bitrate, Clipboard Safety Shield 1MB, Socket tuning `ReceiveBufferSize = 256KB` | win-x64 + linux-x64 (.NET 8) |
| `IPGS.RemoteControl.CcuUI` | App Avalonia 12: viewer/manager — remote screen với Ping-Pong Double-Buffering zero-copy (`Buffer.MemoryCopy`), giải phóng buffer an toàn khi đổi resolution, huy hiệu TLS 🔒, Live Network HUD Badge (`⚡ FPS | 📶 RTT | 📊 Bitrate`), MultiRemote hỗ trợ TLS pinning, Zero-Allocation AVI video recording hỗ trợ Stream trừu tượng, 100% `PlaceholderText` sạch obsolete | win-x64 + linux-x64 (.NET 8) |
| `IPGS.RemoteControl.Tests` | Bộ kiểm thử tự động xUnit: 75 test cases bao phủ `MessageCodec` (18 tests), `ShellQuote` (15 tests), `SecretProtector` (7 tests), `ComputerProfileStore` (6 tests), `WakeOnLanService` (14 tests), `ComputerStatusChecker` (6 tests), `ZcuAgentDiscoveryService` (4 tests), `SessionRecorder` (5 tests) | cross-platform (.NET 9) |

**Tech stack:** .NET 8 / .NET 9, Avalonia 12 (CcuUI — dùng `KztekComponentAvalonia` từ `E:\KZTEK\Code_Git\5.BaseUI`), SSH.NET 2026.0.0 (Renci), SkiaSharp (decode/encode JPEG zero-allocation), X11/XTest/XShm P/Invoke (ZcuAgent), DPAPI (`System.Security.Cryptography.ProtectedData`), xUnit 2.9.2.

**Phụ thuộc giữa project:** `CcuUI` → `CcuClient`; `IPGS.RemoteControl.Tests` → `CcuClient` + `CcuUI` (qua `InternalsVisibleTo`); `ZcuAgent` độc lập nhưng dùng chung chuẩn protocol.

---

## Cấu trúc thư mục

- `6.RemoteControlTool/`
  - `IPGS.RemoteControl.ZcuAgent/`: Dịch vụ chạy ngầm trên ZCU (Linux daemon systemd)
    - `Program.cs`, `RemoteControlHostedService.cs`: Vòng đời service nền
    - `AgentOptions.cs`: Cấu hình Port, Token, AllowedClientIPs, EnableDesktopIntegration
    - `Interfaces.cs`: Định nghĩa `IScreenCapturer`, `IFrameEncoder`, `IInputInjector`
    - `Auth/AuthManager.cs`: Token + IP whitelist (deny-by-default, fail-fast)
    - `Net/TcpServer.cs`, `ClientSession.cs`: Quản lý 1 client, `struct InputCommand` + Channel coalescing chuột, overload `WriteAsync(ReadOnlyMemory<byte>)`, Socket tuning `NoDelay` + `SendBufferSize = 256KB`
    - `Net/FileTransferHandler.cs`: Xử lý truyền tệp 2 chiều, Zero-Copy download chunking qua `buf.AsMemory(0, 1 + n)`
    - `Capture/X11ScreenCapturer.cs`, `JpegEncoder.cs`: Nén Zero-Copy trực tiếp qua `SKImage.FromPixels` + `malloc_trim(0)`
    - `Input/KeyboardInjector.cs`, `MouseInjector.cs`: Inject phím và chuột qua XTest (đã bỏ blocking `XSync`)
    - `Interop/X11Interop.cs`, `XShmInterop.cs`, `XTestInterop.cs`, `X11ErrorTracker.cs`: P/Invoke native X11
    - `appsettings.json`: Tệp cấu hình mẫu (installer ghi đè khi deploy thật)
  - `IPGS.RemoteControl.CcuClient/`: Thư viện logic CCU (.NET 8)
    - `RemoteControlClient.cs`: TCP client, reconnect, TLS Certificate Pinning, zero-copy chunk upload, telemetry RTT/FPS/Bitrate, Clipboard Shield 1MB, Socket buffer 256KB
    - `IRemoteControlClient.cs`: Interface client với telemetry event `NetworkMetricsUpdated`
    - `Protocol/MessageCodec.cs`, `MessageTypes.cs`, `ProtocolException.cs`: Encode/Decode 12 message types + bounds check 8192px
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
    - `Views/`: 18 màn hình và view controls (ConnectionEntry truyền vân tay TLS, RemoteScreenWindow với badge `🔒 TLS` và Live HUD `⚡ FPS | 📶 RTT | 📊 Bitrate`, RemoteScreenControl bọc UIThread.Post, MultiRemoteWindow hỗ trợ TLS pinning, NetworkScanWindow tự động điền vân tay TLS, FileManager, RemoteCommand, BulkAction, CronJob, HealthMonitor với `100 - idle%` CPU metric, KioskDeploy, RemoteAppInstall, ZcuSetupWizard, SystemInventory, ComputerEdit, License, SessionPickerWindow, ConfirmDeleteDialog; toàn bộ dùng `PlaceholderText`)
  - `IPGS.RemoteControl.Tests/`: Bộ kiểm thử tự động xUnit (.NET 9)
    - `IPGS.RemoteControl.Tests.csproj`: Cấu hình xUnit, tham chiếu `CcuClient` và `CcuUI`
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
| Protocol codec & Hardening | `CcuClient/Protocol/` | Khung gói tin TCP nhị phân (1 byte Type + 4 bytes Length + Payload). Hỗ trợ đầy đủ 12 message types; kiểm soát biên độ nghiêm ngặt, văng `ProtocolException` khi dữ liệu bất thường hoặc kích thước khung hình vượt quá 8192px. | `MessageCodec.cs`, `MessageTypes.cs` |
| TCP client & Telemetry | `CcuClient/RemoteControlClient.cs` | Client TCP kết nối ZCU: tự động reconnect với exponential backoff, xác thực token bảo mật, hỗ trợ TLS stream và kiểm tra Certificate Pinning (`TlsCertFingerprint`), upload file zero-copy, socket buffer 256KB, đo kiểm RTT/FPS/Bitrate theo thời gian thực, Clipboard Shield 1MB. | `RemoteControlClient.cs`, `IRemoteControlClient.cs` |
| Profile & Secret Store | `CcuClient/` | Quản trị cấu hình kết nối ZCU trong `%APPDATA%\iPGS\RemoteControl\profiles.json`. Ghi đĩa nguyên tử (atomic write qua tmp + Move); bảo vệ mật khẩu SSH và Token bằng DPAPI (`SecretProtector`, tiền tố `enc:v1:`). Hỗ trợ lưu trữ và cập nhật vân tay chứng chỉ TLS (`TlsCertFingerprint`) tự động qua `UpdateTlsFingerprint`. | `ComputerProfile.cs`, `ComputerProfileStore.cs`, `SecretProtector.cs` |
| Shell safety & STDIN Sudo | `CcuClient/` | Chống tấn công shell command injection qua `ShellQuote`. Toàn bộ thao tác sudo SSH trên ZCU (`RemoteAppInstallService`, `ZcuRemoteInstallerService`, `RemoteCommandWindow`, `BulkActionWindow`) truyền mật khẩu qua luồng STDIN `cmd.CreateInputStream()` với cờ `sudo -S -p ''`, loại bỏ hoàn toàn biến môi trường `KIOSK_SUDO_PASS`. | `ShellQuote.cs`, `RemoteAppInstallService.cs`, `ZcuRemoteInstallerService.cs` |
| ZCU session & Socket Tuning | `ZcuAgent/Net/` & `Input/` | Quản lý phiên 1 client duy nhất với cơ chế watchdog PONG tách riêng task. Socket tuning `NoDelay = true` và `SendBufferSize = 256KB` triệt tiêu trễ Nagle. Hàng đợi inject input sử dụng `Channel<InputCommand>` với `readonly struct InputCommand` (zero GC allocation) và mouse move coalescing. `MouseInjector` và `KeyboardInjector` loại bỏ blocking `XSync`. | `TcpServer.cs`, `ClientSession.cs`, `MouseInjector.cs`, `KeyboardInjector.cs` |
| Screen Capture & Zero-Copy Encode | `ZcuAgent/Capture/` | Chụp màn hình qua XShm (MIT-SHM shared memory) hoặc fallback XGetImage. Nén JPEG trực tiếp từ con trỏ pixel qua `SKImage.FromPixels` mà không cần sao chép bộ nhớ trung gian, kích hoạt định kỳ `malloc_trim(0)` mỗi 60s để ép kernel Linux thu hồi trang nhớ unmanaged của glibc. | `X11ScreenCapturer.cs`, `JpegEncoder.cs` |
| File Transfer Zero-Copy | `ZcuAgent/Net/FileTransferHandler.cs` | Tối ưu hóa tải tệp 2 chiều. Download từ ZCU dùng `buf.AsMemory(0, 1 + n)` đẩy qua `ClientSession.WriteAsync(MessageType, ReadOnlyMemory<byte>, CancellationToken)`, không sinh rác bộ nhớ per-chunk. | `FileTransferHandler.cs`, `ClientSession.cs` |
| Ping-Pong Double-Buffering UI & HUD | `CcuUI/ViewModels/` & `Views/` | Render màn hình remote với hiệu năng cực đại: duy trì 2 `WriteableBitmap` (`_frontBuffer` và `_backBuffer`). Giải mã JPEG qua SkiaSharp và copy trực tiếp sang con trỏ framebuffer bằng `Buffer.MemoryCopy` unmanaged, triệt tiêu 100% rác LOH. Hiển thị huy hiệu `🔒 TLS` và Live Network HUD Badge trên thanh công cụ. | `RemoteScreenViewModel.cs`, `RemoteScreenWindow.axaml`, `RemoteScreenControl.axaml.cs` |
| Zero-Alloc Video Recording | `CcuUI/Services/SessionRecorder.cs` | Ghi video AVI bằng cách stream khung hình JPEG với header chunk 100% FourCC tĩnh `"..."u8` và kích thước qua `stackalloc byte[4]` + `BinaryPrimitives`, hỗ trợ luồng Stream trừu tượng (MemoryStream), đạt 0 bytes/frame allocation trên heap. | `SessionRecorder.cs` |
| Automated Test Suite | `IPGS.RemoteControl.Tests/` | 75 test cases tự động xUnit kiểm chứng toàn diện round-trip serialization/deserialization mọi thông điệp nhị phân, độ an toàn chống injection của module shell SSH, mã hóa bí mật DPAPI, lưu trữ bền vững hồ sơ máy tính, logic kiểm tra MAC Wake-on-LAN, probe trạng thái kết nối, quét mạng LAN, và cấu trúc AVI RIFF container. | `MessageCodecTests.cs`, `ShellQuoteTests.cs`, `SecretProtectorTests.cs`, `ComputerProfileStoreTests.cs`, `WakeOnLanServiceTests.cs`, `ComputerStatusCheckerTests.cs`, `ZcuAgentDiscoveryServiceTests.cs`, `SessionRecorderTests.cs` |

---

## API / Interface chính

| Interface / Class | File | Đặc tả kỹ thuật & Thay đổi mới (v3.4) |
|-------------------|------|---------------------------------------|
| `SessionRecorder(Stream, ...)` | `CcuUI/Services/SessionRecorder.cs` | **[MỚI v3.4]** Khởi tạo bộ ghi hình tới luồng `Stream` trừu tượng, hỗ trợ ghi RAM trong test suite |
| `RemoteControlClient.MaxClipboardTextLength` | `CcuClient/RemoteControlClient.cs` | **[BẢO MẬT v3.4]** Hằng số 1MB an toàn, tự động cắt chuỗi bảo vệ socket và RAM |
| `MessageCodec.DecodeFrameJpeg` | `CcuClient/Protocol/MessageCodec.cs` | **[BẢO VỆ v3.4]** Kiểm tra ranh giới kích thước `width/height <= 8192px` chống payload rác |
| `SessionRecorderTests` | `IPGS.RemoteControl.Tests/SessionRecorderTests.cs` | **[KIỂM THỬ v3.4]** 5 unit tests tự động kiểm tra cấu trúc tệp AVI, chunk 00dc và index idx1 |
| `IRemoteControlClient.NetworkMetricsUpdated` | `CcuClient/IRemoteControlClient.cs` | Event thông báo dữ liệu hiệu năng mạng thời gian thực: FPS, RTT (ms), Bitrate (KB/s) |
| `TcpServer.RunAsync` (Socket Tuning) | `ZcuAgent/Net/TcpServer.cs` | Thiết lập `NoDelay = true` và `SendBufferSize = 256KB`, loại bỏ trễ Nagle 40ms |
| `RemoteScreenViewModel.NetworkHudText` | `CcuUI/ViewModels/RemoteScreenViewModel.cs` | Thuộc tính định dạng chuỗi HUD `⚡ FPS | 📶 RTT | 📊 Bitrate` hiển thị mượt mà trên UI |

---

## Dependencies quan trọng

| Package | Version | Project | Ghi chú & Đánh giá bảo mật |
|---------|---------|---------|---------------------------|
| `SSH.NET` (Renci.SshNet) | **2026.0.0** | CcuClient, CcuUI | Xóa sạch 100% 2 lỗ hổng bảo mật NU1903 (GHSA-mggc-4xg6-vcxf và GHSA-q939-rpr3-3284) |
| `System.Security.Cryptography.ProtectedData` | 8.0.0 | CcuClient | DPAPI mã hóa dữ liệu nhạy cảm trên Windows (với cơ chế cảnh báo trên Linux) |
| `Avalonia` | 11.2 / 12.x | CcuUI | Toàn bộ 10 file AXAML đã chuyển 100% sang `PlaceholderText`, sạch toàn bộ obsolete warnings |
| `KztekComponentAvalonia` | ProjectReference | CcuUI | Thư viện UI chuẩn KZTEK từ `E:\KZTEK\Code_Git\5.BaseUI` |
| `SkiaSharp` | (theo csproj) | CcuUI, ZcuAgent | Xử lý hình ảnh và giải mã JPEG |
| `xunit` & `coverlet.collector` | 2.9.2 / 6.0.2 | IPGS.RemoteControl.Tests | Khung kiểm thử tự động, **75/75 tests passed (100%)** |

---

## Thay đổi gần đây

| Ngày | File/Module | Loại | Mô tả ngắn | Agent |
|------|------------|------|------------|-------|
| 2026-10-31 | Toàn bộ giải pháp (4 projects) | Hardening & Test Suite | **Hoàn thành chu trình cải tiến đỉnh cao v3.4 (F-ROUND5-1 đến F-ROUND5-4):** Trừu tượng hóa `Stream` cho `SessionRecorder`, 100% FourCC tĩnh zero-allocation, chốt chặn Clipboard 1MB, bounds checking 8192px cho JPEG, mở rộng bộ test suite lên 75 unit tests pass 100% | senior-developer, junior-developer, qa-engineer, tech-lead |
| 2026-10-31 | Toàn bộ giải pháp (4 projects) | Feature & Performance | Chu trình cải tiến đẳng cấp công nghiệp v3.3 (F-ROUND4-1 đến F-ROUND4-4): Telemetry thời gian thực (FPS, RTT, Bitrate), Live HUD Badge, Socket tuning NoDelay/256KB, 65 unit tests | senior-developer, junior-developer, qa-engineer, tech-lead |
| 2026-10-31 | Toàn bộ giải pháp (4 projects) | Refactor & Performance | Chu trình cải tiến toàn diện v3.2 (F-ROUND3-1 đến F-ROUND3-5): Zero-copy download chunking trong `FileTransferHandler.cs`, zero-alloc AVI recording, MultiRemote TLS Pinning, 55 unit tests | senior-developer, junior-developer, qa-engineer, tech-lead |

---

## Lessons & Quyết định quan trọng

| Ngày | Quyết định / Bài học | Lý do (WHY) | Agent |
|------|----------------------|--------------|-------|
| 2026-10-31 | Chốt chặn an toàn Clipboard văn bản 1MB | Ngăn chặn việc người dùng vô tình copy tệp log khổng lồ làm nghẽn luồng socket TCP và kích hoạt Full GC Gen 2 gây đứng hình ứng dụng | senior-developer |
| 2026-10-31 | Trừu tượng hóa Stream cho AVI container | Cho phép kiểm thử tự động cấu trúc nhị phân của video container trong RAM (`MemoryStream`) mà không tạo file rác trên ổ cứng | senior-developer |
| 2026-10-31 | Socket Tuning `NoDelay = true` và Buffer 256KB | Thuật toán Nagle mặc định gom gói tin gây trễ 40ms; buffer kernel nhỏ làm nghẽn burst frame khi màn hình chuyển động nhanh. Cấu hình NoDelay và mở rộng buffer 256KB triệt tiêu nghẽn và đưa RTT về mức tối ưu | senior-developer |
| 2026-10-31 | Đo kiểm RTT qua Nonce Match trong PING/PONG | So khớp chính xác nonce gửi và nhận giúp tính toán độ trễ RTT chính xác từng mili-giây mà không bị ảnh hưởng bởi các gói tin trễ của chu kỳ trước | senior-developer |

---

## Ghi chú đặc biệt

- **Chất lượng kiểm thử:** Toàn bộ giải pháp sở hữu test suite tự động `IPGS.RemoteControl.Tests` với **75 bài test đạt 100% tỷ lệ đỗ**.
- **Trạng thái biên dịch:** Sạch sẽ 100% (0 Errors, 0 Warnings trên cả 4 dự án).
- **Tính vững chắc phòng thủ:** Chốt chặn toàn diện từ tầng mạng, buffer socket, giải mã nhị phân đến xử lý văn bản clipboard.
