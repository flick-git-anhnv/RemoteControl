# 00 — Documentation Writer: Screen Inventory + Checklist Screenshot (100% màn hình)

**Bước:** 0.2 — Documentation Writer | **Ngày:** 2026-07-26
**Plan:** `docs/plans/PLAN-user-manual-ccu-zcu-2026-07-26`
**Nguồn khung:** `_workspace/00_pm_docs-scope.md` (bảng 18 view + 3 mức chi tiết — KHÔNG khảo sát lại)
**Cơ sở kiểm kê:** đọc trực tiếp 18 file `.axaml` + code-behind liên quan (`ConnectionEntryWindow.axaml.cs`, `ComputerEditWindow.axaml.cs`, `LicenseWindow.axaml.cs`, grep luồng `new XxxWindow`).

> **Bối cảnh Phase 2 (user cung cấp):** ĐÃ CÓ thiết bị ZCU thật `192.168.1.4`, user `kztek` (credential tại `temp/user-manual-ccu-zcu/zcu-connection.md` — gitignore, KHÔNG chép vào file này). Vì vậy các trạng thái "cần ZCU thật" phần lớn **CHỤP ĐƯỢC**, chỉ giữ BLOCK/decision cho số ít trường hợp thực sự không thể tạo (mục 4).

---

## 1. Bảng Screen Inventory — 18 view

| # | View | Tên hiển thị trong tài liệu | Chức năng (1 dòng) | Mức | Cách mở từ UI (đã xác minh trong code) | Nhóm chụp |
|---|------|------------------------------|--------------------|-----|----------------------------------------|-----------|
| 1 | ConnectionEntryWindow (= MainWindow) | Màn hình chính — Danh sách máy tính | Quản lý danh sách máy, trạng thái SSH/Remote, điểm vào mọi chức năng | 1 | Tự mở khi khởi động app (`App.axaml.cs`) | 2.1 |
| 2 | ComputerEditWindow | Thông tin máy tính (Thêm/Sửa) | Nhập tên, IP, cổng, token, MAC, SSH của một máy | 1 | Màn hình chính → **+ Thêm máy tính** hoặc nút **Sửa** trên từng máy; NetworkScan → nút Thêm máy tìm thấy | 2.1 |
| 3 | NetworkScanWindow | Quét mạng tìm máy | Dò dải IP .1–.254, xác nhận HELLO thật, thêm máy tìm thấy | 2 | Màn hình chính → **🔍 Quét mạng...** | 2.1 |
| 4 | SessionPickerWindow | Chọn máy thêm vào Dashboard | Chọn 1/nhiều máy để thêm vào Multi-Remote | 3 | MultiRemoteWindow → **+ Thêm máy** | 2.1* |
| 5 | ConfirmDeleteDialog | Xác nhận xóa file/thư mục | Xác nhận trước khi xóa vĩnh viễn trên máy đích | 3 | FileManagerWindow → **🗑 Xóa**; RemoteCommandWindow tab SFTP → **🗑️ Xóa** | **2.2** (điều chỉnh — xem ghi chú) |
| 6 | RemoteScreenWindow | Điều khiển màn hình từ xa | Xem/điều khiển màn hình 1 máy, chat, clipboard, ghi hình, privacy | 1 | Màn hình chính → nút **Kết nối** trên máy (hoặc double-click, hoặc Kết nối nhanh) | 2.2 |
| 7 | RemoteScreenControl | Vùng hiển thị màn hình từ xa | UserControl hiển thị frame + nhận chuột/bàn phím (nằm TRONG RemoteScreenWindow) | 1 | Không mở riêng — là vùng giữa của RemoteScreenWindow | 2.2 (chung ảnh `remote-screen-streaming`) |
| 8 | MultiRemoteWindow | Multi-Remote Dashboard | Xem nhiều máy cùng lúc: lưới 2x2/3x3/tùy chỉnh/thẻ tab | 1 | Màn hình chính → **🚀 Remote nhiều máy (Grid View)...** (tự nạp mọi máy đã lưu) | 2.2 |
| 9 | FileManagerWindow | Quản lý File (SFTP) | Duyệt/upload/xóa/đồng bộ file hai chiều qua SFTP | 1 | Màn hình chính → nút **📁 Quản lý File** trên máy (cần SSH xanh) | 2.2 |
| 10 | RemoteCommandWindow | Quản lý File & Lệnh (SFTP/SSH) ⚙️ | 2 tab: Console chạy lệnh shell + Truyền nhận file SFTP | 2 | Màn hình chính → nút **>_ CMD Shell** trên máy (cần SSH xanh) | 2.2 |
| 11 | BulkActionWindow | Thực thi hàng loạt (Mass Deploy) ⚙️ | Chạy lệnh / upload file lên NHIỀU máy được tick chọn | 2 | Màn hình chính → tick checkbox ≥1 máy → thanh dưới → **🚀 Gửi lệnh / Upload File Hàng Loạt** | 2.3 |
| 12 | CronJobWindow | Quản lý Cron Jobs ⚙️ | Xem/thêm/xóa lịch chạy định kỳ trên máy đích | 2 | Màn hình chính → nút **⏰ Cron Jobs** trên máy (cần SSH xanh) | 2.3 |
| 13 | HealthMonitorWindow | Giám sát sức khỏe máy | CPU/RAM/Disk + top process, tự refresh | 2 | Màn hình chính → nút **📊 Giám sát** trên máy (cần SSH xanh) | 2.4 |
| 14 | SystemInventoryWindow | Thông tin cấu hình máy (SysInfo) | CPU, RAM, OS, Architecture của máy ZCU | 2 | RemoteScreenWindow (đang stream) → nút **📊 SysInfo** | **2.2** (điều chỉnh — cần phiên stream đang chạy) |
| 15 | ZcuSetupWizardWindow | Cài đặt Remote Agent từ xa ⚙️ | Cài ZcuAgent qua SSH: port/token/AllowedIPs/FPS/JPEG + log tiến trình | 1 | Màn hình chính → nút **⚡ Cài remote** trên máy (cần SSH xanh) | 2.3 |
| 16 | RemoteAppInstallWindow | Cài đặt phần mềm từ xa ⚙️ | Upload & cài .deb/.sh/.run, gỡ package trên máy đích | 2 | Màn hình chính → nút **📦 Cài App** trên máy (cần SSH xanh) | 2.3 |
| 17 | KioskDeployWindow | Triển khai chế độ Kiosk ⚙️ | Cấu hình OS kiosk từ xa: ẩn GNOME UI, autologin, autostart (2 tab) | 2 | Màn hình chính → nút **🖥️ Setup** trên máy (cần SSH xanh) | 2.3 |
| 18 | LicenseWindow | Kích hoạt bản quyền | Hiển thị Hardware ID, nhập License Key, kích hoạt | 2 | **KHÔNG có đường mở từ UI** — không nơi nào `new LicenseWindow()` (dead code chủ đích, khớp scope doc §6.4). Cần biện pháp riêng — xem mục 4 | 2.4 |

