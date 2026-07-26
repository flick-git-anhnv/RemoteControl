# BUG / UI Findings — CCU Remote Control (phát hiện trong lúc chụp tài liệu)

**Người phát hiện:** Documentation Writer (trong quá trình thao tác thật để chụp screenshot bước 2.2)
**Ngày:** 2026-07-26
**Môi trường:** CcuUI Release (PID 31064), ZCU thật `192.168.1.4` (Ubuntu 22, agent tại `/home/kztek/ipgs/remote-agent`, build 2026-07-24)
**Lưu ý:** Documentation Writer CHỈ ghi nhận, KHÔNG sửa code sản phẩm. Việc sửa thuộc senior-developer ở bước sau.

---

## F01 — Ghi hình (Record) tự dừng ngay lập tức với thông báo "độ phân giải ZCU thay đổi"

| Trường | Nội dung |
|---|---|
| **Màn hình** | RemoteScreenWindow — nút 🔴 Record |
| **Mức độ** | P2 |
| **Các bước tái hiện** | 1. Kết nối P01, xem màn hình ZCU. 2. Bấm 🔴 Record. 3. Ở hộp thoại "Lưu video ghi hình", chọn đường dẫn `.avi`, bấm Save. |
| **Kết quả thực tế** | Trong một số lần, ghi hình dừng NGAY sau khi bắt đầu; tiêu đề cửa sổ đổi thành "⏹ Đã dừng ghi hình: độ phân giải ZCU thay đổi", file `.avi` tạo ra chỉ 232 bytes (header rỗng, không xem được). Nút trở lại "🔴 Record". |
| **Kết quả mong đợi** | Ghi hình bắt đầu và tiếp tục tới khi người dùng bấm ⏹ Stop; file `.avi` chứa nội dung xem được. |
| **Phân tích sơ bộ** | `OnFrameReceived` so `e.Width/e.Height` với `recorder.Width/Height` (snapshot lúc bắt đầu). Nếu frame ĐẦU TIÊN sau khi tạo `SessionRecorder` có kích thước khác snapshot `ScreenWidth/Height` → hủy recorder. Xảy ra không ổn định (lần kết nối mới thường chạy được; lần ghi lại sau khi đã stream một lúc dễ bị hủy). Có thể do lấy `ScreenWidth/Height` trước khi nhận frame ổn định, hoặc ZCU gửi 1 frame kích thước khác giữa chừng. |
| **Ảnh chứng minh** | `docs/bugs/screenshots/bug-record-stopped-resolution.png` |

> **✅ Đã sửa (2026-07-26, senior-developer):**
> - **Nguyên nhân gốc:** `OnRecordClick` khởi tạo `SessionRecorder` bằng `Client.ScreenWidth/Height` — giá trị này CHỈ được set 1 lần lúc HELLO_ACK (`RemoteControlClient.ConnectOnceAsync`). Nếu độ phân giải thực tế của frame đang stream khác snapshot HELLO_ACK (ZCU đổi resolution giữa phiên, hoặc capture size khác lúc handshake), frame đầu tiên sau khi tạo recorder lập tức khác `recorder.Width/Height` → guard chống-hỏng-AVI hủy recorder ngay. (ViewModel đã track kích thước theo TỪNG frame cho mapping chuột — chỉ nhánh Record dùng nhầm snapshot cũ.)
> - **Cách sửa:** `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs` — `HandleFrameJpeg` cập nhật `_screenWidth/_screenHeight` theo mỗi frame (khi > 0). `ScreenWidth/Height` giờ luôn là độ phân giải ĐANG stream → recorder khởi tạo đúng header. Guard dừng-ghi-khi-đổi-resolution giữ nguyên (vẫn đúng cho trường hợp resolution đổi THẬT giữa lúc ghi — AVI header cố định).

---

## F02 — Các tính năng Enterprise (SysInfo / Privacy / Chat / Clipboard) không phản hồi & KHÔNG báo lỗi khi agent ZCU cũ

| Trường | Nội dung |
|---|---|
| **Màn hình** | RemoteScreenWindow — nút 📊 SysInfo, 🕶️ Privacy, Chat, 📋 Sync Clipboard |
| **Mức độ** | P2 (robustness / UX feedback) |
| **Các bước tái hiện** | 1. Kết nối P01 (agent trên ZCU build 2026-07-24, cũ hơn commit thêm các tính năng Enterprise `52e93ae` ngày 2026-07-25). 2. Bấm 📊 SysInfo (hoặc bật 🕶️ Privacy, gửi Chat, 📋 Sync Clipboard). |
| **Kết quả thực tế** | Không có phản hồi nào: bấm SysInfo KHÔNG mở `SystemInventoryWindow`, KHÔNG có thông báo lỗi, KHÔNG có timeout. Bật Privacy: nút sáng lên (client) nhưng màn hình ZCU không bị che (agent không xử lý). Người dùng không biết thao tác đã thất bại hay đang chờ. |
| **Kết quả mong đợi** | Khi agent không hỗ trợ / không phản hồi trong X giây, client hiển thị thông báo rõ ràng (VD: "Agent trên máy đích không hỗ trợ tính năng này — vui lòng cập nhật Remote Agent" hoặc "Hết thời gian chờ phản hồi"). |
| **Phân tích sơ bộ** | Client gửi `MessageType.SysInfoReq` và chờ `SysInfoResp` để mở cửa sổ (`OnSysInfoReceived`). Agent cũ không có case `SysInfoReq` → không bao giờ trả `SysInfoResp` → cửa sổ không bao giờ mở, không có nhánh timeout/thông báo. Tương tự cho Privacy/Chat/Clipboard. |
| **Ảnh chứng minh** | Không có (không có gì hiển thị để chụp — chính đó là vấn đề). Hệ quả: ảnh `system-inventory-data.png` của tài liệu KHÔNG chụp được trên môi trường hiện tại (agent cần được cập nhật/deploy lại — thuộc bước 2.3). |

> **CẬP NHẬT 2026-07-26 (bước 2.3):** Nguyên nhân **ĐÃ XÁC NHẬN là version mismatch** (agent build 2026-07-24 cũ hơn client). Sau khi cập nhật ZcuAgent bản mới lên ZCU `192.168.0.101` qua ZcuSetupWizard (build 2026-07-26 20:57, giữ nguyên Token/Port), bấm 📊 SysInfo mở `SystemInventoryWindow` với dữ liệu thật ngay lập tức — ảnh `docs/user-manuals/screenshots/system-inventory-data.png` đã chụp được. **Hiện tượng "im lặng không phản hồi" đã hết.** Khuyến nghị cho senior-developer vẫn giữ nguyên: client nên (a) so sánh version client/agent khi handshake và hiện cảnh báo "Agent phiên bản cũ — vui lòng cập nhật" thay vì fail âm thầm, (b) thêm timeout + thông báo cho mọi request chờ response. |

> **✅ Đã sửa (2026-07-26, senior-developer):**
> - **Nguyên nhân gốc:** (a) SysInfo là request-response nhưng client không có nhánh timeout — agent không trả `SysInfoResp` thì chờ vô hạn, không thông báo. (b) Privacy/Chat/Clipboard là fire-and-forget, protocol v1 KHÔNG có ACK → không thể phát hiện agent bỏ qua; đồng thời handshake không so sánh version (agent cũ và mới đều báo "ZcuAgent/1.0").
> - **Cách sửa:**
>   1. `ZcuAgent/Net/ClientSession.cs` (`DoHandshakeAsync`): bump version string HELLO_ACK → `"ZcuAgent/1.1"` (mốc đã hỗ trợ nhóm Enterprise).
>   2. `CcuClient/RemoteControlClient.cs`: thêm property `ServerName` (đọc từ HELLO_ACK).
>   3. `CcuUI/Views/RemoteScreenWindow.axaml.cs`: (i) khi vào state Streaming, nếu agent < 1.1 (hoặc không parse được version) → hiện dialog "Agent phiên bản cũ — SysInfo/Privacy/Chat/Clipboard sẽ không hoạt động, cập nhật qua ⚡ Cài remote" (1 lần/cửa sổ); (ii) `OnSysInfoClick` thêm timeout 10s bằng CTS — `SysInfoReceived` hủy CTS; hết 10s không có response → dialog hướng dẫn nguyên nhân/cách xử lý.
> - **Giới hạn ghi nhận:** thêm ACK per-message cho Privacy/Chat/Clipboard đòi hỏi đổi protocol 2 phía + deploy đồng bộ — ngoài scope fix này; cảnh báo version lúc handshake đã che được trường hợp thực tế (agent cũ). Đề xuất đưa vào backlog protocol v2.

---

## F03 — Danh sách gợi ý lệnh mẫu (snippet AutoCompleteBox) không mở

| Trường | Nội dung |
|---|---|
| **Màn hình** | RemoteCommandWindow — tab Console — ô "Nhập từ khóa (vd: Reboot, RAM)…" (`PART_SnippetCombo`) |
| **Mức độ** | P3 |
| **Các bước tái hiện** | 1. Mở >_ CMD Shell của P01. 2. Bấm vào ô "Nhập từ khóa…" (hoặc gõ "RAM"). |
| **Kết quả thực tế** | Danh sách gợi ý (10 lệnh mẫu: Reboot, Kiểm tra RAM & Ổ cứng, apt update…) KHÔNG hiện ra dù bấm chuột thật, dù gõ từ khóa, dù nhấn phím Down. Người dùng không thấy được các lệnh mẫu — mất tác dụng của tính năng gợi ý. |
| **Kết quả mong đợi** | Bấm/gõ vào ô → dropdown hiện danh sách lệnh mẫu (MinimumPrefixLength=0, FilterMode=Contains đã cấu hình đúng), chọn 1 mục → lệnh tương ứng được điền vào ô lệnh. |
| **Phân tích sơ bộ** | `OnSnippetComboTapped` đặt `IsDropDownOpen = true` khi Tapped nhưng popup không hiển thị (quirk `AutoCompleteBox` của Avalonia — có thể do popup bị đóng ngay khi mất/giành focus, hoặc dropdown không được populate cho tới TextChanged). Cần kiểm tra lại binding/behavior. |
| **Ảnh liên quan** | `docs/user-manuals/screenshots/remote-command-snippet.png` (chỉ chụp được ô đã gõ "RAM", dropdown không mở) |

> **✅ Đã sửa (2026-07-26, senior-developer):**
> - **Nguyên nhân gốc:** `OnSnippetComboTapped` set `IsDropDownOpen = true` khi view lọc NỘI BỘ của `AutoCompleteBox` còn rỗng (chưa có lần population nào chạy) → popup mở với 0 item (vô hình) và property kẹt ở `true`; các lần gõ phím sau population có chạy nhưng `IsDropDownOpen` không đổi giá trị nên popup không được mở lại. (Đối chứng: ô package ở RemoteAppInstall hoạt động vì `Text` được prefill → population đã chạy trước khi bấm ▼.)
> - **Cách sửa:** `CcuUI/Views/RemoteCommandWindow.axaml.cs` + `BulkActionWindow.axaml.cs` — `OnSnippetComboTapped` nay: `IsDropDownOpen = false` (reset trạng thái kẹt) → `PopulateComplete()` (ép refresh view từ ItemsSource theo SearchText hiện tại; rỗng = toàn bộ 10-12 snippet vì `MinimumPrefixLength=0` + `FilterMode=Contains`) → `IsDropDownOpen = true`.

---

## F04 — Thông báo lỗi/status cũ KHÔNG được xóa khi bắt đầu thao tác mới (ZcuSetupWizard, RemoteAppInstall)

| Trường | Nội dung |
|---|---|
| **Màn hình** | ZcuSetupWizardWindow (`PART_StatusMsg`), RemoteAppInstallWindow (`PART_StatusMsg`) |
| **Mức độ** | P3 (UX) |
| **Các bước tái hiện** | 1. Mở ⚡ Cài remote của P01, xóa Token, bấm 🚀 Bắt đầu Cài đặt → status đỏ "Thiếu IP/SSH user … hoặc Token Agent." 2. Nhập lại Token hợp lệ, bấm 🚀 Bắt đầu Cài đặt lần nữa. |
| **Kết quả thực tế** | Trong SUỐT quá trình cài (progress 0→100%) status đỏ cũ "Thiếu IP/SSH user…" vẫn hiển thị ở góc trái dưới, chỉ đổi khi cài xong ("Cài đặt ZcuAgent hoàn tất!"). Người dùng nhìn thấy đồng thời log đang chạy + thông báo lỗi cũ → gây nhầm lẫn đang lỗi. Tương tự ở RemoteAppInstallWindow: sau lỗi validation "Vui lòng chọn file cài đặt…", chọn file xong thông báo đỏ vẫn còn. |
| **Kết quả mong đợi** | Khi bấm bắt đầu thao tác mới, `PART_StatusMsg` được reset (xóa trống hoặc đổi thành "Đang cài đặt…") ngay lập tức. (Ghi chú: `OnStartInstallClick` của wizard reset `PART_LogConsole` + `PART_ProgressBar` nhưng KHÔNG reset `PART_StatusMsg`; RemoteAppInstall có set "Đang kết nối và cài đặt…" nhưng chỉ sau khi qua validation — nhánh chọn-file-xong-chưa-bấm-lại thì status lỗi vẫn treo.) |
| **Ảnh chứng minh** | `docs/bugs/screenshots/bug-wizard-stale-error-status.png` (progress 85%, log đang chạy nhưng góc trái dưới vẫn hiện lỗi đỏ cũ) |

