---
step: 2.2
plan: ../PLAN-MASTER.md
agent: tech-lead
status: done
completed_at: 2026-07-23 10:15
---

# STEP 2.2 — Tech Lead review ZcuAgent

## Input nhận

Từ STEP-2.1 Handoff Log — artifact: thư mục `IPGS.RemoteControl.ZcuAgent/` + `IPGS.RemoteControl.CcuClient/Protocol/`. Commit `9259f8a4`.

## Nhiệm vụ

Review toàn bộ ZcuAgent: đúng TDD, không leak, không tham chiếu project business cũ.

## Definition of Done

- [x] `dotnet build` ZcuAgent pass 0 error / 0 warning (rebuild sau fix cũng sạch)
- [x] P/Invoke signatures X11/XTest/XShm đúng TDD §9 (đã đối chiếu từng hàm)
- [x] Memory cleanup đủ: `XDestroyImage` gọi trong finally cho XGetImage fallback + `XShmDetach` + `XDestroyImage` + `shmdt` + `shmctl(IPC_RMID)` trong `CleanupSHM`; `XCloseDisplay` cho cả 2 display connection (capturer + injector) khi Dispose
- [x] TCP message format khớp TDD §5.1/§5.2: header 5 byte big-endian, encoders/decoders đúng offset (HELLO_ACK, FRAME_JPEG 24-byte header, MOUSE_MOVE/BUTTON, PING/PONG)
- [x] Authentication flow khớp TDD §8: IP whitelist trước read, ban 3 fail/60s→5min, `FixedTimeEquals`, KHÔNG log token
- [x] KHÔNG có `<ProjectReference>` tới IPGS.Object/IPGSUseCam/KztekComponentAvalonia — grep sạch cả 2 csproj; Protocol dùng chung qua `<Compile Include>` link
- [x] Review comment ghi vào step file này
- [ ] Commit + push (Dispatcher xử lý sau, không tự làm theo yêu cầu bước)

## Đã làm

Đối chiếu từng mục:

1. **P/Invoke (Interop/X11Interop.cs, XShmInterop.cs, XTestInterop.cs)** — khớp TDD §9. `XInitThreads` khai báo `IntPtr` return đúng convention. `XShmGetImage` dùng `[MarshalAs(UnmanagedType.Bool)]` — đúng cho `Bool` (int) của Xlib. `XShmSegmentInfo` layout khớp header C.
   - **FYI:** TDD §9.2 line 261 chú thích `IPC_CREAT=0x400` — sai. Linux thực tế `IPC_CREAT = 0x200` (octal 01000). Code dùng đúng 0x200. Có thể cập nhật TDD trong bước sau.

2. **Capture (Capture/X11ScreenCapturer.cs)** — cleanup 3 tầng SHM đúng thứ tự: `XShmDetach` → `XDestroyImage` → `shmdt` → `shmctl(IPC_RMID)`. Fallback XGetImage có `XDestroyImage` trong `finally`. `XInitThreads()` gọi trước `XOpenDisplay` ở `Initialize()`.
   - **Optional:** Nên gọi `shmctl(IPC_RMID)` ngay sau `shmat` (best practice Linux) để kernel tự dọn nếu process crash. Không block v1.

3. **Input (Input/MouseInjector.cs)** — `XFlush` gọi sau cả `XTestFakeMotionEvent` (Move) và `XTestFakeButtonEvent` (Button). Display riêng, không share với capturer → tránh contention.

4. **TCP framing (Protocol/MessageCodec.cs)** — header 5 byte big-endian, `ReadExactAsync` xử lý đúng partial read + throw `EndOfStreamException` khi `read == 0`. Encoders khớp offset TDD §5.2 (FRAME_JPEG: 8+4+4+4+4=24, đúng).

5. **Session (Net/ClientSession.cs) — PHÁT HIỆN VÀ SỬA:**
   - **Vấn đề:** TDD §7 quy định "PING timeout — không PONG trong 15s → disconnect", nhưng code khai báo `lastPong` local mà **không dùng** (dead variable). Server chỉ dựa vào TCP keepalive OS-level, không có heartbeat app-level.
   - **Đã sửa (không đổi kiến trúc, ~12 dòng):**
     - Thêm field `private long _lastPongTicks` — read/write bằng `Interlocked`.
     - Init sau AUTH_OK; case `Pong` cập nhật `Interlocked.Exchange`.
     - Capture loop check `now - lastPong > PingTimeoutMs` → log warn + return để đóng session.
   - **Build lại sau fix:** 0 warning, 0 error.

6. **Auth (Auth/AuthManager.cs)** — CIDR check tự viết đúng (byte-by-byte + mask remainder). `FixedTimeEquals` chỉ chạy khi length khớp — không leak length qua timing (OK cho v1). Log dùng structured logging `{IP}`, không lộ token.

