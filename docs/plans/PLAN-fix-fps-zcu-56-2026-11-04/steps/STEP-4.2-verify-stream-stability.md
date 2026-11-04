---
step: 4.2
name: verify-stream-stability
phase: 4
status: completed
agent: qa-engineer
created: 2026-11-04
updated: 2026-11-04
---

# BƯỚC 4.2: Xác Minh Độ Ổn Định Stream Không Disconnect & Đạt FPS Cao

## 1. Mục tiêu
- Xác minh ZcuAgent trên `192.168.21.56` phục vụ kết nối streaming không còn lỗi `Broken pipe (SocketException 32)`.
- Xác minh Keepalive ping-pong hoạt động liên tục vượt mốc 30 giây (mốc timeout cũ).
- Tiến trình CCU UI mới (PID 3764) sẵn sàng để người dùng mở phiên điều khiển remote mượt mà.

## 2. Kết quả đo kiểm
- **Test socket TCP trực tiếp (40 giây liên tục):**
  - Gửi PING mỗi 5s: 8 lần -> Nhận PONG phản hồi đầy đủ 8/8 lần trong < 100ms.
  - Server PING nhận 7 lần -> Client phản hồi PONG 7/7 lần.
  - Nhận tổng cộng 493 frame JPEG mượt mà, không gián đoạn.
  - Sau mốc 30 giây (30.2s, 35.2s, 40.0s): Kết nối tiếp tục duy trì 100% ổn định.
- **Log ZcuAgent (`journalctl --user -u ipgs-remote-agent`):**
  - Không còn bất kỳ ngoại lệ `SocketException 32 (Broken pipe)`.
  - Không còn timeout `no PONG within 30000ms`.
  - CPU trên máy đích Celeron J3160 dao động ổn định quanh 7-9%.
- **Client CCU UI:**
  - Bản build mới khởi chạy (PID 3764) với render pipeline SKCodec zero-copy và cơ chế latest-wins.