**Ghi chú điều chỉnh phân nhóm so với PLAN-MASTER (khảo sát thấy chưa hợp lý — DoD cho phép điều chỉnh):**
- `ConfirmDeleteDialog`: PLAN xếp nhóm 2.1 nhưng dialog CHỈ mở được từ FileManager/RemoteCommand (cần SFTP) → **chuyển sang chụp trong bước 2.2** cùng phiên FileManager.
- `SessionPickerWindow` (2.1*): mở từ MultiRemoteWindow — không cần ZCU, chụp được ở 2.1 bằng cách mở MultiRemote rồi bấm + Thêm máy; giữ nhóm 2.1 như PLAN.
- `SystemInventoryWindow`: PLAN xếp 2.4 nhưng CHỈ mở được từ RemoteScreenWindow khi đang stream → **chụp trong bước 2.2** (cùng phiên stream), tài liệu vẫn viết ở chương 10.2.
- "MainWindow" trong dòng 2.1 của PLAN ≡ ConnectionEntryWindow (đã chốt bước 0.1 — không tồn tại riêng).

---

## 2. Checklist screenshot chi tiết theo màn hình

Quy ước: tên file `[screen-slug]-[state].png`, lưu `docs/user-manuals/screenshots/`. Cột **ĐK tiên quyết**: `—` = không cần gì đặc biệt; `ZCU` = cần thiết bị 192.168.1.4 online; `SSH` = cần SSH tới ZCU; `DATA` = cần dữ liệu mẫu (mục 5).

### 2.1 — Nhóm Kết nối & Quản lý thiết bị (16 ảnh)

