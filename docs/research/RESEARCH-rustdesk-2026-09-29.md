# RESEARCH: RustDesk — Phân tích & Đề xuất cải tiến IPGS Remote Control

**Ngày:** 2026-09-29
**Repo nguồn:** https://github.com/rustdesk/rustdesk (shallow clone, commit mới nhất)
**License:** AGPL-3.0 — CHỈ HỌC Ý TƯỞNG/PATTERN, KHÔNG COPY CODE
**Tác giả nghiên cứu:** ANHNV_2025 / software@kztek.net
**Nhánh:** `research/rustdesk-2026-09-29`
**Mục đích:** Mode A — Cải tiến KZTEK IPGS Remote Control

---

## 1. Tổng quan RustDesk

RustDesk là phần mềm remote desktop mã nguồn mở (Rust + Flutter), hỗ trợ tự host rendezvous/relay server. Giải quyết vấn đề kiểm soát máy tính từ xa theo cả hai hướng (điều khiển và bị điều khiển), trên đa nền tảng (Windows, macOS, Linux X11/Wayland, Android, iOS). Nổi bật: không cần mở port công khai, hỗ trợ codec phần cứng, clipboard đa định dạng, file transfer, multi-monitor.

**Độ trưởng thành:** ~90k stars, ~12k forks, active commits hàng ngày (2026). Production-grade.

---

## 2. Cấu trúc RustDesk (modules liên quan)

```
src/
  clipboard.rs            — Đồng bộ clipboard đa định dạng (text/html/rtf/image/file), dùng arboard
  clipboard_file.rs       — CLIPRDR protocol (file drag-drop, RDP clipboard file transfer)
  keyboard.rs             — 3 keyboard modes: legacy / map / translate; grab loop; modifier tracking
  server/
    video_service.rs      — Capture (DXGI/DesktopDup Win, X11 SHM, scrap), multi-codec, multi-display
    video_qos.rs          — Adaptive FPS (15→120) + bitrate điều tiết theo RTT đo bằng TestDelay
    display_service.rs    — Phát hiện multi-monitor, Wayland layout remap, RandR
    input_service.rs      — Inject input: enigo+rdev (cross-platform), uinput (Wayland), rdp_input
    clipboard_service.rs  — Server-side clipboard polling và push về clients
  client/
    io_loop.rs            — Client receive loop, file transfer jobs, clipboard integration
  privacy_mode/           — Plugin pattern: Win Magnifier / Win VirtualDisplay / macOS overlay
  platform/
    linux.rs              — is_x11(), Wayland detection, GTK sudo
    windows.rs            — DXGI, Virtual Display, SAS (Ctrl+Alt+Del)
  wayland.rs              — PipeWire/portal capture + Wayland input (uinput / RemoteDesktop portal)
```

---

## 3. Phân tích kỹ thuật RustDesk (các mảng liên quan đến KZTEK)

### 3.1 Codec & Adaptive Quality

RustDesk hỗ trợ nhiều codec: JPEG (fallback), VP8/VP9 (libvpx), AV1 (libaom), H.264/H.265 (hardware codec — VRAM/HwRam). Module `video_qos.rs` đo TestDelay (ping đo hàng đợi trước luồng video), điều chỉnh FPS từ `INIT_FPS=15` đến `MAX_FPS=120` và bitrate ratio theo công thức phức tạp (có hysteresis, cooldown 3s, minimum `MIN_AUTO_FPS=5`, startup acceleration).

Pattern học được: gửi TestDelay packet trên cùng stream video; bên nhận trả về thời gian nhận; delta = "excess delay" phản ánh congestion của buffer TCP, không phải RTT mạng thuần. Khi `excess > 150ms` liên tiếp 3 lần → giảm FPS 20%. Khi `excess < 150ms` → tăng dần FPS sau 2 replies tốt.

### 3.2 Clipboard Sync Đa định dạng

