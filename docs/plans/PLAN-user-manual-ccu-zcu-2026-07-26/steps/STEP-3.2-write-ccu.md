---
step: 3.2
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 18:05
---

# STEP 3.2 — Viết phần CCU: thao tác người dùng trên app CcuUI

## Input nhận
- Handoff Log STEP-3.1 (cấu trúc heading MANUAL, quy ước chèn ảnh, phần chờ ảnh).
- Screen Inventory `_workspace/00_docwriter_screen-inventory.md` + toàn bộ ảnh Phase 2.

## Nhiệm vụ
Viết tiếp `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` — phần CCU: hướng dẫn thao tác TỪNG màn hình theo Screen Inventory (100% màn hình, theo 4 nhóm: Kết nối & Quản lý thiết bị / Remote & Điều khiển / Triển khai & Quản trị / Giám sát & Hệ thống), mỗi màn hình: mục đích, cách mở, các bước thao tác, ảnh minh họa đúng chỗ, lưu ý/lỗi thường gặp. Đối chiếu hành vi thực tế từ code/ảnh đã chụp — KHÔNG mô tả chức năng không tồn tại.

## Definition of Done
- [x] Phần CCU phủ 100% màn hình trong Screen Inventory — không sót màn hình nào (18/18 view)
- [x] Mọi ảnh Phase 2 được chèn đúng vị trí; ảnh thiếu có marker `⏳ [CHỜ ẢNH]` rõ ràng (52 marker + Phụ lục A)
- [x] MANUAL .md hoàn chỉnh Phần 1 CCU + khung Phần 2 ZCU (placeholder 🚧 cho bước 3.1), mục lục đánh số mới, nhãn BẢN NHÁP, phiên bản, ngày
- [x] Handoff Log điền đầy đủ
- [x] Cập nhật step file này + PLAN-MASTER.md

## Đã làm
1. **[USER ĐỔI THỨ TỰ]** Bước 3.2 chạy TRƯỚC 3.1 — tạo mới `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` với **Phần 1 = CCU** (chương 3–10), **Phần 2 = ZCU** (chương 11–13, chỉ tiêu đề + ghi chú 🚧 chờ bước 3.1). Mục lục 16 chương của scope doc được đánh số lại theo thứ tự mới: 1–2 giới thiệu/yêu cầu, 3–10 CCU, 11–13 ZCU, 14 FAQ, 15 liên hệ (+Phụ lục A). Chương "13 giữ chỗ" cũ bị bỏ vì đánh số lại toàn bộ.
2. Đọc brand info + đọc lại 15 file `.axaml` (toàn bộ 18 view) để xác minh nguyên văn tên nút/trường/thông báo trước khi viết — KHÔNG bịa chức năng (VD: xóa máy không có confirm dialog → viết ⚠️ Cảnh báo; Kết nối nhanh bỏ trống IP → im lặng; ComputerEdit luôn Lưu được → Lưu ý thay vì ảnh lỗi; LicenseWindow không có đường mở từ UI → ghi trung thực "liên hệ KZTEK").
3. Phủ 18/18 màn hình: ConnectionEntry §3.2, ComputerEdit §4.1, NetworkScan §4.2, RemoteScreen §5.1, RemoteScreenControl §5.1.2, MultiRemote §5.2, SessionPicker §5.2.2, FileManager §6.1, ConfirmDelete §6.1.2, RemoteCommand §6.2, BulkAction §6.3, CronJob §6.4, HealthMonitor §7.1, SystemInventory §7.2, ZcuSetupWizard §9.1, RemoteAppInstall §9.2, KioskDeploy §9.3, License §10.1. Mức chi tiết theo PM scope (Mức 1/2/3). WoL viết ở chương 8.
4. Chèn **19/19 ảnh thật** đang có (Hình 1–19, đánh số liên tục theo thứ tự xuất hiện); **52 marker** `⏳ [CHỜ ẢNH: ...]` đúng format cho ảnh thiếu; Phụ lục A liệt kê đủ 52 ảnh chờ kèm điều kiện tiên quyết (ZCU/SSH/DATA/Harness/User OK).
5. Verify bằng script: 19 link `![]()` = 19 file PNG thật, 0 link chết; sửa 1 câu ở Phụ lục A chứa cú pháp `![]()` literal gây false-positive khi đối chiếu.
6. Chạy `md_to_docx_kztek.py` cho PLAN-MASTER.md sau khi cập nhật (theo yêu cầu bước).

## Artifact
- `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` (MỚI — ~1000 dòng, Phần 1 CCU hoàn chỉnh, nhãn BẢN NHÁP — CHỜ BỔ SUNG ẢNH)

## Quyết định quan trọng
- Số Hình chỉ gán cho ảnh THẬT đã chèn (1–19); marker không mang số Hình — khi bổ sung ảnh sẽ đánh số lại toàn bộ (ghi rõ trong Phụ lục A) để tránh số nhảy cóc trong bản nháp.
- `connection-entry-default.png` hiện có (bản P01 offline) vẫn được chèn làm Hình 2 (ảnh thật hợp lệ), kèm marker riêng yêu cầu chụp lại bản P01-online — Phụ lục A dòng #1.
- ConfirmDeleteDialog viết ở §6.1.2 (xóa file), KHÔNG viết ở §4.3 (xóa máy) — vì code xóa máy không có dialog; §4.3 thay bằng ⚠️ Cảnh báo xóa ngay không hỏi lại.
- FAQ tách 14.1 (ZCU — 🚧 chờ 3.1) và 14.2 (CCU — đã viết 7 câu).

## Handoff Log — bước sau cần biết
- Đã làm: MANUAL tạo mới với cấu trúc chương đánh số MỚI — ch1–2 Giới thiệu/Yêu cầu, **ch3–10 = Phần 1 CCU (đã viết xong)**, **ch11–13 = Phần 2 ZCU (bước 3.1 viết vào đúng khung: 11 Chuẩn bị, 12 Cài ZcuAgent, 13 Khóa & bảo mật — dàn ý con ghi sẵn trong ngoặc tại mỗi chương)**, ch14 FAQ (14.1 ZCU chờ 3.1, 14.2 CCU xong), ch15 Liên hệ, Phụ lục A ảnh thiếu.
- File/module đã đọc hoặc đổi: tạo `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md`; đọc 15 file `IPGS.RemoteControl.CcuUI/Views/*.axaml` để xác minh UI.
- Quyết định quan trọng: **số Hình đã đánh tới Hình 19** — bước 3.1 chèn ảnh thật mới (nếu có, VD terminal ZCU) thì đánh tiếp từ **Hình 20**; ảnh chờ dùng marker `> ⏳ **[CHỜ ẢNH: \`ten.png\`]** — mô tả` KHÔNG mang số Hình. Tham chiếu chéo đã dùng trong Phần 1: mục 9.1 ↔ chương 12 (cách cài từ xa), mục 10.1 ↔ chương 13.4 — bước 3.1 giữ đúng các số chương này.
- Bước sau cần biết: Phần 2 ZCU cần thêm marker cho 8 ảnh terminal + KeyGen (KHÔNG có trong Phụ lục A hiện tại — bước 3.1 phải BỔ SUNG các dòng đó vào bảng Phụ lục A); lưu ý mâu thuẫn cổng 17600 (installer) vs 5900 (appsettings mẫu) khi viết chương 12.4 — nói rõ giá trị thực tế do installer ghi đè; KHÔNG ghi credential thật (temp/user-manual-ccu-zcu/zcu-connection.md là nguồn — gitignore).

## Commit
- Hash: [điền sau khi commit]
- Đã push: không (theo chỉ thị bước — không push)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
