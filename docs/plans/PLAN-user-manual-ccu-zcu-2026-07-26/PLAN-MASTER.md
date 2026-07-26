---
task: user-manual-ccu-zcu
created: 2026-07-26
updated: 2026-07-26 18:06
status: planning
workflow: WF-DOCS
priority: P2
---

# PLAN MASTER: Tài liệu Hướng dẫn sử dụng (HDSD) toàn hệ thống CCU + ZCU — RemoteControlTool

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước (mô tả đầy đủ, Handoff Log, artifact chi tiết) nằm ở `steps/STEP-[N.M]-[tên].md` tương ứng — xem cột "Step file" bên dưới.

## Mô tả
Viết tài liệu HDSD cho TOÀN HỆ THỐNG remote control gồm 2 phần:
- **(a) ZCU:** triển khai/cài đặt ZcuAgent trên máy Linux — gói `.deb`, systemd service, khóa SSH.
- **(b) CCU:** thao tác người dùng trên app desktop `IPGS.RemoteControl.CcuUI` (Avalonia 12.1.0, net8.0, 18 cửa sổ/dialog + màn hình chính).

Screenshot BẮT BUỘC chụp từ app chạy thật (build Release trên máy này) — TUYỆT ĐỐI không dùng ảnh giả/placeholder. Trạng thái cần thiết bị ZCU thật/SSH mà không có → BLOCK bước đó, báo user, KHÔNG bịa ảnh.

## Nguồn yêu cầu
- Yêu cầu gốc: User yêu cầu viết tài liệu HDSD cho toàn hệ thống CCU + ZCU (đã chốt phạm vi: cả triển khai ZcuAgent Linux lẫn thao tác app CcuUI; screenshot từ app thật).
- Workflow: WF-DOCS — Tài liệu hướng dẫn sử dụng
- Agent chain: PRODUCT MANAGER (scope) → DOCUMENTATION WRITER (inventory → build/run → screenshot → viết → xuất DOCX/PDF)

## Phases & Steps

> **Session isolation (CLAUDE.md §16.5):** Mỗi bước ⬜/🔄 PHẢI chạy tách session — LOCAL dùng `Agent` subagent. Agent tự cập nhật step file riêng, commit+push, rồi cập nhật đúng 1 dòng status ở bảng dưới đây.
> **Agent thực thi chính:** `documentation-writer` — PHẢI đọc `.claude/agents/documentation-writer.md` + `.claude/commands/kztek-brand-info.md` trước khi tạo file.

### Phase 0: Chốt scope & kiểm kê màn hình
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 0.1 | PM chốt scope tài liệu + đối tượng đọc + cấu trúc mục lục HDSD | product-manager | ✅ | `steps/STEP-0.1-pm-scope.md` | 2026-07-26 16:29 |
| 0.2 | Kiểm kê 100% màn hình CcuUI (Screen Inventory) + checklist screenshot + phân nhóm chụp | documentation-writer | ✅ | `steps/STEP-0.2-screen-inventory.md` | 2026-07-26 16:39 |

### Phase 1: Build & chạy app thật
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Build Release `IPGS.RemoteControl.CcuUI`, khởi động app, xác nhận chạy được (fail → BLOCK, báo user) | documentation-writer | ✅ | `steps/STEP-1.1-build-run-ccuui.md` | 2026-07-26 16:44 |

### Phase 2: Chụp screenshot theo nhóm màn hình (app thật)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Nhóm Kết nối & Quản lý thiết bị: MainWindow, ConnectionEntryWindow, ComputerEditWindow, NetworkScanWindow, SessionPickerWindow, ConfirmDeleteDialog | documentation-writer | ✅ | `steps/STEP-2.1-shots-connection.md` | 2026-07-26 17:28 |
| 2.2 | Nhóm Remote & Điều khiển: RemoteScreenWindow, RemoteScreenControl, MultiRemoteWindow, RemoteCommandWindow, FileManagerWindow | documentation-writer | 🛑 | `steps/STEP-2.2-shots-remote.md` | - (4/28 ảnh — chờ ZCU online) |
| 2.3 (HOÃN) | Nhóm Triển khai & Quản trị: ZcuSetupWizardWindow, KioskDeployWindow, RemoteAppInstallWindow, BulkActionWindow, CronJobWindow | documentation-writer | ⏭️ | `steps/STEP-2.3-shots-deploy.md` | - |
| 2.4 (HOÃN) | Nhóm Giám sát & Hệ thống: HealthMonitorWindow, SystemInventoryWindow, LicenseWindow (+ ảnh terminal ZCU nếu có thiết bị) | documentation-writer | ⏭️ | `steps/STEP-2.4-shots-monitor.md` | - |

