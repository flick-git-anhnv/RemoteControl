---
title: Báo cáo khảo sát toàn hệ thống Remote Control CCU↔ZCU
id: BUG-remote-control-audit-2026-07-26
created: 2026-07-26
author: Senior Developer (3 khảo sát song song) + Dispatcher tổng hợp
status: Đã xử lý (2026-07-26) — 39/41 fix xong, 2 mục (S5, S6) cố ý giữ nguyên theo quyết định user; review PASS, build 0 error cả 3 project
severity: P1
updated: 2026-07-26
---

# BUG-remote-control-audit-2026-07-26 — Khảo sát toàn hệ thống Remote Control

## 1. Phạm vi khảo sát

Khảo sát **chỉ-đọc** toàn bộ mã nguồn 3 project của hệ thống Remote Control CCU↔ZCU,
thực hiện bởi 3 subagent `senior-developer` chạy song song ngày 2026-07-26:

| Project | Vai trò | Số file .cs đã đọc |
|---|---|---|
| `IPGS.RemoteControl.ZcuAgent` | Linux service — X11 capture (XShm/XGetImage), inject chuột/phím (XTest), TCP server | 15 |
| `IPGS.RemoteControl.CcuClient` | Thư viện .NET — TCP client, protocol codec, SSH installer, WoL, discovery | 11 |
| `IPGS.RemoteControl.CcuUI` | App Avalonia độc lập (win-x64 + linux-x64) — viewer/manager | 20 + 5 `.axaml` |

**Tổng: 41 phát hiện.** Không chạy build/test trong giai đoạn khảo sát.

Thứ tự đánh giá theo chuẩn dự án: **correctness > security > resource leak/race > maintainability > performance**.

---

## 2. Nhóm 1 — 🔴 Mất dữ liệu / hỏng chức năng người dùng (6)

### A1 — Xóa 1 cron job phá hỏng toàn bộ crontab
**Vị trí:** `IPGS.RemoteControl.CcuUI/Views/CronJobWindow.axaml.cs:192-196`

```csharp
string newCrontab = string.Join("\n", lines) + "\n";
var cmdWrite = ssh.CreateCommand($"echo \"{newCrontab.Replace("\"", "\\\"").Replace("\n", "\\n")}\" | crontab -");
```

**Kịch bản fail:** Máy có 3 cron job, user xóa 1. `echo` (bash builtin, không có `-e`) in nguyên văn
chuỗi `\n` chứ không dịch thành xuống dòng → crontab mới là **1 dòng duy nhất** chứa literal `\n`
→ cron parse thất bại → **cả 2 job còn lại chết**.

**Phụ:** nội dung trong `"..."` bị shell remote expand `$VAR`/backtick — job chứa `$HOME` bị expand sai trước khi ghi.

**Hướng xử lý:** dùng `printf '%s'`, hoặc upload qua SFTP rồi `crontab <file>`. Không escape thủ công.

### A2 — Clone profile khi "Thêm từ quét mạng" làm mất MAC address
**Vị trí:** `IPGS.RemoteControl.CcuUI/Views/NetworkScanWindow.axaml.cs:126-139`

Hàm `OnAddFoundClick` copy đủ mọi field **trừ `MacAddress`** — trong khi clone chuẩn tại
`ConnectionEntryWindow.axaml.cs:144-158` có copy đầy đủ.

**Kịch bản fail:** máy đã có profile kèm MAC (để Wake-on-LAN) → user quét mạng, bấm "Thêm" trùng host
→ Save đè lên → MAC biến mất → **WoL hỏng**.

Đây đúng là chủng bug MAC-không-save đã từng fix ở commit `d1ab288` và `38efbb1` — tái phát ở đường code khác.

### A3 — Race giữa `SessionRecorder.Dispose` và `AddFrame` xuyên thread
**Vị trí:** `IPGS.RemoteControl.CcuUI/Services/SessionRecorder.cs:39-47`
+ `Views/RemoteScreenWindow.axaml.cs:83-88,146`

- `OnFrameReceived` chạy trên **thread nhận TCP** của CcuClient → gọi `_recorder.AddFrame(...)`
- `OnRecordClick` (stop) và `OnClosed` gọi `_recorder?.Dispose()` trên **UI thread** — không lock,
  không unsubscribe `FrameReceived` trước