**ConnectionEntryWindow** — `connection-entry` (Mức 1)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `connection-entry-empty.png` | Danh sách trống + hướng dẫn "Nhấn '+ Thêm máy tính'" (chụp TRƯỚC khi thêm máy mẫu) | — (xóa/backup file profile store trước) |
| `connection-entry-default.png` | Danh sách 3 máy mẫu, máy P01 online (chấm SSH + Remote xanh, badge CPU/RAM/Disk), P02/P03 offline; banner vàng hint SSH | ZCU + DATA |
| `connection-entry-offline.png` | Cận cảnh máy offline: chấm đỏ, các nút Kết nối/Cài remote/Quản lý File... bị mờ (disabled) | DATA |
| `connection-entry-search.png` | Đã gõ từ khóa vào ô tìm kiếm, danh sách được lọc | DATA |
| `connection-entry-tab-recent.png` | Tab **Lịch sử gần đây** đang chọn, hiển thị máy kết nối gần nhất | DATA (đã kết nối ≥1 lần) |
| `connection-entry-bulk-selected.png` | Tick checkbox 2 máy → thanh đen dưới "Đã chọn 2 máy tính" + nút Gửi lệnh hàng loạt | DATA |
| `connection-entry-quick-filled.png` | Thanh "Kết nối nhanh" đã điền IP/cổng 17600/token (token che) | — |
| `connection-entry-wol-error-no-mac.png` | Dialog "Lỗi Wake-on-LAN" khi bấm ⚡ Bật nguồn máy chưa có MAC (P02) | DATA |
| `connection-entry-wol-success.png` | Dialog "Thành công — Đã gửi tín hiệu bật nguồn (Magic Packet)..." (máy P01 có MAC) | DATA (MAC thật của ZCU) |

> Không tồn tại trong code (KHÔNG chụp, KHÔNG bịa): hộp thoại xác nhận khi **Xóa** máy khỏi danh sách — code xóa ngay lập tức không hỏi (`OnItemDeleteClick`). Tài liệu sẽ ghi **⚠️ Cảnh báo: xóa không hỏi lại**. Kết nối nhanh bỏ trống IP → không làm gì, không có thông báo lỗi (return im lặng) — không có ảnh lỗi.

**ComputerEditWindow** — `computer-edit` (Mức 1)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `computer-edit-default.png` | Form trống khi bấm + Thêm máy tính (Cổng 17600, SSH port 22 mặc định) | — |
| `computer-edit-filled.png` | Form điền đủ máy P01 (IP 192.168.1.4, token che, khối SSH điền user `kztek`, mật khẩu dạng •••) | DATA |
| `computer-edit-edit-mode.png` | Mở bằng nút **Sửa** — dữ liệu có sẵn được nạp lại | DATA |

> Không tồn tại trong code: thông báo lỗi validation — `OnSaveClick` LUÔN lưu, kể cả host rỗng (cho phép lưu bộ phận, chỉ MAC). Tài liệu ghi Lưu ý thay vì ảnh lỗi.

**NetworkScanWindow** — `network-scan` (Mức 2)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `network-scan-default.png` | Trạng thái "Sẵn sàng quét", ô dải IP `192.168.1.`, cổng 17600, hint 💡 | — |
| `network-scan-scanning.png` | Progress bar đang chạy + status text tiến độ | — (quét dải bất kỳ) |
| `network-scan-results.png` | Tìm thấy máy ZCU: tên, IP 192.168.1.4, độ phân giải, nút Thêm | ZCU (agent đang chạy) |

**SessionPickerWindow** — `session-picker` (Mức 3)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `session-picker-default.png` | Danh sách máy, đã chọn (highlight) 1–2 máy, nút + Thêm vào Dashboard | DATA (mở MultiRemote rỗng → + Thêm máy) |

### 2.2 — Nhóm Remote & Điều khiển (28 ảnh)