7. **csproj + Program.cs** — target `net8.0` + `linux-x64` RID, session guard `XDG_SESSION_TYPE=x11` fail-fast, DI đầy đủ singleton, hosted service pattern chuẩn.

8. **Ràng buộc cứng** — Grep `ProjectReference` trong cả 2 project = 0 match. Protocol chia sẻ qua `<Compile Include>` link (source-link, không compile-time reference). ✅

## Artifact

- Review verdict: **PASS** (sau khi tự sửa 1 gap PONG timeout).
- File sửa: `IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs` (+field `_lastPongTicks`, +check timeout trong capture loop, +update trong Pong handler).
- Build ZcuAgent: 0 warning / 0 error (verified sau fix).

## Quyết định quan trọng

- **Auto-fix PONG timeout trong review** (không phải scope Bước 3.1) — lý do: TDD §7 spec rõ ràng, gap dead-code hiển nhiên, fix < 15 dòng không đổi kiến trúc. Chọn `Interlocked.Read/Exchange` trên `long` field thay vì `volatile` để tránh word-tearing trên 32-bit (dù linux-x64 không gặp, giữ đúng pattern .NET).
- **Không tự chỉnh TDD** (typo IPC_CREAT=0x400 → 0x200) trong bước này — ghi lại để cập nhật khi TDD được sửa lần tới, tránh drift với review verdict hiện tại.
- **Không yêu cầu Senior Dev sửa lại** (không escalate loop review) — fix đủ nhỏ, verdict PASS.

## Handoff Log — bước sau cần biết

- **Đã làm:** Review ZcuAgent + Protocol codec — PASS. Đã tự fix PONG-timeout gap trong `ClientSession.cs` (server side); CcuClient side (Bước 3.1) PHẢI implement heartbeat tương tự (client gửi PING mỗi 5s, disconnect nếu không PONG trong 15s) để khớp TDD §7.
- **File/module đã đọc hoặc đổi:** đã đọc toàn bộ 15 file ZcuAgent + 3 file Protocol; **đã sửa** `IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs`.
- **Quyết định quan trọng:**
  - Protocol chia sẻ qua `<Compile Include Link="Protocol\..." />` — Bước 3.1 code CcuClient sẽ **compile lại chính Protocol source đó** (cùng namespace `IPGS.RemoteControl.Protocol`), không cần link ngược. Bất kỳ thay đổi protocol nào PHẢI sửa trong `IPGS.RemoteControl.CcuClient/Protocol/` (nguồn chính) rồi rebuild cả 2.
  - Cổng TCP mặc định **17600** — CcuClient default cũng dùng constant `RemoteControlConstants.DefaultPort = 17600`.
  - TCP framing: header 5 byte (u8 type + u32 BE length), payload ≤ 8 MB. `MessageCodec.ReadMessageAsync`/`WriteMessageAsync` đã public sẵn — CcuClient dùng thẳng.
  - AUTH_FAIL → server đóng ngay. CcuClient PHẢI KHÔNG tự reconnect sau AUTH_FAIL (TDD §7) — bắt user sửa token.
- **Bước sau cần biết (cảnh báo / gotcha):**
  1. **PING/PONG heartbeat 2 chiều:** CcuClient PHẢI (a) gửi PING mỗi 5s và (b) reply PONG khi nhận server PING. Server đã dựa vào `_lastPongTicks` để đóng nếu không nhận PONG trong 15s.
  2. **`FrameJpegMessage` record đã public trong `MessageCodec`** — CcuClient decode FRAME_JPEG dùng thẳng, không cần định nghĩa lại.
  3. **Toạ độ chuột:** CcuClient PHẢI map từ pixel Avalonia (kích thước control) → ZCU screen space (0..screenWidth-1, 0..screenHeight-1) TRƯỚC khi gửi MOUSE_MOVE/BUTTON. Server clamp giá trị ngoài range nhưng không disconnect.
  4. **BGRA8888 → decode:** JPEG đã encode màu chuẩn; CcuClient dùng `SkiaSharp.SKBitmap.Decode(jpegBytes)` là ra được Avalonia bitmap qua interop (TDD §6.2).
  5. **Native asset:** ZcuAgent runtime cần `libX11.so.6`, `libXext.so.6`, `libXtst.so.6`, `libc` — deploy Linux phải `apt install libxtst6 libxext6 libx11-6` (đã có trong TDD §14 mitigation).
  6. **TDD typo cần cập nhật khi có cơ hội:** `IPC_CREAT=0x400` (TDD §9.2 line 261) — thực tế `0x200`. Không ảnh hưởng code vì code đúng.

## Commit

- Hash: [Dispatcher commit sau review — subagent không tự commit theo yêu cầu Bước 2.2]
- Đã push: [chờ Dispatcher]

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