**Kịch bản fail:** đang record 15fps, user bấm Stop → giữa `if (_recorder != null)` và `AddFrame`,
recorder bị dispose → `_stream.Position` ném `ObjectDisposedException` **bên trong event invocation của client**
→ có thể giết receive-loop → **mất luôn stream màn hình**.

`AddFrame` không guard `CanWrite`; `Dispose` không thread-safe. `OnClosed` còn dispose recorder
trước khi `DisconnectAsync` — frame vẫn tới trong khoảng đó.

### A4 — Treo UI 30 giây trong Setup Wizard
**Vị trí:** `IPGS.RemoteControl.CcuUI/Views/ZcuSetupWizardWindow.axaml.cs:192-198`

`FindZcuAgentPublishDir()` gọi đồng bộ trong `OnStartInstallClick` (dòng 90) trước mọi `await`.
Nhánh auto-publish chạy `Process.Start` + `proc.WaitForExit(30000)` → **UI đơ tối đa 30 giây**
(progress bar và log console đều đứng hình).

### A5 — Sai đường dẫn `./~/` làm cài `.sh`/`.run` luôn thất bại
**Vị trí:** `IPGS.RemoteControl.CcuClient/RemoteAppInstallService.cs:77`

`sudo -S ./~/{fileName}` — `~` sau `./` là ký tự literal, không được shell expand
→ `./~/file.sh` không tồn tại → **nhánh cài installer script luôn fail**. Đúng phải là `~/{fileName}` hoặc `$HOME/{fileName}`.

### A6 — Nút "Add Session" của Multi-Remote không hoạt động
**Vị trí:** `IPGS.RemoteControl.CcuUI/Views/MultiRemoteWindow.axaml.cs:311-315`

`OnAddSessionClick` mở `ConnectionEntryWindow` — vốn là **main window** — bằng `ShowDialog` rồi
bỏ qua kết quả trả về (window này không bao giờ gọi `Close(result)`).

**Kịch bản fail:** bấm "Kết nối" trong dialog đó sẽ mở `RemoteScreenWindow` độc lập,
**không thêm gì vào grid**. Chức năng chết, kèm UX rối: 2 cửa sổ danh sách máy giống hệt nhau cùng tồn tại.

---

## 3. Nhóm 2 — 🔴 Bảo mật (7)

### S1 — Command injection qua tham số người dùng nội suy vào `bash -c`
**Vị trí:**
- `CcuClient/RemoteAppInstallService.cs:73,77,114,117,128`
- `CcuClient/ZcuRemoteInstallerService.cs:89-90,161-164`
- `CcuClient/KioskDeployService.cs:178`

Các lệnh dựng bằng nội suy chuỗi vào `bash -c '...'` chạy dưới `sudo -S`, dữ liệu **không escape**:

| Dòng | Lệnh |
|---|---|
| `RemoteAppInstallService:73` | `sudo -S dpkg -i ~/{fileName}` |
| `RemoteAppInstallService:114` | `dpkg -P {options.PackageName}` |
| `RemoteAppInstallService:117` | `find ... -iname "*{options.PackageName}*.desktop"` |
| `RemoteAppInstallService:128` | `rm -rf /opt/kztek/{baseName}` |
| `ZcuRemoteInstallerService:90` | `mkdir -p /home/{Username}/...` |
| `ZcuRemoteInstallerService:164` | `loginctl enable-linger {Username}` |
| `KioskDeployService:178` | `'{kioskUser}' '{options.AppExec}'` — có single-quote nhưng không escape `'` bên trong |

**Ví dụ khai thác:** package name `foo';rm -rf ~;'` hoặc file `.deb` đặt tên `x';reboot;'.deb`
sẽ đóng single-quote và chèn lệnh tuỳ ý **chạy dưới sudo trên ZCU**.

Hiện chỉ password mới được escape (`Replace("'", "'\\''")`); các tham số còn lại thì không.

### S2 — `Process.Start` với input từ client + quoting ad-hoc
**Vị trí:** `ZcuAgent/Net/ClientSession.cs:251-280`

```csharp
Process.Start("notify-send", $"\"Remote Admin\" \"{chatMsg.Replace("\"", "\\\"")}\"");
```

