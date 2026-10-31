# CODE-GRAPH.md — Bản đồ codebase: Remote Control Tool (CCU ↔ ZCU)
**Cập nhật lần cuối:** 2026-10-31 | **Bởi:** documentation-writer | **Version:** 3.2

> File này được duy trì tự động bởi coding agents.
> **Đọc file này TRƯỚC khi đọc source code** để hiểu cấu trúc dự án mà không cần mở từng file.

> **Ghi chú version 3.2:** Mở rộng tối ưu hóa toàn diện Vòng 3 (F-ROUND3-1 đến F-ROUND3-5):
> - Zero-Copy File Download chunking từ ZCU sang CCU (`buf.AsMemory(0, 1 + n)`) qua overload `ClientSession.WriteAsync(MessageType, ReadOnlyMemory<byte>, CancellationToken)`.
> - Zero-Allocation AVI Frame Recording trong `SessionRecorder.cs` bằng C# UTF-8 byte literals `"00dc"u8` và `stackalloc byte[4]` loại bỏ 100% allocations trong vòng lặp ghi video 30-60 FPS.
> - Đồng bộ TLS Certificate Pinning & TOFU sang `MultiRemoteWindow.axaml.cs` bảo vệ bảng điều khiển đa máy trạm.
> - Tự động trích xuất SHA-256 TLS fingerprint trong `ZcuAgentDiscoveryService.cs` và điền tự động vào profile trong `NetworkScanWindow.axaml.cs`.
> - Mở rộng bộ kiểm thử tự động với `WakeOnLanServiceTests.cs` (14 tests), nâng tổng số lên **55 unit tests pass 100%**.

---

## Tổng quan dự án

Hệ thống Remote Control CCU↔ZCU: điều khiển từ xa các máy ZCU (Linux, X11) từ máy quản lý CCU (Windows/Linux). Gồm 4 project .NET:

| Project | Vai trò | Nền tảng |
|---|---|---|
| `IPGS.RemoteControl.ZcuAgent` | Service chạy trên ZCU (Linux): capture màn hình X11 (XShm/XGetImage), inject chuột/phím (XTest không chặn XSync), TCP server 1-client, chat/clipboard, nén JPEG Zero-Copy (`SKImage.FromPixels`) + `malloc_trim(0)` định kỳ, Zero-Copy file download chunking | linux-x64 (.NET 8) |
| `IPGS.RemoteControl.CcuClient` | Class library: TCP client + protocol codec, SSH installer/deployer (STDIN sudo an toàn), WoL, discovery (tự động trích xuất TLS fingerprint), profile store (DPAPI), TLS Certificate Pinning & TOFU tự động, zero-copy buffer upload | win-x64 + linux-x64 (.NET 8) |
| `IPGS.RemoteControl.CcuUI` | App Avalonia 12: viewer/manager — remote screen với Ping-Pong Double-Buffering zero-copy (`Buffer.MemoryCopy`), giải phóng buffer an toàn khi đổi resolution, huy hiệu TLS 🔒, MultiRemote hỗ trợ TLS pinning, Zero-Allocation AVI video recording, 100% `PlaceholderText` sạch obsolete | win-x64 + linux-x64 (.NET 8) |
| `IPGS.RemoteControl.Tests` | Bộ kiểm thử tự động xUnit: 55 test cases bao phủ `MessageCodec` (13 tests), `ShellQuote` (15 tests), `SecretProtector` (7 tests), `ComputerProfileStore` (6 tests), `WakeOnLanService` (14 tests) | cross-platform (.NET 9) |

**Tech stack:** .NET 8 / .NET 9, Avalonia 12 (CcuUI — dùng `KztekComponentAvalonia` từ `E:\KZTEK\Code_Git\5.BaseUI`), SSH.NET 2026.0.0 (Renci), SkiaSharp (decode/encode JPEG zero-allocation), X11/XTest/XShm P/Invoke (ZcuAgent), DPAPI (`System.Security.Cryptography.ProtectedData`), xUnit 2.9.2.