**RemoteScreenWindow (+ RemoteScreenControl)** — `remote-screen` (Mức 1)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `remote-screen-connecting.png` | Status bar "đang kết nối" (transient — best-effort, chụp nhanh khi mở) | ZCU |
| `remote-screen-streaming.png` | Đang hiển thị màn hình ZCU thật (≡ ảnh của RemoteScreenControl), status chấm xanh, các nút Privacy/SysInfo/Record/Ngắt kết nối đã bật | ZCU |
| `remote-screen-faulted.png` | Banner đỏ lỗi kết nối (tạo bằng kết nối tới IP không tồn tại hoặc token sai) | — |
| `remote-screen-ssh-help.png` | Banner xanh "💡 Cài SSH (nếu máy chưa có)" + lệnh copy (hiện khi profile có SshReachable=false) | DATA (kết nối máy probe SSH fail) |
| `remote-screen-privacy-on.png` | Toggle 🕶️ Privacy đang bật (màn hình đích bị che) | ZCU |
| `remote-screen-record-on.png` | Toggle 🔴 Record đang bật (đang ghi AVI) | ZCU |
| `remote-screen-chat.png` | Đã gõ + gửi tin nhắn chat ở thanh dưới | ZCU |
| `remote-screen-clipboard-sync.png` | Sau khi bấm 📋 Sync Clipboard (best-effort nếu có phản hồi UI) | ZCU |
| `remote-screen-disconnected.png` | Sau khi bấm Ngắt kết nối — status đổi, nút mờ đi | ZCU |

**MultiRemoteWindow** — `multi-remote` (Mức 1)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `multi-remote-empty.png` | Trạng thái rỗng "Chưa có máy tính nào..." (mở khi danh sách máy trống hoặc sau Ngắt tất cả) | — |
| `multi-remote-grid-2x2.png` | Lưới 2x2 có phiên ZCU live (chỉ 1 ZCU thật → 1 ô live + ô trống — ghi chú thật trong caption) | ZCU + DATA |
| `multi-remote-custom-grid.png` | Nhập lưới tùy chỉnh (VD 1x2) + bấm Áp dụng | ZCU + DATA |
| `multi-remote-tab-view.png` | Chế độ **Thẻ Tab** với tab phiên đang mở | ZCU + DATA |

**FileManagerWindow** — `file-manager` (Mức 1)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `file-manager-default.png` | Danh sách file `/home/kztek`, DataGrid 4 cột, status "Sẵn sàng" | SSH |
| `file-manager-navigate.png` | Đã vào thư mục con `kztek-demo/` qua path bar/⬆ Lên 1 cấp | SSH + DATA |
| `file-manager-filter.png` | Ô "Lọc file" đã gõ, danh sách được lọc | SSH + DATA |
| `file-manager-upload-success.png` | Status bar sau khi ⬆ Upload File thành công (`demo-upload.txt`) | SSH + DATA |
| `file-manager-sync-result.png` | Kết quả 🔄 Đồng bộ thư mục local → remote (status bar) | SSH + DATA |
| `file-manager-after-delete.png` | Status bar sau khi xóa file (sau ConfirmDeleteDialog) | SSH + DATA |
| `file-manager-error-connect.png` | Mở Quản lý File với máy không kết nối được → thông báo lỗi ở status | DATA (máy offline P02 — nút disabled khi probe fail; tạo bằng cách probe xanh rồi rút mạng, hoặc chụp lỗi SFTP khi mất kết nối giữa chừng — best-effort) |

**ConfirmDeleteDialog** — `confirm-delete` (Mức 3 — chụp trong phiên FileManager)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `confirm-delete-default.png` | Dialog nền tối liệt kê file sắp xóa + 2 nút Hủy / 🗑 Xóa vĩnh viễn | SSH + DATA |
| `confirm-delete-dir-warning.png` | Có THƯ MỤC trong danh sách → dòng cảnh báo đỏ "xóa đệ quy (rm -rf)" hiện ra | SSH + DATA |

**RemoteCommandWindow** — `remote-command` (Mức 2)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `remote-command-console-default.png` | Tab Console: ô sudo password, ô lệnh, console trống | SSH |
| `remote-command-snippet.png` | AutoCompleteBox gợi ý lệnh mẫu đang mở (gõ "RAM") | SSH |
| `remote-command-console-output.png` | Kết quả lệnh vô hại (`uname -a` / `df -h`) trong Console Output | SSH |
| `remote-command-sftp-tab.png` | Tab 📁 Truyền nhận File: hint vàng + toolbar Upload/Download/Xóa + danh sách file | SSH |
| `remote-command-error.png` | Lỗi tiêu biểu ở status cam (lệnh sai / mất kết nối) | SSH |

**SystemInventoryWindow** — `system-inventory` (Mức 2 — chụp trong phiên stream 2.2)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `system-inventory-data.png` | CPU / Memory / OS / Architecture của ZCU (mở từ nút 📊 SysInfo khi đang stream) | ZCU (đang stream) |

### 2.3 — Nhóm Triển khai & Quản trị (20 ảnh)