- Escaping thủ công không xử lý `\`, newline, `$`. Overload `(fileName, arguments)` trên .NET Core
  không đi qua shell (nên không phải shell-injection cổ điển), nhưng input vẫn vào thẳng argv của
  tiến trình ngoài → phải dùng `ProcessStartInfo.ArgumentList`.
- Không giới hạn độ dài riêng: chat/clipboard tối đa 8MB (`MaxFrameBytes`) → đẩy nguyên 8MB vào 1 argv.
- Đây là ranh giới bảo mật dựa hoàn toàn vào auth — kết hợp với S4 (whitelist mặc định allow-all) thì rất nguy hiểm.

### S3 — Password bị pipe vào mọi lệnh và lộ qua `ps`
**Vị trí:** `CcuUI/Views/RemoteCommandWindow.axaml.cs:213-218`, `Views/BulkActionWindow.axaml.cs:179-182`

Mọi lệnh đều thành `echo '<pass>' | env ... bash -c '<cmd với "sudo "→"sudo -S ">'`:
- Lệnh **không có sudo** nhưng đọc stdin (`cat`, script chờ input) sẽ **nhận password làm input**
- `Replace("sudo ", "sudo -S ")` naive: phá lệnh chứa chuỗi `"sudo "` trong string literal;
  lệnh có 2 sudo thì chỉ sudo đầu nhận được password từ pipe
- Password xuất hiện trong command line → thấy qua `ps -ef` trên máy remote;
  `KIOSK_SUDO_PASS` env cũng lộ qua `/proc/<pid>/environ` cho cùng user

### S4 — Cấu hình mặc định mở toang
**Vị trí:** `ZcuAgent/appsettings.json:4-5` + `ZcuAgent/Auth/AuthManager.cs:44`

- `"AllowedClientIPs": ["0.0.0.0/0"]` = cho phép mọi IP
- `IsIpAllowed` còn coi **danh sách rỗng = allow all**
- `Token` để placeholder `REPLACE_WITH_...` nhưng **không có kiểm tra chặn khởi động** khi token
  còn placeholder hoặc rỗng

Nếu deploy quên sửa → chỉ còn token bảo vệ; nếu token cũng để nguyên → không còn gì bảo vệ.

### S5 — ⚠️ Backdoor license key cứng — **KHÔNG SỬA theo quyết định user**
**Vị trí:** `CcuUI/Services/LicenseManagerService.cs:62`

Key cứng `"ANHNV"` cho quyền Super Admin vĩnh viễn. Chuỗi nằm **plaintext trong assembly**,
decompile thấy ngay.

> **Quyết định user (2026-07-26):** chỉ báo cáo, **giữ nguyên code**. Có khả năng đây là backdoor
> cố ý cho mục đích nội bộ. Ghi nhận tại đây để user tự quyết định sau.

### S6 — ⚠️ License hoàn toàn không được enforce — **KHÔNG SỬA theo quyết định user**
**Vị trí:** `CcuUI/App.axaml.cs:12-20`

Grep toàn project: **không có call site nào** của `ValidateAndLoadLicense()` hay `IsLicensed`.
`App.axaml.cs` mở thẳng `ConnectionEntryWindow`; `LicenseWindow` không bao giờ được instantiate.
→ Toàn bộ hệ thống license hiện là **dead code**, app chạy full chức năng không cần license.

**Phụ:** `LicenseWindow.OnActivateClick` sau khi apply thành công mở `ConnectionEntryWindow` mới
nhưng không gọi `ValidateAndLoadLicense()` → `IsLicensed` vẫn false trong session đó.

**Phụ:** `LicenseManagerService.cs:148-158` — HardwareId = MAC của NIC "Up đầu tiên"
→ đổi WiFi↔LAN hoặc bật VPN adapter là đổi HWID → license tự hỏng.

> **Quyết định user (2026-07-26):** chỉ báo cáo, giữ nguyên code.

### S7 — SSH password + token lưu plaintext
**Vị trí:** `CcuClient/ComputerProfileStore.cs:78,189` + `ComputerProfile.cs:40`

`SshPassword` và `Token` được serialize thẳng ra `%APPDATA%\iPGS\RemoteControl\profiles.json`,
không mã hoá. Bất kỳ tiến trình/người dùng cùng máy đọc được **mật khẩu SSH của toàn bộ ZCU**
(mật khẩu này đủ quyền cài đặt/sudo).

> **Quyết định user (2026-07-26):** mã hoá bằng **DPAPI** + tự migrate file cũ.
> Ràng buộc: app cross-platform, `ProtectedData` chỉ chạy Windows → trên Linux phải cảnh báo rõ,
> không im lặng ghi plaintext.

---

## 4. Nhóm 3 — 🟠 Leak / Race / DoS (9)

### L1 — Rò socket + NetworkStream mỗi lần reconnect thất bại
**Vị trí:** `CcuClient/RemoteControlClient.cs:224-300`

`ConnectOnceAsync` gán `_tcp` (224) và `_stream` (229), nhưng `try/finally` gọi `CloseConnectionAsync`
chỉ bao quanh giai đoạn streaming (289-299). Exception ở `_tcp.ConnectAsync` (228) hoặc trong
handshake HELLO/AUTH (237-276) propagate thẳng lên `ConnectionLoopAsync` (catch tại 182)
→ `_tcp`/`_stream` **không bao giờ dispose** → retry sau ghi đè reference → **rò 1 socket + 1 NetworkStream mỗi lần fail**.

Auth thành công reset `_reconnectAttempts=0` (279), nên trên kết nối chập chờn dài ngày **rò tích lũy không giới hạn**.

### L2 — Leak file descriptor mỗi lần đồng bộ clipboard
**Vị trí:** `ZcuAgent/Net/ClientSession.cs:263-280`

```csharp
var proc = Process.Start(psi);
if (proc != null) { proc.StandardInput.Write(clipData); proc.StandardInput.Close(); proc.WaitForExit(1000); }
// proc KHÔNG bao giờ Dispose()
```

- `xclip` **tự daemonize** để giữ quyền sở hữu selection → `WaitForExit(1000)` gần như luôn timeout
  → tiến trình bị bỏ rơi, `proc` leak. Mỗi lần sync clipboard = leak 1 fd + 1 `Process`.
- `Write` + `WaitForExit(1000)` là call **đồng bộ chặn** ngay trong receive-loop async
  → chặn thread pool tới 1s mỗi message → làm trễ xử lý PONG (ảnh hưởng heartbeat).
- `ChatText` tại `:256/:258` cũng bỏ giá trị trả về của `Process.Start` → leak `Process` tương tự.

### L3 — Slow-reader chiếm luôn slot session duy nhất, heartbeat không cứu được
**Vị trí:** `ZcuAgent/Net/ClientSession.cs:149-208` + `Net/TcpServer.cs:99-108`

Server chỉ phục vụ 1 client tại một thời điểm. Trong capture loop, `WriteAsync(FrameJpeg...)`
ghi lên `NetworkStream` **không đặt WriteTimeout**. Client đã auth nhưng ngừng đọc
→ `WriteAsync` backpressure và chặn vô hạn.

**Điểm then chốt:** kiểm tra heartbeat (`now - lastPong > PingTimeoutMs`, dòng 165) và việc gửi PING
nằm **cùng trong capture loop đang bị chặn ở WriteAsync** → timeout 15s **không bao giờ kích hoạt được**.
Một client độc hại (hoặc kẹt mạng) chiếm luôn slot v1, chặn mọi client hợp lệ.

### L4 — Decoder không kiểm tra độ dài payload
**Vị trí:** `CcuClient/Protocol/MessageCodec.cs:184-235`

- `DecodeFrameJpeg` (207-216): `jpegLen` đọc từ `payload[20..24]` (server cấp);
  `payload.AsMemory(24, jpegLen)` ném `ArgumentOutOfRangeException` nếu `24+jpegLen > payload.Length`.
  Handler trong `ReceiveLoopAsync` (~314) không có try/catch → **1 frame lỗi làm rớt session liên tục (DoS phiên)**.
- `DecodeHelloAck` (`Math.Min(nameLen, payload.Length-11)` ra số âm nếu payload < 11), `DecodeAuth` (≥2B),
  `DecodePingPong` (≥8B), `DecodeKeyEvent` (≥5B), `DecodeMouseButton` (≥10B), `DecodeMouseMove` (≥8B)
  — đều đọc offset cố định không validate.

**Phía ZcuAgent:** các exception này không phải `ProtocolException`/`EndOfStreamException` nên lọt lên
`catch (Exception)` tại `ClientSession.cs:86`, bị log ở mức **Error "unexpected error"** — sai bản chất,
gây log-spam và che giấu lỗi thật. `DoAuthAsync` (134) cũng có thể ném từ payload AUTH dị dạng **trước khi auth thành công**.

### L5 — Leak WriteableBitmap sau Dispose (nhân lên trong Multi-Remote)
**Vị trí:** `CcuUI/ViewModels/RemoteScreenViewModel.cs:331-336, 346-359`

GOTCHA cũ (dispose bitmap cũ khi gán mới) **vẫn đúng** cho đường nóng. Nhưng còn khe hở:
`Dispose()` (UI thread) unsubscribe + dispose `CurrentFrame`; một `Dispatcher.UIThread.Post`
do frame decode trước đó **đã nằm trong queue** sẽ chạy SAU `Dispose` → gán `CurrentFrame = wb` mới,
`old = null` → `wb` (~8MB ở 1080p) **không bao giờ được dispose**, VM đã chết nên không ai dọn.

Đơn-session: 8MB/lần đóng — chấp nhận được. **MultiRemoteWindow 3×3 + "Close All": leak tới 9 bitmap một lượt.**

### L6 — Dispose SFTP client đua với background task
**Vị trí:** `CcuUI/Views/FileManagerWindow.axaml.cs:125-134`

`Closed → Disconnect()` dispose và set `_sftpClient = null` trong khi upload/list background
còn đang dùng `_sftpClient!` → NRE hoặc `ObjectDisposedException` trên thread pool.
Phần lớn bị `catch` nội bộ nuốt, nhưng `OnSyncClick`/`OnUploadClick` ở giai đoạn giữa 2 `Task.Run` thì không.

### L7 — Bão probe theo từng keystroke
**Vị trí:** `CcuUI/Views/ConnectionEntryWindow.axaml.cs:41-50, 110`

`PART_SearchBox.PropertyChanged` → `RefreshList()` → `_ = CheckAllStatusesAsync(...)` **mỗi ký tự gõ**.
Không debounce, không `CancellationToken`, không chống chồng lần chạy → N máy × M keystroke probe
TCP/SSH đồng thời; các batch cũ hoàn thành muộn **ghi đè trạng thái mới bằng kết quả cũ**.
Fire-and-forget không try/catch → unobserved task exception.

### L8 — Cờ SHM toàn cục bị lỗi X của luồng khác set (race)
**Vị trí:** `ZcuAgent/Interop/X11ErrorTracker.cs:92-93` + `Capture/X11ScreenCapturer.cs:252-268`

`OnX11Error` set `ShmErrorOccurred = true` cho **mọi lỗi X trên mọi display connection trong process**.
Lúc khởi động thì đơn luồng (an toàn). Nhưng khi **đổi độ phân giải giữa phiên** (`Capture()` gọi lại
`TryInitSHM` trên capture-thread), receive-thread có thể đang `XSync` trên display của mouse/keyboard.
Một lỗi XTest (vd BadAccess) đúng cửa sổ này → `ShmErrorOccurred=true` → SHM reinit bị
**false-positive fallback** sang `XGetImage` (chậm hơn nhiều) dù SHM vẫn tốt.

### L9 — Torn read `ScreenSize` giữa 2 luồng
**Vị trí:** `ZcuAgent/Capture/X11ScreenCapturer.cs:54,141` — đọc tại `Net/ClientSession.cs:229-230`

`ScreenSize` (record struct 2×int) được **ghi** trên capture-thread khi đổi resolution và **đọc**
trên receive-thread để clamp toạ độ chuột. Ghi struct 8 byte không nguyên tử trên mọi nền tảng
→ có thể đọc rách (W mới + H cũ) trong khoảnh khắc reinit → clamp sai 1 frame.

---

## 5. Nhóm 4 — 🟡 Chất lượng / hiệu năng (19)

| ID | Vị trí | Vấn đề |
|---|---|---|
| Q1 | `ZcuAgent/Net/ClientSession.cs:260,279` | `catch { /* ignore */ }` nuốt toàn bộ exception, không log |
| Q2 | `ZcuAgent/Net/ClientSession.cs:255-256` | Dead code nhánh `PlatformID.Win32NT` trong service chỉ chạy Linux |
| Q3 | `ZcuAgent` capture/encode | Mỗi frame alloc `new byte[dataLength]` + `data.ToArray()` + `new byte[24+jpeg.Length]` → GC pressure ở 1080p/15fps; nên `ArrayPool` |
| Q4 | `CcuClient/ZcuRemoteInstallerService.cs:120-137` | JSON config dựng bằng nội suy chuỗi trong heredoc → token chứa `"`/newline/dòng `EOF` phá cấu trúc |
| Q5 | `CcuClient/ZcuRemoteInstallerService.cs:309-310` | `Process.Start` không dispose, không kiểm kết quả `WaitForExit(45000)` → orphan process khi publish quá 45s |
| Q6 | `CcuClient/ZcuRemoteInstallerService.cs:216-221` | `using var cmd` rồi `return cmd` → caller đọc `.Result` trên `SshCommand` đã Dispose |
| Q7 | `CcuClient/ComputerProfileStore.cs:180-196` | `File.WriteAllText` không atomic → crash giữa chừng làm hỏng `profiles.json` |
| Q8 | `CcuClient/ComputerProfileStore.cs:156-177` | `Load` nuốt exception im lặng → file hỏng = mất toàn bộ profile không cảnh báo |
| Q9 | `CcuClient/Protocol/MessageCodec.cs:88,101,111,121` | Length prefix cast `(ushort)` → wrap khi name/token > 65535 byte, khung lệch |
| Q10 | `CcuClient/Services/WakeOnLanService.cs:35` | `Convert.ToByte(...,16)` ném `FormatException` thay vì `ArgumentException` như hàm cam kết |
| Q11 | `CcuClient/ComputerStatusChecker.cs:28-52` | `Task.Run` nền set `CpuUsage/RamUsage/DiskUsage` → raise `PropertyChanged` **ngoài UI thread**; fire-and-forget nuốt lỗi, bỏ qua `cancellationToken` |
| Q12 | `CcuClient` (3 service) | Logic escape password lặp 3 nơi → nên gom `ShellQuote` helper và áp cho **mọi** tham số |
| Q13 | `CcuUI/Views/CronJobWindow.axaml.cs:140,153` | Thêm cron job bọc `"..."` → `$`/backtick bị shell remote expand |
| Q14 | `CcuUI/Views/FileManagerWindow.axaml.cs:351-393`, `RemoteCommandWindow.axaml.cs:571+` | Xóa file / `rm -rf` thư mục **không có dialog xác nhận**; lỗi `DeleteDirectory` bị `catch {}` nuốt |
| Q15 | `CcuUI/Views/BulkActionWindow.axaml.cs:255` | `completed++` từ nhiều task song song không `Interlocked` → progress bar đếm sai |
| Q16 | `CcuUI/Views/NetworkScanWindow.axaml.cs` | Đóng cửa sổ giữa lúc quét không `Cancel()` CTS → scan 254 IP tiếp tục chạy ngầm, post vào window đã đóng |
| Q17 | `CcuUI/Views/RemoteCommandWindow.axaml.cs:105-112` | `Closed` chỉ dispose `_sftpClient` khi `IsConnected` → tạo rồi connect fail thì không dispose |
| Q18 | `CcuUI/Views/HealthMonitorWindow.axaml.cs` | Mỗi tick mở **kết nối SSH mới** mỗi 5s thay vì giữ 1 kết nối → chậm, nặng cho ZCU |
| Q19 | `CcuUI/Views/MultiRemoteWindow.axaml.cs:101-105` | Chấm trạng thái `🟢` trong header cell là **text tĩnh** — luôn xanh kể cả khi session Faulted. Kèm: session ở tab ẩn vẫn decode JPEG full-rate (SkiaSharp decode + alloc WriteableBitmap mỗi frame × N session) |