### Phase 3: Viết nội dung Markdown
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
> ⚠️ **[USER CHỐT 2026-07-26] ĐỔI THỨ TỰ:** viết phần **CCU TRƯỚC**, ZCU sau. Trong MANUAL: **Phần 1 = CCU (thao tác app)**, **Phần 2 = ZCU (triển khai)** — ngược với dự kiến ban đầu của plan/scope doc. Bước **3.2 chạy TRƯỚC 3.1**.

| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.2 | **(chạy trước)** Viết phần CCU: thao tác từng màn hình theo Screen Inventory, chèn ảnh đúng chỗ — trở thành Phần 1 của MANUAL | documentation-writer | ✅ | `steps/STEP-3.2-write-ccu.md` | 2026-07-26 18:05 |
| 3.1 | **(chạy sau)** Viết phần ZCU: cài đặt .deb, systemd service, khóa SSH, kiểm tra hoạt động — trở thành Phần 2 của MANUAL | documentation-writer | ⬜ | `steps/STEP-3.1-write-zcu.md` | - |

### Phase 4: Xuất & nghiệm thu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 4.1 | Xuất DOCX + PDF (`scripts/md_to_docx_kztek.py`), kiểm tra Definition of Done của documentation-writer | documentation-writer | ⬜ | `steps/STEP-4.1-export-verify.md` | - |

## Artifacts dự kiến (tổng)
- [ ] `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md`
- [ ] `docs/user-manuals/MANUAL-ccu-zcu-remote-control.docx`
- [ ] `docs/user-manuals/MANUAL-ccu-zcu-remote-control.pdf`
- [ ] `docs/user-manuals/screenshots/*.png` (đủ 100% màn hình theo Screen Inventory bước 0.2)
- [ ] `_workspace/` trung gian: `00_pm_docs-scope.md`, `00_docwriter_screen-inventory.md` (không commit)

## Blockers
- 🛑 **[2026-07-26 17:52 — Bước 2.2] ZCU `192.168.1.4` vẫn OFFLINE dù user báo đã bật lại VM.** Ping fail ~6 phút liên tục (Destination host unreachable); cổng 22/17600 đóng; quét cả subnet 192.168.1.1–254 không thấy máy nào mở 2 cổng này (loại trừ khả năng đổi IP DHCP). Nghi vấn: VM đang ở network mode **NAT thay vì Bridged**, hoặc VM chưa boot xong/treo. **Cần user:** kiểm tra VM (chế độ mạng Bridged, chạy `ip a` trong VM xác nhận IP) rồi yêu cầu chạy tiếp bước 2.2 — 24/28 ảnh nhóm 2.2 + 2 ảnh bù 2.1 + dữ liệu demo SSH đang chờ.

**Rủi ro đã nhận diện:**
1. Một số màn hình/trạng thái (RemoteScreen đang stream, FileManager kết nối SFTP, HealthMonitor có dữ liệu, ZcuSetupWizard cài thật...) cần thiết bị ZCU thật hoặc kết nối SSH đang hoạt động. Nếu không có thiết bị → bước screenshot tương ứng ghi 🛑 BLOCK + báo user, KHÔNG bịa ảnh, KHÔNG dùng placeholder. Tài liệu vẫn viết phần text, đánh dấu vị trí ảnh chờ bổ sung.
2. Build Release có thể fail do môi trường (SDK, dependency) → Phase 1 BLOCK và báo user, không tiếp tục Phase 2.
3. Ảnh terminal Linux (cài .deb, systemctl status) cần máy Linux/ZCU thật — cùng chính sách BLOCK như trên.

