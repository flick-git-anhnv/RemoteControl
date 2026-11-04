---
step: 5.1
name: kill-duplicate-app-and-guard
phase: 5
status: completed
agent: devops-engineer
created: 2026-11-04
updated: 2026-11-04
completed_at: 2026-11-04 10:35:00
---

# BƯỚC 5.1: Diệt Tiến Trình Trùng Lặp IPGSv4 & Chặn Mở Đè Từ Desktop

## 1. Mục tiêu
- Diệt tiến trình `IPGSv4` phụ (PID 3658 do double-click từ Desktop), giải phóng ngay ~100% CPU và 1.1 GB RAM trên máy đích `192.168.21.56`.
- Duy trì nguyên vẹn tiến trình kiosk chính (PID 3126 / 1108 do watchdog systemd `ipgs-kiosk-app.service` quản lý).
- Cập nhật Desktop shortcut `~/Desktop/kztek-ipgsv4.desktop` hoặc tạo launcher script an toàn: nếu `pgrep -f IPGSv4` đã chạy thì không mở thêm tiến trình mới.

## 2. Thực hiện & Kết quả đo đạc thực tế
- Đã kill tiến trình PID 3658 thành công.
- Tạo wrapper script an toàn `/home/ccu/bin/launch-ipgsv4.sh`: kiểm tra `pgrep -f IPGSv4`, nếu đang chạy thì bỏ qua, nếu chưa chạy mới thực thi `/opt/kztek/ipgsv4/IPGSv4`.
- Cập nhật `~/Desktop/kztek-ipgsv4.desktop`: `Exec=/home/ccu/bin/launch-ipgsv4.sh`.
- Đo lường sau khi áp dụng:
  - CPU idle từ 0% tăng vọt lên 40.7% - 67.5% idle.
  - Load average giảm từ 3.66 xuống 1.14 - 1.55.
  - Bộ nhớ RAM trống tăng thêm 1.3 GB (free 2.3 GB).
  - Không còn hiện tượng 2 tiến trình IPGSv4 giành giật GPU và CPU.