> **✅ Đã sửa (2026-07-26, senior-developer):**
> - **Nguyên nhân gốc:** đúng như phân tích — `ZcuSetupWizardWindow.OnStartInstallClick` reset log + progress nhưng bỏ sót `PART_StatusMsg`; `RemoteAppInstallWindow` chỉ set status mới SAU validation nên nhánh "chọn file xong nhưng chưa bấm lại" vẫn treo lỗi đỏ.
> - **Cách sửa:** (1) `ZcuSetupWizardWindow.axaml.cs` — ngay sau khi qua validation, reset `PART_StatusMsg` → "Đang cài đặt ZcuAgent..." (SlateGray) cùng lúc reset log/progress. (2) `RemoteAppInstallWindow.axaml.cs` — `OnBrowseAppClick` xóa status đỏ ngay khi người dùng chọn xong file (nguyên nhân lỗi validation đã được khắc phục).

---

## F05 — BulkAction hiển thị raw .NET exception thay vì thông báo thân thiện khi máy thiếu cấu hình SSH

| Trường | Nội dung |
|---|---|
| **Màn hình** | BulkActionWindow — kết quả từng máy |
| **Mức độ** | P3 (UX) |
| **Các bước tái hiện** | 1. Tick chọn P01 (đủ SSH) + P02 (KHÔNG có SSH username). 2. Bấm 🚀 Gửi lệnh / Upload File Hàng Loạt, nhập `uname -a`, bấm 🚀 Chạy lệnh. |
| **Kết quả thực tế** | P02 báo Lỗi với nguyên văn exception .NET: `The value cannot be an empty string or composed entirely of whitespace. (Parameter 'username')` — người dùng cuối không hiểu nguyên nhân là máy chưa khai báo SSH user. |
| **Kết quả mong đợi** | Thông báo tiếng Việt rõ ràng, VD: "Máy chưa cấu hình SSH user — vào 'Sửa' máy tính để bổ sung", hoặc loại máy thiếu SSH khỏi danh sách chạy kèm cảnh báo trước khi thực thi. |
| **Ảnh chứng minh** | `docs/bugs/screenshots/bug-bulk-raw-exception.png` |

> **✅ Đã sửa (2026-07-26, senior-developer):**
> - **Nguyên nhân gốc:** `BulkActionWindow` truyền thẳng `profile.SshUsername ?? ""` vào constructor `SshClient`/`SftpClient` — SSH.NET ném `ArgumentException` tiếng Anh ("Parameter 'username'") và message này được hiển thị nguyên văn qua `SetError(ex.Message)`.
> - **Cách sửa:** `CcuUI/Views/BulkActionWindow.axaml.cs` — thêm helper `ValidateSshProfile(profile)` gọi ở đầu cả 2 action (chạy lệnh + upload file): thiếu SSH username → ném `InvalidOperationException` với thông báo tiếng Việt "Máy chưa cấu hình SSH user — bấm '✏️ Sửa' máy tính này để bổ sung SSH username/password rồi chạy lại." Máy đủ cấu hình vẫn chạy song song bình thường.

---

## F06 — Backdoor cứng "ANHNV" + agent mở cho mọi IP (rủi ro bảo mật)

| Trường | Nội dung |
|---|---|
| **Màn hình / Thành phần** | `LicenseManagerService.ValidateLicenseKey` (CcuUI) + `appsettings.json` của ZcuAgent |
| **Mức độ** | P2 (bảo mật) |
| **Các bước tái hiện** | 1. Mở LicenseWindow (qua harness doc), nhập đúng chuỗi `ANHNV` vào ô License Key → bấm Kích hoạt → "Kích hoạt thành công" bất kể Hardware ID. 2. Trên ZCU đọc `~/ipgs/remote-agent/appsettings.json` → `Token: "ANHNV"`, `AllowedClientIPs: ["0.0.0.0/0"]`. |
| **Kết quả thực tế** | (a) Chuỗi `ANHNV` là superadmin backdoor hardcode trong source — bỏ qua toàn bộ kiểm tra chữ ký số + hết hạn + Hardware ID, cho phép kích hoạt vĩnh viễn trên mọi máy. (b) Agent chấp nhận kết nối từ **mọi IP** (`0.0.0.0/0`) với token đoán được `ANHNV` (trùng tên user git `ANHNV_2025`, trùng license backdoor). |
| **Kết quả mong đợi** | (a) Không nhúng backdoor cứng trong bản phát hành, hoặc ít nhất chuyển sang cơ chế ký/kiểm tra không hardcode chuỗi tĩnh trong mã nguồn. (b) `AllowedClientIPs` mặc định giới hạn dải LAN quản trị, token sinh ngẫu nhiên mạnh (không phải chuỗi từ điển ngắn 5 ký tự) — ZcuSetupWizard đã có nút 🎲 Sinh Token, nên bỏ default `ANHNV`. |
| **Ảnh liên quan** | `docs/user-manuals/screenshots/license-success.png` (kích hoạt bằng backdoor, đã che), `docs/user-manuals/screenshots/zcu-terminal-appsettings.png` (Token đã che, thấy `AllowedClientIPs: 0.0.0.0/0`) |
| **Ghi chú** | Documentation Writer CHỈ ghi nhận, KHÔNG sửa. Thuộc phạm vi security-audit-stride của senior-developer/Tech Lead. Chuỗi backdoor thực tế KHÔNG được ghi vào tài liệu người dùng; ảnh success đã che ô key. |

> **⚠️ Đã sửa MỘT PHẦN theo quyết định user (2026-07-26, senior-developer):**
> - **(a) Backdoor license `ANHNV` — ⏭️ KHÔNG sửa:** quyết định user giữ nguyên (xem `docs/plans/PLAN-remote-control-audit-fix-2026-07-26/PLAN-MASTER.md` mục S5). `LicenseManagerService.cs` không bị chạm.
> - **(b) Phần agent — ✅ Đã siết:**
>   1. **Mặc định `AllowedClientIPs` → 3 dải LAN riêng RFC 1918** (`192.168.0.0/16,10.0.0.0/8,172.16.0.0/12`) thay `0.0.0.0/0`, tại: `CcuClient/ZcuRemoteInstallerService.cs` (`SshInstallerOptions.DefaultLanAllowedClientIPs`), `CcuUI/Views/ZcuSetupWizardWindow.axaml(.cs)` (giá trị mặc định ô nhập + fallback), `scripts/setup-zcu-agent.sh`. **Lý do chọn 3 dải RFC 1918 thay vì tự dò subnet:** ZCU hiện tại (`192.168.0.101`) và mọi CCU/ZCU LAN nội bộ đều nằm trong `192.168.0.0/16` → không phá kết nối hiện có; tự dò subnet của interface CCU có thể sai khi CCU nhiều NIC/VPN và tạo config khó đoán — 3 dải tĩnh dễ hiểu, chặn được toàn bộ IP public.
>   2. **Hỗ trợ nhiều CIDR phân tách dấu phẩy:** installer C# tách chuỗi thành mảng JSON từng entry (`AuthManager.IsInRange` parse từng entry riêng — 1 entry gộp sẽ deny-all); script bash tách tương tự.
>   3. **Cảnh báo token yếu / mở toàn mạng:** agent (`ZcuAgent/RemoteControlHostedService.ValidateSecurityConfig`) log WARNING khi token < 16 ký tự (không fail-fast để không phá deployment hiện có; cảnh báo 0.0.0.0/0 đã có sẵn từ audit S4); wizard (`ZcuSetupWizardWindow`) ghi cảnh báo bảo mật vào Nhật ký Cài đặt khi token < 16 ký tự hoặc AllowedClientIPs chứa `0.0.0.0/0`/`::/0`.
>   4. **File mẫu `ZcuAgent/appsettings.json` KHÔNG đổi được** — bị hook `config-protection` chặn (đúng thiết kế). Không ảnh hưởng thực tế: installer/wizard/script luôn ghi đè file này khi deploy, và token placeholder khiến agent fail-fast nếu chạy file mẫu nguyên trạng.
> - **Ảnh cần chụp lại (không chụp trong phiên này):** `docs/user-manuals/screenshots/zcu-setup-wizard-default.png` (ô AllowedClientIPs nay hiển thị mặc định dải LAN thay vì `0.0.0.0/0`).

---

## Trạng thái sửa lỗi (2026-07-26 — senior-developer, WF-BUGFIX)

| Finding | Trạng thái | File chính đã sửa |
|---|---|---|
| F01 Record tự dừng | ✅ Đã sửa | `CcuClient/RemoteControlClient.cs` (HandleFrameJpeg cập nhật resolution theo frame) |
| F02 Enterprise fail âm thầm | ✅ Đã sửa | `ZcuAgent/Net/ClientSession.cs` (version 1.1), `CcuClient/RemoteControlClient.cs` (ServerName), `CcuUI/Views/RemoteScreenWindow.axaml.cs` (timeout SysInfo 10s + cảnh báo agent cũ) |
| F03 Snippet dropdown không mở | ✅ Đã sửa | `CcuUI/Views/RemoteCommandWindow.axaml.cs`, `BulkActionWindow.axaml.cs` (PopulateComplete trước khi mở) |
| F04 Status cũ không reset | ✅ Đã sửa | `CcuUI/Views/ZcuSetupWizardWindow.axaml.cs`, `RemoteAppInstallWindow.axaml.cs` |
| F05 Raw exception BulkAction | ✅ Đã sửa | `CcuUI/Views/BulkActionWindow.axaml.cs` (ValidateSshProfile) |
| F06 Backdoor + agent mở toàn mạng | ⚠️ Sửa phần agent (license giữ nguyên theo quyết định user) | `CcuClient/ZcuRemoteInstallerService.cs`, `CcuUI/Views/ZcuSetupWizardWindow.axaml(.cs)`, `scripts/setup-zcu-agent.sh`, `ZcuAgent/RemoteControlHostedService.cs` |

Build verify: `dotnet build IPGS.RemoteControl.CcuUI -c Release` → **0 Error** (456 warnings = baseline full-rebuild đã biết); `dotnet build IPGS.RemoteControl.ZcuAgent -c Release` → **0 Warning, 0 Error**.

---

## Ghi chú tổng hợp cho senior-developer

- F01 và F02 cùng liên quan tới cặp client mới / agent cũ. Ưu tiên: (a) thêm timeout + thông báo cho mọi request chờ response (SysInfo…); (b) kiểm tra logic chống-hủy-record do resolution.
- Môi trường tài liệu hiện tại: **agent ZCU cần được cập nhật lên bản cùng version client** để chụp được `system-inventory-data.png` và kiểm chứng đầy đủ Privacy/Chat/Clipboard. Việc deploy/cập nhật agent thuộc bước 2.3 (đang bị giới hạn ở phiên này).
- Không phát hiện lỗi ở: NetworkScan, ConnectionEntry, MultiRemote (grid/custom/tab), FileManager (duyệt/lọc/upload/sync/xóa/dir-warning/lỗi-quyền), ConfirmDelete, RemoteCommand Console (chạy lệnh + báo lỗi command-not-found) — tất cả hoạt động đúng như mong đợi.
- **Bổ sung bước 2.3 (2026-07-26, ZCU tại IP mới `192.168.0.101`):** F02 đã hết sau khi cập nhật agent (xem cập nhật trong F02). Thêm F04 (status cũ không reset — ZcuSetupWizard/RemoteAppInstall) và F05 (BulkAction lộ raw exception). Hoạt động đúng: ZcuSetupWizard cài đặt 7/7 bước thành công (~10s, agent tự restart, stream + SysInfo hoạt động ngay); RemoteAppInstall dropdown danh sách package (nút ▼) mở và lọc đúng (kztek-*, agent, kiosk…); KioskDeploy 2 tab hiển thị đủ checkbox; BulkAction chạy `uname -a` song song P01 thành công/P02 lỗi đúng như thiết kế; CronJob (đã chụp trước) thêm/xóa job bình thường.

---

## Review của Tech Lead — 2026-07-26 22:17

**Phạm vi:** commit `b5b8033` (fix F01–F06 phần agent) + `6e5084d` (GOTCHAS G018). Đã đọc TOÀN BỘ diff, đối chiếu code liên quan ngoài diff (`AuthManager.IsInRange`, `SessionRecorder` flow trong `RemoteScreenWindow.OnFrameReceived`/`OnRecordClick`, `MessageCodec.DecodeHelloAck/DecodeFrameJpeg`, `IRemoteControlClient`), và **tự build lại** để kiểm chứng (không tin báo cáo suông): CcuUI Release **0 Error** (456 warnings = baseline full-rebuild), ZcuAgent Release **0 Warning 0 Error** — khớp báo cáo Senior Developer.

### Bảng verdict từng finding