## Quyết định / Ghi chú tổng
- **[USER DUYỆT 2026-07-26]** Plan được duyệt, chạy liên tục các bước (không dừng chờ OK giữa từng bước).
- **[USER CHỐT 2026-07-26]** Scope BAO GỒM cả `KeyGen` (quy trình sinh khóa) và `LicenseWindow` — không loại trừ. Bước 0.1 không cần đề xuất lại vấn đề này.
- **[USER CHỐT 2026-07-26]** 1 file duy nhất `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md`. KHÔNG tách 2 file.
- **[USER ĐỔI Ý 2026-07-26 — ưu tiên cao hơn mọi ghi chú cũ]** Thứ tự trong MANUAL: **Phần 1 = CCU (thao tác app)**, **Phần 2 = ZCU (triển khai)**. Viết CCU trước, ZCU sau → chạy **STEP-3.2 trước STEP-3.1**. Mục lục 16 chương ở `_workspace/00_pm_docs-scope.md` phải đánh số lại theo thứ tự mới khi viết.
- **[USER 2026-07-26 — ĐỔI THỨ TỰ THI CÔNG, ưu tiên cao nhất]** Máy ảo ZCU `192.168.1.4` **bị lỗi, không kết nối được** (bước 2.2 BLOCK ở 4/28 ảnh). User quyết định: **viết tài liệu trước, bổ sung ảnh sau**.
  - Phase 2 còn lại (phần thiếu của 2.2, toàn bộ 2.3, 2.4) → **HOÃN** sang sau Phase 3, chạy lại khi VM hoạt động.
  - Phase 3 (3.2 CCU → 3.1 ZCU) chạy NGAY với **19 ảnh thật đã có**; mọi vị trí thiếu ảnh phải chèn marker rõ ràng dạng `> ⏳ **[CHỜ ẢNH: `<ten-file>.png` — mô tả cần chụp gì]**` để lần bổ sung sau chỉ việc thay marker bằng ảnh. **TUYỆT ĐỐI KHÔNG dùng ảnh giả/placeholder/ảnh tái sử dụng sai ngữ cảnh.**
  - Phase 4 (xuất DOCX/PDF) chỉ được coi là **BẢN NHÁP** cho tới khi đủ ảnh — Definition of Done đầy đủ của WF-DOCS chưa đạt cho tới lúc đó, phải nói rõ với user.
- **[USER CUNG CẤP 2026-07-26]** Thiết bị ZCU thật cho Phase 2: host `192.168.1.4`, user `kztek`. **Credential đầy đủ (kèm mật khẩu) lưu tại `temp/user-manual-ccu-zcu/zcu-connection.md` — thư mục `temp/` đã gitignore, TUYỆT ĐỐI KHÔNG chép mật khẩu vào bất kỳ file nào dưới `docs/` hoặc `_workspace/`.** Trong MANUAL phải dùng giá trị ví dụ giả (`192.168.1.x`, `<user>`), không ghi credential thật.
- **[USER CHỐT 2026-07-26 — Phase 2, trả lời 5 mục cần quyết ở `_workspace/00_docwriter_screen-inventory.md` §4]**
  - ✅ **ĐƯỢC PHÉP** chạy thật ZcuSetupWizard cài/cập nhật ZcuAgent lên `192.168.1.4` (giữ nguyên Token/Port hiện tại, chấp nhận restart agent) → chụp `zcu-setup-wizard-installing/success`, `zcu-terminal-*`.
  - 🛑 **KHÔNG được phép** bấm Deploy thật ở KioskDeployWindow (đổi GNOME/autologin) → `kiosk-deploy-log.png` = BLOCK, mô tả bằng chữ.
  - 🛑 **KHÔNG được phép** cài gói .deb thử qua RemoteAppInstall → ảnh trạng thái "cài thành công" = BLOCK, chỉ chụp tới bước chọn gói/xác nhận.
  - ✅ **LicenseWindow:** mở bằng **harness tạm trong `temp/`** (project console nhỏ, KHÔNG commit, **KHÔNG sửa code sản phẩm**) để chụp ảnh thật. `license-success` vẫn BLOCK nếu không sinh được key hợp lệ.
