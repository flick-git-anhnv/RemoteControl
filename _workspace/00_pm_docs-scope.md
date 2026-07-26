# 00 — PM Scope: Tài liệu HDSD toàn hệ thống CCU + ZCU (Remote Control Tool)

**Bước:** 0.1 — Product Manager | **Ngày:** 2026-07-26
**Plan:** `docs/plans/PLAN-user-manual-ccu-zcu-2026-07-26`
**Output cuối:** 1 file duy nhất `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` (+ `.docx` + `.pdf` + `screenshots/`) — Phần 1 = triển khai ZCU, Phần 2 = thao tác CCU (quyết định user đã chốt, KHÔNG tách file).

> **Ghi chú kiểm kê quan trọng:** `MainWindow` của app **chính là** `ConnectionEntryWindow` (`App.axaml.cs`: `desktop.MainWindow = new ConnectionEntryWindow()`). Không tồn tại file `MainWindow.axaml` riêng. Vậy "19 màn hình" trong plan = **18 view duy nhất** trong `IPGS.RemoteControl.CcuUI/Views/` (ConnectionEntryWindow đóng cả 2 vai). Mục lục dưới đây phủ 100% cả 18 view.

---

## 1. Đối tượng đọc

Tài liệu phục vụ **2 nhóm người đọc tách biệt** — mỗi nhóm đọc phần tương ứng, có chỉ dẫn điều hướng ngay ở chương Giới thiệu ("Bạn là ai → đọc phần nào"):

### Nhóm A — Kỹ thuật viên triển khai (đọc Phần 1 + chương License/KeyGen)
- **Là ai:** Nhân viên kỹ thuật KZTEK hoặc đối tác lắp đặt, chịu trách nhiệm cài ZcuAgent lên máy Linux (ZCU) tại hiện trường và cấp phát bản quyền.
- **Giả định trình độ:**
  - Biết thao tác terminal Linux cơ bản (chạy lệnh `sudo`, copy file, `systemctl`).
  - Biết khái niệm SSH, IP tĩnh/động; KHÔNG cần biết lập trình.
  - Chưa từng biết kiến trúc nội bộ của hệ thống → mọi lệnh phải cho ở dạng copy-paste được, có giải thích từng tham số.
- **Cần từ tài liệu:** checklist chuẩn bị máy ZCU (X11, SSH server), 3 cách cài ZcuAgent, cấu hình Token/AllowedIPs, kiểm tra service chạy, xử lý sự cố thường gặp, quy trình sinh khóa (KeyGen) + kích hoạt license.

### Nhóm B — Người vận hành hằng ngày (đọc Phần 2)
- **Là ai:** Nhân viên vận hành/giám sát tại trung tâm CCU, dùng app desktop `IPGS.RemoteControl.CcuUI` trên Windows để theo dõi và điều khiển các máy ZCU.
- **Giả định trình độ:**
  - Dùng máy tính văn phòng thành thạo (chuột, bàn phím, cửa sổ ứng dụng).
  - KHÔNG biết Linux, KHÔNG biết thuật ngữ lập trình/mạng nâng cao.
  - Một số chức năng nâng cao (Chạy lệnh từ xa, Cron, Kiosk Deploy) người vận hành chỉ dùng khi được kỹ thuật viên hướng dẫn — tài liệu vẫn mô tả đủ nhưng gắn nhãn "⚙️ Chức năng nâng cao — cân nhắc trước khi dùng".
- **Cần từ tài liệu:** hướng dẫn từng bước có ảnh cho mọi thao tác thường nhật: thêm máy, kết nối, xem/điều khiển màn hình, gửi file, giám sát sức khỏe.

---

## 2. Scope IN / OUT

### IN — có trong tài liệu
| # | Nội dung | Ghi chú |
|---|---|---|
| 1 | Chuẩn bị máy ZCU: Ubuntu 22.04, session X11 (Xorg — không Wayland), cài/bật `openssh-server` | Theo yêu cầu thực tế trong `scripts/setup-zcu-agent.sh` + hint trong ConnectionEntryWindow |
| 2 | Cài đặt ZcuAgent đủ 3 cách: (a) từ xa qua app CCU (ZcuSetupWizard — khuyến nghị), (b) script `setup-zcu-agent.sh`, (c) gói `.deb` (`scripts/linux-deb/build-deb.sh`) | Phần 1 |
| 3 | Cấu hình `appsettings.json`: Port (mặc định 17600), Token (bắt buộc đổi — fail-fast), AllowedClientIPs, EnableDesktopIntegration; dịch vụ systemd (start/stop/status/log) | Phần 1 |
| 4 | Khóa & bảo mật: khóa SSH, Token ZcuAgent, **KeyGen** (sinh cặp khóa RSA 2048 public/private phục vụ license) — **user đã chốt: IN** | Phần 1 |
| 5 | **LicenseWindow** — nhập/kích hoạt bản quyền trên app CCU — **user đã chốt: IN** | Phần 2 |
| 6 | Thao tác 100% 18 màn hình app CcuUI (danh sách đầy đủ ở mục lục bên dưới) | Phần 2 |
| 7 | FAQ + xử lý sự cố thường gặp (cả 2 phần) + Liên hệ hỗ trợ KZTEK | Chương cuối |