**Ghi chú thêm (mức thấp, gộp xử lý):**
- `CcuUI/Views/RemoteScreenWindow.axaml:109` — `PlaceholderText` trên plain `TextBox`
  (Avalonia chuẩn dùng `Watermark`) — cần verify hiển thị
- `CcuUI/Views/RemoteScreenWindow.axaml.cs:136` — recorder lấy `ScreenWidth/Height` một lần lúc bấm Record;
  ZCU đổi độ phân giải giữa chừng → AVI header sai, video hỏng
- `CcuClient/Protocol/MessageCodec.cs:392` — `(int)elapsed.TotalMilliseconds` có thể overflow ở biên

---

## 6. Điểm tích cực ghi nhận

- `RemoteControlClient` phần async viết tốt: reader-single-consumer, `_sendLock`, CTS phân cấp,
  `Interlocked` cho pong ticks. Vấn đề chính chỉ là leak ở path lỗi handshake (L1).
- `KeyboardMapper` / `RemoteScreenControl` / input path: throttle chuột, dedup phím,
  2-layer release-all đều hợp lý — đọc kỹ không thấy bug logic.
- 2 gotcha lịch sử **hiện đang được tuân thủ đúng**: `ComputerEditWindow.axaml:93-96`
  `PART_SshPassword` là `KzTextBox` có `PasswordChar` (khớp fix `38efbb1`);
  `PART_MacAddress` là plain `TextBox` (khớp fix `d1ab288`).