- Phạm vi + screenshot từ app thật đã được user chốt trước khi tạo plan — không hỏi lại.
- Phase 2 chia 4 bước theo cụm chức năng để mỗi session subagent không quá lớn (§16.5).
- Documentation Writer PHẢI đọc `.claude/commands/kztek-brand-info.md` trước khi tạo bất kỳ file tài liệu nào (điều kiện WF-DOCS).

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-07-26 | Plan tạo mới | task-planner |
| 2026-07-26 | User duyệt plan; chốt 4 quyết định (chạy liên tục, KeyGen+License trong scope, 1 file gộp, có thiết bị ZCU thật cho Phase 2); Bước 0.1 → In Progress | Dispatcher |
| 2026-07-26 | Bước 0.1 ✅ — scope + mục lục 16 chương tại `_workspace/00_pm_docs-scope.md`; ghi nhận MainWindow ≡ ConnectionEntryWindow (18 view duy nhất) | product-manager |
| 2026-07-26 | Bước 0.2 ✅ — Screen Inventory 18/18 view + checklist 79 ảnh (2.1=16, 2.2=28, 2.3=20, 2.4=15) tại `_workspace/00_docwriter_screen-inventory.md`; điều chỉnh: ConfirmDeleteDialog + SystemInventoryWindow chụp trong bước 2.2; LicenseWindow không có entry point UI → cần harness dev tạm; 5 mục cần quyết định trước Phase 2 (mục 4 inventory) | documentation-writer |
| 2026-07-26 | Bước 1.1 ✅ — Build Release CcuUI 0 error; app chạy thật từ exe Release (PID 17180, để nguyên cho Phase 2); script chụp `temp/user-manual-ccu-zcu/capture-window.ps1` (PrintWindow + PW_RENDERFULLCONTENT — không bị cửa sổ khác che); kiểm chứng bằng ảnh thật `screenshots/connection-entry-default.png` (1216×799) | documentation-writer |
| 2026-07-26 | Bước 2.2 🛑 BLOCKED — ZCU 192.168.1.4 vẫn offline (ping+cổng 22/17600 fail ~6 phút, quét subnet không thấy); chụp được 4/28 ảnh không cần ZCU (`remote-screen-connecting/-ssh-help/-faulted`, `multi-remote-empty` — verify bằng Read); 2 ảnh bù 2.1 (`network-scan-results`, `connection-entry-default` P01 online) CHƯA chụp được — cùng chờ ZCU; tool mới `temp/user-manual-ccu-zcu/actions-22.ps1` (double-click item mở RemoteScreen) | documentation-writer |
| 2026-07-26 | Bước 3.2 ✅ — Tạo `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` (BẢN NHÁP): Phần 1 CCU hoàn chỉnh phủ 18/18 màn hình (ch3–10, đánh số chương MỚI: CCU trước, ZCU sau), 19 ảnh thật chèn (Hình 1–19), 52 marker ⏳ CHỜ ẢNH + Phụ lục A checklist bổ sung; Phần 2 ZCU (ch11–13) để khung 🚧 chờ bước 3.1; verify 0 link ảnh chết | documentation-writer |
| 2026-07-26 | Bước 2.1 ✅ — 15/16 ảnh nhóm Kết nối & Quản lý thiết bị (verify từng ảnh bằng Read); 🛑 thiếu `network-scan-results` + ⚠️ `connection-entry-default` chụp bản P01-offline (ZCU 192.168.1.4 không mở cổng 22/17600 — cần user kiểm tra thiết bị trước bước 2.2); dữ liệu mẫu P01/P02/P03 giữ trong store, backup store thật tại `temp/user-manual-ccu-zcu/profiles.backup.json` (khôi phục ở 4.1) | documentation-writer |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
**Cách đọc nhanh:** đọc MASTER trước → nếu cần chi tiết bước cụ thể mới mở step file tương ứng.
