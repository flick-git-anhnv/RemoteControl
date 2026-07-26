---
step: 2.1
title: Fix IPGS.RemoteControl.ZcuAgent — S2, S4, L2, L3, L8, L9, Q1, Q2, Q3
agent: senior-developer
status: done
completed_at: 2026-07-26 14:19
commit: 1ab4f03
---

# STEP 2.1 — Fix ZcuAgent (9 mục audit)

## Nhiệm vụ

Sửa 9 phát hiện trong project `IPGS.RemoteControl.ZcuAgent` theo plan MASTER:
S2, S4 (bảo mật), L2, L3, L8, L9 (leak/race/DoS), Q1, Q2, Q3 (chất lượng/hiệu năng).
Kèm xác nhận L4 phía ZcuAgent (ProtocolException từ decoder — Bước 1.1 đã đổi MessageCodec).

## Đã làm

| Mục | Fix | File |
|---|---|---|
| S2 🔴 | Chat/clipboard: `ProcessStartInfo.ArgumentList` (mỗi arg 1 phần tử argv, không quoting thủ công); cap chat 4.096 chars (truncate + warn), clipboard 262.144 chars (reject + warn); thêm flag `EnableDesktopIntegration` (default true) tắt được cả notify-send lẫn xclip | `Net/ClientSession.cs`, `AgentOptions.cs` |
| S4 🔴 | Fail-fast khởi động: Token rỗng hoặc bắt đầu `REPLACE_WITH` → LogCritical + throw TRƯỚC khi mở listener. `IsIpAllowed`: list rỗng = **deny-all** (đảo semantics allow-all cũ) + log hướng dẫn. Startup warning nổi bật khi whitelist chứa `0.0.0.0/0`/`::/0` | `RemoteControlHostedService.cs` (inject `IOptions<AgentOptions>`, thêm `ValidateSecurityConfig()`), `Auth/AuthManager.cs` |
| L2 🔴 | notify-send/xclip → 2 helper async (`ShowChatNotificationAsync`, `SetClipboardAsync`): `using var proc`, `WriteAsync` + `WaitForExitAsync` với CancelAfter 2s — không còn chặn receive-loop, không leak Process/fd. xclip daemonize → hết grace period thì **release handle, KHÔNG kill** (kill sẽ mất clipboard). notify-send treo → kill tree | `Net/ClientSession.cs` |
| L3 🔴 | `WriteAsync` bọc `CancelAfter(WriteTimeoutMs=10s)` → timeout ném `IOException` "slow or stalled reader" (NetworkStream.WriteTimeout KHÔNG áp dụng cho async write). Tách check PONG-timeout ra task riêng `RunHeartbeatWatchdogAsync` (poll 1s, không ghi network) — luôn fire được kể cả khi capture loop kẹt trong WriteAsync. PING vẫn gửi từ capture loop | `Net/ClientSession.cs` |
| L4 🟠 | Xác nhận: `ProtocolException` từ decoder (kể cả AUTH dị dạng trong `DoAuthAsync`) propagate lên `RunAsync` → catch riêng LogWarning + đóng session gọn (đã có sẵn từ trước, đúng thiết kế). Thêm comment chốt hành vi ở cuối receive-loop để không ai "sửa nhầm" sau này. Build lại ZcuAgent với MessageCodec mới: 0 error 0 warning | `Net/ClientSession.cs` |
| L8 🟠 | `X11ErrorTracker` thêm `ShmMajorOpcode` (volatile int, -1 = chưa biết). `OnX11Error` chỉ set `ShmErrorOccurred` khi `request_code == ShmMajorOpcode`; opcode chưa biết HOẶC parse event fail → conservative set cờ (giữ nguyên khả năng chống crash XShmAttach BadAccess 2026-07-23). Capturer đăng ký opcode qua `XQueryExtension("MIT-SHM")` (P/Invoke mới) ngay sau `XShmQueryExtension` | `Interop/X11ErrorTracker.cs`, `Interop/X11Interop.cs`, `Capture/X11ScreenCapturer.cs` |
| L9 🟠 | `ScreenSize` publish qua `ScreenSizeHolder` record immutable + field `volatile` → đọc/ghi là 1 phép gán reference nguyên tử, hết torn read W-mới/H-cũ. `ClientSession` clamp chuột đọc `ScreenSize` đúng 1 lần vào local (trước đây đọc 2 lần riêng cho Width/Height) | `Capture/X11ScreenCapturer.cs`, `Net/ClientSession.cs` |
| Q1 🟡 | 2 chỗ `catch {}` → helper có `LogWarning`/`LogDebug` đầy đủ | `Net/ClientSession.cs` |
| Q2 🟡 | Xóa dead code nhánh `PlatformID.Win32NT` gọi `msg` | `Net/ClientSession.cs` |
| Q3 🟡 | Reuse buffer thay vì alloc mỗi frame: `X11ScreenCapturer._pixelBuffer` (grow-only, `GC.AllocateUninitializedArray`) + `JpegEncoder._jpegBuffer`; `IFrameEncoder.EncodeJpeg` đổi chữ ký `byte[]?` → `ReadOnlyMemory<byte>` (empty = fail). An toàn vì capture loop đơn luồng, frame tiêu thụ đồng bộ, v1 chỉ 1 session. Docs GOTCHA về buffer reuse ghi rõ trong `Interfaces.cs`. **KHÔNG đụng `MessageCodec`** (thuộc CcuClient đã commit) — alloc `new byte[24+jpeg.Length]`/frame trong `EncodeFrameJpeg` vẫn còn, để bước 4.1 quyết | `Capture/X11ScreenCapturer.cs`, `Capture/JpegEncoder.cs`, `Interfaces.cs`, `Net/ClientSession.cs` |