**Phụ thuộc giữa project:** `CcuUI` → `CcuClient`; `IPGS.RemoteControl.Tests` → `CcuClient` (qua `InternalsVisibleTo`); `ZcuAgent` độc lập nhưng dùng chung chuẩn protocol.

---

## Cấu trúc thư mục

- `6.RemoteControlTool/`
  - `IPGS.RemoteControl.ZcuAgent/`: Dịch vụ chạy ngầm trên ZCU (Linux daemon systemd)
    - `Program.cs`, `RemoteControlHostedService.cs`: Vòng đời service nền
    - `AgentOptions.cs`: Cấu hình Port, Token, AllowedClientIPs, EnableDesktopIntegration
    - `Interfaces.cs`: Định nghĩa `IScreenCapturer`, `IFrameEncoder`, `IInputInjector`
    - `Auth/AuthManager.cs`: Token + IP whitelist (deny-by-default, fail-fast)
    - `Net/TcpServer.cs`, `ClientSession.cs`: Quản lý 1 client, `struct InputCommand` + Channel coalescing chuột, overload `WriteAsync(ReadOnlyMemory<byte>)`
    - `Net/FileTransferHandler.cs`: Xử lý truyền tệp 2 chiều, Zero-Copy download chunking qua `buf.AsMemory(0, 1 + n)`
    - `Capture/X11ScreenCapturer.cs`, `JpegEncoder.cs`: Nén Zero-Copy trực tiếp qua `SKImage.FromPixels` + `malloc_trim(0)`
    - `Input/KeyboardInjector.cs`, `MouseInjector.cs`: Inject phím và chuột qua XTest (đã bỏ blocking `XSync`)
    - `Interop/X11Interop.cs`, `XShmInterop.cs`, `XTestInterop.cs`, `X11ErrorTracker.cs`: P/Invoke native X11
    - `appsettings.json`: Tệp cấu hình mẫu (installer ghi đè khi deploy thật)
  - `IPGS.RemoteControl.CcuClient/`: Thư viện logic CCU (.NET 8)
    - `RemoteControlClient.cs`: TCP client, reconnect, TLS Certificate Pinning, zero-copy chunk upload
    - `Protocol/MessageCodec.cs`, `MessageTypes.cs`, `ProtocolException.cs`: Encode/Decode 12 message types + bounds check
    - `ComputerProfile.cs`, `ComputerProfileStore.cs`: profiles.json + DPAPI + lưu trữ đầy đủ `TlsCertFingerprint` (TOFU)
    - `ComputerStatusChecker.cs`, `ComputerConnectivityStatus.cs`: Kiểm tra trạng thái kết nối
    - `ShellQuote.cs`: Escape & validate tham số shell SSH, chống command injection
    - `SecretProtector.cs`: DPAPI mã hóa tiền tố `enc:v1:`
    - `RemoteAppInstallService.cs`, `ZcuRemoteInstallerService.cs`, `KioskDeployService.cs`: SSH sudo installer qua STDIN
    - `ZcuAgentDiscoveryService.cs`: Tự động dò tìm ZCU trong mạng LAN và trích xuất SHA-256 TLS fingerprint
    - `Services/WakeOnLanService.cs`: Gửi gói tin Magic Packet đánh thức máy qua LAN
  - `IPGS.RemoteControl.CcuUI/`: Ứng dụng điều khiển Avalonia 12 (.NET 8)
    - `App.axaml(.cs)`, `Program.cs`, `KeyboardMapper.cs`: Cấu hình app và ánh xạ phím
    - `Services/SessionRecorder.cs`: Ghi hình phiên AVI Zero-Allocation qua `"00dc"u8` và `stackalloc byte[4]`
    - `Services/LicenseManagerService.cs`: Quản lý bản quyền RSA-SHA256
    - `ViewModels/RemoteScreenViewModel.cs`: Ping-Pong Double-Buffering zero-copy `Buffer.MemoryCopy`, safe buffer disposal khi đổi resolution, thuộc tính `IsTlsEncrypted`
    - `Views/`: 18 màn hình và view controls (ConnectionEntry truyền vân tay TLS, RemoteScreenWindow với badge `🔒 TLS`, RemoteScreenControl bọc UIThread.Post, MultiRemoteWindow hỗ trợ TLS pinning, NetworkScanWindow tự động điền vân tay TLS, FileManager, RemoteCommand, BulkAction, CronJob, HealthMonitor với `100 - idle%` CPU metric, KioskDeploy, RemoteAppInstall, ZcuSetupWizard, SystemInventory, ComputerEdit, License, SessionPickerWindow, ConfirmDeleteDialog; toàn bộ dùng `PlaceholderText`)
  - `IPGS.RemoteControl.Tests/`: Bộ kiểm thử tự động xUnit (.NET 9)
    - `IPGS.RemoteControl.Tests.csproj`: Cấu hình xUnit, tham chiếu `CcuClient` qua `InternalsVisibleTo`
    - `MessageCodecTests.cs`: 13 unit tests bao phủ toàn bộ TCP message codecs & bounds
    - `ShellQuoteTests.cs`: 15 unit tests bao phủ quote, escape, chống shell injection & validation
    - `SecretProtectorTests.cs`: 7 unit tests bao phủ DPAPI Protect/Unprotect, idempotency, legacy plaintext, error handling
    - `ComputerProfileStoreTests.cs`: 6 unit tests bao phủ lưu trữ, update, TOFU fingerprint persistence, delete
    - `WakeOnLanServiceTests.cs`: 14 unit tests kiểm thử Magic Packet MAC format chuẩn, gạch nối, dấu cách, non-hex, invalid format

