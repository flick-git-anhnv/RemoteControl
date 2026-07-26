# CODE-GRAPH.md — Bản đồ codebase: Remote Control Tool (CCU ↔ ZCU)
**Cập nhật lần cuối:** 2026-07-26 | **Bởi:** senior-developer | **Version:** 2.0

> File này được duy trì tự động bởi coding agents.
> **Đọc file này TRƯỚC khi đọc source code** để hiểu cấu trúc dự án mà không cần mở từng file.

> **Ghi chú version 2.0:** Bản v1.0 (2026-07-12) mô tả workspace agent framework cũ (không đúng repo này) — đã viết lại toàn bộ theo khảo sát thực tế repo `6.RemoteControlTool` sau đợt audit-fix 41 phát hiện (plan `PLAN-remote-control-audit-fix-2026-07-26`).

---

## Tổng quan dự án

Hệ thống Remote Control CCU↔ZCU: điều khiển từ xa các máy ZCU (Linux, X11) từ máy quản lý CCU (Windows/Linux). Gồm 3 project .NET:

| Project | Vai trò | Nền tảng |
|---|---|---|
| `IPGS.RemoteControl.ZcuAgent` | Service chạy trên ZCU (Linux): capture màn hình X11 (XShm/XGetImage), inject chuột/phím (XTest), TCP server 1-client, chat/clipboard | linux-x64 |
| `IPGS.RemoteControl.CcuClient` | Class library: TCP client + protocol codec, SSH installer/deployer, WoL, discovery, profile store (DPAPI) | win-x64 + linux-x64 |
| `IPGS.RemoteControl.CcuUI` | App Avalonia 12: viewer/manager — remote screen, multi-remote, file manager, cron, bulk action, kiosk deploy | win-x64 + linux-x64 |

**Tech stack:** .NET 8, Avalonia 12 (CcuUI — dùng `KztekComponentAvalonia` từ `E:\KZTEK\Code_Git\5.BaseUI`), SSH.NET (Renci), SkiaSharp (decode JPEG), X11/XTest/XShm P/Invoke (ZcuAgent), DPAPI (`System.Security.Cryptography.ProtectedData`).

**Phụ thuộc giữa project:** `CcuUI` → `CcuClient`; `ZcuAgent` độc lập nhưng dùng chung protocol (copy codec logic). KHÔNG build 3 project song song (tranh chấp `obj/` của project chung).

---

## Cấu trúc thư mục

```
6.RemoteControlTool/
├── IPGS.RemoteControl.ZcuAgent/        ← Linux service (systemd)
│   ├── Program.cs / RemoteControlHostedService.cs
│   ├── AgentOptions.cs                 ← Options: Port, Token, AllowedClientIPs, EnableDesktopIntegration
│   ├── Interfaces.cs                   ← IScreenCapturer, IFrameEncoder, IInputInjector
│   ├── Auth/AuthManager.cs             ← Token + IP whitelist (deny-by-default, fail-fast placeholder)
│   ├── Net/TcpServer.cs, ClientSession.cs  ← 1 client/lượt; watchdog PONG tách task riêng
│   ├── Capture/X11ScreenCapturer.cs, JpegEncoder.cs
│   ├── Input/KeyboardInjector.cs, MouseInjector.cs
│   ├── Interop/X11Interop.cs, XShmInterop.cs, XTestInterop.cs, X11ErrorTracker.cs
│   └── appsettings.json                ← ⚠️ file mẫu — installer ghi đè khi deploy thật
├── IPGS.RemoteControl.CcuClient/       ← Class library
│   ├── RemoteControlClient.cs          ← TCP client, reconnect, heartbeat (IRemoteControlClient)
│   ├── Protocol/MessageCodec.cs, MessageTypes.cs, ProtocolException.cs
│   ├── ComputerProfile.cs / ComputerProfileStore.cs (IComputerProfileStore)  ← profiles.json + DPAPI
│   ├── ComputerStatusChecker.cs / ComputerConnectivityStatus.cs
│   ├── ShellQuote.cs                   ← [MỚI] escape/validate tham số shell SSH (internal)
│   ├── SecretProtector.cs              ← [MỚI] DPAPI enc:v1: + migrate plaintext
│   ├── RemoteAppInstallService.cs / ZcuRemoteInstallerService.cs / KioskDeployService.cs  ← SSH sudo installer
│   ├── ZcuAgentDiscoveryService.cs
│   └── Services/WakeOnLanService.cs
└── IPGS.RemoteControl.CcuUI/           ← App Avalonia
    ├── App.axaml(.cs), Program.cs, KeyboardMapper.cs
    ├── Services/SessionRecorder.cs (AVI), LicenseManagerService.cs (⚠️ dead code — không enforce, quyết định user)
    ├── ViewModels/RemoteScreenViewModel.cs
    ├── Converters/
    └── Views/  ← 18 window/control (ConnectionEntry = main window; RemoteScreen, MultiRemote,
        FileManager, RemoteCommand, BulkAction, CronJob, HealthMonitor, NetworkScan,
        KioskDeploy, RemoteAppInstall, ZcuSetupWizard, SystemInventory, ComputerEdit,
        License, RemoteScreenControl, SessionPickerWindow [MỚI], ConfirmDeleteDialog [MỚI])
```

