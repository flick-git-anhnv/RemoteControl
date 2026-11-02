---
step: 4.1
plan: ../PLAN-MASTER.md
agent: devops-lead
status: done
completed_at: 2026-11-02 08:44:43
deps: [3.1]
---

# STEP 4.1 — Kiểm tra, Xác minh Dịch vụ Hoạt động & Báo cáo Hoàn tất

## Input nhận
- Trạng thái triển khai và cấu hình dịch vụ từ Step 3.1.

## Nhiệm vụ
Kiểm tra trạng thái thực tế của systemd user service `ipgs-remote-agent.service` (`is-active`), kiểm tra socket listen trên cổng 17600, xem log khởi động ban đầu để đảm bảo X11 capturer và TCP server đã sẵn sàng nhận kết nối, thực hiện kết nối end-to-end trực tiếp (Handshake -> Auth -> Stream Frame) và phê duyệt sign-off.

## Definition of Done
- [x] Service `ipgs-remote-agent.service` ở trạng thái `active (running)`.
- [x] Port 17600/tcp đang ở trạng thái `LISTEN` (PID 4044898).
- [x] Nhật ký log systemd hiển thị agent đã bind port 17600, X11 Display `:0` (768x1024) mở thành công, Mouse & Keyboard injector sẵn sàng.
- [x] Kết nối thử nghiệm trực tiếp từ máy trạm: Bắt tay HELLO (0x01) -> HELLO_ACK (0x02) thành công.
- [x] Xác thực mật khẩu: Gửi AUTH (0x03) với Token `123456` -> Nhận AUTH_OK (0x04) thành công.
- [x] Nhận luồng video: Nhận FRAME_JPEG (0x10) độ phân giải 768x1024, dung lượng 141,409 bytes với byte ma thuật JPEG hợp lệ (`FF D8 ... FF D9`).
- [x] Đóng kết nối duyên dáng (BYE 0x7F) thành công.

## Đã làm
- Phát triển và chạy script kiểm chứng end-to-end `scratch/verify_live_agent.py`.
- Thực hiện đầy đủ chu trình giao thức binary chuẩn v1.2 từ phía client kết nối tới server ZCU mới triển khai.
- Toàn bộ các bước bắt tay, xác thực token và nhận frame trực tiếp đều đạt kết quả xuất sắc.

## Artifact
- `scratch/verify_live_agent.py`
- Báo cáo kết quả kiểm chứng hoạt động trực tiếp của ZcuAgent

## Quyết định quan trọng
- Phê duyệt Sign-off bản triển khai ZcuAgent mới nhất lên môi trường ZCU `192.168.21.230`. Dịch vụ đã sẵn sàng phục vụ các phiên làm việc điều khiển từ xa từ CCU UI.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Quá trình triển khai và kiểm thử nghiệm thu đã hoàn tất 100%.
- watch_out: Không có.
- next_inputs: Cung cấp thông số kết nối (IP: 192.168.21.230, Port: 17600, Token: 123456) cho người dùng.

## Commit
- Hash: b952e20
- Đã push: Không