**ZcuSetupWizardWindow** — `zcu-setup-wizard` (Mức 1)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `zcu-setup-wizard-default.png` | Form mặc định: target host hiển thị, cổng 17600, FPS 15, JPEG 70, AllowedIPs | SSH |
| `zcu-setup-wizard-token-generated.png` | Sau khi bấm 🎲 Sinh Token — ô token có giá trị (che 1 phần) | SSH |
| `zcu-setup-wizard-installing.png` | Progress bar + log console xanh đang chạy sau 🚀 Bắt đầu Cài đặt | SSH + **user OK chạy cài thật** (ghi đè agent hiện có trên ZCU) |
| `zcu-setup-wizard-success.png` | Log hoàn tất, progress 100%, trạng thái thành công | SSH + user OK |
| `zcu-setup-wizard-error.png` | Status đỏ lỗi tiêu biểu (SSH sai mật khẩu/host không tới được) | DATA (profile sai) |

**RemoteAppInstallWindow** — `remote-app-install` (Mức 2)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `remote-app-install-default.png` | Form mặc định: target host, ô sudo, ô chọn file, ô gỡ package | SSH |
| `remote-app-install-file-selected.png` | Đã Duyệt File... chọn 1 file `.deb` mẫu | DATA (file .deb vô hại, VD gói hello) |
| `remote-app-install-output.png` | Log console sau 🚀 Bắt đầu Cài đặt (cài gói .deb vô hại) | SSH + DATA |
| `remote-app-install-uninstall.png` | AutoComplete danh sách package mở (bấm ▼) hoặc log sau 🗑️ Gỡ ứng dụng (gỡ đúng gói vô hại vừa cài) | SSH + DATA |
| `remote-app-install-error.png` | Status cam lỗi tiêu biểu | DATA (profile sai) |

**KioskDeployWindow** — `kiosk-deploy` (Mức 2)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `kiosk-deploy-tab-computer.png` | Tab 🖥️ Config máy tính: 2 cột checkbox ẩn GNOME + hành vi máy, kiosk user | SSH |
| `kiosk-deploy-tab-software.png` | Tab ⚙️ Config phần mềm: App exec + 2 checkbox update/autostart | SSH |
| `kiosk-deploy-log.png` | Console log sau 🚀 Deploy | SSH + **user OK — Deploy thật SẼ THAY ĐỔI cấu hình GNOME máy ZCU** (xem mục 4) |
| `kiosk-deploy-error.png` | Status cam lỗi tiêu biểu (SSH fail) | DATA (profile sai) |

**BulkActionWindow** — `bulk-action` (Mức 2)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `bulk-action-default.png` | Form lệnh + danh sách máy đã tick (P01 online + P02 offline) chưa chạy | SSH + DATA |
| `bulk-action-running.png` | Progress panel hiện "Đang xử lý: n/N" | SSH + DATA |
| `bulk-action-results.png` | Kết quả: P01 success (icon xanh + output), P02 fail (icon đỏ) — minh họa cả 2 trạng thái trong 1 ảnh | SSH + DATA |

**CronJobWindow** — `cron-job` (Mức 2)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `cron-job-default.png` | Danh sách cron hiện có (có thể rỗng) + panel Thêm Job | SSH |
| `cron-job-added.png` | Sau ➕ Thêm Job: job mẫu xuất hiện trong DataGrid + status | SSH + DATA |
| `cron-job-after-delete.png` | Sau 🗑 Xóa mục chọn: job biến mất + status | SSH + DATA |

### 2.4 — Nhóm Giám sát, License & Terminal ZCU (15 ảnh)

**HealthMonitorWindow** — `health-monitor` (Mức 2)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `health-monitor-loading.png` | Trạng thái "--%" + "Đang kết nối..." lúc vừa mở | SSH |
| `health-monitor-data.png` | CPU/RAM/Disk có số liệu thật + bảng Top Process | SSH |

**LicenseWindow** — `license` (Mức 2 — cách mở đặc biệt, xem mục 4)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `license-default.png` | Hardware ID hiển thị (che 1 phần), ô nhập key trống | Harness dev mở window (mục 4) |
| `license-error-empty.png` | Bấm Kích hoạt khi trống → "Vui lòng nhập License Key." (đỏ) | Harness |
| `license-error-invalid.png` | Nhập key sai → thông báo lỗi đỏ từ LicenseManagerService | Harness |
| `license-success.png` | "Kích hoạt thành công! Ứng dụng sẽ khởi động lại..." (xanh) | Harness + **key hợp lệ ký bằng private key KeyGen** — ứng viên BLOCK (mục 4) |

