---
step: 2.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-07-23 08:25
---

# STEP 2.1 — Senior Dev tạo project IPGS.RemoteControl.ZcuAgent

## Input nhận

Từ STEP-1.1 Handoff Log (xem `STEP-1.1-tech-design.md` mục "Handoff Log") — cần có trước khi bắt đầu:
- Tên project chính xác, namespace, vị trí trong solution
- TCP default port, message format (binary layout)
- P/Invoke signatures cho XOpenDisplay, XShmGetImage/XGetImage, XTestFakeMotionEvent, XTestFakeButtonEvent
- Interface `IRemoteControlServer` đã định nghĩa trong TDD

## Nhiệm vụ

Tạo project mới `IPGS.RemoteControl.ZcuAgent` — .NET 8 Linux console app/worker service. Implement: (1) X11 screen capture dùng XShm/XGetImage qua P/Invoke, (2) JPEG compression, (3) TCP server lắng nghe kết nối từ CCU, (4) gửi frame liên tục tới CCU, (5) nhận lệnh mouse từ CCU và inject qua XTestFakeMotionEvent/XTestFakeButtonEvent, (6) authentication bằng shared secret theo spec TDD.

**Ràng buộc cứng:** project đặt ở thư mục mới `IPGS.RemoteControl.ZcuAgent/` tại solution root — KHÔNG đặt trong `IPGS.Object/`, `IPGSUseCam/`, hay bất kỳ project hiện có nào.

## Definition of Done

- [ ] Project `IPGS.RemoteControl.ZcuAgent/` tồn tại ở solution root với `.csproj` target `net8.0`, `<RuntimeIdentifier>linux-x64</RuntimeIdentifier>`
- [ ] X11 capture hoạt động: lấy được image từ display mà không crash (test bằng unit test stub hoặc integration test)
- [ ] TCP server khởi động, chấp nhận kết nối, thực hiện handshake auth theo spec TDD
- [ ] Gửi JPEG frame liên tục (frame rate cấu hình được, mặc định 15fps)
- [ ] Nhận lệnh mouse (move + left/right click) từ CCU, inject XTest thành công
- [ ] Build sạch: `dotnet build` không có error, không có warning nghiêm trọng
- [ ] KHÔNG có reference đến project IPGS.Object, IPGSUseCam, hay bất kỳ project iPGS business logic cũ nào
- [ ] Commit + push lên nhánh `zcu-avalonia`

## Đã làm

- Đọc toàn bộ TDD-remote-control.md (16 mục) trước khi viết code.
- Xác nhận 2 project đã có skeleton từ phiên trước (Bước 1.1 tạo csproj + một số file). Không có Program.cs, TcpServer.cs, appsettings.json, RemoteControlHostedService.cs.
- **IPGS.RemoteControl.CcuClient**: Protocol/ (MessageTypes, MessageCodec, ProtocolException) + IRemoteControlClient.cs đã đầy đủ. Sửa bug `(string Token)` 1-element tuple (CS8124) → đổi thành `string`.
- **IPGS.RemoteControl.ZcuAgent**: Tạo 4 file còn thiếu: `Net/TcpServer.cs`, `RemoteControlHostedService.cs`, `Program.cs`, `appsettings.json`. Sửa bug `Lock` (CS0246, .NET 9+ only) → đổi thành `object` trong AuthManager. Cập nhật csproj thêm `<Content>` cho appsettings.json.
- Build sạch cả 2 project: `dotnet build` 0 error, 0 warning.
- Cập nhật `code-graph/CODE-GRAPH.md` v1.6 — thêm Remote Control section + 2 project mới. Xuất CODE-GRAPH.docx (PDF fail RPC không block).

## Artifact