## Verification

```
Lệnh:   dotnet build IPGS.RemoteControl.ZcuAgent/IPGS.RemoteControl.ZcuAgent.csproj
Output: Build succeeded.  0 Warning(s)  0 Error(s)  (net8.0/linux-x64)
Kết luận: Pass
```

## Quyết định quan trọng

1. **`appsettings.json` KHÔNG sửa được** — hook `config-protection.js` chặn Edit (file thuộc danh sách config bảo vệ). S4 được xử lý hoàn toàn bằng source code (fail-fast token + deny-on-empty + warning catch-all). Default `"AllowedClientIPs": ["0.0.0.0/0"]` vẫn nằm trong file config → **runtime hiện tại vẫn allow-all IP nhưng có warning nổi bật lúc khởi động**, và agent không thể chạy với token placeholder nữa. Việc đổi default config sang `[]` cần user xác nhận (bước 4.1 trình user).
2. **Semantics đảo chiều:** `AllowedClientIPs` rỗng trước = allow-all, giờ = deny-all. Deployment nào đang cố ý để rỗng sẽ bị từ chối kết nối — breaking change có chủ đích (security), cần ghi vào release note.
3. **xclip không bị kill khi quá grace period** — daemonize là hành vi đúng của xclip (giữ selection); kill sẽ xoá clipboard vừa set. Chỉ release Process handle.
4. **Q3 dùng buffer reuse thay ArrayPool**: capture loop đơn luồng + tiêu thụ đồng bộ → 1 buffer grow-only đơn giản hơn, 0 alloc/frame, không cần điểm Return() (ArrayPool sai chỗ trả = bug khó thấy hơn).
5. `EnableDesktopIntegration` default `true` để giữ behavior parity; site nhạy cảm tự tắt qua config.

## Artifact

- 9 file source đã sửa trong `IPGS.RemoteControl.ZcuAgent/` (xem bảng trên)
- Commit: `1ab4f03` (chưa push — chờ review bước 4.1)

## Handoff Log — bước sau cần biết

- **Đã làm:** Fix xong 9 mục ZcuAgent (S2, S4, L2, L3, L8, L9, Q1-Q3) + xác nhận L4. Build `IPGS.RemoteControl.ZcuAgent` 0 error 0 warning với MessageCodec mới của bước 1.1. Commit `1ab4f03`, chưa push.
- **File/module đã đổi:** `Net/ClientSession.cs` (nhiều nhất), `RemoteControlHostedService.cs`, `Auth/AuthManager.cs`, `AgentOptions.cs`, `Capture/X11ScreenCapturer.cs`, `Capture/JpegEncoder.cs`, `Interfaces.cs`, `Interop/X11ErrorTracker.cs`, `Interop/X11Interop.cs`.
- **Quyết định quan trọng:** (1) `appsettings.json` bị hook config-protection chặn — default `0.0.0.0/0` VẪN CÒN trong config, bước 4.1 PHẢI trình user xác nhận đổi sang `[]` (source đã sẵn sàng deny-on-empty). (2) `IFrameEncoder.EncodeJpeg` đổi chữ ký sang `ReadOnlyMemory<byte>` — chỉ dùng nội bộ ZcuAgent, không ảnh hưởng CcuClient/CcuUI. (3) `AllowedClientIPs` rỗng giờ = deny-all (breaking, có chủ đích).
- **Bước sau cần biết:**
  - Bước 3.1/3.2 (CcuUI): KHÔNG liên quan file ZcuAgent nào — không đụng.
  - Bước 4.1 (review): (a) còn tồn đọng Q3 phần `MessageCodec.EncodeFrameJpeg` alloc `new byte[24+len]`/frame — thuộc CcuClient, cố tình bỏ qua theo chỉ đạo; (b) quyết định + xin user duyệt sửa `appsettings.json` `AllowedClientIPs` → `[]`; (c) fail-fast token nghĩa là chạy thử agent với config mẫu sẽ THROW ngay — khi smoke test phải set token thật; (d) CapturedFrame.PixelData và kết quả EncodeJpeg giờ là buffer reuse — nếu tương lai thêm consumer async/multi-session phải copy ra trước.
  - Bước 5.1 (docs): CODE-GRAPH hiện mô tả workspace cũ (không có 3 project RemoteControl) — cần cập nhật tổng thể ở bước 5.1; interface `IFrameEncoder` đã đổi chữ ký.