- `X11ErrorTracker` đã được refactor dùng chung có logging sau sự cố `XShmAttach` (2026-07-23).

## 7. Vấn đề quy trình phát hiện được

- **Không có unit/integration test** nào cho encode/decode round-trip hay reconnect leak
  trong cả 3 project → khuyến nghị bổ sung test tái hiện L1 và L4 trước khi fix.
- Nhóm "Phase 6 Enterprise Features" (`ZcuAgent/Net/ClientSession.cs:250-293`) có dấu hiệu
  được thêm sau với **chất lượng thấp hơn phần lõi** (S2, L2, Q1, Q2 đều tập trung ở đây)
  → cần review kỹ hơn cho các tính năng bổ sung sau này.

## 8. Kế hoạch xử lý

Xem `docs/plans/PLAN-remote-control-audit-fix-2026-07-26/PLAN-MASTER.md`.

| Bước | Phạm vi |
|---|---|
| 1.1 | Fix `CcuClient` + Protocol — L1, L4, A5, S1, S7(DPAPI), Q4-Q12 |
| 2.1 | Fix `ZcuAgent` — S2, S4, L2, L3, L8, L9, Q1, Q2, Q3 |
| 3.1 | Fix `CcuUI` phần 1 (critical) — A1, A2, A3, A4, A6, L5, Q13, Q16, Q19 |
| 3.2 | Fix `CcuUI` phần 2 — S3, L6, L7, Q14, Q15, Q17, Q18 |
| 4.1 | Tech Lead review + build sạch cả 3 project |
| 5.1 | Đồng bộ tài liệu (GOTCHAS, lessons, CODE-GRAPH) |

