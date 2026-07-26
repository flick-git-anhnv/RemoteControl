---
step: 0.2
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 16:39
---

# STEP 0.2 — Screen Inventory 100% màn hình + checklist screenshot

## Input nhận
- Scope + mục lục từ STEP-0.1 (`_workspace/00_pm_docs-scope.md`) — nhúng Handoff Log của 0.1 vào đây khi giao việc.
- Danh sách 19 file .axaml đã khảo sát trong `IPGS.RemoteControl.CcuUI/Views/`: BulkActionWindow, ComputerEditWindow, ConfirmDeleteDialog, ConnectionEntryWindow, CronJobWindow, FileManagerWindow, HealthMonitorWindow, KioskDeployWindow, LicenseWindow, MultiRemoteWindow, NetworkScanWindow, RemoteAppInstallWindow, RemoteCommandWindow, RemoteScreenControl, RemoteScreenWindow, SessionPickerWindow, SystemInventoryWindow, ZcuSetupWizardWindow (+ App.axaml, + màn hình chính MainWindow nếu có).

## Nhiệm vụ
Đọc từng View (.axaml + code-behind/ViewModel nếu cần) để lập bảng Screen Inventory 100%: tên màn hình, chức năng, cách mở (từ menu/nút nào), các trạng thái cần chụp (mặc định / có dữ liệu / dialog con / lỗi), điều kiện tiên quyết (cần ZCU thật/SSH hay không), thuộc nhóm chụp nào (2.1–2.4). Xác nhận lại phân nhóm 4 cụm trong PLAN-MASTER — điều chỉnh nếu khảo sát thấy chưa hợp lý.

## Definition of Done
- [ ] `_workspace/00_docwriter_screen-inventory.md`: bảng inventory đủ 100% màn hình (không sót view nào trong Views/)
- [ ] Mỗi màn hình có: cách mở, danh sách ảnh cần chụp + tên file ảnh dự kiến (`screenshots/NN-ten-man-hinh-trang-thai.png`), cột "Cần ZCU thật? (Y/N)"
- [ ] Đánh dấu rõ màn hình/trạng thái nào KHÔNG chụp được nếu thiếu thiết bị → làm cơ sở BLOCK ở Phase 2
- [ ] Cập nhật step file này + PLAN-MASTER.md (status ✅)

## Đã làm
- Đọc 18 file `.axaml` + code-behind chọn lọc (`ConnectionEntryWindow.axaml.cs`, `ComputerEditWindow.axaml.cs`, `LicenseWindow.axaml.cs`, grep toàn bộ luồng `new XxxWindow`) để xác minh cách mở từng cửa sổ và các trạng thái THỰC SỰ tồn tại trong code.
- Lập `_workspace/00_docwriter_screen-inventory.md`: bảng inventory 18/18 view (tên tiếng Việt, chức năng, mức chi tiết 1/2/3, cách mở từ UI, nhóm chụp 2.1–2.4) + checklist screenshot chi tiết từng màn hình với tên file `[slug]-[state].png` và điều kiện tiên quyết (ZCU/SSH/DATA).
- Tổng 79 ảnh: 2.1=16, 2.2=28, 2.3=20, 2.4=15 (gồm KeyGen console + 8 ảnh terminal ZCU); 3 ảnh best-effort.
- Vì đã có ZCU thật 192.168.1.4 → phân loại lại: chỉ còn 5 mục cần quyết định/BLOCK (license-success, cách mở LicenseWindow, kiosk-deploy chạy thật, cài lại agent thật, lỗi phần cứng).
- Kịch bản dữ liệu mẫu thống nhất: 3 máy `Máy khách Trạm P01/P02/P03`, thư mục `/home/kztek/kztek-demo/`, cron mẫu, gói .deb `hello`.

## Artifact
- `_workspace/00_docwriter_screen-inventory.md` (trung gian — không commit)

## Quyết định quan trọng
- **Điều chỉnh phân nhóm PLAN:** ConfirmDeleteDialog chụp ở bước 2.2 (chỉ mở được từ FileManager/RemoteCommand); SystemInventoryWindow chụp ở 2.2 (chỉ mở được từ RemoteScreenWindow khi đang stream) dù tài liệu vẫn viết ở chương 10.2.
- **Không bịa trạng thái không có trong code:** ComputerEditWindow KHÔNG có validation error (Save luôn thành công); Xóa máy khỏi danh sách KHÔNG có dialog xác nhận (xóa ngay); Kết nối nhanh bỏ trống IP return im lặng — tài liệu ghi Lưu ý/Cảnh báo thay vì ảnh.
- **LicenseWindow không có entry point trong UI** (không nơi nào `new LicenseWindow()`) — chụp bằng harness dev tạm trong `temp/`, cần xác nhận cách làm ở bước 2.4.

## Handoff Log — bước sau cần biết
- Đã làm: Lập xong Screen Inventory 18/18 view + checklist 79 ảnh (2.1=16, 2.2=28, 2.3=20, 2.4=15) tại `_workspace/00_docwriter_screen-inventory.md`, kèm kịch bản dữ liệu mẫu (3 máy P01/P02/P03, thư mục kztek-demo) và 5 mục cần quyết định trước Phase 2.
- File/module đã đọc hoặc đổi: đọc `IPGS.RemoteControl.CcuUI/Views/*.axaml` (18 file) + `ConnectionEntryWindow.axaml.cs`, `ComputerEditWindow.axaml.cs`, `LicenseWindow.axaml.cs`; tạo `_workspace/00_docwriter_screen-inventory.md`; sửa step file này + PLAN-MASTER.md.
- Quyết định quan trọng: ConfirmDeleteDialog + SystemInventoryWindow chuyển sang chụp trong bước 2.2; LicenseWindow không mở được từ UI → cần harness dev tạm ở 2.4; kiosk-deploy-log và cài-lại-agent cần user xác nhận trước khi chạy thật (thay đổi máy ZCU thật); license-success là ứng viên BLOCK duy nhất về mặt kỹ thuật.
- Bước sau cần biết (1.1): build `dotnet build IPGS.RemoteControl.CcuUI -c Release` (KHÔNG build 3 project song song — tranh chấp obj/); app mở thẳng ConnectionEntryWindow, không có license gate; trước khi chụp 2.1 phải backup profile store và chụp `connection-entry-empty` TRƯỚC khi thêm 3 máy mẫu.

## Commit
- Hash: 36dbf7c
- Đã push: không (theo chỉ đạo bước này)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