---

## Module chính

| Module | Path | Mục đích | Files quan trọng |
|--------|------|----------|-----------------|
| Protocol codec | `CcuClient/Protocol/` | Frame TCP: HELLO/AUTH/PING/FrameJpeg/Key/Mouse/Chat/Clipboard — mọi decoder validate độ dài → `ProtocolException` | `MessageCodec.cs`, `MessageTypes.cs` |
| TCP client | `CcuClient/RemoteControlClient.cs` | Reconnect có backoff, dispose sạch mọi path lỗi handshake, heartbeat Interlocked | `IRemoteControlClient.cs` |
| Profile store | `CcuClient/ComputerProfileStore.cs` | Lưu `%APPDATA%\iPGS\RemoteControl\profiles.json`; atomic write (tmp+Move); backup `.corrupt-*`; DPAPI qua `SecretProtector` | `ComputerProfile.cs`, `SecretProtector.cs` |
| Shell safety | `CcuClient/ShellQuote.cs` | `Quote()` single-quote escape, `ValidateFileName` (cho phép `~`/`%` giữa tên — chuẩn Debian), `ValidateUsername`, `ValidatePackageName` — dùng bởi 3 installer service | — |
| SSH installers | `CcuClient/*Service.cs` | Cài `.deb`/`.sh`, deploy ZcuAgent (systemd), kiosk setup — mọi tham số qua `ShellQuote` | `RemoteAppInstallService`, `ZcuRemoteInstallerService`, `KioskDeployService` |
| ZCU session | `ZcuAgent/Net/ClientSession.cs` | Capture loop + watchdog PONG (task riêng) + WriteAsync CancelAfter 10s; chat/clipboard qua `ArgumentList` cap 4KB/256KB | `TcpServer.cs` |
| X11 capture | `ZcuAgent/Capture/` | XShm ưu tiên, fallback XGetImage; error tracker scope theo opcode MIT-SHM; ScreenSize publish atomic (holder record + volatile) | `X11ScreenCapturer.cs`, `Interop/X11ErrorTracker.cs` |
| Remote screen UI | `CcuUI/Views/RemoteScreenWindow` + `ViewModels/RemoteScreenViewModel` | Decode JPEG → WriteableBitmap (dispose bản cũ), record AVI (`SessionRecorder` lock + idempotent Dispose) | `RemoteScreenControl.axaml.cs` |
| Multi remote | `CcuUI/Views/MultiRemoteWindow` | Grid nhiều session; thêm session qua `SessionPickerWindow` (dialog trả kết quả); tab ẩn pause render (`IsRenderPaused`) | `SessionPickerWindow.axaml.cs` |
| Remote command / bulk | `CcuUI/Views/RemoteCommandWindow`, `BulkActionWindow` | SSH lệnh ad-hoc; sudo password qua STDIN channel (`sudo -S -p ''`), KHÔNG env/command-line | — |

---

## API / Interface chính

