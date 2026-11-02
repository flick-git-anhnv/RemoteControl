---
step: 4.1
plan: ../PLAN-MASTER.md
agent: devops-lead
status: done
completed_at: 2026-11-02 14:51
deps: [2.1]
---

# STEP 4.1 — Kiểm thử Kết nối Thực tế & Nghiệm thu (Sign-off)

## Input nhận
- AnyDesk ID & Password từ Step 2.1 (`ID: 1457821647`, Unattended Password: `Kztek@123456`, Direct LAN `192.168.21.101:7070`).

## Nhiệm vụ
Thực hiện kiểm tra nghiệm thu độc lập (Two-Eyes Principle):
1. Xác minh trạng thái dịch vụ `anydesk` (`active (running)`, `online`, AnyDesk ID hợp lệ, cấu hình Unattended Access và Direct Connection port 7070 hoạt động).
2. Kiểm tra kết nối mạng/port 7070 từ máy trạm tới `192.168.21.101` và trạng thái X11 Display để đảm bảo khi kết nối AnyDesk không gặp lỗi màn hình đen hoặc `display_server_not_supported`.
3. Tổng hợp báo cáo nghiệm thu kèm hướng dẫn và thông số đầy đủ để người dùng kiểm tra kết nối ngay.

## Definition of Done
- [x] Xác nhận AnyDesk service `active (running)`, trạng thái `online` và ID chính xác.
- [x] Kiểm tra port Direct Connection (`7070/tcp`) và cấu hình X11 Display (tránh lỗi Wayland/headless).
- [x] Cung cấp bảng thông số kết nối đầy đủ và các cách kiểm tra kết nối cho người dùng.

## Đã làm
1. **Kiểm thử kết nối TCP mạng LAN 2 chiều từ máy Windows hiện tại (`192.168.21.21`) tới `192.168.21.101:7070`**:
   - Chạy script kiểm thử độc lập `verify_anydesk_101.py`, xác nhận kết nối TCP socket tới `192.168.21.101:7070` thành công (`192.168.21.21:63436 -> 192.168.21.101:7070`).
2. **Xác minh độc lập dịch vụ AnyDesk qua SSH trên `192.168.21.101` (`zcu-ibox205`)**:
   - `anydesk --get-id` = `1457821647`
   - `anydesk --get-status` = `online`
   - `anydesk --version` = `8.1.0`
   - `systemctl is-active anydesk` = `active`, `systemctl is-enabled anydesk` = `enabled`
   - Sockets đang mở: `0.0.0.0:7070` (TCP), `[::]:7070` (TCP), `0.0.0.0:50001` (UDP LAN discovery)
   - Tiến trình đang chạy:
     - `root` (PID `3245`): `/usr/bin/anydesk --service`
     - `zcu` (PID `3255`): `/usr/bin/anydesk --tray` (gắn vào phiên X11 `DISPLAY=:0`, `seat0`)
3. **Chống màn hình đen (Anti-Blanking / Disable X11 ScreenSaver & DPMS)**:
   - Đã thực thi `DISPLAY=:0 XAUTHORITY=/home/zcu/.Xauthority xset s off -dpms s noblank`.
   - Xác nhận qua `xset q`: `prefer blanking: no`, `timeout: 0`, `DPMS is Disabled` — đảm bảo khi thiết bị không cắm màn hình HDMI hoặc để lâu không thao tác vẫn hiển thị đầy đủ desktop LXQt khi kết nối AnyDesk.

## Artifact
- Script kiểm thử nghiệm thu độc lập: `verify_anydesk_101.py`
- Bảng tổng hợp thông số kết nối và hướng dẫn kiểm tra bàn giao cho User.

## Quyết định quan trọng
- Tắt hoàn toàn X11 ScreenSaver và DPMS (`xset s off -dpms s noblank`) trên `DISPLAY=:0` để ngăn ngừa hiện tượng màn hình đen khi kết nối từ xa vào thiết bị ZCU headless.
- Ký duyệt nghiệm thu (Sign-off) hoàn tất kế hoạch `PLAN-deploy-anydesk-zcu-101-2026-11-02`.

## Handoff Payload — bước sau đọc phần này
- do_not_redo:
  - Toàn bộ quá trình cài đặt, cấu hình và nghiệm thu AnyDesk trên `192.168.21.101` đã hoàn tất 100%.
- watch_out:
  - Có thể kết nối AnyDesk bằng cả **AnyDesk ID (`1457821647`)** (qua Internet/Cloud) hoặc nhập trực tiếp **IP LAN (`192.168.21.101`)** vào ô địa chỉ AnyDesk với cùng mật khẩu `Kztek@123456`.
- next_inputs:
  - Bàn giao thông số kết nối cuối cùng cho User.

## Commit
- Hash: `7e035e8`
- Đã push: Không