**Không sửa:** S5, S6 (license) — theo quyết định user, chỉ ghi nhận tại tài liệu này.

---

## 9. KẾT QUẢ XỬ LÝ (cập nhật 2026-07-26 — sau Tech Lead review PASS)

**Commits (chưa push, chờ user duyệt):** `0146cb4` (1.1 CcuClient), `1ab4f03` (2.1 ZcuAgent), `58909eb` (3.1 CcuUI critical), `8b0aaa3` (3.2 CcuUI security/quality), `de981cf` (4.1 review fix ShellQuote).
**Build verify:** CcuClient/ZcuAgent 0 error 0 warning (kể cả publish linux-x64); CcuUI 0 error cả 3 RID (warning pre-existing từ lib tham chiếu ngoài).

| ID | Kết quả | Commit | Ghi chú |
|---|---|---|---|
| A1 | ✅ Đã fix | `58909eb` | Crontab ghi qua `printf '%s\n'` argument, single-quote; xóa job cuối → `crontab -r`; check ExitStatus |
| A2 | ✅ Đã fix | `58909eb` | Clone copy đủ mọi field persist (gồm `MacAddress`) |
| A3 | ✅ Đã fix | `58909eb` | `SessionRecorder` lock + Dispose idempotent; window null-trước-dispose-sau |
| A4 | ✅ Đã fix | `58909eb` | `WaitForExitAsync` + kill tree khi timeout |
| A5 | ✅ Đã fix | `0146cb4` | `./~/` → `"$HOME/{file}"` + validate filename |
| A6 | ✅ Đã fix | `58909eb` | Tạo mới `Views/SessionPickerWindow` — dialog trả kết quả thật |
| S1 | ✅ Đã fix | `0146cb4` | Helper mới `ShellQuote.cs` — quote/validate mọi tham số vào `bash -c` |
| S2 | ✅ Đã fix | `1ab4f03` | `ProcessStartInfo.ArgumentList` + cap 4KB chat / 256KB clipboard + cờ `EnableDesktopIntegration` |
| S3 | ✅ Đã fix | `8b0aaa3` | Password sudo qua STDIN channel (`sudo -S -p ''`), bỏ khỏi command line và env `KIOSK_SUDO_PASS` (CcuUI) |
| S4 | ✅ Đã fix (code) | `1ab4f03` | Fail-fast token placeholder; list rỗng = deny-all; `0.0.0.0/0` → warning. ⚠️ `appsettings.json` mẫu vẫn `0.0.0.0/0` (hook chặn sửa) — **chờ user quyết**, installer ghi đè config thật khi deploy |
| S5 | ⏭️ **Cố ý không sửa** | — | **Quyết định user:** backdoor `"ANHNV"` giữ nguyên (có thể cố ý cho nội bộ). Verify diff: `LicenseManagerService.cs` không bị đụng |
| S6 | ⏭️ **Cố ý không sửa** | — | **Quyết định user:** license không enforce — giữ nguyên. Verify diff: `App.axaml.cs` không bị đụng |
| S7 | ✅ Đã fix | `0146cb4` | Helper mới `SecretProtector.cs` — DPAPI prefix `enc:v1:` + tự migrate plaintext cũ; Linux cảnh báo tường minh, không im lặng plaintext |
| L1 | ✅ Đã fix | `0146cb4` | try/catch bọc từ ConnectAsync; `CloseConnectionAsync` idempotent (Interlocked.Exchange) |
| L2 | ✅ Đã fix | `1ab4f03` | xclip ghi stdin async + dispose Process ngay (không kill tree — giữ clipboard) |
| L3 | ✅ Đã fix | `1ab4f03` | `WriteAsync` CancelAfter 10s; watchdog PONG tách task riêng |
| L4 | ✅ Đã fix | `0146cb4` | 8 decoder RequireMinLength + length-consistency → `ProtocolException` |
| L5 | ✅ Đã fix | `58909eb` | Cờ `_disposed` volatile; lambda Dispatcher.Post tự dispose bitmap khi VM chết |
| L6 | ✅ Đã fix | `8b0aaa3` | Acquire/release đếm op trên UI thread quanh SFTP background task |
| L7 | ✅ Đã fix | `8b0aaa3` | Debounce 300ms + cancel batch cũ |
| L8 | ✅ Đã fix | `1ab4f03` | `ShmMajorOpcode` scope lỗi theo opcode MIT-SHM; opcode chưa biết → conservative flag (giữ khả năng chống crash 2026-07-23) |
| L9 | ✅ Đã fix | `1ab4f03` | ScreenSize holder record + volatile — publish atomic |
| Q1-Q3 | ✅ Đã fix | `1ab4f03` | Q3 fix phần capture/encode (buffer reuse); phần `EncodeFrameJpeg` alloc còn lại → **TD-2** |
| Q4-Q12 | ✅ Đã fix | `0146cb4` | Gồm Q7 atomic write (tmp+Move), Q8 backup `.corrupt-*`, Q11 `ProbeAsync` thêm `uiDispatch` |
| Q13, Q16, Q19 | ✅ Đã fix | `58909eb` | Q19: chấm trạng thái bind `StatusBrush`; tab ẩn pause render |
| Q14, Q15, Q17, Q18 | ✅ Đã fix | `8b0aaa3` | Q14: tạo mới `Views/ConfirmDeleteDialog`; Q18: giữ 1 SSH connection + tự reconnect |
| (review) | ✅ Fix bổ sung | `de981cf` | `ShellQuote.ValidateFileName` cho phép `~`/`%` (tên `.deb` Debian hợp lệ) — Tech Lead tự fix khi review |