**KeyGen (console — mục 5.3 tài liệu)**

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `keygen-console.png` | Output console KeyGen sinh cặp khóa RSA 2048 — **CHE TOÀN BỘ private key** (quyết định bước 0.1) | — (chạy local) |

**Terminal ZCU (Phần 1 — chương 3, 4, 5)** — chụp qua SSH tới 192.168.1.4 (che token/mật khẩu/IP công cộng)

| Ảnh | Cần chụp gì | ĐK tiên quyết |
|---|---|---|
| `zcu-terminal-x11-check.png` | `echo $XDG_SESSION_TYPE` trả về `x11` (chương 3.1) | ZCU |
| `zcu-terminal-ssh-install.png` | Chạy lệnh cài openssh-server (chương 3.2 — nếu đã cài thì output "already installed" cũng hợp lệ) | ZCU |
| `zcu-terminal-setup-script.png` | Chạy `setup-zcu-agent.sh` với 5 tham số (chương 4.2) | ZCU + user OK cài lại |
| `zcu-terminal-deb-install.png` | `sudo dpkg -i` / `apt install ./xxx.deb` gói ZcuAgent (chương 4.3) | ZCU + gói .deb build sẵn + user OK |
| `zcu-terminal-appsettings.png` | Nội dung `appsettings.json` (Port 17600, Token che, AllowedClientIPs) (chương 4.4) | ZCU |
| `zcu-terminal-systemctl-status.png` | `systemctl status` service ZcuAgent **active (running)** — BẮT BUỘC theo scope doc | ZCU |
| `zcu-terminal-journalctl.png` | `journalctl -u <service> -n 20` log gần nhất (chương 4.5.2) | ZCU |
| `zcu-terminal-ssh-keygen.png` | `ssh-keygen` + `ssh-copy-id` đưa public key lên ZCU (chương 5.1 — che fingerprint/key) | ZCU |

---

## 3. Tổng số ảnh dự kiến

| Nhóm | Màn hình | Số ảnh |
|---|---|---|
| 2.1 — Kết nối & Quản lý thiết bị | ConnectionEntry (9), ComputerEdit (3), NetworkScan (3), SessionPicker (1) | **16** |
| 2.2 — Remote & Điều khiển | RemoteScreen+Control (9), MultiRemote (4), FileManager (7), ConfirmDelete (2), RemoteCommand (5), SystemInventory (1) | **28** |
| 2.3 — Triển khai & Quản trị | ZcuSetupWizard (5), RemoteAppInstall (5), KioskDeploy (4), BulkAction (3), CronJob (3) | **20** |
| 2.4 — Giám sát, License & Terminal | HealthMonitor (2), License (4), KeyGen (1), Terminal ZCU (8) | **15** |
| **TỔNG** | 18/18 view + KeyGen + 8 ảnh terminal | **79** |

(Trong đó 3 ảnh gắn nhãn *best-effort* — transient/khó tái tạo: `remote-screen-connecting`, `remote-screen-clipboard-sync`, `file-manager-error-connect` — thiếu chúng KHÔNG tính là thiếu coverage, chỉ ghi chú trong tài liệu.)

---

## 4. Trạng thái KHÔNG chụp được / cần quyết định trước Phase 2

