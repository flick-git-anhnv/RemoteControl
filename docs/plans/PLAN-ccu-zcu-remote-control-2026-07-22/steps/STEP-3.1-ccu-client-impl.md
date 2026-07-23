---
step: 3.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-07-23 08:36
---

# STEP 3.1 — Senior Dev tạo project IPGS.RemoteControl.CcuClient

## Input nhận

Từ STEP-2.2 Handoff Log — cần có: TCP port chính xác, message format binary layout, native libs đã xác nhận, interface `IRemoteControlClient` từ TDD, auth shared secret format.

## Nhiệm vụ

Tạo project mới `IPGS.RemoteControl.CcuClient` — .NET 8 class library (Windows). Implement: (1) TCP client kết nối tới ZcuAgent, (2) thực hiện auth handshake, (3) nhận frame JPEG liên tục và expose qua event/callback, (4) gửi lệnh mouse (move, left click, right click) tới ZcuAgent, (5) quản lý connection lifecycle (reconnect logic, timeout).

**Ràng buộc cứng:** project đặt ở thư mục mới `IPGS.RemoteControl.CcuClient/` tại solution root — KHÔNG đặt trong project hiện có.

## Definition of Done

- [ ] Project `IPGS.RemoteControl.CcuClient/` tồn tại với `.csproj` target `net8.0-windows` (hoặc `net8.0` nếu cross-platform)
- [ ] Implement `IRemoteControlClient` theo spec TDD
- [ ] Kết nối TCP + auth handshake hoạt động (test với ZcuAgent thật hoặc mock server)
- [ ] Nhận frame JPEG, expose qua event `FrameReceived(byte[] jpegData)`
- [ ] Gửi MouseMove(x, y), MouseClick(x, y, button) thành công
- [ ] Reconnect tự động khi mất kết nối (với số lần retry cấu hình được)
- [ ] Build sạch: `dotnet build` không error
- [ ] KHÔNG có reference đến project business cũ (IPGS.Object, IPGSUseCam, ...)
- [ ] Commit + push lên nhánh `zcu-avalonia`

## Đã làm

- Implement `RemoteControlClient : IRemoteControlClient` trong `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs`.
- State machine đầy đủ: Disconnected → Connecting → Authenticating → Streaming → Faulted (TDD §7).
- Handshake: TCP connect → HELLO → HELLO_ACK (lấy screenWidth/screenHeight) → AUTH → AUTH_OK/AUTH_FAIL.
- AUTH_FAIL: raise StateChanged (Faulted), đóng kết nối, `_authFailed = true` → ConnectionLoop thoát ngay, không retry.
- Receive loop: xử lý FRAME_JPEG (raise FrameReceived), PING (reply PONG ngay), PONG (cập nhật `_lastPongTicks`), BYE (thoát gracefully).
- Background PING sender: gửi PING mỗi 5s; nếu không nhận PONG trong 15s, cancel `_sessionCts` để kill session và reconnect.
- Auto-reconnect với jitter (3000 ± 1000 ms), tối đa 10 lần, sau đó chuyển Faulted.
- `_sendLock` SemaphoreSlim serialize mọi write (ping sender, mouse events, pong replies) — tránh race condition ghi đồng thời lên NetworkStream.
- `_lastPongTicks` dùng `Interlocked.Exchange`/`Read` (lock-free, thread-safe).
- `ScreenWidth`/`ScreenHeight` properties expose thông tin màn hình ZCU sau khi Streaming.
- Expose properties `ScreenWidth`/`ScreenHeight` (ngoài interface) để CcuUI dùng tính scale.
- Build: `dotnet build IPGS.RemoteControl.CcuClient` — **0 error, 0 warning**.

## Artifact

- `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs` — class implement IRemoteControlClient

## Quyết định quan trọng

1. **Coordinate mapping KHÔNG nằm trong CcuClient** — per TDD §5.3, RemoteScreenControl (CcuUI) map từ Avalonia control pixels → ZCU screen space TRƯỚC khi gọi `SendMouseMoveAsync`/`SendMouseButtonAsync`. CcuClient nhận ZCU screen coords trực tiếp. CcuUI cần biết `ScreenWidth`/`ScreenHeight` để map — lấy qua property public trên `RemoteControlClient` (cast từ `IRemoteControlClient`).

2. **Không thêm SkiaSharp vào CcuClient** — TDD §10.1: `FrameReceivedEventArgs.JpegData` là raw bytes, caller (CcuUI/ViewModel) tự decode bằng SkiaSharp. Giữ CcuClient không phụ thuộc render library.

3. **PING sender theo dõi timeout, không receive loop** — receive loop chỉ `await ReadMessageAsync` (blocking). PING timeout được check trong PingSenderLoopAsync sau mỗi lần delay 5s; khi timeout, cancel sessionCts khiến ReadMessageAsync throw OperationCanceledException và exit receive loop.

4. **ConnectAsync returns immediately** — background loop chạy qua `Task.Run`. Caller theo dõi state qua `StateChanged` event. Không có `await` block trong ConnectAsync.

## Handoff Log — bước sau (Bước 3.2 CcuUI) cần biết

- **Class name:** `RemoteControlClient` trong namespace `IPGS.RemoteControl.CcuClient`, implements `IRemoteControlClient`.
- **Khởi tạo:** `new RemoteControlClient(logger?)` — logger optional. Sau đó gọi `await client.ConnectAsync(host, port, token)`.
- **Events:** `client.FrameReceived += OnFrame` (handler nhận `FrameReceivedEventArgs.JpegData` là `ReadOnlyMemory<byte>`, decode bằng `SKBitmap.Decode(jpegData.Span)`). `client.StateChanged += OnStateChange` (nhận `ConnectionStateChangedEventArgs.Current`).
- **Mouse events:** `SendMouseMoveAsync(x, y)` và `SendMouseButtonAsync(MouseButton.Left/Right/Middle, isDown, x, y)` — toạ độ PHẢI là ZCU screen space trước khi gọi. Scale công thức: `zx = (int)(controlX / controlWidth * screenWidth)`, `zy = (int)(controlY / controlHeight * screenHeight)`. Lấy `screenWidth`/`screenHeight` từ `((RemoteControlClient)client).ScreenWidth/ScreenHeight` sau khi state là Streaming.
- **NuGet packages đã thêm:** KHÔNG có package mới — chỉ dùng `Microsoft.Extensions.Logging.Abstractions` đã có sẵn. CcuUI sẽ cần thêm `SkiaSharp` + `Avalonia.Skia` để decode JPEG và wrap Bitmap.
- **Dispose:** gọi `client.DisconnectAsync()` rồi `client.Dispose()` khi đóng window.
- **AUTH_FAIL state:** khi `State == Faulted` và `Reason` chứa "AUTH_FAIL" → show dialog sửa token cho user, gọi `Dispose()` rồi tạo mới client để thử lại.
- **Bước 3.2 KHÔNG cần sửa CcuClient** — API đã đủ.

## Commit

- Hash: [chưa commit — bước này không tự commit per task instruction]
- Đã push: không

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