`clipboard.rs` dùng crate `arboard` (wrapper cross-platform cho X11 selection, Win32 OLE clipboard, macOS NSPasteboard). Hỗ trợ `ClipboardFormat`: Text, Html, Rtf, ImageRgba, ImagePng, ImageSvg, FileUrl (feature `unix-file-copy-paste`), và custom formats (e.g. Excel XML). Polling mỗi `CLIPBOARD_INTERVAL=333ms` để phát hiện thay đổi. Hai chiều (S→C và C→S). Có `RUSTDESK_CLIPBOARD_OWNER_FORMAT` để tránh echo-loop.

### 3.3 Keyboard Modes

3 chế độ (keyboard.rs, line 455-458):
- **legacy**: gửi keysym X11, dùng `XKeysymToKeycode` phía agent — tương đương cách KZTEK đang làm.
- **map**: gửi raw keycode (scancode vật lý), không qua keysym → tránh vấn đề layout khác biệt giữa client và server.
- **translate**: dùng Unicode → luôn gửi ký tự đúng bất kể layout, dùng `set_get_key_unicode(true)`.

Ngoài ra có grab loop (rdev crate) để chặn phím trước khi OS xử lý, tránh phím tắt hệ thống bị capture (e.g. Alt+Tab, Super+...).

Ctrl+Alt+Del: Windows agent dùng `SendSAS()` (Windows API gọi thẳng vào Secure Attention Sequence). Linux: inject qua evdev/uinput.

### 3.4 Multi-Monitor

`display_service.rs` theo dõi danh sách màn hình (`try_get_displays()`). Client có thể chọn `display_idx`. Wayland layout remap: nếu compositor di chuyển monitor giữa session, tọa độ input được remap theo layout mới (`remap_wayland_uinput_coord`). Server gửi `SyncDisplayInfo` message khi thay đổi.

### 3.5 Wayland Support

`src/server/wayland.rs` dùng PipeWire portal (`xdg-desktop-portal`) để capture. Input injection qua: (a) uinput (kernel virtual device, yêu cầu quyền), hoặc (b) RemoteDesktop portal (org.freedesktop.portal.RemoteDesktop) — không cần quyền đặc biệt. Phát hiện runtime: `is_x11()` kiểm tra env `WAYLAND_DISPLAY`, `DISPLAY`.

### 3.6 File Transfer

Triển khai như một loại kết nối riêng (`ConnType::FILE_TRANSFER`). Protocol: chunked binary với `FileTransferSendConfirmRequest` (offset block, skip, resume). Bên gửi có thể pause/cancel. `file_trait.rs` trong client quản lý job queue. Hỗ trợ resume (server ghi nhớ offset).

### 3.7 Privacy Mode

Plugin pattern (`privacy_mode/`): mỗi platform có implementation riêng. Windows: Magnifier API (Win Mag) hoặc Virtual Display. macOS: overlay window. Linux: chưa native, placeholder. Trait `PrivacyMode` với `turn_on_privacy` / `turn_off_privacy`.

### 3.8 Session Reconnect & Relay

RustDesk có rendezvous server (STUN-like) + relay server. Client kết nối trực tiếp nếu có thể (hole-punch UDP), qua relay nếu không. Hỗ trợ session resume khi reboot (reconnect grace window, `3953`). KZTEK chỉ dùng TCP trực tiếp — phù hợp LAN nội bộ, không cần relay.

---

## 4. Hiện trạng KZTEK IPGS Remote Control

Đọc trực tiếp từ code (xem file liệt kê trong Bước 2):