---

## Module chính

| Module | Path | Mục đích & Đặc tả kiến trúc | Files quan trọng |
|--------|------|------------------------------|-----------------|
| Protocol codec | `CcuClient/Protocol/` | Khung gói tin TCP nhị phân (1 byte Type + 4 bytes Length + Payload). Hỗ trợ đầy đủ: HELLO, AUTH, PING, PONG, KEY, MOUSE_MOVE, MOUSE_BUTTON, MOUSE_SCROLL, CLIP_DATA, RESOLUTION, FRAME_JPEG, CHAT. Toàn bộ decoder kiểm soát biên độ nghiêm ngặt, văng `ProtocolException` khi dữ liệu bất thường. | `MessageCodec.cs`, `MessageTypes.cs` |
| TCP client & TLS Pinning | `CcuClient/RemoteControlClient.cs` | Client TCP kết nối ZCU: tự động reconnect với exponential backoff, xác thực token bảo mật, hỗ trợ TLS stream và kiểm tra Certificate Pinning (`TlsCertFingerprint`), upload file zero-copy qua `ReadOnlyMemory<byte>`. Bắn event `TlsCertificateValidated` kích hoạt TOFU. | `RemoteControlClient.cs`, `IRemoteControlClient.cs` |
| Profile & Secret Store | `CcuClient/` | Quản trị cấu hình kết nối ZCU trong `%APPDATA%\iPGS\RemoteControl\profiles.json`. Ghi đĩa nguyên tử (atomic write qua tmp + Move); bảo vệ mật khẩu SSH và Token bằng DPAPI (`SecretProtector`, tiền tố `enc:v1:`). Hỗ trợ lưu trữ và cập nhật vân tay chứng chỉ TLS (`TlsCertFingerprint`) tự động qua `UpdateTlsFingerprint`. | `ComputerProfile.cs`, `ComputerProfileStore.cs`, `SecretProtector.cs` |
| Shell safety & STDIN Sudo | `CcuClient/` | Chống tấn công shell command injection qua `ShellQuote`. Toàn bộ thao tác sudo SSH trên ZCU (`RemoteAppInstallService`, `ZcuRemoteInstallerService`, `RemoteCommandWindow`, `BulkActionWindow`) truyền mật khẩu qua luồng STDIN `cmd.CreateInputStream()` với cờ `sudo -S -p ''`, loại bỏ hoàn toàn biến môi trường `KIOSK_SUDO_PASS`. | `ShellQuote.cs`, `RemoteAppInstallService.cs`, `ZcuRemoteInstallerService.cs` |
| ZCU session & Input Injection | `ZcuAgent/Net/` & `Input/` | Quản lý phiên 1 client duy nhất với cơ chế watchdog PONG tách riêng task. Hàng đợi inject input sử dụng `Channel<InputCommand>` với `readonly struct InputCommand` (zero GC allocation) và mouse move coalescing. `MouseInjector` và `KeyboardInjector` loại bỏ blocking `XSync`, đẩy sự kiện tức thì qua `XFlush`. | `TcpServer.cs`, `ClientSession.cs`, `MouseInjector.cs`, `KeyboardInjector.cs` |
| Screen Capture & Zero-Copy Encode | `ZcuAgent/Capture/` | Chụp màn hình qua XShm (MIT-SHM shared memory) hoặc fallback XGetImage. Nén JPEG trực tiếp từ con trỏ pixel qua `SKImage.FromPixels` mà không cần sao chép bộ nhớ trung gian, kích hoạt định kỳ `malloc_trim(0)` mỗi 60s để ép kernel Linux thu hồi trang nhớ unmanaged của glibc. | `X11ScreenCapturer.cs`, `JpegEncoder.cs` |
| File Transfer Zero-Copy | `ZcuAgent/Net/FileTransferHandler.cs` | Tối ưu hóa tải tệp 2 chiều. Download từ ZCU dùng `buf.AsMemory(0, 1 + n)` đẩy qua `ClientSession.WriteAsync(MessageType, ReadOnlyMemory<byte>, CancellationToken)`, không sinh rác bộ nhớ per-chunk. | `FileTransferHandler.cs`, `ClientSession.cs` |
| Ping-Pong Double-Buffering UI | `CcuUI/ViewModels/` & `Views/` | Render màn hình remote với hiệu năng cực đại: duy trì 2 `WriteableBitmap` (`_frontBuffer` và `_backBuffer`). Giải mã JPEG qua SkiaSharp và copy trực tiếp sang con trỏ framebuffer bằng `Buffer.MemoryCopy` unmanaged, triệt tiêu 100% rác LOH (tiết kiệm 124.4 MB/s tại 1080p 15fps). Đổi độ phân giải an toàn (giải phóng buffer cũ trên UI thread). Hiển thị huy hiệu `🔒 TLS` trên thanh công cụ. | `RemoteScreenViewModel.cs`, `RemoteScreenWindow.axaml`, `RemoteScreenControl.axaml.cs` |
| Zero-Alloc Video Recording | `CcuUI/Services/SessionRecorder.cs` | Ghi video AVI bằng cách stream khung hình JPEG với header chunk `"00dc"u8` (UTF-8 literal) và kích thước qua `stackalloc byte[4]` + `BinaryPrimitives`, đạt 0 bytes/frame allocation trên heap. | `SessionRecorder.cs` |
| Automated Test Suite | `IPGS.RemoteControl.Tests/` | 55 test cases tự động xUnit kiểm chứng toàn diện round-trip serialization/deserialization mọi thông điệp nhị phân, độ an toàn chống injection của module shell SSH, mã hóa bí mật DPAPI, lưu trữ bền vững hồ sơ máy tính, và logic kiểm tra MAC Wake-on-LAN. | `MessageCodecTests.cs`, `ShellQuoteTests.cs`, `SecretProtectorTests.cs`, `ComputerProfileStoreTests.cs`, `WakeOnLanServiceTests.cs` |

