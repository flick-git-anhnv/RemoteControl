---
step: 0.1
plan: ../PLAN-MASTER.md
agent: product-manager
status: done
completed_at: 2026-07-26 16:29
---

# STEP 0.1 — PM chốt scope tài liệu + đối tượng đọc

## Input nhận
- User đã chốt: HDSD toàn hệ thống — (a) triển khai ZcuAgent trên Linux (.deb, systemd service, khóa SSH) + (b) thao tác app desktop `IPGS.RemoteControl.CcuUI`. Screenshot từ app thật.
- Project: `E:\KZTEK\Code_Git\6.RemoteControlTool`. CcuUI = Avalonia 12.1.0, net8.0, WinExe, KztekComponentAvalonia. 18 cửa sổ/dialog trong `IPGS.RemoteControl.CcuUI/Views/` + màn hình chính.
- Các project khác: CcuClient (library — không cần HDSD riêng), ZcuAgent (agent Linux — phần triển khai), KeyGen (cân nhắc có đưa vào scope không).

## Nhiệm vụ
Xác nhận scope tài liệu, định nghĩa đối tượng người đọc (kỹ thuật viên triển khai vs người vận hành hằng ngày), đề xuất cấu trúc mục lục HDSD (2 phần ZCU + CCU, thứ tự chương, mức chi tiết từng màn hình), quyết định KeyGen/License có nằm trong scope không.

## Definition of Done
- [ ] File `_workspace/00_pm_docs-scope.md` với: đối tượng đọc, scope in/out (nêu rõ quyết định về KeyGen), mục lục dự kiến của MANUAL, mức chi tiết yêu cầu cho từng nhóm màn hình
- [ ] Ghi rõ giọng văn/ngôn ngữ (tiếng Việt) và quy ước đặt tên screenshot
- [ ] Cập nhật step file này + PLAN-MASTER.md (status ✅)

## Đã làm
- Đọc CODE-GRAPH.md v2.0, documentation-writer.md (khuôn §Bước 3), khảo sát 18 view `.axaml` trong `CcuUI/Views/`, `scripts/setup-zcu-agent.sh`, `scripts/linux-deb/`, `KeyGen/Program.cs`, `ConnectionEntryWindow.axaml`.
- Viết `_workspace/00_pm_docs-scope.md` đủ 6 mục: đối tượng đọc (2 nhóm A/B + giả định trình độ), scope IN/OUT (KeyGen + LicenseWindow IN theo chốt user; CcuClient/kiến trúc code OUT), mục lục 16 chương đánh số tới cấp 3 + bảng đối chiếu phủ 100% 18 view, 3 mức chi tiết theo nhóm màn hình, giọng văn & quy ước screenshot/caption, 8 rủi ro/lưu ý cho documentation-writer.

## Artifact
- `_workspace/00_pm_docs-scope.md`

## Quyết định quan trọng
- **MainWindow ≡ ConnectionEntryWindow** (`App.axaml.cs` gán trực tiếp) — "19 màn hình" của plan thực tế = 18 view duy nhất; bảng đối chiếu trong scope doc phủ đủ 18.
- Mục lục 2 phần trong 1 file (theo chốt user): Phần 1 chương 3–5 (triển khai ZCU + KeyGen/khóa), Phần 2 chương 6–14 (app CCU), FAQ chương 15, liên hệ chương 16.
- 3 mức chi tiết: Mức 1 đầy đủ mọi trạng thái (6 màn hình lõi), Mức 2 chuẩn (9 màn hình), Mức 3 ngắn gọn (ConfirmDeleteDialog, SessionPickerWindow).
- Phát hiện mâu thuẫn cổng: script cài dùng 17600, appsettings.json mẫu 5900 → tài liệu lấy 17600 làm chuẩn, xác minh lại ở Phase 2.

## Handoff Log — bước sau cần biết
- Đã làm: Chốt scope, đối tượng đọc, mục lục 16 chương phủ 100% 18 view + phần ZCU/KeyGen/License, mức chi tiết, quy ước screenshot — tất cả trong `_workspace/00_pm_docs-scope.md`.
- File/module đã đọc hoặc đổi: đọc `code-graph/CODE-GRAPH.md`, `IPGS.RemoteControl.CcuUI/Views/*.axaml` (glob), `scripts/setup-zcu-agent.sh`, `KeyGen/Program.cs`, `.claude/agents/documentation-writer.md`; tạo `_workspace/00_pm_docs-scope.md`; sửa step file này + PLAN-MASTER.md.
- Quyết định quan trọng: MainWindow ≡ ConnectionEntryWindow (KHÔNG có MainWindow.axaml riêng — bước 0.2 kiểm kê theo 18 view, đừng tìm MainWindow); KeyGen là console app in cả private key → ảnh phải che; License không enforce (dead code chủ đích) → tài liệu không hứa hành vi khóa app; cổng chuẩn tài liệu = 17600.
- Bước sau cần biết: Bước 0.2 dùng bảng đối chiếu 18 view + 3 mức chi tiết trong scope doc làm khung Screen Inventory — KHÔNG cần khảo sát lại danh sách view; danh sách trạng thái cần ZCU thật vs không cần đã liệt kê sẵn ở mục 6.1/6.2 của scope doc.

## Commit
- Hash: c1a0e0e
- Đã push: không (theo chỉ thị bước này)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