| Mảng | Hiện trạng KZTEK | File tham chiếu |
|---|---|---|
| Capture | X11 MIT-SHM + XGetImage fallback, Linux only | `ZcuAgent/Capture/X11ScreenCapturer.cs` |
| Encode | JPEG cố định 70% / 15fps, không adaptive | `ZcuAgent/Capture/JpegEncoder.cs` |
| Keyboard | XTest + keysym mapping (3-tier: special, unicode, fallback). Không có mode switching, không có grab loop | `ZcuAgent/Input/KeyboardInjector.cs`, `CcuUI/KeyboardMapper.cs` |
| Mouse | XTest move/button/wheel. Không có relative mode | `ZcuAgent/Input/MouseInjector.cs` |
| Clipboard | Chỉ CCU→ZCU (text), xclip set. Không ZCU→CCU, không image/file | `ZcuAgent/Net/ClientSession.cs:SetClipboardAsync()` |
| Multi-monitor | Không hỗ trợ, chỉ primary X11 root window | `X11ScreenCapturer.cs`: `XDefaultRootWindow` |
| Wayland | Không hỗ trợ, cứng yêu cầu X11 | `Program.cs` (enforce X11) |
| File Transfer | Không có. RemoteMountService là CIFS/NFS mount qua SSH, khác hoàn toàn | `CcuClient/RemoteMountService.cs` |
| Privacy Mode | Chỉ log, chưa implement | `ClientSession.cs:359` |
| Ctrl+Alt+Del | Không có nút riêng; người dùng phải tự bấm tổ hợp → OS CCU hay chặn | Chưa có |
| Session | 1 client/lần (v1), reconnect với jitter, max 10 lần | `TcpServer.cs`, `RemoteControlClient.cs` |
| Transport | Plain TCP, shared token + IP whitelist, rate-limit | `AuthManager.cs`, `RemoteControlConstants.cs` |
| Cursor | Không gửi hình cursor; client luôn thấy cursor mặc định | Chưa có |
| Audio | Không hỗ trợ | Chưa có |

---

## 5. So sánh trực tiếp KZTEK vs RustDesk

| Tính năng | RustDesk | KZTEK hiện tại | Khoảng trống |
|---|---|---|---|
| Adaptive FPS/Quality | Có, 1-120fps, TestDelay-based | Không, cố định 15fps/70% JPEG | Lớn |
| Clipboard CCU→ZCU | Có (text+html+rtf+image+file) | Có (text only, xclip) | Trung bình |
| Clipboard ZCU→CCU | Có, polling 333ms | Không | Lớn |
| Image/File clipboard | Có | Không | Trung bình |
| Keyboard: map/translate mode | Có | Không (chỉ legacy keysym) | Trung bình |
| Ctrl+Alt+Del toolbar | Có (UI button) | Không | Nhỏ-Trung bình |
| Multi-monitor | Có | Không | Trung bình |
| Wayland capture | Có (PipeWire) | Không | Lớn |
| File Transfer | Có (session riêng, chunked, resume) | Không | Lớn |
| Privacy Mode (blank screen) | Có | Placeholder only | Trung bình |
| Cursor shape sync | Có | Không | Nhỏ |
| Audio | Có | Không | Trung bình |
| TLS/Encryption | Có (ECDH + AES-GCM) | Không (plain TCP) | Trung bình |
| Relative mouse mode | Có | Không | Nhỏ |

---

## 6. Bảng đề xuất cải tiến (Top 8 — xếp theo ưu tiên)

> Lưu ý: RustDesk dùng AGPL-3.0. Mọi đề xuất dưới đây CHỈ học ý tưởng/pattern — không copy code Rust. Implement bằng C# (.NET) từ đầu.