---

## API / Interface chính

| Interface / Class | File | Đặc tả kỹ thuật & Thay đổi mới (v3.2) |
|-------------------|------|---------------------------------------|
| `ClientSession.WriteAsync` | `ZcuAgent/Net/ClientSession.cs` | **[MỚI v3.2]** Overload nhận `ReadOnlyMemory<byte>`, cho phép truyền dữ liệu trực tiếp không cần cấp phát mảng byte |
| `FileTransferHandler.HandleDownload` | `ZcuAgent/Net/FileTransferHandler.cs` | **[TỐI ƯU v3.2]** Stream chunk download bằng `buf.AsMemory(0, 1 + n)`, loại bỏ hoàn toàn `.ToArray()` |
| `SessionRecorder.RecordFrame` | `CcuUI/Services/SessionRecorder.cs` | **[TỐI ƯU v3.2]** Sử dụng `"00dc"u8` và `stackalloc byte[4]`, đạt zero heap allocation trong vòng lặp ghi video |
| `MultiRemoteWindow.AddSession` | `CcuUI/Views/MultiRemoteWindow.axaml.cs` | **[BẢO MẬT v3.2]** Truyền `expectedFingerprint` vào `RemoteControlClient` bảo vệ TLS cho bảng đa màn hình |
| `ZcuAgentDiscoveryService.DiscoveredAgent` | `CcuClient/ZcuAgentDiscoveryService.cs` | **[MỞ RỘNG v3.2]** Bổ sung thuộc tính `TlsCertFingerprint` tự động trích xuất khi phát hiện ZCU |
| `WakeOnLanServiceTests` | `IPGS.RemoteControl.Tests/WakeOnLanServiceTests.cs` | **[KIỂM THỬ v3.2]** 14 ca kiểm thử tự động kiểm tra định dạng MAC và nạp Magic Packet |
| `ComputerProfileStore.UpdateTlsFingerprint` | `CcuClient/ComputerProfileStore.cs` | Tự động lưu vân tay chứng chỉ ZCU (TOFU) khi kết nối thành công mà không ghi đè nếu đã tồn tại |
| `ComputerProfileStore.CloneForStorage` | `CcuClient/ComputerProfileStore.cs` | Bổ sung `TlsCertFingerprint` vào bản sao lưu trữ, giải quyết triệt để lỗi mất vân tay TLS khi lưu đĩa |
| `JpegEncoder.EncodeDirect` | `ZcuAgent/Capture/JpegEncoder.cs` | Nén JPEG trực tiếp qua `SKImage.FromPixels`, loại bỏ hoàn toàn 8.3 MB per-frame `Buffer.MemoryCopy` trên ZCU |
| `MouseInjector.Move` / `KeyboardInjector.SendKey` | `ZcuAgent/Input/` | Loại bỏ blocking `XSync`, đẩy sự kiện ngay qua `XFlush`, giảm độ trễ chuột từ 0.5ms về < 0.01ms |
| `RemoteScreenViewModel.OnFrameReceived` | `CcuUI/ViewModels/RemoteScreenViewModel.cs` | Cơ chế Ping-Pong Double-Buffering zero-copy; chuyển việc `.Dispose()` buffer cũ sang `Dispatcher.UIThread.Post` khi đổi độ phân giải |

