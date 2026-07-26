---
task: user-manual-ccu-zcu
created: 2026-07-26
updated: 2026-07-26
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
| 0.2 | Kiểm kê 100% màn hình CcuUI (Screen Inventory) + checklist screenshot + phân nhóm chụp | documentation-writer | ⬜ | `steps/STEP-0.2-screen-inventory.md` | - |

### Phase 1: Build & chạy app thật
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Build Release `IPGS.RemoteControl.CcuUI`, khởi động app, xác nhận chạy được (fail → BLOCK, báo user) | documentation-writer | ⬜ | `steps/STEP-1.1-build-run-ccuui.md` | - |

### Phase 2: Chụp screenshot theo nhóm màn hình (app thật)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Nhóm Kết nối & Quản lý thiết bị: MainWindow, ConnectionEntryWindow, ComputerEditWindow, NetworkScanWindow, SessionPickerWindow, ConfirmDeleteDialog | documentation-writer | ⬜ | `steps/STEP-2.1-shots-connection.md` | - |
| 2.2 | Nhóm Remote & Điều khiển: RemoteScreenWindow, RemoteScreenControl, MultiRemoteWindow, RemoteCommandWindow, FileManagerWindow | documentation-writer | ⬜ | `steps/STEP-2.2-shots-remote.md` | - |
| 2.3 | Nhóm Triển khai & Quản trị: ZcuSetupWizardWindow, KioskDeployWindow, RemoteAppInstallWindow, BulkActionWindow, CronJobWindow | documentation-writer | ⬜ | `steps/STEP-2.3-shots-deploy.md` | - |
| 2.4 | Nhóm Giám sát & Hệ thống: HealthMonitorWindow, SystemInventoryWindow, LicenseWindow (+ ảnh terminal ZCU nếu có thiết bị) | documentation-writer | ⬜ | `steps/STEP-2.4-shots-monitor.md` | - |

### Phase 3: Viết nội dung Markdown
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | Viết phần ZCU: cài đặt .deb, systemd service, khóa SSH, kiểm tra hoạt động | documentation-writer | ⬜ | `steps/STEP-3.1-write-zcu.md` | - |
| 3.2 | Viết phần CCU: thao tác từng màn hình theo Screen Inventory, chèn ảnh đúng chỗ, hoàn thiện MANUAL .md | documentation-writer | ⬜ | `steps/STEP-3.2-write-ccu.md` | - |

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
Không có (tại thời điểm tạo plan).

**Rủi ro đã nhận diện:**
1. Một số màn hình/trạng thái (RemoteScreen đang stream, FileManager kết nối SFTP, HealthMonitor có dữ liệu, ZcuSetupWizard cài thật...) cần thiết bị ZCU thật hoặc kết nối SSH đang hoạt động. Nếu không có thiết bị → bước screenshot tương ứng ghi 🛑 BLOCK + báo user, KHÔNG bịa ảnh, KHÔNG dùng placeholder. Tài liệu vẫn viết phần text, đánh dấu vị trí ảnh chờ bổ sung.
2. Build Release có thể fail do môi trường (SDK, dependency) → Phase 1 BLOCK và báo user, không tiếp tục Phase 2.
3. Ảnh terminal Linux (cài .deb, systemctl status) cần máy Linux/ZCU thật — cùng chính sách BLOCK như trên.

## Quyết định / Ghi chú tổng
- **[USER DUYỆT 2026-07-26]** Plan được duyệt, chạy liên tục các bước (không dừng chờ OK giữa từng bước).
- **[USER CHỐT 2026-07-26]** Scope BAO GỒM cả `KeyGen` (quy trình sinh khóa) và `LicenseWindow` — không loại trừ. Bước 0.1 không cần đề xuất lại vấn đề này.
- **[USER CHỐT 2026-07-26]** 1 file duy nhất `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` — Phần 1 = triển khai ZCU, Phần 2 = thao tác CCU. KHÔNG tách 2 file.
- **[USER CHỐT 2026-07-26]** Sẽ có thiết bị ZCU thật/SSH cho Phase 2 — user cung cấp thông tin kết nối trước khi bắt đầu Phase 2. Dispatcher PHẢI hỏi user IP/tài khoản SSH trước bước 2.1.
- Phạm vi + screenshot từ app thật đã được user chốt trước khi tạo plan — không hỏi lại.
- Phase 2 chia 4 bước theo cụm chức năng để mỗi session subagent không quá lớn (§16.5).
- Documentation Writer PHẢI đọc `.claude/commands/kztek-brand-info.md` trước khi tạo bất kỳ file tài liệu nào (điều kiện WF-DOCS).

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-07-26 | Plan tạo mới | task-planner |
| 2026-07-26 | User duyệt plan; chốt 4 quyết định (chạy liên tục, KeyGen+License trong scope, 1 file gộp, có thiết bị ZCU thật cho Phase 2); Bước 0.1 → In Progress | Dispatcher |
| 2026-07-26 | Bước 0.1 ✅ — scope + mục lục 16 chương tại `_workspace/00_pm_docs-scope.md`; ghi nhận MainWindow ≡ ConnectionEntryWindow (18 view duy nhất) | product-manager |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
**Cách đọc nhanh:** đọc MASTER trước → nếu cần chi tiết bước cụ thể mới mở step file tương ứng.
