---
step: 4.1
name: fix-ping-pong-and-client-render
phase: 4
status: completed
agent: senior-developer
created: 2026-11-04
updated: 2026-11-04
---

# BƯỚC 4.1: Sửa Lỗi Ngắt Kết Nối Ping/Pong 30s & Cập Nhật CCU UI Client

## 1. Mục tiêu
- Sửa lỗi keepalive timeout trên `RemoteControlClient.cs`: Khi nhận được frame hình ảnh (`MessageType.FrameJpeg`) hoặc tin nhắn từ server (`MessageType.Ping`), client ghi nhận tín hiệu sống (`_lastPongTicks`), không tự ngắt kết nối sau 30s.
- Sửa cooperative scheduling trên `ClientSession.cs`: Khi `sleep <= 0` trong vòng lặp gửi frame, thêm `await Task.Yield()` để không tranh chấp hoặc bỏ đói `_writeLock` cho các gói tin PONG/Control.
- Rebuild toàn bộ solution (`IPGS.RemoteControl.CcuClient`, `IPGS.RemoteControl.CcuUI`, `IPGS.RemoteControl.ZcuAgent`).
- Triển khai cập nhật lên `192.168.21.56` và khởi động lại CCU UI mới trên Windows.

## 2. Kết quả thực hiện
1. Sửa `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs`:
   - `MessageType.FrameJpeg`: `Interlocked.Exchange(ref _lastPongTicks, DateTime.UtcNow.Ticks);`
   - `MessageType.Ping`: `Interlocked.Exchange(ref _lastPongTicks, DateTime.UtcNow.Ticks);`
2. Sửa `IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs`:
   - Trong `RunCaptureSendLoopAsync`: Bổ sung `await Task.Yield()` khi `sleep <= 0` để nhường CPU và giải phóng `_writeLock` kịp thời cho PONG.
3. Rebuild thành công toàn bộ solution.
4. Triển khai ZcuAgent cập nhật lên `/home/ccu/ipgs/remote-agent/` trên `192.168.21.56` và restart user service `ipgs-remote-agent`.
5. Đã tắt tiến trình CCU UI cũ (PID 24488 chạy từ 08:47 ngày 03/11) và khởi động tiến trình CCU UI mới (PID 3764).
6. Kiểm thử độc lập 40s bằng `test_ping_pong.py`: stream chạy ổn định 40s liên tục, nhận 493 frame, 8 PONG, 7 server PING, 0 disconnect, 0 Broken pipe!