| # | Đề xuất | Hiện trạng KZTEK (đang có / chưa có) | Học từ đâu (file/pattern cụ thể RustDesk) | Lý do thay đổi (vấn đề / khoảng trống cụ thể) | Áp dụng vào đâu trong KZTEK (file cụ thể) | Đạt được gì (kết quả cụ thể, quan sát được) | Rủi ro / Effort |
|---|---|---|---|---|---|---|---|
| **1** | **Adaptive FPS + Quality dựa trên PING RTT** | Cố định 15fps, 70% JPEG, không thay đổi theo mạng. `AgentOptions.TargetFps` và `JpegQuality` là hằng số runtime. | `src/server/video_qos.rs`: TestDelay packet gửi cùng stream video; đo excess delay; giảm FPS khi >150ms liên tiếp 3 lần, tăng khi <150ms 2 lần. | Máy ZCU yếu (Intel Atom/Celeron J3xxx) hiện tốn 0.55-0.75s/frame — không đạt 15fps. Mạng chậm gây buffer đầy nhưng server vẫn push full rate. Kết quả: lag, kết nối rớt. | `ZcuAgent/Net/ClientSession.cs` (capture loop), `Protocol/MessageTypes.cs` (thêm TestDelay message), `CcuClient/RemoteControlClient.cs` (trả TestDelay reply) | FPS tự giảm xuống 5-8fps khi mạng yếu thay vì làm đầy buffer; tự tăng lên 20-25fps khi mạng tốt. Cắt hiện tượng "remote lag" trên máy Atom (hiện tốn 30s timeout). | Effort: Trung bình. Cần thêm 1 message type, logic điều chỉnh trong capture loop. Rủi ro thấp — fallback: giữ nguyên fixed FPS. |
| **2** | **Clipboard ZCU→CCU (chiều ngược lại)** | CCU gửi text cho ZCU (xclip set) — đã có. ZCU không tự push clipboard về CCU. | `src/server/clipboard_service.rs`: polling `xclip -o -selection clipboard` mỗi 333ms, so hash với lần trước, push `ClipboardData` S→C khi thay đổi. | Người vận hành copy text từ màn hình ZCU nhưng không paste được về CCU — phải tự nhập lại. Đây là gap thao tác hàng ngày rõ ràng nhất. | `ZcuAgent/Net/ClientSession.cs` (thêm clipboard-poll task song song capture loop). `CcuClient/RemoteControlClient.cs` (xử lý ClipboardData S→C — event đã có). `CcuUI` (wire up event → Avalonia Clipboard.SetTextAsync). | Người dùng copy text trên ZCU, tự động hiện trong clipboard CCU trong vòng <1s mà không cần thao tác thêm. | Effort: Thấp-Trung bình. Dùng lại `xclip -o` (đã có xclip trên agent). Cần thêm 1 background task + anti-echo (lưu hash của lần mình vừa set). Rủi ro: echo-loop nếu không có anti-echo. |
| **3** | **Wayland/PipeWire Capture Support** | Chỉ X11, cứng yêu cầu `DISPLAY` env. Ubuntu 22.04+ mặc định Wayland. | `src/server/wayland.rs`: dùng `xdg-desktop-portal` D-Bus API (org.freedesktop.portal.ScreenCast) để capture. Input injection qua `org.freedesktop.portal.RemoteDesktop` portal — không cần `uinput` nếu portal khả dụng. | Ubuntu 22.04 LTS mặc định Wayland (GNOME). Khi deploy lên ZCU mới chạy Ubuntu 22.04, agent không khởi động được (`XOpenDisplay` trả null). Hiện phải ép `GDK_BACKEND=x11` và `DISPLAY=:0` — không sustainable. | `ZcuAgent/Capture/` (thêm `PipeWireCapturer.cs`), `ZcuAgent/Input/` (thêm `PortalInputInjector.cs` dùng D-Bus RemoteDesktop portal hoặc `uinput` device). `ZcuAgent/RemoteControlHostedService.cs` (runtime detect X11 vs Wayland). | Agent chạy được trên Ubuntu 22.04 Wayland mà không cần workaround env. | Effort: Cao. PipeWire portal phức tạp (D-Bus, screencast portal, token persistence). Nên dùng NuGet binding `Tmds.DBus` hoặc gọi `dbus-send`. Giai đoạn 1: thêm detection + fail-fast với thông báo rõ ràng. Giai đoạn 2: implement portal. |
| **4** | **Transport Encryption (TLS)** | Plain TCP, shared static token, không mã hóa. Bất kỳ ai sniff LAN đều thấy JPEG frames + token. | RustDesk dùng ECDH key exchange + AES-128-GCM stream (`hbb_common` crate). Đơn giản hơn: có thể dùng TLS thuần. | KZTEK deploy trong LAN nội bộ, nhưng một số dự án có audit security. Plain TCP lộ token + toàn bộ màn hình ZCU ra LAN. Token tĩnh không rotate. | `ZcuAgent/Net/TcpServer.cs` (wrap `TcpClient.GetStream()` bằng `SslStream`). `CcuClient/RemoteControlClient.cs` (tương tự phía client). `ZcuAgent/Auth/AuthManager.cs` (giữ nguyên, TLS xử lý transport security). | Token không lộ qua LAN. Frames mã hóa. Audit security pass. Đáp ứng yêu cầu ISO 27001 nếu khách hàng đòi. | Effort: Thấp-Trung bình. .NET `SslStream` built-in. Cần gen self-signed cert cho agent, CCU trust cert. Rủi ro: cần quản lý cert lifecycle (rotation). |
| **5** | **Nút Ctrl+Alt+Del trong UI** | Không có nút riêng. Người dùng phải bấm tổ hợp nhưng OS CCU (Windows) bắt SAS trước, không bao giờ qua được. | RustDesk: UI button "Ctrl+Alt+Del" → client gửi key event sequence. Windows agent dùng `SendSAS()`. Linux agent: inject `KEY_LEFTCTRL + KEY_LEFTALT + KEY_DELETE` qua XTest. | Khi ZCU cần đăng nhập lại hoặc mở Task Manager, không có cách nào gửi Ctrl+Alt+Del vì Windows CCU bắt SAS. Phải SSH vào ZCU để xử lý. | `CcuUI/Views/` (thêm toolbar button hoặc menu item "Gửi Ctrl+Alt+Del"). `CcuClient/IRemoteControlClient.cs` + `RemoteControlClient.cs` (thêm `SendCtrlAltDelAsync()` gửi 3 KeyEvent riêng biệt). Phía agent không cần thay đổi — XTest đã xử lý được. | Người vận hành click 1 nút trong UI để gửi Ctrl+Alt+Del tới ZCU mà không bị OS CCU bắt. | Effort: Thấp. Logic client đơn giản: gửi `XK_Control_L down`, `XK_Alt_L down`, `XK_Delete down`, rồi release theo thứ tự ngược. Rủi ro rất thấp. |
| **6** | **Multi-Monitor: chọn màn hình cần xem** | Chỉ capture `XDefaultRootWindow` — primary screen. Không có concept display index. | `src/server/display_service.rs`: `try_get_displays()` liệt kê screens. Client gửi `SwitchDisplay(idx)` message. Server restart capture với `display_idx`. | Một số ZCU có dual monitor (VD: máy kiosk có màn hình kiosk + màn hình quản trị riêng). Hiện chỉ xem được primary, không xem được secondary. | `Protocol/MessageTypes.cs` (thêm `SwitchDisplay = 0x53`). `ZcuAgent/Capture/X11ScreenCapturer.cs` (tham số hóa screen index thay vì hardcode `XDefaultScreen`). `ZcuAgent/Net/ClientSession.cs` (xử lý SwitchDisplay message). `CcuUI` (dropdown chọn màn hình trong session toolbar). | Trong session, có thể switch giữa các màn hình ZCU mà không cần ngắt kết nối. | Effort: Trung bình. X11 đa screen: `XOpenDisplay` có thể truyền `":0.1"` cho screen 1. Cần thêm 1 message type và restart capture task. |
| **7** | **Keyboard Mode: Raw Scancode (scan mode)** | Chỉ có "legacy" keysym mode. Khi client (CCU) có bàn phím layout khác ZCU (VD: CCU dùng Unikey VNI, ZCU dùng layout US), ký tự gửi sai. | `src/keyboard.rs:456`: "map" mode gửi raw keycode (evdev code), không qua keysym. Agent nhận code → inject thẳng qua `uinput` hoặc `XTestFakeKeyEvent` với keycode. Giải quyết mismatch layout. | Người dùng nhập tiếng Việt hoặc ký tự đặc biệt trên CCU nhưng ZCU nhận keysym sai vì layout mapping khác. Hiện Tier 2 (Unicode codepoint) phần nào giúp nhưng bị skip khi Ctrl held, và không đúng khi layout ZCU khác. | `Protocol/MessageTypes.cs` (thêm `KeyEventRaw = 0x41` gửi evdev scancode thay vì keysym). `ZcuAgent/Input/KeyboardInjector.cs` (thêm `SendRawKeycode(ushort evdev, bool isDown)` via `XTestFakeKeyEvent` với keycode trực tiếp). `CcuUI/KeyboardMapper.cs` + `CcuUI` settings (toggle mode). | Khi bật scan mode, bấm phím 'A' trên CCU layout → ZCU nhận đúng 'A' bất kể ZCU dùng layout gì (vì keycode vật lý giống nhau). | Effort: Trung bình. Cần biết evdev scancode ở phía CCU (Windows: VirtualKey → scancode via MapVirtualKey). Cần xử lý khác nhau giữa Windows client và Linux client. |
| **8** | **File Transfer (chunked, bidirectional)** | Không có. RemoteMountService là CIFS/NFS mount qua SSH — khác hoàn toàn, cần setup phức tạp. | `src/client/io_loop.rs`: file transfer dùng `ConnType::FILE_TRANSFER` — session riêng song song với session remote. Chunked (`FileChunk` message), có resume (offset block). | Người vận hành cần copy file cấu hình / script lên ZCU hoặc lấy log về. Hiện phải dùng SSH/SCP riêng hoặc setup CIFS mount. | `Protocol/MessageTypes.cs` (thêm group `0x60-0x6F` cho file transfer — đã reserved). `CcuClient/` (thêm `RemoteFileTransferService.cs`). `ZcuAgent/Net/` (thêm `FileTransferSession.cs`). `CcuUI/Views/` (thêm `FileTransferWindow.axaml`). | Kéo thả file từ CCU lên ZCU và ngược lại trong UI remote, không cần SSH riêng. Progress bar hiển thị %. | Effort: Cao. Cần thêm nhiều message type, quản lý job concurrent, resume logic. Nên làm sau khi các mục 1-5 ổn định. |