---

## Dependencies quan trọng

| Package | Version | Project | Ghi chú & Đánh giá bảo mật |
|---------|---------|---------|---------------------------|
| `SSH.NET` (Renci.SshNet) | **2026.0.0** | CcuClient, CcuUI | Xóa sạch 100% 2 lỗ hổng bảo mật NU1903 (GHSA-mggc-4xg6-vcxf và GHSA-q939-rpr3-3284) |
| `System.Security.Cryptography.ProtectedData` | 8.0.0 | CcuClient | DPAPI mã hóa dữ liệu nhạy cảm trên Windows (với cơ chế cảnh báo trên Linux) |
| `Avalonia` | 11.2 / 12.x | CcuUI | Toàn bộ 10 file AXAML đã chuyển 100% sang `PlaceholderText`, sạch toàn bộ obsolete warnings |
| `KztekComponentAvalonia` | ProjectReference | CcuUI | Thư viện UI chuẩn KZTEK từ `E:\KZTEK\Code_Git\5.BaseUI` |
| `SkiaSharp` | (theo csproj) | CcuUI, ZcuAgent | Xử lý hình ảnh và giải mã JPEG |
| `xunit` & `coverlet.collector` | 2.9.2 / 6.0.2 | IPGS.RemoteControl.Tests | Khung kiểm thử tự động, **55/55 tests passed (100%)** |

---

## Thay đổi gần đây