| Finding | Verdict | Lý do / bằng chứng |
|---|---|---|
| **F01** — resolution theo frame | **APPROVE-WITH-COMMENT** | Đúng root cause: `OnRecordClick` (`RemoteScreenWindow.axaml.cs:263`) đọc `Client.ScreenWidth/Height` vốn chỉ set 1 lần lúc HELLO_ACK; nay `HandleFrameJpeg` (`RemoteControlClient.cs:381-386`) cập nhật theo từng frame có guard `> 0` (chặn frame lỗi decode). Case "đổi độ phân giải GIỮA lúc đang record" vẫn được guard cũ tại `RemoteScreenWindow.axaml.cs:111-126` xử lý đúng (dừng ghi, báo user — AVI header cố định, không thể ghi tiếp). **Nit:** `_screenWidth/_screenHeight` không `volatile` và cặp W/H có thể tearing lý thuyết giữa receive-thread (ghi) và UI thread (đọc lúc tạo recorder) — hậu quả tối đa là recorder bị guard dừng ngay ở frame kế (hành vi an toàn), chấp nhận được; ghi chú để cân nhắc gom W×H vào 1 struct/long atomic khi refactor. |
| **F02** — version 1.1 + timeout SysInfo | **APPROVE-WITH-COMMENT** | **Backward compat 2 chiều đã kiểm chứng:** (1) client CŨ + agent MỚI — client cũ chỉ log `serverName` (`RemoteControlClient.cs:261`), chuỗi "ZcuAgent/1.1" không được parse ở client cũ → không crash; (2) client MỚI + agent CŨ — `IsAgentOutdated` (`RemoteScreenWindow.axaml.cs:163-168`) parse phòng thủ bằng `Version.TryParse`, chuỗi lạ/không đúng định dạng → coi là outdated và CHỈ cảnh báo (1 lần nhờ `_agentVersionWarned`), không crash. Threading đúng: `OnClientStateChanged` chạy trên connect/receive-thread nhưng dialog qua `Dispatcher.UIThread.Post` (dòng 153); `OnSysInfoClick` là async void event handler → continuation sau `Task.Delay` quay về UI thread (Avalonia sync context) → `ShowInfoDialog` an toàn; đóng cửa sổ hủy CTS trong `OnClosing` → không dialog mồ côi. Timeout 10s: chấp nhận được — SysInfoResp là payload nhỏ, và dialog chỉ mang tính hướng dẫn ("thử lại sau"), false-positive trên mạng chậm không gây hại chức năng. **Nit 1:** `ServerName` chưa thêm vào `IRemoteControlClient` (build pass vì `RemoteScreenViewModel.Client` expose concrete class — `RemoteScreenViewModel.cs:21`) — thêm vào interface cho nhất quán với `ScreenWidth/Height`. **Nit 2:** `CancellationTokenSource` không được `Dispose` sau dùng — leak nhỏ, dọn khi tiện. **FYI:** SysInfoResp trễ của request TRƯỚC có thể cancel nhầm timer của request SAU — hậu quả chỉ là mất 1 cảnh báo trong khi dữ liệu thật đã về, chấp nhận. |
| **F03** — snippet dropdown | **APPROVE** | Chẩn đoán đúng quirk `AutoCompleteBox` (view lọc nội bộ rỗng → popup 0 item vô hình + property kẹt `true`); fix `false → PopulateComplete() → true` áp dụng nhất quán cả 2 cửa sổ (`RemoteCommandWindow.axaml.cs:239-241`, `BulkActionWindow.axaml.cs:234-236`). Handler `Tapped` chạy trên UI thread — không vấn đề threading. Đối chứng RemoteAppInstall (prefill Text) củng cố root cause. GOTCHAS G018 (`6e5084d`) ghi đúng chuẩn, có mục "Không cần làm lại". |
| **F04** — reset status cũ | **APPROVE** | Wizard reset `PART_StatusMsg` sau validation cùng chỗ reset log/progress (`ZcuSetupWizardWindow.axaml.cs:91-94`); RemoteAppInstall xóa status đỏ ngay khi chọn xong file (`RemoteAppInstallWindow.axaml.cs:120-123`) — đúng cả 2 nhánh mô tả trong finding. Cả 2 đều là UI event handler trên UI thread. |
| **F05** — thông báo SSH tiếng Việt | **APPROVE** | `ValidateSshProfile` gọi ở đầu CẢ 2 action (chạy lệnh `BulkActionWindow.axaml.cs:261` + upload `:294`) TRƯỚC khi tạo `SshClient`/`SftpClient`; message tiếng Việt chỉ rõ cách khắc phục; máy khác trong batch vẫn chạy song song (exception per-task → `SetError`). Đúng AC. |
| **F06** (phần agent) | **APPROVE-WITH-COMMENT** | **Parse nhiều CIDR đúng:** đã đối chiếu `AuthManager.IsInRange` (`AuthManager.cs:144-182`) — parse TỪNG entry (IP đơn lẻ hoặc CIDR), 1 entry gộp chứa dấu phẩy sẽ deny-all → việc installer C# split `,`/`;` (`ZcuRemoteInstallerService.cs:142-145`) và script bash tách mảng JSON (`setup-zcu-agent.sh:72-80`) là BẮT BUỘC và đã làm đúng (kèm trim, bỏ entry rỗng). **Backward compat OK:** máy đã cài giữ config `["0.0.0.0/0"]` — vẫn là mảng 1 entry hợp lệ, agent mới đọc bình thường + warning catch-all có sẵn (`RemoteControlHostedService.cs:120-127`); nâng cấp agent KHÔNG chết cấu hình cũ. Không đụng `LicenseManagerService.cs` — đúng quyết định user. **Quan điểm về mức độ siết:** cảnh báo (không fail-fast) token < 16 ký tự là lựa chọn đúng cho bản vá — chặn cứng sẽ phá deployment hiện có; wizard/script luôn sinh token 32 hex nên cài mới không bị ảnh hưởng. Đề xuất backlog: fail-fast token yếu cho **cài đặt MỚI** ở bản major kế tiếp. **Required (docs, đã tự sửa trong review này — xem ghi chú dưới bảng):** CCU kết nối qua VPN/CGNAT ngoài RFC 1918 (VD Tailscale `100.64.0.0/10`, ZeroTier) sẽ bị chặn bởi default mới — MANUAL mới nói chung chung "IP ngoài LAN bị chặn", cần nêu rõ trường hợp VPN để tránh ticket "sau nâng cấp không kết nối được". **Nit:** script bash chỉ tách `,` trong khi C# tách cả `,` và `;` — nên nhất quán (thêm `;` vào IFS hoặc bỏ `;` phía C#); không chặn merge vì MANUAL/watermark chỉ hướng dẫn dấu phẩy. |
| **Docs sync** (§15, §17) | **APPROVE** | MANUAL chương 9.1 (bảng tham số + cảnh báo Hình 53) và chương 12 (bảng ALLOWED_IPS) khớp default mới; CODE-GRAPH cập nhật đủ 3 API (`ScreenWidth/Height` đổi ngữ nghĩa, `ServerName`, `DefaultLanAllowedClientIPs`) + dòng lịch sử + DOCX/PDF xuất lại. BUG report có Nguyên nhân gốc/Cách sửa/bảng trạng thái từng finding. |

**Ghi chú Required F06-docs:** một dòng lưu ý VPN đã được Tech Lead bổ sung trực tiếp vào MANUAL chương 9.1 trong commit review này (thay đổi 1 dòng hiển nhiên, ghi rõ theo quy tắc review); các Nit còn lại (interface `IRemoteControlClient.ServerName`, CTS dispose, IFS `;` trong script) giao Senior Developer xử lý ở vòng sau hoặc gộp vào PR kế — KHÔNG chặn QA.

### Kết luận

**APPROVE-WITH-COMMENT — CHO PHÉP chuyển sang QA verify (Bước 4 WF-BUGFIX).** Không phát hiện lỗi chặn merge: root cause cả 6 finding được xử lý đúng (không vá triệu chứng), backward compat protocol 2 chiều và config `AllowedClientIPs` cũ đều an toàn, threading UI đúng chuẩn Dispatcher/async-context, build 2 project sạch (tự kiểm chứng). QA lưu ý test thêm: (1) record → đổi resolution ZCU giữa lúc ghi (guard dừng ghi phải kích hoạt đúng, KHÔNG kích hoạt lúc mới bấm Record); (2) kết nối agent cũ 1.0 nếu còn máy chưa nâng cấp (dialog cảnh báo hiện đúng 1 lần); (3) cài mới qua wizard rồi xác nhận `appsettings.json` trên ZCU có mảng 3 entry CIDR riêng biệt và CCU trong LAN vẫn kết nối được.

— Tech Lead, 2026-07-26 22:17

---

## Kết quả QA verify — 2026-07-26 23:05

**Người thực hiện:** QA Engineer (WF-BUGFIX Bước 4)
**Môi trường:** CcuUI Release build có fix (commit `9caed3d`, CCU `192.168.0.100`) + ZCU thật `192.168.0.101` (Ubuntu 22, kztek). Agent bản mới **ZcuAgent/1.1** deploy qua ZcuSetupWizard (build `dotnet publish linux-x64` từ HEAD). Build CcuUI Release: **0 Error / 19 warning**; ZcuAgent Release: **0 Error**.
**Cách test:** thao tác UI thật qua UIA + chuột/bàn phím thật (bộ script `temp/user-manual-ccu-zcu/`), đối chiếu file/log thật trên ZCU qua SSH.

### Bảng kết quả V1–V7

| Case | Finding | Kết quả | Bằng chứng |
|---|---|---|---|
| **V1** | F01 Record | ✅ **PASS** | Sau khi stream ~15s rồi Record ~20s → file `RemoteSession_20260726_223656.avi` **37.6 MB**, header AVI hợp lệ (`RIFF/AVI `, MJPG, 15fps, 586 frames, 1279×799), frame đầu decode được đúng độ phân giải đang stream (không phải 232 bytes). Đổi độ phân giải ZCU **giữa lúc ghi** (`xrandr` 1279→3020/4096) → guard dừng ghi đúng, file partial 278 frames vẫn hợp lệ, tiêu đề "⏹ Đã dừng ghi hình: độ phân giải ZCU thay đổi". `verify-v1-record-frame1.png`, `verify-v1-record-resolution-guard.png` |
| **V2** | F02-a agent cũ | ✅ **PASS** | Dựng agent **thật** bản pre-Enterprise (commit `b7bbfef`, publish linux-x64) deploy lên ZCU → kết nối: dialog "Agent phiên bản cũ" (báo `ZcuAgent/1.0` < 1.1) hiện đúng **1 lần**; bấm "Đã hiểu" xong không hiện lại; reconnect (cửa sổ mới) hiện lại 1 lần — đúng "1 lần/cửa sổ". `verify-v2-agent-old-dialog.png` |
| **V3** | F02-b SysInfo | ✅ **PASS** | Agent CŨ: bấm SysInfo → sau ~10s hiện dialog "Không nhận được phản hồi SysInfo" nêu nguyên nhân + cách xử lý (không fail âm thầm). Agent MỚI 1.1: `SystemInventoryWindow` mở ngay với dữ liệu thật (CPU i7-12700H, RAM 5.74 GB, Unix 6.8.0-134, X64), **KHÔNG** có dialog cảnh báo. `verify-v3-sysinfo-timeout.png`, `verify-v3-sysinfo-new-agent.png` |
| **V4** | F03 snippet | ✅ **PASS** | CMD Shell (Console): click ô snippet → dropdown 12 lệnh mẫu mở (Reboot, Kiểm tra RAM & Ổ cứng, apt update…); set "RAM" qua UIA + click → lọc đúng còn "Kiểm tra RAM & Ổ cứng". BulkAction: tương tự, dropdown mở + lọc "RAM" đúng. `verify-v4-snippet-dropdown-cmd.png`, `verify-v4-snippet-filter-ram.png`, `verify-v4-snippet-bulk-ram.png` |
| **V5** | F04 reset status | ✅ **PASS** | Wizard: xóa Token → Bắt đầu → status đỏ "Thiếu IP/SSH user… hoặc Token Agent."; nhập lại Token + Bắt đầu → status đổi ngay thành "Đang cài đặt ZcuAgent..." (xám), lỗi đỏ cũ biến mất tức thì (log/progress chạy song song không còn lỗi treo). RemoteAppInstall: chọn xong file → status đỏ validation biến mất. `verify-v5-wizard-error.png`, `verify-v5-wizard-status-reset.png` |
| **V6** | F05 SSH message | ✅ **PASS** | BulkAction chạy `uname -a` trên VietAnh (đủ SSH) + Kien (thiếu SSH user): VietAnh "Thành công"; Kien "Lỗi" với thông báo tiếng Việt thân thiện "Máy chưa cấu hình SSH user — bấm '✏️ Sửa' máy tính này để bổ sung SSH username/password rồi chạy lại." — KHÔNG còn raw .NET exception. `verify-v6-bulk-ssh-message.png` |
| **V7** | F06 agent security | ✅ **PASS** | Wizard default `AllowedClientIPs` = `192.168.0.0/16,10.0.0.0/8,172.16.0.0/12` (không còn `0.0.0.0/0`). Cài mới qua wizard → `appsettings.json` trên ZCU ghi **mảng 3 CIDR riêng biệt** đúng dạng; CCU `192.168.0.100` (thuộc `192.168.0.0/16`) kết nối + stream ZCU bình thường (agent log "accepted connection from 192.168.0.100"). Nhập token ngắn (`short123` < 16) + `0.0.0.0/0` → wizard ghi **2 dòng CẢNH BÁO BẢO MẬT** vào Nhật ký Cài đặt; agent ZCU cũng log `SECURITY: Token is only … chars`. `verify-v7-connect-lan-cidr.png`, `verify-v7-wizard-security-warning.png` (bằng chứng text log trong mục dưới) |

**Tổng: 7 PASS / 0 FAIL / 0 SKIP.**

### Regression test nhanh (luồng chính) — tất cả OK