### OUT — không có trong tài liệu (và lý do)
| # | Nội dung | Lý do loại |
|---|---|---|
| 1 | `IPGS.RemoteControl.CcuClient` | Library nội bộ, người dùng không tương tác trực tiếp |
| 2 | Kiến trúc code, protocol TCP, API/interface, sơ đồ class | Thuộc TDD/CODE-GRAPH, không phải HDSD người dùng cuối |
| 3 | Hướng dẫn build/compile source (dotnet build/publish của dev) | Người đọc nhận bản phát hành sẵn; chỉ nói cách CÀI, không nói cách BUILD từ source (riêng `build-deb.sh` chỉ nhắc ở mức "gói .deb do KZTEK cung cấp/tạo bằng script này") |
| 4 | Quản trị hạ tầng chung: cài Ubuntu, cấu hình router/firewall doanh nghiệp | Ngoài phạm vi sản phẩm; chỉ nêu cổng cần mở (17600, SSH 22) |
| 5 | Nội dung bộ script kiosk chi tiết (`scripts/linux-kiosk/*` từng dòng) | Chỉ hướng dẫn thao tác qua màn hình KioskDeployWindow; nội bộ script là chi tiết kỹ thuật |
| 6 | Chi tiết thuật toán license/nơi lưu private key nội bộ KZTEK | Nhạy cảm bảo mật; chỉ mô tả QUY TRÌNH sinh khóa + kích hoạt, không in private key thật vào tài liệu |

---

## 3. Mục lục dự kiến của MANUAL (đánh số tới cấp 3)

> Khớp khuôn documentation-writer §Bước 3: Giới thiệu → Yêu cầu hệ thống → Hướng dẫn từng bước → FAQ → Liên hệ hỗ trợ. Mỗi mục màn hình theo cấu trúc con chuẩn: *Mục đích → Giao diện khi mở (ảnh) → Các bước thực hiện (ảnh từng bước) → Kết quả mong đợi → Trường hợp lỗi → Lưu ý*.