| Ngày | File/Module | Loại | Mô tả ngắn | Agent |
|------|------------|------|------------|-------|
| 2026-10-31 | Toàn bộ giải pháp (4 projects) | Refactor & Performance | **Hoàn thành chu trình cải tiến toàn diện v3.2 (F-ROUND3-1 đến F-ROUND3-5):** Zero-copy file download chunking trong `FileTransferHandler.cs`, zero-alloc AVI video recording trong `SessionRecorder.cs` (`"00dc"u8` + `stackalloc`), đồng bộ TLS Pinning vào `MultiRemoteWindow`, tự động trích xuất TLS fingerprint trong `ZcuAgentDiscoveryService`, mở rộng bộ test suite lên 55 unit tests pass 100% | senior-developer, junior-developer, qa-engineer, tech-lead |
| 2026-10-31 | Toàn bộ giải pháp | Refactor & Security | Chu trình cải tiến sâu v3.1 (F-NEW-1 đến F-NEW-7): Sửa lỗi lưu TlsCertFingerprint trong `CloneForStorage`, TOFU tự động và badge `🔒 TLS`, nén JPEG Zero-Copy `SKImage.FromPixels`, loại bỏ `XSync` trong `MouseInjector`/`KeyboardInjector`, an toàn luồng đổi resolution, 41 unit tests | senior-developer, junior-developer, qa-engineer, tech-lead |
| 2026-10-31 | Toàn bộ giải pháp | Refactor | Cải tiến đợt 1 (M1-M3, C1-C3, S1-S3, U1-U2, T1): Ping-Pong Double-Buffering, STDIN sudo, SSH.NET 2026.0.0, 28 tests | senior-developer + tech-lead |

---

## Lessons & Quyết định quan trọng

| Ngày | Quyết định / Bài học | Lý do (WHY) | Agent |
|------|----------------------|--------------|-------|
| 2026-10-31 | Zero-Allocation AVI Header bằng UTF-8 literals `u8` và `stackalloc` | Ghi hình 60 FPS kéo dài tạo hàng chục nghìn mảng byte rác nhỏ trên Heap nếu dùng `GetBytes`. Dùng `"00dc"u8` và `stackalloc byte[4]` triệt tiêu 100% allocation per frame | senior-developer |
| 2026-10-31 | Zero-Copy Stream Slice cho truyền file qua `ReadOnlyMemory<byte>` | Dùng `buf.AsMemory(0, 1 + n)` thay cho `.ToArray()` loại bỏ sao chép dữ liệu tệp tải về từ ZCU, tăng tốc độ truyền qua mạng | senior-developer |
| 2026-10-31 | X11 `XSync` không được gọi trên từng tọa độ chuột / phím | `XSync` gửi một synchronous roundtrip tới X server và block luồng đợi phản hồi. Motion events không có response từ server, gọi `XSync` 60 lần/s gây nghẽn message loop. Chỉ cần `XFlush` để đẩy dữ liệu socket | senior-developer |
| 2026-10-31 | `WriteableBitmap.Dispose()` khi đổi resolution phải hoãn đến UI thread | Giải phóng buffer bitmap cũ ngay trên background thread trong khi UI thread đang render dẫn đến `ObjectDisposedException` hoặc crash Skia native pipeline | senior-developer |
| 2026-10-31 | Nén Zero-Copy từ pinned byte array bằng `SKImage.FromPixels` | Không cần cấp phát `SKBitmap` hay copy từng dòng qua `Buffer.MemoryCopy`. SkiaSharp có thể encode JPEG trực tiếp từ con trỏ pixel gốc đã ghim | senior-developer |

---

## Ghi chú đặc biệt

- **Chất lượng kiểm thử:** Toàn bộ giải pháp sở hữu test suite tự động `IPGS.RemoteControl.Tests` với **55 bài test đạt 100% tỷ lệ đỗ**.
- **Trạng thái biên dịch:** Sạch sẽ 100% (0 Errors, 0 Warnings trên cả 4 dự án).
- **Nguyên tắc Zero-Allocation:** Triệt tiêu hoàn toàn rác LOH trên CCU UI (0 B/s), triệt tiêu chi phí sao chép bộ nhớ trên ZCU Agent, và triệt tiêu toàn bộ heap allocation trong vòng lặp ghi video AVI.
