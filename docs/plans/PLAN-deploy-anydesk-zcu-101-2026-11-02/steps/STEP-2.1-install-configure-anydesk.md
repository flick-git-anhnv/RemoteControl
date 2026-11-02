---
step: 2.1
plan: ../PLAN-MASTER.md
agent: devops-engineer
status: todo
completed_at: —
deps: [1.1]
---

# STEP 2.1 — Cài đặt & Cấu hình AnyDesk trên 192.168.21.101

## Input nhận
- Thông số môi trường từ Step 1.1.
- Yêu cầu cấu hình Unattended Access Password: `Kztek@123456`.

## Nhiệm vụ
Cài đặt phần mềm AnyDesk chính thức lên thiết bị `192.168.21.101`, kích hoạt dịch vụ `anydesk.service` chạy nền, thiết lập mật khẩu truy cập không giám sát (`echo "Kztek@123456" | sudo anydesk --set-password`), và lấy mã AnyDesk ID (`anydesk --get-id`) cùng trạng thái kết nối máy chủ (`anydesk --get-status`).

## Definition of Done
- [ ] Cài đặt thành công gói `anydesk` và các thư viện phụ thuộc (ví dụ `libgtkglext1` / `libpangox-1.0-0` nếu cần trên Ubuntu).
- [ ] Dịch vụ `anydesk` ở trạng thái `active (running)` và `enabled`.
- [ ] Thiết lập thành công mật khẩu Unattended Access `Kztek@123456`.
- [ ] Lấy được mã AnyDesk ID (9-10 chữ số) và trạng thái `online`.

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