```
# Hệ Thống Remote Control CCU–ZCU — Hướng Dẫn Sử Dụng

1. Giới thiệu
   1.1  Hệ thống Remote Control CCU–ZCU là gì
   1.2  Tài liệu này dành cho ai — bạn nên đọc phần nào
   1.3  Thuật ngữ & quy ước (CCU, ZCU, ZcuAgent, Token, SSH...)
   1.4  Sơ đồ tổng quan hoạt động (CCU điều khiển nhiều ZCU qua mạng LAN)

2. Yêu cầu hệ thống
   2.1  Máy CCU (Windows — app IPGS.RemoteControl.CcuUI)
   2.2  Máy ZCU (Ubuntu 22.04, phiên làm việc X11/Xorg)
   2.3  Mạng & cổng kết nối (cổng agent mặc định 17600, SSH 22)

════════ PHẦN 1 — TRIỂN KHAI ZCU (dành cho Kỹ thuật viên) ════════

3. Chuẩn bị máy ZCU trước khi cài đặt
   3.1  Kiểm tra và chuyển phiên làm việc sang X11 (Ubuntu on Xorg)
   3.2  Cài đặt & bật SSH server (openssh-server)
   3.3  Kiểm tra kết nối mạng giữa CCU và ZCU

4. Cài đặt ZcuAgent
   4.1  Cách 1 (khuyến nghị): Cài từ xa bằng app CCU — trình hướng dẫn "Setup ZCU"
        (tham chiếu chi tiết thao tác màn hình tại mục 12.1)
   4.2  Cách 2: Chạy script setup-zcu-agent.sh trực tiếp trên máy ZCU
        4.2.1  Cú pháp & ý nghĩa 5 tham số (PORT, TOKEN, ALLOWED_IPS, TARGET_FPS, JPEG_QUALITY)
        4.2.2  Quá trình script tự thực hiện (thư viện X11, .NET 8 Runtime, service)
   4.3  Cách 3: Cài bằng gói .deb
        4.3.1  Nguồn gói .deb (KZTEK cung cấp / tạo bằng scripts/linux-deb/build-deb.sh)
        4.3.2  Cài đặt, nâng cấp, gỡ bỏ gói
   4.4  Cấu hình ZcuAgent (appsettings.json)
        4.4.1  Port — cổng lắng nghe
        4.4.2  Token — mã bảo mật bắt buộc (agent từ chối chạy nếu để mặc định)
        4.4.3  AllowedClientIPs — danh sách IP được phép kết nối (khuyến cáo KHÔNG để 0.0.0.0/0)
        4.4.4  EnableDesktopIntegration — bật/tắt thông báo & clipboard trên ZCU
   4.5  Quản lý dịch vụ systemd
        4.5.1  Khởi động / dừng / khởi động lại service
        4.5.2  Xem trạng thái & log (systemctl status, journalctl)
        4.5.3  Bật tự chạy khi khởi động máy
   4.6  Kiểm tra sau cài đặt & xử lý sự cố cài đặt thường gặp

5. Khóa & bảo mật hệ thống
   5.1  Khóa SSH: tạo cặp khóa, đưa public key lên ZCU, dùng thay mật khẩu
   5.2  Token ZcuAgent: sinh, đổi, lưu trữ an toàn
   5.3  Sinh cặp khóa bản quyền bằng công cụ KeyGen (RSA 2048 — quy trình nội bộ KZTEK)
   5.4  Quy trình cấp & kích hoạt bản quyền cho máy CCU (thao tác chi tiết tại mục 14.1)

════════ PHẦN 2 — SỬ DỤNG APP CCU (dành cho Người vận hành) ════════

6. Bắt đầu với ứng dụng CCU
   6.1  Khởi động ứng dụng
   6.2  Màn hình chính — Danh sách máy tính (ConnectionEntryWindow)
        6.2.1  Bố cục màn hình: thanh tiêu đề, tìm kiếm/lọc, danh sách máy
        6.2.2  Ý nghĩa trạng thái máy (online/offline/đang kiểm tra)
        6.2.3  Các nút thao tác trên từng máy & trên thanh công cụ

7. Quản lý danh sách máy tính
   7.1  Thêm máy tính mới / Sửa thông tin máy (ComputerEditWindow)
        7.1.1  Các trường thông tin: tên, IP, cổng, token, tài khoản SSH
        7.1.2  Lưu & kiểm tra kết nối
   7.2  Quét mạng tìm máy ZCU tự động (NetworkScanWindow)
   7.3  Xóa máy khỏi danh sách — hộp thoại xác nhận (ConfirmDeleteDialog)

8. Điều khiển màn hình từ xa
   8.1  Kết nối và xem màn hình một máy (RemoteScreenWindow)
        8.1.1  Kết nối / ngắt kết nối
        8.1.2  Vùng hiển thị & điều khiển chuột, bàn phím (RemoteScreenControl)
        8.1.3  Chat với người dùng máy ZCU
        8.1.4  Đồng bộ clipboard hai chiều
        8.1.5  Ghi hình phiên làm việc (file AVI)
   8.2  Điều khiển nhiều máy cùng lúc — Grid View (MultiRemoteWindow)
        8.2.1  Thêm phiên vào lưới — hộp thoại chọn máy (SessionPickerWindow)
        8.2.2  Chuyển đổi giữa các phiên, đóng phiên

9. Quản lý file & thực thi lệnh từ xa
   9.1  Trình quản lý file hai chiều qua SFTP (FileManagerWindow)
        9.1.1  Duyệt thư mục, tải lên / tải xuống
        9.1.2  Xóa file/thư mục — xác nhận trước khi xóa
   9.2  Chạy lệnh từ xa qua SSH (RemoteCommandWindow)  ⚙️ nâng cao
   9.3  Thao tác hàng loạt trên nhiều máy (BulkActionWindow)  ⚙️ nâng cao
   9.4  Lập lịch công việc định kỳ — Cron (CronJobWindow)  ⚙️ nâng cao

10. Giám sát hệ thống
   10.1  Theo dõi sức khỏe các máy (HealthMonitorWindow)
   10.2  Xem thông tin cấu hình phần cứng/phần mềm máy ZCU (SystemInventoryWindow)

11. Đánh thức & kiểm tra máy từ xa
   11.1  Wake-on-LAN — bật máy ZCU từ xa (thao tác từ màn hình chính)
   11.2  Kiểm tra trạng thái kết nối (ping/probe tự động)

12. Triển khai & cài đặt từ xa  ⚙️ nâng cao (Kỹ thuật viên)
   12.1  Trình hướng dẫn cài ZcuAgent từ xa (ZcuSetupWizardWindow)
   12.2  Cài ứng dụng .deb/.sh lên máy ZCU từ xa (RemoteAppInstallWindow)
   12.3  Triển khai chế độ Kiosk (KioskDeployWindow)

13. (đã gộp vào 12 — giữ chỗ đánh số nếu cần tách khi viết)

14. Bản quyền phần mềm
   14.1  Nhập & kích hoạt bản quyền (LicenseWindow)
   14.2  Câu hỏi khi bản quyền hết hạn / không hợp lệ

15. Câu hỏi thường gặp (FAQ)
   15.1  FAQ triển khai ZCU (không kết nối được, service không chạy, Wayland...)
   15.2  FAQ vận hành CCU (màn hình đen, giật/lag, mất kết nối giữa chừng...)

16. Liên hệ hỗ trợ
   — Email sales@kztek.net | Hotline 0988 637 099 | ĐT 0243 99 88 033 | kztek.net
```

