---
step: 4.2
plan: ../PLAN-MASTER.md
agent: qa-engineer
status: todo
completed_at:
---

# STEP 4.2 — QA Engineer verify chức năng remote control

## Input nhận

Từ STEP-4.1 Handoff Log — hướng dẫn setup môi trường test (ZcuAgent host/port/secret, cách chạy IPGSUseCam), kết quả UX review (issues nếu có đã fix).

## Nhiệm vụ

Verify chức năng end-to-end: kết nối CCU → ZCU, hiển thị màn hình ZCU trên CCU, và điều khiển chuột từ CCU sang ZCU. Bao gồm cả test authentication và test mất kết nối.

## Definition of Done

- [ ] TC-RC-001: Kết nối thành công với host/port/secret đúng → frame hiển thị trong ≤ 3 giây
- [ ] TC-RC-002: Kết nối thất bại với secret sai → hiển thị lỗi rõ ràng, không crash
- [ ] TC-RC-003: Mouse move trên RemoteScreenView → con trỏ ZCU di chuyển tương ứng (quan sát trên màn hình ZCU thật)
- [ ] TC-RC-004: Left click trên RemoteScreenView → ZCU nhận click đúng toạ độ
- [ ] TC-RC-005: Mất kết nối mạng giữa chừng → CCU hiển thị trạng thái Disconnected, tự reconnect sau khi mạng phục hồi
- [ ] TC-RC-006: Frame rate ổn định (≥ 10fps trong điều kiện mạng LAN bình thường)
- [ ] `docs/test-cases/TC-remote-control.md` tạo xong với kết quả Pass/Fail từng TC
- [ ] Không có P0/P1 bug còn mở khi sign-off
- [ ] Commit + push lên nhánh `zcu-avalonia`

## Đã làm

[Điền sau khi hoàn thành]

## Artifact

- [điền sau khi xong]

## Quyết định quan trọng

Không có

## Handoff Log — bước sau cần biết

[Điền sau khi hoàn thành — tổng kết Pass/Fail, bug còn lại (nếu có P2/P3), và sign-off status]

## Commit

- Hash: [điền sau khi commit]
- Đã push: [có/không]

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