- **Kết nối + xem màn hình:** connect VietAnh (agent 1.1) → stream ZCU realtime OK, không dialog cảnh báo.
- **CMD Shell chạy lệnh:** `uname -a && df -h /` trả kết quả đúng (Linux ubuntu-22 … / 67G 38%).
- **FileManager (SFTP) duyệt:** browse `/home/kztek/ipgs/remote-agent` → tải đúng 36 mục, hiển thị danh sách file/kích thước/thời gian (appsettings.json, ZcuAgent.dll…). `verify-regression-filemanager.png`
- **BulkAction song song:** 2 máy chạy đồng thời, máy lỗi không chặn máy thành công.
- **Wizard 7/7 bước:** cài đặt hoàn tất ~15s, agent tự restart về `active`.

### Bằng chứng text — cảnh báo bảo mật wizard (V7, token yếu + 0.0.0.0/0)

```
[23:01:23] ⚠ CẢNH BÁO BẢO MẬT: Token ngắn hơn 16 ký tự - dễ bị đoán. Dùng nút 🎲 Sinh Token để tạo token ngẫu nhiên mạnh.
[23:01:23] ⚠ CẢNH BÁO BẢO MẬT: AllowedClientIPs đang mở cho MỌI IP (0.0.0.0/0) - nên giới hạn về dải LAN quản trị (VD 192.168.0.0/16).
```

Agent ZCU (journalctl) khi Token = 5 ký tự:
```
warn: SECURITY: RemoteControl:Token is only 5 chars — short tokens are guessable. Generate a strong random token (>= 32 chars) via the CCU Setup Wizard.
```

### Ảnh tài liệu chụp lại (do fix đổi giao diện)

- `docs/user-manuals/screenshots/zcu-setup-wizard-default.png` — ô AllowedClientIPs nay hiển thị 3 dải LAN riêng (token đã che). Khớp caption Hình 53 (đã đúng sẵn — không cần sửa MANUAL).
- `docs/user-manuals/screenshots/zcu-terminal-appsettings.png` — `appsettings.json` nay có mảng 3 CIDR thay `0.0.0.0/0` (token đã che). Khớp caption Hình 70.
- MANUAL text đã khớp default mới (Tech Lead cập nhật ở commit review) → không sửa `.md`, không cần chạy lại md_to_docx.

### Lỗi mới phát hiện

Không phát hiện lỗi mới trong phạm vi verify (không append F07). Ghi chú không chặn: cửa sổ ZcuSetupWizard tự đóng ~1.5s sau khi cài xong nên khó chụp trạng thái "hoàn tất" — không phải lỗi, chỉ là điểm bất tiện cho việc chụp tài liệu.

### Kết luận

✅ **Đủ điều kiện SIGN-OFF** — cả 6 finding F01–F06 (F06 phần agent) đã verify PASS trên app + ZCU thật với agent 1.1; regression luồng chính không bị phá. **Bàn giao QA Lead sign-off (Bước 5 WF-BUGFIX).** F06(a) backdoor license `ANHNV` vẫn ⏭️ giữ nguyên theo quyết định user (ngoài scope vòng fix này).

— QA Engineer, 2026-07-26 23:05

---

## F07 — Lệnh reboot/shutdown báo "❌ LỖI" đỏ dù đã chạy thành công + lộ dòng `[debug]` kỹ thuật nội bộ

> **Người phát hiện:** USER (thao tác thật trên app, sau vòng QA verify ở trên).

| Trường | Nội dung |
|---|---|
| **Màn hình** | RemoteCommandWindow "Quản lý File & Lệnh (SFTP / SSH)" — tab >_ Console (CMD), kết nối `kztek@192.168.0.101:22` |
| **Mức độ** | P3 (UX) |
| **Các bước tái hiện** | 1. Mở >_ CMD Shell của máy ZCU. 2. Gõ `sudo reboot`. 3. Bấm 🚀 Chạy lệnh. |
| **Kết quả thực tế** | Console hiện: `[debug] sudo=1, cmd gửi đi: env DISPLAY=:0 ... bash -c 'sudo -S -p '\''\'' reboot'` rồi `❌ LỖI: An established connection was aborted by the server.`; thanh trạng thái đỏ `❌ Lỗi: An established connection was aborted by the server.` — trong khi máy ZCU THỰC CHẤT đã reboot thành công (SSH ngắt là tất yếu). Dòng `[debug]` lộ chi tiết kỹ thuật nội bộ (cấu trúc lệnh sudo/escape) cho người dùng cuối. |
| **Kết quả mong đợi** | (1) Lệnh thuộc nhóm reboot/shutdown (`reboot`/`poweroff`/`halt`/`shutdown`/`init 0|6`/`systemctl reboot|poweroff|halt`, kể cả kèm `sudo`) + lỗi trả về là MẤT KẾT NỐI → hiển thị thông tin thân thiện, VD "ℹ️ Đã gửi lệnh khởi động lại tới máy — kết nối SSH sẽ ngắt trong giây lát. Vui lòng kết nối lại sau khoảng 1 phút." — KHÔNG tô đỏ LỖI. Lỗi khác (sai password sudo, lệnh không tồn tại...) vẫn báo lỗi như cũ, không nuốt lỗi thật. (2) Dòng `[debug]` ẩn mặc định, chỉ hiện khi bật chế độ debug. |

> **✅ Đã sửa (2026-07-26, senior-developer):**
> - **Nguyên nhân gốc:** (1) `RemoteCommandWindow.OnRunCommandClick` có duy nhất 1 nhánh `catch` chung → mọi exception (kể cả `SshConnectionException` do máy đích reboot cắt TCP — hệ quả TẤT YẾU của chính lệnh vừa gửi) đều hiển thị "❌ LỖI" đỏ. (2) Dòng chẩn đoán `[debug] sudo=..., cmd gửi đi: ...` được thêm khi fix hồi quy S3/G017 (`RunSshCommandAsync` → `diag?.Invoke`) và luôn ghi thẳng vào Console Output không có điều kiện.
> - **Cách sửa:**
>   1. **File mới `CcuUI/Views/SshCommandHints.cs`** (helper dùng chung): `IsShutdownCommand()` — regex match nhóm lệnh shutdown ở VỊ TRÍ LỆNH (đầu chuỗi/sau `;&|(`, cho phép tiền tố `sudo [options]`): `reboot|poweroff|halt|shutdown|(tel)init 0|6|systemctl reboot|poweroff|halt`; `IsConnectionDropped()` — duyệt cả chuỗi InnerException, nhận `SshConnectionException`/`SocketException` hoặc message chứa aborted/closed/reset/broken pipe/EOF/timed out; `DiagEnabled` — đọc env var `IPGS_RC_DEBUG=1` một lần.
>   2. `CcuUI/Views/RemoteCommandWindow.axaml.cs` — catch của `OnRunCommandClick`: điều kiện KÉP `IsShutdownCommand(cmdToRun) && IsConnectionDropped(ex)` → status xám "ℹ️ Đã gửi lệnh khởi động lại/tắt máy — kết nối SSH sẽ ngắt trong giây lát." + log thông tin thân thiện; mọi trường hợp khác giữ nguyên nhánh báo lỗi cũ. Dòng `[debug]` chỉ ghi khi `SshCommandHints.DiagEnabled`.
>   3. `CcuUI/Views/BulkActionWindow.axaml.cs` — cùng vấn đề (snippet "🔄 Khởi động lại" cũng điền `sudo reboot`, chạy hàng loạt): bọc `RunSshCommandAsync` bằng `catch ... when` cùng điều kiện kép → máy đó hiển thị Thành công kèm thông báo thân thiện thay vì ❌ Lỗi đỏ. (BulkAction không có dòng `[debug]` nên không cần gate.)
> - **Lý do chọn env var `IPGS_RC_DEBUG` cho debug flag:** codebase CcuUI chưa có cơ chế log-level/debug-flag nào sẵn có; env var cho phép kỹ sư hỗ trợ bật chẩn đoán ngay trên bản Release tại hiện trường không cần build lại (hơn `#if DEBUG`), và không thêm UI setting mới ngoài scope P3.
> - **Màn hình khác đã rà:** `CronJobWindow` (chỉ chạy `crontab` — không có lệnh shutdown), các installer (`ZcuSetupWizard`/`RemoteAppInstall`/`KioskDeploy` — không cho user gõ lệnh tùy ý, restart service không cắt SSH) → KHÔNG cần sửa.
> - **Build verify:** `dotnet build IPGS.RemoteControl.CcuUI -c Release` → **0 Error** (456 warnings = baseline full-rebuild đã biết).

| Finding | Trạng thái | File chính đã sửa |
|---|---|---|
| F07 Reboot báo LỖI đỏ + lộ dòng `[debug]` | ✅ Đã sửa (chờ QA smoke test) | `CcuUI/Views/SshCommandHints.cs` (mới), `RemoteCommandWindow.axaml.cs`, `BulkActionWindow.axaml.cs` |

**QA cần smoke test:** (1) `sudo reboot` trên tab Console → thông báo ℹ️ xám, không còn ❌ đỏ, không còn dòng `[debug]`; (2) sai password sudo → vẫn báo lỗi đỏ như cũ ("incorrect password attempts"); (3) lệnh không tồn tại → vẫn [STDERR] như cũ; (4) BulkAction snippet Reboot trên nhiều máy → từng máy Thành công kèm ghi chú ℹ️; (5) set `IPGS_RC_DEBUG=1` rồi chạy lệnh sudo → dòng `[debug]` xuất hiện lại.

---

## F08 — Kiosk Deploy: đã cài Autologin nhưng sau khi khởi động lại máy ZCU vẫn hiện màn hình đăng nhập

> **Người phát hiện:** USER (thao tác thật trên máy ZCU `192.168.0.101` sau khi chạy Kiosk Deploy phần autologin).

| Trường | Nội dung |
|---|---|
| **Màn hình / Thành phần** | KioskDeployWindow (tab Config máy tính — ô Autologin) → `KioskDeployService` → `scripts/linux-kiosk/2-configure-system.sh` mục [6/8] |
| **Mức độ** | P2 |
| **Các bước tái hiện** | 1. Chạy Kiosk Deploy với ô Autologin tick (đã chạy trên ZCU ngày 24/07, log báo hoàn thành). 2. Khởi động lại máy ZCU nhiều lần. 3. Quan sát: một lần khởi động (26/07 ~17:13) máy đứng ở màn hình đăng nhập GDM, phải nhập mật khẩu / khởi động lại mới vào được desktop. |
| **Kết quả thực tế** | Autologin hoạt động ở đa số lần boot nhưng KHÔNG ổn định — có lần boot greeter vẫn hiện và đứng đó vô hạn. Ngoài ra app luôn báo "Deploy hoàn tất" kể cả khi script cấu hình thất bại (không kiểm tra exit code) → nếu ghi config fail (VD sai mật khẩu sudo, máy không dùng gdm3) user vẫn tưởng đã cài xong. |
| **Kết quả mong đợi** | Máy tự vào desktop ở MỌI lần khởi động; nếu không áp dụng được autologin, app phải báo lỗi đỏ rõ ràng thay vì "Deploy hoàn tất". |
| **Bằng chứng từ máy thật (điều tra 26/07 23:1x)** | (1) `/etc/gdm3/custom.conf` ĐÃ có `AutomaticLoginEnable = true` + `AutomaticLogin = kztek` đúng cú pháp (ghi 24/07 21:41 — code ghi ĐÚNG file, máy dùng đúng gdm3). (2) `journalctl`: mọi boot có log đều mở session bằng PAM `gdm-autologin` thành công (16:26, 18:21, 20:49, 23:13 ngày 26/07). (3) Boot 17:13:36 (`last -x`): KHÔNG có dòng đăng nhập `kztek :0` nào suốt 17:13→18:20 và boot này KHÔNG để lại journal (máy là VM, boot bất thường sau crash 16:53) — đúng khung giờ user thấy màn hình đăng nhập. → Khớp lỗi đã biết của GDM: race hiếm khi boot chậm/bất thường khiến `AutomaticLogin` bị bỏ qua và greeter hiện ra chờ vô hạn. |