**Đối chiếu phủ 100% màn hình (18 view — MainWindow ≡ ConnectionEntryWindow):**

| # | View | Mục trong mục lục |
|---|---|---|
| 1 | ConnectionEntryWindow (= MainWindow) | 6.2 |
| 2 | ComputerEditWindow | 7.1 |
| 3 | NetworkScanWindow | 7.2 |
| 4 | ConfirmDeleteDialog | 7.3 (và 9.1.2) |
| 5 | RemoteScreenWindow | 8.1 |
| 6 | RemoteScreenControl | 8.1.2 |
| 7 | MultiRemoteWindow | 8.2 |
| 8 | SessionPickerWindow | 8.2.1 |
| 9 | FileManagerWindow | 9.1 |
| 10 | RemoteCommandWindow | 9.2 |
| 11 | BulkActionWindow | 9.3 |
| 12 | CronJobWindow | 9.4 |
| 13 | HealthMonitorWindow | 10.1 |
| 14 | SystemInventoryWindow | 10.2 |
| 15 | ZcuSetupWizardWindow | 12.1 (tham chiếu từ 4.1) |
| 16 | RemoteAppInstallWindow | 12.2 |
| 17 | KioskDeployWindow | 12.3 |
| 18 | LicenseWindow | 14.1 (tham chiếu từ 5.4) |

---

## 4. Mức chi tiết yêu cầu theo nhóm màn hình

| Mức | Yêu cầu | Màn hình áp dụng |
|---|---|---|
| **Mức 1 — Đầy đủ nhất** (từng bước + ảnh cho MỌI trạng thái: default, filled, từng button, success, error, dialog con) | Đây là các flow thường nhật/quan trọng nhất của người vận hành hoặc điểm vào của kỹ thuật viên | ConnectionEntryWindow, ComputerEditWindow, RemoteScreenWindow (+RemoteScreenControl), MultiRemoteWindow, FileManagerWindow, ZcuSetupWizardWindow |
| **Mức 2 — Chuẩn** (từng bước + ảnh default, ảnh sau thao tác chính, ảnh success/error tiêu biểu — không cần ảnh từng button phụ) | Chức năng dùng có chủ đích, tần suất thấp hơn | NetworkScanWindow, RemoteCommandWindow, BulkActionWindow, CronJobWindow, HealthMonitorWindow, SystemInventoryWindow, RemoteAppInstallWindow, KioskDeployWindow, LicenseWindow |
| **Mức 3 — Ngắn gọn** (1 ảnh + 2–4 câu mô tả, vì màn hình chỉ có 1 quyết định) | Dialog đơn giản | ConfirmDeleteDialog, SessionPickerWindow |
| **Phần 1 ZCU (không phải màn hình app)** | Mỗi bước lệnh terminal kèm khối lệnh copy-paste + ảnh chụp terminal kết quả (khi có thiết bị thật ở Phase 2); ảnh `systemctl status` chạy OK là BẮT BUỘC | Chương 3, 4, 5 |
| **KeyGen** | Mô tả quy trình + 1 ảnh console output (che/giả lập phần private key — KHÔNG in khóa thật) | Mục 5.3 |

---

## 5. Giọng văn & quy ước