**Tech-debt phát sinh (Tech Lead quyết, xem `docs/tech-debt/TECH-DEBT.md`):**
- **TD-1:** Nâng `ShellQuote` → `public` + helper sudo-stdin dùng chung (gỡ ~45 dòng trùng ở 2 window CcuUI).
- **TD-2:** `MessageCodec.EncodeFrameJpeg` còn alloc `new byte[24+len]`/frame — cần đổi API codec dùng ArrayPool.

**Caveat ghi nhận:** CcuUI bỏ `KIOSK_SUDO_PASS` (S3) an toàn cho luồng chính (KioskDeployService vẫn set env cho script kiosk); riêng user chạy TAY script kiosk qua RemoteCommandWindow sẽ không còn env này — hàm `_sudo()` trong script fallback `sudo` thường (có thể hỏi TTY).

---

## Lịch sử cập nhật

| Ngày | Cập nhật | Người thực hiện |
|------|----------|-----------------|
| 2026-07-26 | Tạo báo cáo từ 3 khảo sát song song — 41 phát hiện | Senior Developer ×3 + Dispatcher |
| 2026-07-26 | Cập nhật kết quả xử lý (§9): 39/41 fix xong qua 5 commit, S5/S6 cố ý giữ nguyên theo quyết định user; status → Đã xử lý | Senior Developer (Bước 5.1) |