---

## 7. Thông tin repo

- **License:** AGPL-3.0 — Toàn bộ code KZTEK implement lại từ đầu bằng C#/.NET, không copy code Rust
- **Stars:** ~90,000 (2026-09-29)
- **Forks:** ~12,000
- **Hoạt động:** Commits hàng ngày, community lớn
- **Version:** Không có version tag cố định trong shallow clone — theo trunk (main branch)

---

## 8. Trạng thái áp dụng đề xuất

| # | Đề xuất | Trạng thái |
|---|---|---|
| 1 | Adaptive FPS + Quality | Chờ user duyệt |
| 2 | Clipboard ZCU→CCU | Chờ user duyệt |
| 3 | Wayland/PipeWire Capture | Chờ user duyệt |
| 4 | TLS Encryption | Chờ user duyệt |
| 5 | Nút Ctrl+Alt+Del | Chờ user duyệt |
| 6 | Multi-Monitor | Chờ user duyệt |
| 7 | Keyboard Scan Mode | Chờ user duyệt |
| 8 | File Transfer | Chờ user duyệt |

---

*Ghi chú: Tài liệu này là kết quả nghiên cứu thuần — KHÔNG có thay đổi code nào được áp dụng. Mọi triển khai phải được user xác nhận từng mục tại Bước 4 của quy trình WF-GITHUB-RESEARCH.*
