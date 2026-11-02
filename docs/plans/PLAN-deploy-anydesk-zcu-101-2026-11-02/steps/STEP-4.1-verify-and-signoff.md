---
step: 4.1
plan: ../PLAN-MASTER.md
agent: devops-lead
status: todo
completed_at: —
deps: [2.1]
---

# STEP 4.1 — Kiểm thử Kết nối Thực tế & Nghiệm thu (Sign-off)

## Input nhận
- AnyDesk ID & Password từ Step 2.1.

## Nhiệm vụ
Thực hiện kiểm tra nghiệm thu độc lập (Two-Eyes Principle):
1. Xác minh trạng thái dịch vụ `anydesk` (`active (running)`, `online`, AnyDesk ID hợp lệ, cấu hình Unattended Access và Direct Connection port 7070 hoạt động).
2. Kiểm tra kết nối mạng/port 7070 từ máy trạm tới `192.168.21.101` và trạng thái X11 Display để đảm bảo khi kết nối AnyDesk không gặp lỗi màn hình đen hoặc `display_server_not_supported`.
3. Tổng hợp báo cáo nghiệm thu kèm hướng dẫn và thông số đầy đủ để người dùng kiểm tra kết nối ngay.

## Definition of Done
- [ ] Xác nhận AnyDesk service `active (running)`, trạng thái `online` và ID chính xác.
- [ ] Kiểm tra port Direct Connection (`7070/tcp`) và cấu hình X11 Display (tránh lỗi Wayland/headless).
- [ ] Cung cấp bảng thông số kết nối đầy đủ và các cách kiểm tra kết nối cho người dùng.

## Đã làm


## Artifact


## Quyết định quan trọng


## Handoff Payload — bước sau đọc phần này
- do_not_redo:
- watch_out:
- next_inputs:

## Commit
- Hash: —
- Đã push: Không