| Interface | File | Ghi chú (đợt audit-fix 2026-07-26) |
|-----------|------|------|
| `IRemoteControlClient` | `CcuClient/IRemoteControlClient.cs` | Client TCP; `FrameReceived` event chạy trên thread nhận TCP — subscriber phải tự marshal UI |
| `IComputerProfileStore` | `CcuClient/IComputerProfileStore.cs` | Load/Persist profile; password/token mã hoá `enc:v1:` (DPAPI) |
| `ShellQuote` (internal) | `CcuClient/ShellQuote.cs` | **[MỚI]** helper dùng chung 3 installer; TD-1: dự kiến nâng `public` |
| `SecretProtector` (internal) | `CcuClient/SecretProtector.cs` | **[MỚI]** `Protect`/`Unprotect` prefix `enc:v1:`; Windows-only DPAPI, Linux cảnh báo 1 lần, KHÔNG fallback plaintext im lặng |
| `ComputerStatusChecker.ProbeAsync` | `CcuClient/ComputerStatusChecker.cs` | **[ĐỔI SIGNATURE]** thêm param optional `Action<Action>? uiDispatch` — mutation property marshal về UI thread |
| `ZcuRemoteInstallerService.ExecuteCommand` | `CcuClient/ZcuRemoteInstallerService.cs` | **[ĐỔI]** trả `string` (kết quả) thay vì `SshCommand` (trước đây trả object đã Dispose — Q6) |
| `IFrameEncoder.EncodeJpeg` | `ZcuAgent/Interfaces.cs` | **[ĐỔI]** trả `ReadOnlyMemory<byte>` thay `byte[]?` — buffer reuse, chỉ hợp lệ đến lần encode kế (single-threaded capture loop) |
| `AgentOptions.EnableDesktopIntegration` | `ZcuAgent/AgentOptions.cs` | **[MỚI]** default true; false = tắt hẳn notify-send/xclip |
| `X11ErrorTracker.ShmMajorOpcode` | `ZcuAgent/Interop/X11ErrorTracker.cs` | **[MỚI]** scope lỗi SHM theo opcode `XQueryExtension("MIT-SHM")` (P/Invoke mới); opcode chưa biết → flag mọi lỗi (conservative) |
| `SessionRecorder.Width/Height` | `CcuUI/Services/SessionRecorder.cs` | **[MỚI]** expose độ phân giải đang ghi — dừng ghi khi ZCU đổi resolution (AVI header cố định) |
| `RemoteControlClient.ScreenWidth/Height` | `CcuClient/RemoteControlClient.cs` | **[ĐỔI ngữ nghĩa — F01]** cập nhật theo TỪNG frame (không chỉ HELLO_ACK) — luôn là độ phân giải đang stream |
| `RemoteControlClient.ServerName` | `CcuClient/RemoteControlClient.cs` | **[MỚI — F02]** tên/version agent từ HELLO_ACK (VD `ZcuAgent/1.1`); UI cảnh báo khi < 1.1 (agent cũ bỏ qua âm thầm SysInfo/Privacy/Chat/Clipboard) |
| `SshInstallerOptions.DefaultLanAllowedClientIPs` | `CcuClient/ZcuRemoteInstallerService.cs` | **[MỚI — F06]** const `"192.168.0.0/16,10.0.0.0/8,172.16.0.0/12"` — mặc định AllowedClientIPs mới; installer tách chuỗi nhiều CIDR (`,`/`;`) thành mảng JSON |
| `SessionPickerWindow` | `CcuUI/Views/SessionPickerWindow.axaml(.cs)` | **[FILE MỚI]** dialog chọn máy trả `ComputerProfile?` cho MultiRemote (fix A6) |
| `ConfirmDeleteDialog` | `CcuUI/Views/ConfirmDeleteDialog.axaml(.cs)` | **[FILE MỚI]** xác nhận xóa file/`rm -rf` (fix Q14) |

---

## Dependencies quan trọng

| Package | Version | Project | Dùng cho |
|---------|---------|---------|---------|
| `System.Security.Cryptography.ProtectedData` | 8.0.0 | CcuClient | **[MỚI]** DPAPI mã hoá SSH password/token trong profiles.json (Windows-only) |
| `SSH.NET` (Renci.SshNet) | (theo csproj) | CcuClient, CcuUI | SSH/SFTP installer, remote command |
| `Avalonia` | 12.x | CcuUI | UI (lưu ý G011: `PlaceholderText`, không dùng `Watermark`) |
| `KztekComponentAvalonia` | ProjectReference `E:\KZTEK\Code_Git\5.BaseUI` | CcuUI | Kz controls (nguồn phần lớn warning CS1591/AVLN5001 khi full rebuild) |
| `SkiaSharp` | (theo csproj) | CcuUI, ZcuAgent | Decode/encode JPEG |

---

## Config / Environment Variables