| # | Trạng thái | Lý do | Đề xuất xử lý |
|---|---|---|---|
| 1 | `license-success.png` | Cần License Key hợp lệ ký bằng **private key** tương ứng public key nhúng trong app. KeyGen chỉ sinh cặp khóa mới — chưa xác minh có tool ký license theo HardwareId trong repo | Bước 2.4 kiểm tra `LicenseManagerService` + KeyGen: nếu sinh được key hợp lệ → chụp; nếu không → 🛑 BLOCK ảnh này, tài liệu mô tả text + ảnh error-invalid |
| 2 | Cách mở LicenseWindow | KHÔNG có entry point trong UI (`new LicenseWindow()` không được gọi ở đâu — dead code chủ đích, license không enforce) | Dùng harness dev tạm (project console/test nhỏ trong `temp/` mở window, hoặc sửa code cục bộ KHÔNG commit) — cần Dispatcher/user xác nhận cách làm ở bước 2.4 |
| 3 | `kiosk-deploy-log.png` | Bấm Deploy thật sẽ **thay đổi cấu hình GNOME/autologin máy ZCU thật** (ẩn Top Bar, autologin, autostart...) — có thể ảnh hưởng thiết bị người dùng đang dùng | Hỏi user xác nhận trước khi Deploy thật ở bước 2.3; nếu không cho phép → 🛑 BLOCK ảnh log, vẫn có 3 ảnh còn lại của màn hình |
| 4 | `zcu-setup-wizard-installing/success`, `zcu-terminal-setup-script`, `zcu-terminal-deb-install` | Chạy cài thật sẽ ghi đè/khởi động lại agent đang chạy trên ZCU (mất kết nối stream tạm thời, đổi token nếu sinh mới) | Hỏi user xác nhận ở bước 2.3; khi cài giữ nguyên Token/Port hiện tại để không phá kết nối; gói .deb cần build trước bằng `build-deb.sh` (cần xác nhận build được trên môi trường hiện có) |
| 5 | Lỗi phần cứng thật (mất điện, hỏng card mạng...) | Không thể tạo chủ đích | Không chụp — tài liệu FAQ mô tả text |

---

## 5. Kịch bản dữ liệu mẫu (chuẩn bị TRƯỚC khi chụp — dùng nhất quán toàn tài liệu)

### 5.1 Danh sách máy trong app (3 máy)

| Tên hiển thị | IP | Cổng | SSH | MAC | Vai trò trong ảnh |
|---|---|---|---|---|---|
| `Máy khách Trạm P01` | `192.168.1.4` (ZCU thật) | 17600 | user `kztek` (mật khẩu lấy từ `temp/user-manual-ccu-zcu/zcu-connection.md` — KHÔNG ghi ra tài liệu/ảnh) | MAC thật của ZCU (lấy bằng `ip link` — dùng cho `wol-success`) | Máy online chính — mọi ảnh live |
| `Máy khách Trạm P02` | `192.168.1.250` (không tồn tại) | 17600 | không điền | **bỏ trống** (dùng cho `wol-error-no-mac`) | Máy offline — ảnh disabled/lỗi/bulk-fail |
| `Máy khách Trạm P03` | `192.168.1.251` (không tồn tại) | 17600 | không điền | bỏ trống | Máy thứ 3 cho ảnh search/bulk-selected |

- Token nhập giá trị demo, khi chụp **che** hoặc hiển thị dạng `demo-****`.
- Backup file profile store hiện có (nếu tồn tại) trước khi tạo dữ liệu mẫu; chụp `connection-entry-empty` TRƯỚC khi thêm 3 máy.

### 5.2 Dữ liệu trên máy ZCU (tạo qua SSH trước bước 2.2)

- Thư mục demo: `/home/kztek/kztek-demo/` chứa vài file (`bao-cao-thang.txt`, `config-mau.json`, thư mục con `logs/`) — phục vụ FileManager duyệt/lọc/xóa/dir-warning.
- File upload từ máy CCU: `demo-upload.txt` (tạo trên Desktop máy Windows).
- Cron mẫu: phút `0`, giờ `3`, còn lại `*`, lệnh `/home/kztek/kztek-demo/backup.sh` (tạo file script rỗng chmod +x để lệnh có thật).
- Lệnh demo cho Console/Bulk: `uname -a`, `df -h` (vô hại, output ngắn đẹp).
- Gói .deb vô hại cho RemoteAppInstall: `hello_*.deb` (tải bằng `apt download hello` trên ZCU rồi copy về máy CCU, hoặc dùng gói KZTEK build sẵn).

### 5.3 Quy ước che thông tin (nhắc lại từ scope doc §5.6)

Che trước khi lưu ảnh: mật khẩu SSH (ô • đã tự che), Token thật, private key KeyGen (che TOÀN BỘ), Hardware ID (che 1 phần), MAC thật (che 3 octet cuối nếu xuất hiện rõ), fingerprint SSH.

---

## 6. Handoff cho bước 1.1 (Build & Run)

- Build: `dotnet build IPGS.RemoteControl.CcuUI -c Release` — KHÔNG build 3 project song song (tranh chấp `obj/` — CODE-GRAPH).
- Chạy exe từ `bin/Release/net8.0/` — app mở thẳng ConnectionEntryWindow (không có splash/login/license gate).
- Trước khi chụp 2.1: backup profile store, chuẩn bị dữ liệu mẫu mục 5.1.