> **✅ Đã sửa (2026-07-26, senior-developer):**
> - **Nguyên nhân gốc:** (1) **GDM autologin race** — cấu hình được ghi đúng nhưng GDM có lỗi đã biết: ở lần boot chậm/bất thường (nhất là trên VM), `AutomaticLogin` bị bỏ qua và greeter hiện ra, không có cơ chế fallback → máy đứng ở màn hình đăng nhập vô hạn (bằng chứng boot 17:13 ngày 26/07). (2) **Báo thành công giả:** `KioskDeployService.RunCommand` bỏ qua `ExitStatus` của script; `2-configure-system.sh` khi không tìm thấy `/etc/gdm3/custom.conf` (máy dùng lightdm/sddm) chỉ warning rồi vẫn exit 0 → app luôn hiện "🎉 Deploy hoàn tất". (3) Script hardcode gdm3, không dò display manager thật.
> - **Cách sửa:**
>   1. `scripts/linux-kiosk/2-configure-system.sh` mục [6/8]: dò display manager động (`/etc/X11/default-display-manager` + `display-manager.service`), hỗ trợ gdm3/gdm/lightdm/sddm; với GDM ghi thêm **`TimedLoginEnable/TimedLogin/TimedLoginDelay = 5` làm fallback** — nếu autologin bị GDM bỏ qua và greeter hiện ra, TimedLogin tự đăng nhập user sau 5 giây, máy vẫn tự vào desktop; **KIỂM CHỨNG sau khi ghi** bằng cách grep đọc lại file thật (bắt được trường hợp sudo sai mật khẩu — `_sudo` nuốt stderr); không áp dụng được → in `AUTOLOGIN-FAILED` + `exit 1`.
>   2. `IPGS.RemoteControl.CcuClient/KioskDeployService.cs` (`RunCommand`): thêm `throwOnError` — kiểm tra `cmd.ExitStatus`, script setup fail → ném exception (kèm 5 dòng lỗi cuối) → `KioskDeployWindow` hiển thị "❌ Deploy thất bại: ..." thay vì thành công giả.
>   3. Áp dụng trực tiếp lên ZCU `192.168.0.101` (CHỈ phần autologin, không bật kiosk mode toàn phần): backup `/etc/gdm3/custom.conf.bak-2026-07-26`, ghi block Automatic+TimedLogin, verify grep OK.
> - **Kiểm chứng trên máy thật:** `sudo reboot` lúc 23:29 → boot 23:30:54, SSH kiểm tra: `who` → `kztek :0 23:31`, `loginctl show-session` → **`Service=gdm-autologin`, `State=active`**, `gnome-shell` chạy dưới kztek — máy tự vào desktop không cần nhập mật khẩu.
> - **Build verify:** `dotnet build IPGS.RemoteControl.CcuUI -c Release` → **0 Error** (456 warnings = baseline đã biết). `bash -n 2-configure-system.sh` → syntax OK.
> - **Lưu ý vận hành:** lần boot bất thường (crash/mất điện giữa chừng) giờ tối đa chờ thêm 5 giây ở greeter rồi TimedLogin tự vào; nếu vẫn phải đăng nhập → kiểm tra `grep -E '^(Automatic|Timed)' /etc/gdm3/custom.conf` và log deploy có dòng `AUTOLOGIN-VERIFIED` không.

| Finding | Trạng thái | File chính đã sửa |
|---|---|---|
| F08 Autologin không tác dụng sau restart | ✅ Đã sửa + kiểm chứng reboot thật trên ZCU (chờ QA smoke test bản deploy qua app) | `scripts/linux-kiosk/2-configure-system.sh`, `CcuClient/KioskDeployService.cs` |

---

## Review của Tech Lead — F07, F08 — 2026-07-26 23:38

Phạm vi: commit `fc7fd09` (F07) + `2727b87` (F08). Đã đọc toàn bộ diff, đối chiếu code hiện hành (`KioskDeployService.cs`, `KioskDeployWindow.axaml.cs`, grep toàn CcuUI tìm đường `[debug]` khác), MANUAL 6.2.1/9.3, CODE-GRAPH, GOTCHAS G019.

### Bảng verdict

| # | Thay đổi | Verdict | Nhận xét (kèm severity) |
|---|---|---|---|
| 1 | F07 — `SshCommandHints.ShutdownPattern` (`SshCommandHints.cs:22-24`) | **APPROVE** | Đã kiểm tra false-positive: `echo reboot`, `cat /var/log/reboot.log`, `grep reboot f` đều KHÔNG match (từ khóa phải ở vị trí lệnh — đầu chuỗi hoặc sau `;&\|(`, chỉ cho tiền tố `sudo [opts]`); `x && reboot`, `sudo reboot` match đúng. **FYI:** `shutdown -c` (hủy hẹn tắt máy) match pattern nhưng vô hại — lệnh này không làm đứt SSH nên điều kiện kép không kích hoạt. |
| 2 | F07 — `IsConnectionDropped` (`SshCommandHints.cs:47-68`) | **APPROVE-WITH-COMMENT** | **Optional:** vế `"timed out"` là rộng nhất — nếu đúng lúc chạy `sudo reboot` mà mạng chết thật (WiFi rớt/ZCU treo trước khi lệnh tới nơi), app vẫn hiện "ℹ️ Đã gửi lệnh khởi động lại" trong khi lệnh có thể chưa thực thi. Rủi ro CHẤP NHẬN ĐƯỢC cho P3 UX: chỉ xảy ra khi trùng hợp cả 2 điều kiện, thông báo đã dặn "kết nối lại sau ~1 phút" — nếu máy không lên lại, user tự phát hiện. Cân nhắc thêm chữ "nếu sau 2 phút máy không phản hồi, kiểm tra lại kết nối mạng" vào `ShutdownInfoMessage` ở vòng sau. |
| 3 | F07 — catch điều kiện kép `RemoteCommandWindow.axaml.cs:311-329` + `BulkActionWindow.axaml.cs:268-281` | **APPROVE** | Không nuốt lỗi thật: sai mật khẩu sudo / command-not-found đi qua stderr (không phải exception) → không rơi vào filter; exception khác shutdown-cmd vẫn báo đỏ như cũ. `catch ... when` ở BulkAction đúng scope per-máy. |
| 4 | F07 — gate `[debug]` bằng env `IPGS_RC_DEBUG` (`RemoteCommandWindow.axaml.cs:78-81`) | **APPROVE** | Env var hợp lý hơn `#if DEBUG` (bật được trên bản Release hiện trường, lý do đã ghi trong code). Grep toàn CcuUI xác nhận chỉ còn 1 điểm gọi `diag?.Invoke` và `bashCmd` không chứa mật khẩu (password đi qua stdin — S3). Không còn đường lộ khác. |
| 5 | F08 — kết luận root cause "GDM race hiếm" | **APPROVE-WITH-COMMENT** | Nói thẳng: chẩn đoán này **chưa được chứng minh trực tiếp** — bằng chứng là loại trừ (config đúng từ 24/07, mọi boot có journal đều `gdm-autologin` OK, boot lỗi 17:13 KHÔNG có journal nên không thể xác nhận điều gì đã xảy ra). "Không có journal" nghĩa là không loại trừ được nguyên nhân khác (VM crash giữa boot, disk chưa flush). TUY NHIÊN chấp nhận được vì **cách sửa không phụ thuộc chẩn đoán đúng**: TimedLogin 5s là defense-in-depth che mọi trường hợp greeter hiện ra bất kể lý do; verify-sau-ghi + exit-code che nhánh "ghi fail". Yêu cầu: nếu lỗi tái diễn sau bản này (greeter đứng > 10s) → mở finding mới, không đóng bằng "race hiếm" lần nữa. |
| 6 | F08 — `TimedLogin` fallback 5s mặc định | **APPROVE-WITH-COMMENT** | Tác dụng phụ có thật: TimedLogin áp dụng ở MỌI lần greeter hiện (kể cả sau khi admin chủ động Log Out để đăng nhập tài khoản khác) — có cửa sổ 5s trước khi máy tự vào lại kztek; GDM hủy đếm ngược khi user tương tác greeter nên thực tế vẫn đăng nhập tài khoản khác được. Trên thiết bị kiosk đã bật AutomaticLogin thì TimedLogin **không hạ thêm mức bảo mật nào** (ai chạm máy cũng đã vào được desktop). Chấp nhận mặc định, KHÔNG cần tách thành option riêng cho scope kiosk. **Nit:** nên ghi 1 câu vào MANUAL 9.3 rằng khi Log Out thủ công máy sẽ tự đăng nhập lại sau 5s nếu không thao tác — tránh kỹ thuật viên bất ngờ. |
| 7 | F08 — `RunCommand` thêm `throwOnError` (`KioskDeployService.cs:216-247`) | **APPROVE** | Opt-in đúng cách: default `false`, chỉ 2 call site script bật (dòng 165, 179) — các call site khác (`chmod` dòng 138, health-check) không đổi hành vi. Exception nổi lên qua `Task.Run` → `KioskDeployWindow.axaml.cs:113-117` bắt và hiện "❌ Deploy thất bại" (đã kiểm chứng). `using var ssh` (dòng 120) đảm bảo dispose khi ném giữa chừng. Fail giữa chừng để máy ở trạng thái cấu hình dở là chấp nhận được: script idempotent, chạy Deploy lại là đủ — tốt hơn hẳn "thành công giả" cũ; không cần rollback tự động. **FYI:** `cmd.ExitStatus ?? 0` coi null (kết nối đứt) là thành công — hiếm, và trường hợp đó thường đã ném exception ở tầng SSH.NET. |
| 8 | F08 — `2-configure-system.sh` [6/8] viết lại | **APPROVE-WITH-COMMENT** | Dò DM động đúng cách (`/etc/X11/default-display-manager` → `readlink -f display-manager.service`, xử lý symlink chuẩn, không parse `systemctl status`). Idempotent: xóa hết dòng cũ (`Automatic*`/`Timed*`) rồi ghi lại 1 block — chạy N lần không nhân bản dòng; thiếu section `[daemon]` → verify grep fail → `AUTOLOGIN-FAILED` exit 1 (fail đúng hướng). Gotcha `_sudo tee` chiếm stdin đã né đúng bằng `_sudo bash -c '... > file'` và ghi vào G019. **Nit:** `$KIOSK_USER` nội suy thẳng vào sed/printf trong double-quote — username chứa `&`, `/`, `'` sẽ vỡ; thực tế username kiosk do mình kiểm soát (kztek) + đã ShellQuote phía C#, chấp nhận, không cần sửa vòng này. |
| 9 | F08 — thao tác trên ZCU thật | **APPROVE** | Có backup `/etc/gdm3/custom.conf.bak-2026-07-26`, thay đổi chỉ 5 dòng trong `[daemon]`, hoàn tác được bằng copy ngược backup. Kiểm chứng reboot thật (`Service=gdm-autologin`) là bằng chứng mạnh cho happy path. |
| 10 | Đồng bộ tài liệu (MANUAL 6.2.1 + 9.3, CODE-GRAPH, GOTCHAS G019, BUG report F07/F08) | **APPROVE** | Khớp code: MANUAL 6.2.1 mô tả đúng thông báo ℹ️ mới; 9.3 mô tả đúng AUTOLOGIN-VERIFIED/FAILED + TimedLogin 5s + lệnh grep chẩn đoán; CODE-GRAPH có `SshCommandHints.cs` + env `IPGS_RC_DEBUG`; G019 chính xác kỹ thuật. **FYI:** PDF MANUAL/CODE-GRAPH chưa xuất lại được (Word COM RPC lỗi) — đã ghi chú, cần xuất bù khi Word ổn, không block. |

### Kết luận

**✅ APPROVE cả 2 commit — cho phép chuyển QA Engineer verify (Bước 4 WF-BUGFIX).** Không có REQUEST-CHANGES; các comment ở mục 2, 6, 8 là Optional/Nit — không chặn merge, đưa vào backlog cải tiến vòng sau.

**Lưu ý cho QA:** ngoài 5 case smoke test F07 SD đã liệt kê, bổ sung cho F08: (a) Deploy với sudo password SAI → app phải hiện "❌ Deploy thất bại ... AUTOLOGIN-FAILED", KHÔNG còn "🎉 Deploy hoàn tất"; (b) Deploy autologin 2 lần liên tiếp → `custom.conf` không bị nhân bản dòng; (c) tắt autologin (bỏ tick) → `AutomaticLoginEnable = false` + `TimedLoginEnable = false`.

— Tech Lead, 2026-07-26 23:38

---

## F09 — Kiosk: bấm/giữ màn hình hoặc nhấn phím Super vẫn bung GNOME Activities Overview ("Type to search") → thoát được chế độ kiosk

> **Người phát hiện:** USER (thao tác thật trên máy ZCU `192.168.0.101` đã cài kiosk).

| Trường | Nội dung |
|---|---|
| **Màn hình / Thành phần** | Máy kiosk ZCU (GNOME Shell 42.9, X11) — cấu hình bởi `scripts/linux-kiosk/1-install-software.sh` + `2-configure-system.sh` qua KioskDeployWindow |
| **Mức độ** | P2 (bảo mật / kiosk hardening) |
| **Các bước tái hiện** | 1. Máy ZCU đã chạy Kiosk Deploy (ẩn Top Bar/Dock/Activities). 2. Nhấn phím Super (hoặc thao tác chạm trên màn hình cảm ứng). 3. Activities Overview bung ra toàn màn hình kèm ô "Type to search". |
| **Kết quả thực tế** | Từ ô search có thể gõ tên ứng dụng để mở Terminal, Settings… hoặc thoát app kiosk. Ngoài ra Alt+F2 mở "Run a Command", Ctrl+Alt+T mở Terminal, Alt+Tab/Alt+F4 chuyển/đóng cửa sổ, menu hệ thống cho Log Out — user thường đổi lại được mọi `gsettings` vì trước đây chỉ set theo user, không lock. GNOME 42 còn tự KHỞI ĐỘNG VÀO overview khi login chưa có cửa sổ nào. |
| **Kết quả mong đợi** | Người dùng kiosk KHÔNG gọi được overview/search/terminal/settings, KHÔNG thoát/đóng app kiosk, KHÔNG đăng xuất/đổi user, và KHÔNG tự chỉnh lại được các thiết lập đó. |