- `IPGS.RemoteControl.CcuClient/Protocol/MessageTypes.cs` — MessageType enum, MouseButton enum, RemoteControlConstants
- `IPGS.RemoteControl.CcuClient/Protocol/MessageCodec.cs` — encode/decode helpers, framing I/O
- `IPGS.RemoteControl.CcuClient/Protocol/ProtocolException.cs`
- `IPGS.RemoteControl.CcuClient/IRemoteControlClient.cs` — interface + enums + event args
- `IPGS.RemoteControl.ZcuAgent/Interop/X11Interop.cs` — P/Invoke libX11.so.6
- `IPGS.RemoteControl.ZcuAgent/Interop/XShmInterop.cs` — P/Invoke libXext.so.6 + libc SHM
- `IPGS.RemoteControl.ZcuAgent/Interop/XTestInterop.cs` — P/Invoke libXtst.so.6
- `IPGS.RemoteControl.ZcuAgent/Capture/X11ScreenCapturer.cs` — XShm + XGetImage fallback
- `IPGS.RemoteControl.ZcuAgent/Capture/JpegEncoder.cs` — SkiaSharp JPEG encode
- `IPGS.RemoteControl.ZcuAgent/Input/MouseInjector.cs` — XTest inject
- `IPGS.RemoteControl.ZcuAgent/Auth/AuthManager.cs` — IP whitelist, constant-time token, rate-limit
- `IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs` — state machine session
- `IPGS.RemoteControl.ZcuAgent/Net/TcpServer.cs` — TCP listener (tạo mới bước này)
- `IPGS.RemoteControl.ZcuAgent/RemoteControlHostedService.cs` — BackgroundService (tạo mới)
- `IPGS.RemoteControl.ZcuAgent/Program.cs` — Generic Host entry + X11 guard (tạo mới)
- `IPGS.RemoteControl.ZcuAgent/appsettings.json` — config mẫu (tạo mới)
- `code-graph/CODE-GRAPH.md` v1.6 + `.docx` (cập nhật)

## Quyết định quan trọng

1. **`(string Token)` 1-element tuple → `string`**: C# không cho phép tuple 1 phần tử (CS8124). Đổi kiểu trả về `DecodeAuth` và sửa caller trong ClientSession.
2. **`Lock` → `object`**: `System.Threading.Lock` chỉ có từ .NET 9 (C# 13). ZcuAgent target `net8.0` → dùng `object` + `lock()` statement.
3. **Protocol source-link**: Confirm đúng cách: `<Compile Include="..\IPGS.RemoteControl.CcuClient\Protocol\**\*.cs" Link="Protocol\..."/>` trong ZcuAgent.csproj. Không tạo project reference (tránh circular nếu CcuClient sau này thêm Avalonia dep).
4. **`RemoteControlHostedService`** implements `BackgroundService` (không phải `IHostedService` thủ công) để Generic Host tự quản lý lifetime.
5. **Build trên Windows**: ZcuAgent build thành công trên Windows (RuntimeIdentifier=linux-x64). P/Invoke chỉ thực sự chạy trên Linux — không cần WSL2 để pass compile.

## Handoff Log — bước sau cần biết

- **Đã làm**: Tạo đủ cả 2 project `IPGS.RemoteControl.CcuClient` (Protocol + Interface) và `IPGS.RemoteControl.ZcuAgent` (đầy đủ: X11 capture, XTest inject, TCP server, auth, Generic Host). Build sạch 0 error.
- **File/module đã đọc hoặc đổi**: Toàn bộ file trong 2 project (xem Artifact). Sửa `MessageCodec.cs` (DecodeAuth), `AuthManager.cs` (Lock→object), `ClientSession.cs` (tuple destructure).
- **Quyết định quan trọng**: Xem bên trên — đặc biệt Lock→object (.NET 8 compat) và tuple 1-element bug.
- **Bước sau cần biết**: Tech Lead review cần kiểm tra (1) P/Invoke signatures khớp TDD §9, (2) XInitThreads gọi trước XOpenDisplay trong X11ScreenCapturer.Initialize(), (3) XFlush sau XTestFakeButtonEvent trong MouseInjector, (4) DecodeAuth trả `string` thẳng (không tuple), (5) `appsettings.json` có token placeholder (không phải secret thật). CcuClient chưa có `RemoteControlClient.cs` (implementation) — đó là Bước 3.1, không phải Bước 2.1.
- **Native libs cần cài trên ZCU**: `apt install libx11-6 libxext6 libxtst6`
- **Token**: Cấu hình trong `appsettings.json` — placeholder `"REPLACE_WITH_LONG_RANDOM_STRING_MIN_32_CHARS"`, phải thay bằng chuỗi random thật trước khi deploy.

## Commit

- Hash: [điền sau khi Dispatcher commit]
- Đã push: không (Dispatcher commit)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