| Key | Vị trí | Default | Ghi chú |
|-----|--------|---------|-------|
| `Agent:Port` | ZcuAgent appsettings.json | 5900 | TCP listener |
| `Agent:Token` | ZcuAgent appsettings.json | placeholder | **Fail-fast**: còn placeholder/rỗng → service từ chối start |
| `Agent:AllowedClientIPs` | ZcuAgent appsettings.json | File mẫu còn `["0.0.0.0/0"]` (hook chặn sửa) nhưng **mặc định hiệu lực khi deploy = 3 dải LAN RFC 1918** (F06: installer/wizard/script `setup-zcu-agent.sh` đều dùng `DefaultLanAllowedClientIPs`) | List rỗng = **deny-all**; `0.0.0.0/0` tường minh → warning; token < 16 ký tự → warning (không fail-fast) |
| `Agent:EnableDesktopIntegration` | ZcuAgent appsettings.json | true | Tắt notify-send/xclip |
| `KIOSK_SUDO_PASS` | env khi chạy script kiosk | — | Chỉ còn `KioskDeployService` set; CcuUI RemoteCommand/BulkAction ĐÃ BỎ (S3) — chạy tay script kiosk qua RemoteCommandWindow sẽ không có env này, `_sudo()` fallback sudo thường |

---

## Thay đổi gần đây

| Ngày | File/Module | Loại | Mô tả ngắn | Agent |
|------|------------|------|------------|-------|
| 2026-07-26 | Toàn bộ 3 project | Fix | Audit-fix 41 phát hiện (A1-A6, S1-S4+S7, L1-L9, Q1-Q19) — commits `0146cb4`, `1ab4f03`, `58909eb`, `8b0aaa3`, `de981cf`. S5/S6 (license) giữ nguyên theo quyết định user | senior-developer + tech-lead |
| 2026-07-26 | `CcuClient/ShellQuote.cs`, `SecretProtector.cs` | Add | Helper shell-quoting + DPAPI secret (chi tiết bảng Interface) | senior-developer |
| 2026-07-26 | `CcuUI/Views/SessionPickerWindow`, `ConfirmDeleteDialog` | Add | Dialog mới (A6, Q14) | senior-developer |
| 2026-07-26 | `code-graph/CODE-GRAPH.md` | Rewrite | Viết lại v2.0 đúng repo RemoteControlTool (bản cũ mô tả workspace khác) | senior-developer |
| 2026-07-26 | CcuClient + CcuUI Views + ZcuAgent | Fix | F01-F05 + F06 phần agent (BUG-ccu-ui-findings): resolution theo frame, ServerName + timeout SysInfo, snippet PopulateComplete, reset status, ValidateSshProfile, default AllowedClientIPs → LAN + cảnh báo token yếu; agent HELLO_ACK → `ZcuAgent/1.1` | senior-developer |

---

## Lessons & Quyết định quan trọng

| Ngày | Quyết định / Bài học | Lý do (WHY) | Agent |
|------|----------------------|--------------|-------|
| 2026-07-26 | License S5/S6 (backdoor `"ANHNV"` + không enforce) GIỮ NGUYÊN | Quyết định user — có thể cố ý cho nội bộ; ghi nhận tại `docs/bugs/BUG-remote-control-audit-2026-07-26.md` | dispatcher |
| 2026-07-26 | DPAPI chỉ Windows — Linux cảnh báo tường minh, KHÔNG im lặng plaintext | App cross-platform; im lặng fallback = lỗ hổng ẩn | tech-lead |
| 2026-07-26 | TD-1 (ShellQuote public + sudo-stdin helper chung), TD-2 (EncodeFrameJpeg ArrayPool) hoãn sang task refactor riêng | Refactor ngay trước push tăng rủi ro regression cho code vừa review PASS | tech-lead |
| 2026-07-26 | Baseline warning phải đo cùng chế độ build (full rebuild đếm cả project tham chiếu ngoài KztekComponentAvalonia — 456 vs 19 incremental) | Tránh báo động giả "warning mới" | tech-lead |

---

## Ghi chú đặc biệt

- **Không có unit/integration test** trong cả 3 project — khuyến nghị bổ sung round-trip codec test + reconnect leak test (ghi nhận tại BUG report §7).
- **Buffer reuse ZcuAgent:** pixel buffer + jpeg buffer tái dùng giữa các frame — an toàn vì capture loop single-threaded, consume đồng bộ; KHÔNG giữ tham chiếu `ReadOnlyMemory<byte>` qua frame kế.
- **`appsettings.json` ZcuAgent được hook `config-protection` bảo vệ** — không sửa qua Edit/Write; đổi default cần user duyệt.
- **Tech-debt đang mở:** xem `docs/tech-debt/TECH-DEBT.md` (TD-1, TD-2).