> **✅ Đã sửa (2026-07-27, senior-developer):**
> - **Nguyên nhân gốc:** kiosk setup cũ chỉ ẨN giao diện (Top Bar/Dock/Activities button qua Just Perfection) và set `gsettings` THEO USER — không vô hiệu phím tắt hệ thống (Super/Alt+F2/Ctrl+Alt+T/Alt+Tab/Alt+F4), không khoá lockdown schema, và mọi key user đều tự set ngược lại được (không có dconf lock).
> - **Cách sửa:**
>   1. `scripts/linux-kiosk/2-configure-system.sh` — thêm mục **[9/9] "Khoá lối thoát kiosk"** (tham số 11, 2 chiều): ghi dconf DB HỆ THỐNG `/etc/dconf/db/local.d/00-kiosk-lockdown` + **LOCK** `/etc/dconf/db/local.d/locks/00-kiosk-lockdown` + profile `/etc/dconf/profile/user` (GOTCHA: Ubuntu không có sẵn file profile này — thiếu nó thì toàn bộ system-db bị bỏ qua), rồi `dconf update`. Khoá: `overlay-key=''`, toggle-overview/application-view/message-tray, panel-run-dialog (Alt+F2), panel-main-menu, switch/cycle-windows + switch-applications (Alt+Tab), close (Alt+F4), switch-to-workspace-*, media-keys terminal (Ctrl+Alt+T)/logout/control-center/home/email/www/search, lockdown `disable-command-line/log-out/user-switching/lock-screen/printing/print-setup=true`, `screensaver lock-enabled=false`, dynamic-workspaces=false + num-workspaces=1. Chiều 0 = gỡ khoá bảo trì (xóa 2 file + `dconf update`).
>   2. `scripts/linux-kiosk/1-install-software.sh` — khi ẩn Activities: set Just Perfection `startup-status=0` (login vào thẳng desktop thay vì overview — GNOME 40+ mặc định boot vào overview), `search=false` + `type-to-search=false` (ẩn ô "Type to search" trong overview).
>   3. `CcuClient/KioskDeployService.cs` — option `LockdownShell` (mặc định true, truyền tham số 11); `CcuUI/Views/KioskDeployWindow.axaml(.cs)` — checkbox "Khoá lối thoát kiosk (dconf lock)" (bỏ tick = gỡ khoá bảo trì).
> - **Kiểm chứng trên máy thật (ZCU 192.168.0.101, 2 lần reboot):**
>   - `gsettings get` mọi key trả về giá trị khoá; `gsettings set org.gnome.mutter overlay-key 'Super_L'` bị từ chối: **"The key is not writable"**.
>   - GOTCHA kiểm chứng được: NGAY SAU `dconf update`, GNOME Shell ĐANG CHẠY vẫn dùng profile cũ (Alt+F2 vẫn mở "Run a Command" — có screenshot) vì dconf profile chỉ được đọc lúc process khởi động → **BẮT BUỘC reboot/re-login**.
>   - Sau reboot: gửi phím thật qua SSH (`xdotool key super / super+s / super+a / alt+F2 / ctrl+alt+t`) + chụp `gnome-screenshot` — ảnh trước/sau **giống hệt từng byte**: không overview, không Run-a-Command, không Terminal. Login vào thẳng desktop (không còn màn "Type to search"). SSH + ZcuAgent (PID 1750) vẫn hoạt động bình thường.
> - **Giới hạn còn lại (KHÔNG chặn được bằng dconf):** cử chỉ cảm ứng vuốt 3 ngón lên (GNOME 40+) mở overview là hành vi hard-code trong GNOME Shell, không có key cấu hình; không giả lập được touch qua SSH nên chưa kiểm chứng từ xa — cần thử tay trên màn cảm ứng thật. Giảm nhẹ đã áp: overview (nếu mở được bằng gesture) giờ KHÔNG còn ô search, không dash, không Activities, chỉ 1 workspace — không có gì để thao tác, chạm vào thumbnail là quay lại app.
> - **Đề xuất hướng 2 (chỉ khảo sát, chưa làm):** gói `gnome-kiosk` (session kiosk chính thức không có overview/gesture) **KHÔNG có trong repo Ubuntu 22.04** (`apt-cache policy/search` rỗng) — chỉ khả thi nếu nâng Ubuntu 24.04 hoặc tự build; phương án thay thế là session WM tối giản (openbox/cage) nhưng đổi session mặc định cần user xác nhận riêng. Đưa vào backlog.
> - **Gỡ khoá để bảo trì (quản trị viên, qua SSH — QUAN TRỌNG):**
>   `sudo rm /etc/dconf/db/local.d/00-kiosk-lockdown /etc/dconf/db/local.d/locks/00-kiosk-lockdown && sudo dconf update` rồi đăng xuất/reboot — hoặc bỏ tick "Khoá lối thoát kiosk" trong KioskDeployWindow và Deploy lại. Khoá lại: tick + Deploy.
> - **Backup / hoàn tác trên ZCU:** `/etc/dconf/profile/user` là file TẠO MỚI (trước đó không tồn tại — hoàn tác = xóa file); 2 file lockdown là tạo mới (hoàn tác = xóa + `dconf update`); các lần ghi đè sau có backup đuôi `.bak-2026-07-26`. Đã cài thêm `xdotool` + `gnome-screenshot` (chỉ phục vụ kiểm chứng, không ảnh hưởng kiosk).
> - **Build verify:** `dotnet build IPGS.RemoteControl.CcuUI -c Release` → 0 Error. `bash -n` cả 2 script → syntax OK. (Xem mục build cuối phiên.)

| Finding | Trạng thái | File chính đã sửa |
|---|---|---|
| F09 Kiosk thoát được qua Activities overview / phím tắt | ✅ Đã sửa + kiểm chứng phím thật trên ZCU (gesture cảm ứng cần thử tay tại máy) | `scripts/linux-kiosk/2-configure-system.sh`, `1-install-software.sh`, `CcuClient/KioskDeployService.cs`, `CcuUI/Views/KioskDeployWindow.axaml(.cs)` |

---

## F10 — Kiosk cảm ứng (không bàn phím): chạm/giữ màn hình vẫn bung Activities overview + app kiosk không tự khởi động lại khi đóng/crash

> **Người phát hiện:** USER (thao tác thật trên màn hình cảm ứng máy ZCU `192.168.0.101` — máy KHÔNG có bàn phím vật lý, chỉ có màn cảm ứng).

> **Phạm vi đã thu hẹp theo quyết định user (2026-07-27):** Máy kiosk ZCU là **màn hình cảm ứng, KHÔNG có bàn phím**. Vì vậy user quyết định **BỎ QUA toàn bộ nhóm lỗ phím tắt** mà audit `AUDIT-kiosk-escape-vectors-2026-07-27.md` nêu (Super+1..9, Ctrl+Alt+Fx VT switch, Alt+SysRq, Alt+Space, Alt+F7/F8, switch-group...) — không khai thác được khi không có bàn phím. **Cũng giữ nguyên** user `kztek` trong group `sudo` (cần cho luồng deploy agent). F10 chỉ xử lý 2 việc: (A) chặn cử chỉ CẢM ỨNG mở overview, (B) watchdog tự khởi động lại app kiosk.

| Trường | Nội dung |
|---|---|
| **Màn hình / Thành phần** | Máy kiosk ZCU (GNOME Shell 42.9, **X11**) cấu hình bởi `scripts/linux-kiosk/1-install-software.sh` + `2-configure-system.sh` qua KioskDeployWindow |
| **Mức độ** | P2 (bảo mật / kiosk hardening — chỉ cảm ứng) |
| **Các bước tái hiện (A - cử chỉ)** | 1. Máy ZCU đã chạy Kiosk Deploy (F09 đã khoá phím tắt). 2. Trên màn **cảm ứng**, vuốt nhiều ngón lên / chạm-giữ / vuốt mép → Activities overview bung ra (dù F09 đã ẩn ô search). |
| **Các bước tái hiện (B - watchdog)** | 1. App kiosk đang chạy fullscreen. 2. App bị đóng/crash (hoặc chưa từng khởi động). 3. Màn hình rơi về **desktop GNOME trống** — không có cơ chế tự bật lại app (audit xác nhận: không có systemd service/watchdog, chỉ autostart `.desktop` chạy 1 lần). |
| **Kết quả thực tế** | (A) Cử chỉ cảm ứng vẫn mở được overview — F09 (dconf lock) chỉ vô hiệu được **phím tắt**, còn cử chỉ đa chạm/edge-swipe/hot-corner mở overview là hard-code trong GNOME Shell 42, KHÔNG có dconf key nào tắt (Just Perfection **v26** — bản duy nhất tương thích Shell 42 — cũng KHÔNG có key `gesture`, đã kiểm chứng schema thật trên ZCU). (B) App đóng/crash → lộ desktop trống, phơi bày mọi vector. |
| **Kết quả mong đợi** | (A) Cử chỉ cảm ứng KHÔNG mở được overview; chạm 1 ngón để dùng app vẫn hoạt động bình thường. (B) App bị đóng/crash → tự khởi động lại trong vài giây, không để lộ desktop trống; nếu binary chưa tồn tại thì KHÔNG rơi vào vòng restart vô hạn ghi đầy log. |

> **✅ Đã sửa (2026-07-27, senior-developer):**
> - **Nguyên nhân gốc:**
>   - (A) F09 khoá phím tắt bằng dconf, nhưng cử chỉ cảm ứng mở overview (vuốt đa chạm, edge-swipe, hot corner, double-super) do GNOME Shell xử lý ở tầng compositor và **không có gsettings/dconf key** để tắt trên GNOME 42. Just Perfection v26 (max cho Shell 42) không có key `gesture`. `startup-status=0`/`search=false` của F09 chỉ ẩn ô search, KHÔNG chặn được việc overview bung ra.
>   - (B) App kiosk chạy 1 lần qua autostart `.desktop`, không có supervisor → đóng/crash là mất, rơi về desktop trống.
> - **Cách sửa:**
>   1. **(A) Extension GNOME Shell CỤC BỘ `disable-overview-gestures@kztek`** (nhúng heredoc trong `1-install-software.sh`, cài bằng cách ghi file — KHÔNG tải từ store nên không cần bấm popup xác nhận trên màn hình như `gext install`). Extension vô hiệu **HOÀN TOÀN** overview bằng 3 lớp: (i) override `Main.overview.show`/`showApps` thành no-op, (ii) bắt tín hiệu `'showing'` → `hide()` ngay, (iii) tắt các `SwipeTracker` cử chỉ (overview + chuyển workspace). Vì kiosk = 1 app fullscreen duy nhất, việc chặn overview theo MỌI trigger (thay vì cố nhận diện từng cử chỉ cụ thể) là cách bền vững nhất. Chạm 1 ngón để dùng app KHÔNG bị ảnh hưởng (chỉ overview bị chặn). Tự động cài/gỡ theo ô tick **"Ẩn nút Activities"** (cùng ý nghĩa "chặn truy cập overview"). Có hiệu lực sau **restart/đăng nhập lại** (deploy vốn đã yêu cầu restart).
>   2. **(B) Watchdog systemd USER service `ipgs-kiosk-app.service`** (`2-configure-system.sh` mục [10], tham số 12): `Restart=always` + `RestartSec=3` → app đóng/crash tự bật lại sau 3s; `StartLimitIntervalSec=60` + `StartLimitBurst=5` (ở `[Unit]`, systemd 249) → binary CHƯA tồn tại thì fail 5 lần/60s rồi **DỪNG hẳn** kèm log rõ ("start request repeated too quickly"), KHÔNG lặp vô hạn; `ExecStart=/bin/bash -lc 'exec <app>'` để nạp PATH đăng nhập; `WantedBy=graphical-session.target`. Khi bật watchdog, mục [8] **bỏ autostart `.desktop` của app** để tránh chạy 2 instance. Script chỉ **cài + enable** (không `start` ngay) vì app chưa deploy — service tự chạy ở lần đăng nhập kế tiếp.
>   3. **`CcuClient/KioskDeployService.cs`** — thêm option `EnableWatchdog` (mặc định true, truyền tham số 12); **`CcuUI/Views/KioskDeployWindow.axaml(.cs)`** — checkbox mới **"Watchdog: tự khởi động lại app kiosk khi đóng/crash"** ở Tab "⚙️ Config phần mềm" (mặc định tick), đồng bộ style các checkbox có sẵn.
> - **Kiểm chứng thật trên ZCU `192.168.0.101` (X11, VirtualBox — chỉ có VirtualBox USB Tablet, KHÔNG có màn cảm ứng vật lý để giả lập gesture):**
>   - **Watchdog — ĐÃ kiểm chứng bằng binary giả an toàn, đã gỡ sạch:** (i) binary KHÔNG tồn tại → service restart đúng **5 lần** rồi vào trạng thái `failed` (NRestarts=5, ActiveState=failed) — chống-loop hoạt động; (ii) binary sống (fake `sleep`) → `kill -9` MainPID 5770 → service **tự restart**, MainPID mới 5879, NRestarts=1, log ghi 2 lần start. Sau test đã **gỡ sạch** unit `ipgs-kiosk-test.service` + fake app + file /tmp; xác nhận không còn process/unit rớt lại; `ZcuAgent` vẫn RUNNING, SSH sống.
>   - **Script:** `bash -n` cả 2 script trên ZCU → SYNTAX OK. `metadata.json` (JSON valid), `extension.js` (`node --check` valid).
> - **⚠️ CẦN USER THỬ TAY trên màn cảm ứng thật (không giả lập được từ xa — VM không có touchscreen vật lý):**
>   1. Sau khi Deploy (tick "Ẩn nút Activities") + **khởi động lại** ZCU, mở app kiosk. **Vuốt 3 ngón từ dưới lên** giữa màn hình → mong đợi: overview **KHÔNG** bung (nếu có bung thì tự đóng ngay, không thao tác được). Trước F10: overview bung + có ô "Type to search".
>   2. **Chạm-giữ ~1s** (long-press = right-click) lên vùng trống và trên app → mong đợi: không ra menu nguy hiểm, không bung overview.
>   3. **Chạm 1 ngón** vào các nút trong app → mong đợi: app phản hồi bình thường (extension KHÔNG chặn chạm dùng app).
>   4. (Watchdog) Sau khi deploy app thật + tick Watchdog + restart: đóng/kill app → mong đợi app **tự mở lại trong ~3 giây**, không lộ desktop trống.
> - **Giới hạn còn lại:** nếu bản GNOME/extension tương lai đổi cấu trúc `Main.overview`, các override có `try/catch` nên không crash shell nhưng có thể mất tác dụng — cần verify lại khi nâng GNOME. Hướng bền vững nhất vẫn là session kiosk chuyên dụng (`gnome-kiosk`/`cage`) — chưa có gói cho Ubuntu 22.04 (backlog).
> - **Backup / hoàn tác trên ZCU:** F10 CHƯA áp lên cấu hình kiosk thật của ZCU (chỉ test watchdog bằng binary giả rồi gỡ). Khi Deploy thật: extension là thư mục tạo mới (gỡ = bỏ tick Ẩn Activities + Deploy, hoặc `rm -rf ~/.local/share/gnome-shell/extensions/disable-overview-gestures@kztek`); watchdog unit có backup `.bak-*` khi ghi đè, gỡ = bỏ tick Watchdog + Deploy.
> - **Build verify:** `dotnet build IPGS.RemoteControl.CcuClient -c Release` → 0 Warning 0 Error; `dotnet build IPGS.RemoteControl.CcuUI -c Release --no-dependencies` → **0 Error** (19 warnings = baseline).

