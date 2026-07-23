---
step: 4.1
plan: ../PLAN-MASTER.md
agent: ux-ui-reviewer
status: todo
completed_at:
---

# STEP 4.1 — UX/UI Reviewer kiểm tra UI remote screen

## Input nhận

Từ STEP-3.4 Handoff Log — cách chạy demo (ZcuAgent host/port/secret, cách mở RemoteControlWindow từ IPGSUseCam).

## Nhiệm vụ

Chạy ứng dụng thật (IPGSUseCam), mở tính năng Remote ZCU, kết nối tới ZcuAgent đang chạy, chụp screenshot thực tế, và đánh giá 7 tiêu chí C1–C7.

## Definition of Done

- [ ] Chạy IPGSUseCam thật, thực hiện flow: click menu/button Remote ZCU → nhập host/port/secret → kết nối → hiển thị màn hình ZCU
- [ ] Chụp screenshot ít nhất 3 trạng thái: (1) đang kết nối, (2) đã kết nối hiển thị frame, (3) mất kết nối/lỗi
- [ ] Đánh giá C1 (Layout/alignment), C2 (Typography/readability), C3 (Color/contrast), C4 (Feedback/loading state), C5 (Error handling UI), C6 (Responsiveness — cửa sổ resize thì RemoteScreenView scale đúng không), C7 (Overall usability)
- [ ] `docs/ux-review/UX-REVIEW-remote-control.md` tạo xong với đánh giá + screenshot
- [ ] `docs/ux-review/UX-REVIEW-remote-control.docx` + `.pdf` xuất từ script
- [ ] Nếu có issue nghiêm trọng (C1-C7 Fail) → ghi rõ và BLOCK bước 4.2 chờ fix
- [ ] Commit + push lên nhánh `zcu-avalonia`

## Đã làm

[Điền sau khi hoàn thành]

## Artifact

- [điền sau khi xong]

## Quyết định quan trọng

Không có

## Handoff Log — bước sau cần biết

[Điền sau khi hoàn thành — ghi rõ: pass/fail từng tiêu chí C1-C7, có issue nào cần fix trước QA không, và hướng dẫn setup môi trường test cho QA]

## Commit

- Hash: [điền sau khi commit]
- Đã push: [có/không]

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
