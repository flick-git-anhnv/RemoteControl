---
step: 5.3
name: writeablebitmap-pool
phase: 5
status: done
agent: senior-developer
created: 2026-11-04
updated: 2026-11-04
completed_at: 2026-11-04 10:43:30
---

# BƯỚC 5.3: Tái Sử Dụng WriteableBitmap Pool Trong RemoteScreenViewModel.cs

## 1. Mục tiêu
- Triệt tiêu hoàn toàn 157 MB/giây rác cấp phát bộ nhớ native và LOH do `new WriteableBitmap` mỗi frame.
- Thiết lập cơ chế Ping-Pong Bitmap Recycling:
  - Tái sử dụng bitmap cũ thông qua `_recycledBitmap`.
  - Khi kích thước màn hình thay đổi (Resize/Monitor switch), tự động hủy và cấp phát lại đúng kích thước mới.
  - Sau 2 frame đầu tiên, số lượng cấp phát mới giảm về 0 B/giây.

## 2. Kết quả thực tế
- Đã triển khai slot tái sử dụng `_recycledBitmap` với cơ chế lock-free swap `Interlocked.Exchange`.
- Kết hợp với SKCodec direct memory decoding: giải mã JPEG trực tiếp vào con trỏ pixel framebuffer của WriteableBitmap.
- Đã giải phóng hoàn toàn GC áp lực trên CCU UI: không còn cấp phát mới mỗi frame, triệt tiêu vi giật (micro-stutter).