> **🔁 Vòng 2 — sửa theo review Tech Lead (`830c5e7`), 2026-07-27 (senior-developer):**
> - **R1 (Required) — autostart `.desktop` mồ côi:** tái cấu trúc mục [8/9] của `2-configure-system.sh` — trạng thái `ipgs-kiosk.desktop` của APP giờ quyết định theo ma trận (Watchdog trước, Autostart sau), luôn nhất quán với cấu hình VỪA chọn bất kể lần deploy trước: Watchdog=1 → XÓA `.desktop` (bất kể Autostart, chặn kịch bản deploy lần 1 Autostart=1/Watchdog=0 rồi lần 2 Autostart=0/Watchdog=1 → 2 instance); Watchdog=0 + Autostart=1 → TẠO; Watchdog=0 + Autostart=0 → XÓA (2 chiều). `unclutter.desktop` theo Autostart 2 chiều (bỏ tick = xóa). Chiều ngược lại đã an toàn sẵn: mục [10] nhánh Watchdog=0 disable+xóa `ipgs-kiosk-app.service` vô điều kiện — không còn service mồ côi.
> - **R2 (Required) — watchdog chết vĩnh viễn:** đổi unit `RestartSec=3` + `StartLimitBurst=5/60s` → **`RestartSec=10` + `StartLimitIntervalSec=0`** (tắt hẳn rate-limit start). Lý do: kiosk KHÔNG NGƯỜI TRỰC — app crash-loop/binary chưa deploy mà service vào `failed` vĩnh viễn thì lộ desktop trống mãi mãi, tái mở đúng lỗ P1 watchdog sinh ra để chặn; thà restart mãi. Chống spam log: nhịp 10s → ~6 chu kỳ/phút, dưới xa ngưỡng rate-limit mặc định journald (RateLimitBurst=10000/30s) và journald tự xoay vòng theo SystemMaxUse; KHÔNG dùng `StandardOutput=null` để giữ log chẩn đoán vì sao app chết. (Lưu ý: kết quả test "dừng sau 5 lần" ở vòng 1 mô tả hành vi unit CŨ — unit mới cố ý không dừng.)
> - **R3 (gộp theo đề xuất Tech Lead):** thêm vào dconf lock F09: `switch-to-application-1..9` (Super+1..9 — lỗ P1 audit: mở thẳng Terminal/Nautilus từ favorites trên máy có bàn phím/cắm bàn phím USB), `switch-group(-backward)` (Alt+`), `activate-window-menu` (Alt+Space), `minimize` (Super+H) — cả settings lẫn locks. Không ảnh hưởng máy cảm ứng hiện tại; script dùng chung cho mọi máy kiosk.
> - **MANUAL 9.3:** sửa câu chữ gesture từ "đã được chặn" → "được cấu hình để chặn, **cần nghiệm thu trực tiếp tại máy**" (chưa thử tay trên màn cảm ứng thật); cập nhật mô tả Watchdog theo unit mới (thử mãi mỗi 10s, không tự dừng); bổ sung Super+1..9/Alt+`/Alt+Space/Super+H vào danh sách phím bị khoá.
> - **Verify vòng 2:** `bash -n` cả 2 script → OK; không chạm C# (checkbox/option giữ nguyên, chỉ đổi nội dung script) → không cần build lại; DOCX/PDF xuất lại cho BUG + MANUAL (kiểm tra mtime theo G021).

| Finding | Trạng thái | File chính đã sửa |
|---|---|---|
| F10 (A) Cử chỉ cảm ứng mở overview | ✅ Đã sửa (extension chặn overview) — **cần user thử tay trên màn cảm ứng thật** (VM không có touchscreen để giả lập) | `scripts/linux-kiosk/1-install-software.sh` |
| F10 (B) App kiosk không tự khởi động lại | ✅ Đã sửa (vòng 2: RestartSec=10, không bao giờ chết hẳn) + kiểm chứng cơ chế restart thật bằng binary giả (vòng 1), đã gỡ sạch | `scripts/linux-kiosk/2-configure-system.sh`, `CcuClient/KioskDeployService.cs`, `CcuUI/Views/KioskDeployWindow.axaml(.cs)` |
| F10 vòng 2 (R1/R2/R3 theo review Tech Lead) | ✅ Đã sửa — chờ Tech Lead re-review | `scripts/linux-kiosk/2-configure-system.sh`, `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` |

---

## Review của Tech Lead — F09, F10 + audit — 2026-07-27 00:49

Phạm vi: commit `0dc0d42` (F09), `e267826` (audit), `4f19374` (F10). Đã đọc toàn bộ diff từng commit, không chỉ commit message. Bối cảnh đã chốt bởi user (KHÔNG review ngược lại): máy kiosk chỉ có màn cảm ứng, không bàn phím → không vá nhóm phím tắt, giữ `kztek` trong `sudo`, không gỡ Terminal/Nautilus, không đổi session.

### Verdict tổng: 🔶 REQUEST-CHANGES (2 lỗi Required trong F10 watchdog — đều nhỏ, sửa nhanh; phần còn lại APPROVE)

### Bảng verdict theo hạng mục

| # | Hạng mục | Verdict | Lý do chính |
|---|---|---|---|
| 1 | F10-A: Extension monkey-patch `Main.overview.show`/`showApps` — độ bền qua nâng cấp GNOME | 🔶 APPROVE-WITH-COMMENT | `metadata.json` pin `"shell-version": ["42"]` (`1-install-software.sh`, heredoc metadata): nâng GNOME >42 → shell TỰ TẮT extension **âm thầm**, gesture mở overview hoạt động lại mà không có bất kỳ tín hiệu nào. `try/catch` quanh `_setTracker` chống crash tốt nhưng đồng thời cũng nuốt lỗi im lặng. **Required:** bổ sung cơ chế phát hiện — tối thiểu (a) thêm test case QA/MANUAL: sau reboot chạy `gnome-extensions info disable-overview-gestures@kztek` phải ra `State: ACTIVE`; (b) backlog: ZcuAgent SysInfo/health-check báo trạng thái extension về CCU để giám sát từ xa. Không cần đổi cách làm — với Shell 42 không có API chính thức, monkey-patch là lựa chọn đúng, đã ghi nhận ở G021. |
| 2 | F10-A: Override có làm hỏng luồng nội bộ Shell không (treo/crash → màn đen) | ✅ APPROVE | Đánh giá kỹ `extension.js`: (i) gán `Main.overview.show = function(){}` là shadow trên instance, `disable()` khôi phục đúng — không đụng prototype; (ii) handler `'showing' → hide()` an toàn trên Shell 42 (chính `toggle()` nội bộ cũng gọi hide khi đang showing), và gần như không bao giờ fire vì `show()` đã no-op; (iii) luồng khởi động Shell dùng `runStartupAnimation` — không qua `show()`, và Just Perfection `startup-status=0` (F09) đã bỏ overview lúc boot → không xung đột; (iv) mọi truy cập `_swipeTracker` bọc try/catch → API đổi thì chỉ mất tính năng, không crash. Rủi ro màn đen: THẤP. FYI: chuỗi private `_overview._controls._workspacesDisplay._swipeTracker` chắc chắn vỡ khi nâng Shell — quay về rủi ro mục #1, không thêm rủi ro mới. |
| 3 | F10-A: Vị trí cài + khả năng user tự tắt | 🔶 APPROVE-WITH-COMMENT | Cài user-dir `~/.local/share/gnome-shell/extensions/` (`EXT_DIR_GESTURE` trong `1-install-software.sh`) — user sở hữu file, tắt được qua Extension Manager (Super+3) hoặc terminal. Trên máy chỉ-cảm-ứng đây là vòng bảo vệ khép kín (muốn tắt extension phải mở được app khác → cần overview → đã bị chính extension chặn) → chấp nhận được. **Nợ kỹ thuật ghi nhận:** cắm bàn phím USB là phá được vòng này. Optional (backlog): cài system-wide `/usr/share/gnome-shell/extensions/` + root-owned để user không xoá/sửa được file. |
| 4 | F10-A: Ràng extension vào checkbox "Ẩn nút Activities" | ✅ APPROVE (Optional) | Gộp hợp lý về ngữ nghĩa — "ẩn Activities" trong ngữ cảnh kiosk nghĩa là "không cho vào overview"; MANUAL 9.3 đã ghi rõ hành vi gộp (📱 note) nên không gây bất ngờ. Optional: nếu sau này có khách cần overview nhưng vẫn ẩn nút → tách checkbox riêng; chưa cần bây giờ (YAGNI). |
| 5 | F10-B: Watchdog `StartLimitBurst=5/60s` → `failed` vĩnh viễn | 🔴 REQUEST-CHANGES (Required R2) | Unit trong `2-configure-system.sh` mục [10]: `StartLimitIntervalSec=60` + `StartLimitBurst=5` + `RestartSec=3` — nếu app THẬT crash-loop (config hỏng, thiếu lib sau update) service vào `failed` sau ~15-20 giây và **không bao giờ tự hồi phục** cho tới khi có người reboot/`reset-failed` → kiosk không người trực lộ desktop trống vĩnh viễn — tái mở đúng lỗ P1 mà F10 sinh ra để vá. Chống-loop khi "binary chưa tồn tại" là ý định tốt nhưng trả giá sai chỗ. **Yêu cầu sửa (chọn 1):** (a) `RestartSec=10` + `StartLimitIntervalSec=0` (tắt limit) — retry vô hạn mỗi 10s, journald tự rate-limit, ~6 dòng log/phút chấp nhận được; hoặc (b) giữ StartLimit nhưng thêm systemd timer mỗi 5-10 phút chạy `systemctl --user reset-failed ipgs-kiosk-app` — tự hồi phục theo chu kỳ. Khuyến nghị (a) — đơn giản hơn. Nhớ cập nhật MANUAL đoạn "thử vài lần rồi tự dừng" theo phương án chọn. |
| 6 | F10-B: Trạng thái mồ côi autostart `.desktop` khi deploy 2 lần khác cấu hình | 🔴 REQUEST-CHANGES (Required R1) | Lệnh `rm -f ~/.config/autostart/ipgs-kiosk.desktop` chỉ nằm TRONG nhánh `if [ "$ENABLE_AUTOSTART" = "1" ]` (mục [8/9]). Kịch bản lỗi: deploy #1 (Autostart=1, Watchdog=0) tạo `.desktop`; deploy #2 (Autostart=0, Watchdog=1) → nhánh else của [8/9] chỉ `echo`, KHÔNG xoá `.desktop` cũ, mục [10] cài service → lần login sau app chạy **2 instance** (autostart cũ + watchdog). Fix ~2 dòng: chuyển/duplicate lệnh `rm` vào mục [10] nhánh `ENABLE_WATCHDOG=1` (luôn xoá `.desktop` app khi watchdog bật, bất kể ENABLE_AUTOSTART). Chiều ngược (Watchdog 1→0, Autostart=1) đã đúng: [8/9] tái tạo `.desktop`, [10] gỡ service — không mồ côi. |
| 7 | F10-B: `WantedBy=graphical-session.target`, linger, thứ tự với autologin F08 | ✅ APPROVE (FYI) | Không cần `enable-linger`: `graphical-session.target` chỉ đạt khi có phiên đồ hoạ; autologin GDM (F08) đảm bảo phiên luôn được tạo lúc boot → service start đúng thời điểm, và phiên SSH thuần không kích hoạt target này (không start nhầm — thiết kế đúng). `systemctl --user enable` qua SSH hoạt động vì SSH login tự spawn user manager. **FYI cho QA:** kiểm chứng trên ZCU dùng binary GIẢ (không cần DISPLAY) — chưa chứng minh app GUI thật mở được cửa sổ từ service (phụ thuộc gnome-session import `DISPLAY`/`XAUTHORITY` vào systemd user env — Ubuntu 22.04 có làm, nhưng PHẢI verify bằng app thật khi deploy). |
| 8 | F09: thiếu `switch-to-application-1..9` trong danh sách lock | 🔶 APPROVE-WITH-COMMENT | Quan điểm Tech Lead: **NÊN thêm** vào script ở lần sửa kế tiếp (không block đợt này). Lý do: (i) script dùng chung cho MỌI máy kiosk — máy tương lai có thể có bàn phím; (ii) chính AUDIT (bản cập nhật 2026-07-27) ghi "vẫn khai thác được nếu ai đó CẮM bàn phím USB vào" — Super+4 → Terminal là lỗ P1 đã chứng minh bằng ảnh; (iii) chi phí ≈ 0: thêm 9 dòng key + 9 dòng lock, không ảnh hưởng máy chỉ-cảm-ứng. Tôn trọng quyết định scope của user cho máy hiện tại — đề xuất gộp vào cùng commit fix R1/R2 (cùng file) hoặc ghi backlog rõ ràng. Cân nhắc thêm cùng lúc `switch-group`/`activate-window-menu`/`minimize` (cùng lý do, cùng chi phí ≈ 0). **Nợ kỹ thuật đã chấp nhận — phải liệt kê trong release note nội bộ.** |
| 9 | F09: ghi đè `/etc/dconf/profile/user` | ✅ APPROVE (Optional) | Có backup trước khi ghi đè (`cp "$DCONF_PROFILE" "$DCONF_PROFILE.$BAK_SUFFIX"`) — đạt yêu cầu tối thiểu. Rủi ro thực tế thấp: Ubuntu mặc định KHÔNG có file này (G020). Optional: nếu file đã tồn tại và chứa `system-db` khác (VD site-db của khách) thì bị thay bằng bản 2 dòng — nên chỉ append `system-db:local` khi thiếu thay vì ghi đè cả file. Nhánh gỡ khoá không khôi phục profile: chấp nhận được (profile trỏ tới db rỗng là vô hại). |
| 10 | Đồng bộ tài liệu (§15/§17): MANUAL 9.3, AUDIT, BUG, CODE-GRAPH, GOTCHAS G020/G021 | 🔶 APPROVE-WITH-COMMENT | Đối chiếu đủ: CODE-GRAPH mô tả đúng tham số 11/12 + extension; GOTCHAS G020/G021 chính xác và hữu ích; AUDIT cập nhật trung thực (giữ bảng gốc, đánh dấu ⏭️ N/A kèm lý do). **Nit (tài liệu hứa hơn code chứng minh):** MANUAL 9.3 viết "cử chỉ cảm ứng **đã được chặn**"/"vô hiệu hoàn toàn" như sự thật đã kiểm chứng, trong khi AUDIT + BUG report ghi rõ "cần user thử tay" (VM không có touchscreen). Đề nghị thêm 1 câu vào MANUAL: "đã kiểm chứng bằng mọi trigger giả lập được; cử chỉ đa chạm thật cần xác nhận tại máy" — sửa cùng đợt fix R1/R2. Watchdog MANUAL khớp code (trừ đoạn "thử vài lần rồi dừng" sẽ đổi theo R2). |
| 11 | Audit `e267826` (QA) | ✅ APPROVE | Audit chất lượng cao: ~30 hạng mục, có bằng chứng screenshot/`gsettings writable` cho từng dòng, phân mức rủi ro đúng, không sửa máy, khôi phục baseline. Bản cập nhật trong `4f19374` giữ nguyên dữ liệu gốc thay vì xoá — đúng chuẩn audit trail. |

### Rủi ro tồn đọng — user CẦN BIẾT (kể cả nợ đã chấp nhận theo quyết định scope)

1. **Cắm bàn phím USB = phá toàn bộ kiosk** (nợ đã chấp nhận): Super+4 → Terminal (đã chứng minh bằng ảnh), VT switch Ctrl+Alt+F3, Alt+SysRq+B reboot. `kztek` trong `sudo` + home ghi được → biết mật khẩu là chiếm máy vĩnh viễn. Khuyến nghị vận hành: khoá vật lý cổng USB hoặc tem niêm phong.
2. **USB automount-open còn hở** — vector KHÔNG cần bàn phím: cắm USB → Nautilus tự mở (audit Nhóm 4). Nằm ngoài scope F10 theo chốt của user, nhưng là vector cảm-ứng-độc-lập duy nhất còn lại → đề nghị cân nhắc vá đợt sau (`automount-open=false` + lock, ~4 dòng).
3. **Extension chết âm thầm khi nâng GNOME/Ubuntu** (mục #1) — hàng rào duy nhất chặn gesture; mất là hở lại không ai biết. Cần bước verify sau reboot + health check backlog.
4. **Gesture thực tế CHƯA được kiểm chứng trên màn cảm ứng thật** — toàn bộ F10-A mới chứng minh được bằng suy luận + trigger giả lập. QA/user PHẢI thử tay theo AUDIT §3 (mục 1, 2) trước khi coi F10 là done.
5. Chuột USB cắm thêm: middle-click/nút phụ không chặn được bằng dconf (audit Nhóm 2) — rủi ro thấp vì overview đã bị extension vô hiệu.

### Kết luận

**CHƯA chuyển QA verify.** Yêu cầu Senior Developer sửa 2 điểm Required trên cùng file `scripts/linux-kiosk/2-configure-system.sh`:

- **R1** (mục #6): xoá `~/.config/autostart/ipgs-kiosk.desktop` trong mục [10] khi `ENABLE_WATCHDOG=1`, độc lập với `ENABLE_AUTOSTART` — chặn double-instance.
- **R2** (mục #5): đổi chính sách restart để watchdog không chết vĩnh viễn trên kiosk không người trực (khuyến nghị `RestartSec=10` + `StartLimitIntervalSec=0`), cập nhật MANUAL tương ứng.

Khuyến nghị gộp cùng commit fix (không bắt buộc): thêm `switch-to-application-1..9` (+ `switch-group`, `activate-window-menu`, `minimize`) vào dconf lock F09 (mục #8) và câu dè dặt "cần xác nhận tại máy" vào MANUAL (mục #10). Sau khi fix R1+R2 và Tech Lead re-review nhanh (≤15 phút) → chuyển QA verify; QA lưu ý test case ở mục #1 (extension ACTIVE sau reboot), #7 (app GUI thật chạy từ service) và thử tay gesture theo AUDIT §3.

— Tech Lead, 2026-07-27 00:49

---

## Re-review Tech Lead — F10 vòng 2 — 2026-07-27 01:00

Phạm vi: commit `2dbd68c` — đã đọc toàn bộ diff `2-configure-system.sh` + MANUAL + BUG + CODE-GRAPH, tự đối chiếu logic (không tin báo cáo SD nguyên trạng).

### Verdict: ✅ APPROVE — cho chuyển QA verify

| Điểm | Kết quả kiểm chứng |
|---|---|
| **R1 — ma trận `.desktop`** | ✅ ĐÚNG THỰC CHẤT. Đã tự dò 4 tổ hợp trên code mới mục [8/9]: (WD=1,AS=1) → rm app `.desktop`, tạo unclutter, [10] cài service; (WD=1,AS=0) → rm app `.desktop` + rm unclutter, cài service; (WD=0,AS=1) → tạo `.desktop` + unclutter, [10] gỡ service (nhánh else gỡ vô điều kiện nếu file unit tồn tại — đã có sẵn từ vòng 1); (WD=0,AS=0) → rm cả 2 `.desktop`, gỡ service. Không còn tổ hợp nào để lại file/service mồ côi khi deploy đè cấu hình cũ khác — cấu trúc `if WD=1 / elif AS=1 / else rm` đảm bảo trạng thái cuối chỉ phụ thuộc cấu hình VỪA chọn. Bonus đúng: `unclutter.desktop` giờ cũng 2 chiều (trước đây bỏ tick Autostart không xoá — mồ côi tiềm ẩn cũ). |
| **R2 — watchdog không chết vĩnh viễn** | ✅ ĐÚNG. Unit mới: `StartLimitIntervalSec=0` (tắt hẳn start rate-limit — theo systemd, interval=0 vô hiệu cơ chế StartLimit) + `Restart=always` + `RestartSec=10`, không còn `StartLimitBurst`. Không còn đường nào đưa service vào `failed` vĩnh viễn: crash-loop/binary thiếu → retry vô hạn mỗi 10s. Lập luận log hợp lệ: ~6 chu kỳ/phút « RateLimitBurst mặc định 10000/30s của journald; giữ log chẩn đoán (không null output) — đúng khuyến nghị phương án (a) của review vòng 1. |
| **R3 — khoá thêm key, đúng schema** | ✅ ĐÚNG SCHEMA. `switch-to-application-1..9` đặt trong `[org/gnome/shell/keybindings]` — đúng (schema `org.gnome.shell.keybindings`, khớp bằng chứng audit `gsettings writable org.gnome.shell.keybindings switch-to-application-4`). `switch-group(-backward)`, `activate-window-menu`, `minimize` đặt trong `[org/gnome/desktop/wm/keybindings]` — đúng. Đối chiếu từng key trong `settings` đều có dòng lock tương ứng trong `locks` (9 + 4 = 13 key, 13 lock, path khớp 1-1). |
| **Tài liệu khớp code** | ✅ KHỚP. MANUAL 9.3: gesture đổi "đã chặn" → "được cấu hình để chặn + cần nghiệm thu trực tiếp tại máy" (đúng Nit mục #10 vòng 1); watchdog mô tả "thử mãi mỗi 10 giây, không bao giờ tự bỏ cuộc" khớp unit mới; danh sách Super+1..9/Alt+`/Alt+Space/Super+H bổ sung khớp lock list. BUG report có mục Vòng 2 ghi rõ cả lưu ý "kết quả test 'dừng sau 5 lần' vòng 1 là hành vi unit CŨ" — trung thực. CODE-GRAPH cập nhật đúng tham số R1/R2/R3. Không chạm C# → không cần build lại: chấp nhận. |

**Ghi chú tồn dư (không block):** kiểm chứng vòng 2 chỉ bằng `bash -n` + suy luận logic — hành vi ma trận R1 và retry-vô-hạn R2 trên máy thật giao cho QA verify (danh sách dưới). Các nợ kỹ thuật đã chấp nhận ở review vòng 1 (bàn phím USB, USB automount-open, extension chết âm thầm khi nâng GNOME, user `kztek` trong sudo) giữ nguyên hiệu lực — không thay đổi.

### Danh sách case QA cần verify trên ZCU thật

| # | Case | Cách verify | Thử tay tại màn cảm ứng? |
|---|---|---|---|
| Q1 | R1 ma trận: deploy (AS=1,WD=0) → deploy lại (AS=0,WD=1) | Sau lần 2: `ls ~/.config/autostart/` KHÔNG còn `ipgs-kiosk.desktop`; `systemctl --user is-enabled ipgs-kiosk-app` = enabled; reboot → chỉ 1 instance app (`pgrep -c`) | Không (SSH đủ) |
| Q2 | R1 chiều ngược: deploy (WD=1) → deploy lại (WD=0,AS=1) | Service bị gỡ (`systemctl --user cat ipgs-kiosk-app` → lỗi), `.desktop` được tạo lại, unclutter còn | Không (SSH đủ) |
| Q3 | R2 retry vô hạn: đặt binary giả crash ngay (exit 1) | `systemctl --user status` KHÔNG bao giờ vào `failed`; `NRestarts` tăng đều nhịp ~10s; theo dõi ≥ 3 phút | Không (SSH đủ) |
| Q4 | R2 hồi phục: giữa lúc retry, cài binary đúng | Lần retry kế tiếp app chạy, không cần thao tác gì | Không (SSH đủ) |
| Q5 | App GUI THẬT chạy từ service (DISPLAY/XAUTHORITY) | Deploy app thật + reboot → cửa sổ app hiện trên màn hình (vòng 1 chỉ test binary giả không cần DISPLAY) | Không bắt buộc (nhìn qua Remote Desktop được) |
| Q6 | Extension ACTIVE sau reboot | `gnome-extensions info disable-overview-gestures@kztek` → `State: ACTIVE` | Không (SSH đủ) |
| Q7 | **Gesture cảm ứng: vuốt 3/4 ngón lên, vuốt từ mép, chạm-giữ (long-press)** — overview KHÔNG mở | Thao tác ngón tay trực tiếp trên màn cảm ứng, sau reboot | **✋ BẮT BUỘC thử tay** — không giả lập được qua SSH/VM |
| Q8 | **Chạm 1 ngón dùng app bình thường** (extension không phá touch input của app) | Thao tác trực tiếp trên app kiosk thật | **✋ BẮT BUỘC thử tay** |
| Q9 | R3 (nếu có bàn phím cắm thử): Super+1..9, Alt+`, Alt+Space, Super+H đều câm | Cắm bàn phím USB tạm, bấm thử sau reboot; hoặc `gsettings writable org.gnome.shell.keybindings switch-to-application-4` = false qua SSH | Không bắt buộc (SSH check writable đủ mức tối thiểu) |
| Q10 | Regression F09: SSH + ZcuAgent + Remote Desktop vẫn hoạt động sau deploy đầy đủ | Kết nối từ CCU như thường lệ | Không |

— Tech Lead, 2026-07-27 01:00