1. **Ngôn ngữ:** Tiếng Việt toàn bộ (user đã chốt). Giữ nguyên tên riêng kỹ thuật không dịch: SSH, Token, systemd, Wake-on-LAN, X11...
2. **Viết cho người dùng cuối** — theo bảng cấm/nên của documentation-writer: "Nhấp vào ô nhập liệu" (không "click component Input"); "Hệ thống sẽ tự cập nhật" (không "trigger API"); lỗi diễn giải bằng hành động khắc phục, không in exception.
3. **Cấu trúc bước:** mỗi bước bắt đầu bằng động từ hành động (Nhấp, Chọn, Gõ, Chạy lệnh...); ≤ 7 bước/nhóm; mỗi section có "Kết quả mong đợi".
4. **Tên nút/menu** in đậm đúng nguyên văn trên giao diện (VD: nhấn **+ Thêm máy tính**, **🔍 Quét mạng...**).
5. **Quy ước screenshot:** tên file `[screen-slug]-[state].png` (VD: `connection-entry-default.png`, `computer-edit-error-required.png`, `zcu-terminal-systemctl-status.png`); lưu tại `docs/user-manuals/screenshots/`; mỗi ảnh chèn NGAY dưới đoạn mô tả tương ứng, kèm caption nghiêng `*Hình X: [mô tả]*` đánh số liên tục toàn tài liệu; alt text ngắn trong `[]`.
6. **Che thông tin nhạy cảm** trước khi chụp: mật khẩu SSH, Token thật, private key, IP công cộng thật (dùng IP LAN mẫu 192.168.x.x).
7. **Nhãn cảnh báo thống nhất:** `> **Lưu ý:**` (thông tin), `> ⚠️ **Cảnh báo:**` (rủi ro mất dữ liệu/bảo mật), `⚙️ nâng cao` (gắn ở tiêu đề mục dành cho kỹ thuật viên).

---

## 6. Rủi ro / điểm cần lưu ý cho documentation-writer

1. **Trạng thái cần thiết bị ZCU thật/SSH đang chạy** (user xác nhận sẽ có ở Phase 2; Dispatcher hỏi IP/tài khoản SSH trước bước 2.1): RemoteScreen đang stream + chat + clipboard + ghi hình; MultiRemote có ≥ 2 phiên; FileManager duyệt/upload/download; RemoteCommand/BulkAction có output thật; HealthMonitor & SystemInventory có dữ liệu; ZcuSetupWizard/RemoteAppInstall/KioskDeploy chạy tiến trình thật; toàn bộ ảnh terminal Phần 1. **Không có thiết bị → BLOCK bước đó, KHÔNG bịa ảnh** (đúng plan).
2. **Trạng thái chụp được KHÔNG cần ZCU:** mọi màn hình mở ở trạng thái default/empty, form validation lỗi, ConfirmDeleteDialog, SessionPicker, LicenseWindow, NetworkScan (quét LAN không có ZCU → kết quả rỗng cũng là 1 trạng thái đáng chụp).
3. **App là Avalonia, KHÔNG phải WinForms** — bước "build Release bắt buộc" của documentation-writer vẫn áp dụng tinh thần (chụp từ bản Release), nhưng lệnh build là `dotnet build -c Release` cho CcuUI; KHÔNG build 3 project song song (CODE-GRAPH: tranh chấp `obj/`).
4. **LicenseWindow:** license hiện KHÔNG enforce (dead code có chủ đích — quyết định user, xem CODE-GRAPH). Tài liệu mô tả thao tác nhập/kích hoạt như thiết kế, KHÔNG hứa hẹn hành vi khóa app khi hết hạn; TUYỆT ĐỐI không nhắc chuỗi backdoor trong tài liệu.
5. **KeyGen là console app** (in public+private key ra console) — ảnh chụp phải che private key.
6. **Hint SSH trên màn hình chính** (banner vàng): là nội dung tốt để trích vào mục 3.2; lệnh chuẩn: `sudo apt update && sudo apt install -y openssh-server && sudo systemctl enable --now ssh`.
7. **Cổng mặc định:** script `setup-zcu-agent.sh` dùng 17600, còn `appsettings.json` mẫu ghi 5900 — khi viết chương 4 phải nói rõ giá trị thực tế do installer ghi đè (17600), tránh mâu thuẫn. Xác minh lại con số khi chụp ở Phase 2.
8. **Đọc bắt buộc trước khi viết:** `.claude/commands/kztek-brand-info.md`; file này (`_workspace/00_pm_docs-scope.md`); Screen Inventory bước 0.2.
