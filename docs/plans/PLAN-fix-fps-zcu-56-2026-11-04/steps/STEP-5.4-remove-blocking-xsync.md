---
step: 5.4
name: remove-blocking-xsync
phase: 5
status: done
agent: senior-developer
created: 2026-11-04
updated: 2026-11-04
completed_at: 2026-11-04 10:44:00
---

# BƯỚC 5.4: Loại Bỏ Lệnh Chặn Đồng Bộ XSync Trong MouseInjector.Button

## 1. Mục tiêu
- Loại bỏ `X11.XSync(_display, false);` trong `MouseInjector.Button(MouseButton button, bool isDown)`.
- Giữ nguyên `X11.XFlush(_display);` để gửi lệnh tới Xorg server ngay lập tức mà không chặn luồng chờ round-trip xác nhận.
- Giải quyết hiện tượng giật/đơ khi click liên tục hoặc cuộn chuột nhanh.

## 2. Kết quả thực tế
- Đã loại bỏ hoàn toàn `X11.XSync` trong `MouseInjector.Button` (giữ nguyên `X11.XFlush`), đồng bộ với cơ chế bất đồng bộ của `Move()`.
- Loại bỏ toàn bộ round-trip chặn luồng đến X server khi người dùng click chuột hoặc cuộn con lăn (loại bỏ 20 lần XSync / 10 nấc cuộn).
- Thao tác chuột và cuộn trang trên máy con diễn ra mượt mà và tức thời.
