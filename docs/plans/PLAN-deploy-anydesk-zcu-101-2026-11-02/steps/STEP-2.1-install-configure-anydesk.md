---
step: 2.1
plan: ../PLAN-MASTER.md
agent: devops-engineer
status: done
completed_at: 2026-11-02 14:48
deps: [1.1]
---

# STEP 2.1 — Cài đặt & Cấu hình AnyDesk trên 192.168.21.101

## Input nhận
- Thông số môi trường từ Step 1.1: `Ubuntu 22.04.5 LTS amd64`, Display X11 `:0` (LXQt, `sddm` autologin user `zcu`), `/home/zcu/.Xauthority`.
- Yêu cầu cấu hình Unattended Access Password: `Kztek@123456`.

## Nhiệm vụ
Cài đặt phần mềm AnyDesk chính thức lên thiết bị `192.168.21.101`, kích hoạt dịch vụ `anydesk.service` chạy nền, thiết lập mật khẩu truy cập không giám sát (`echo "Kztek@123456" | sudo anydesk --set-password`), và lấy mã AnyDesk ID (`anydesk --get-id`) cùng trạng thái kết nối máy chủ (`anydesk --get-status`).

## Definition of Done
- [x] Cài đặt thành công gói `anydesk` và các thư viện phụ thuộc (bản `8.1.0 amd64` từ repo chính thức AnyDesk, cài thẳng qua `apt-get` không thiếu dependency).
- [x] Dịch vụ `anydesk` ở trạng thái `active (running)` và `enabled`.
- [x] Thiết lập thành công mật khẩu Unattended Access `Kztek@123456`.
- [x] Lấy được mã AnyDesk ID (9-10 chữ số) và trạng thái `online` (`1457821647`, `online`).

## Đã làm
1. **Thêm APT Repository chính thức của AnyDesk**:
   - Tải GPG key từ `https://keys.anydesk.com/repos/DEB-GPG-KEY` lưu vào `/etc/apt/keyrings/keys.anydesk.com.asc` (quyền `0644`).
   - Tạo `/etc/apt/sources.list.d/anydesk-stable.list` với nội dung `deb [signed-by=/etc/apt/keyrings/keys.anydesk.com.asc] http://deb.anydesk.com/ all main`.
2. **Cài đặt gói `anydesk`**:
   - Chạy `apt-get update && apt-get install -y anydesk` cài đặt thành công phiên bản **AnyDesk `8.1.0` (`amd64`)**. Bản 8.1.0 đã loại bỏ phụ thuộc cũ `libgtkglext1` / `libpangox-1.0-0` nên tương thích hoàn toàn với Ubuntu 22.04 LTS.
3. **Cấu hình dịch vụ & Mật khẩu Unattended Access**:
   - Kích hoạt và khởi động lại `anydesk.service` (`systemctl enable anydesk && systemctl restart anydesk`).
   - Đặt mật khẩu truy cập tự động: `echo 'Kztek@123456' | sudo anydesk --set-password`.
   - Kiểm tra `/etc/anydesk/service.conf` và `/etc/anydesk/system.conf`: xác nhận `ad.anynet.pwd_hash` và `ad.security.permission_profiles._unattended_access.pwd` (`44c11acff4f39e7bcb4dd83dc16185f8413e45f46f8f7b0e03ad0c8ccc364307`) cùng salt (`4a5b3e08251de9a539ce5311a76379a8`) đã được ghi nhận đầy đủ.
4. **Xác minh trạng thái kết nối, X11 session và cổng mạng**:
   - **AnyDesk Version**: `8.1.0`
   - **AnyDesk ID**: `1457821647` (10 chữ số)
   - **AnyDesk Status**: `online` (đã kết nối relay `relay-036452cf.net.anydesk.com:80:443:6568`)
   - **Tiến trình hoạt động**:
     - `root`: `/usr/bin/anydesk --service` (PID `3245`)
     - `zcu`: `/usr/bin/anydesk --tray` (PID `3255`, gắn với `DISPLAY=:0`, `XAUTHORITY=/home/zcu/.Xauthority`, `XDG_CURRENT_DESKTOP=LXQt`)
   - **Cổng lắng nghe LAN Direct Connection**:
     - TCP `0.0.0.0:7070` và `[::]:7070` (`LISTEN`)
     - UDP `0.0.0.0:50001` (AnyDesk Discovery trong LAN)
   - **Firewall**: `ufw` đang `inactive` và `iptables` để policy `ACCEPT` toàn bộ nên kết nối trực tiếp qua IP LAN `192.168.21.101:7070` hoặc qua AnyDesk ID `1457821647` đều thông suốt.

## Artifact
- Script triển khai tự động: `C:\Users\tam01\.gemini\antigravity\brain\c5905275-fa6c-40fe-bc54-8e152402e5b9\scratch\install_anydesk_101.py`
- Cấu hình AnyDesk trên máy đích `192.168.21.101`:
  - `/etc/apt/keyrings/keys.anydesk.com.asc`
  - `/etc/apt/sources.list.d/anydesk-stable.list`
  - `/etc/anydesk/service.conf` & `/etc/anydesk/system.conf`
  - `/etc/systemd/system/anydesk.service` (`enabled`, `active (running)`)

## Quyết định quan trọng
- Sử dụng chuẩn APT Keyring mới (`/etc/apt/keyrings/keys.anydesk.com.asc` với `signed-by`) thay vì `apt-key` đã deprecated trên Ubuntu 22.04.
- Xác nhận cả 2 tiến trình `--service` (root) và `--tray` (user `zcu` trên `DISPLAY=:0`) đều đang chạy ổn định, đảm bảo khi kết nối từ xa sẽ hiển thị đúng màn hình desktop LXQt hiện tại.

## Handoff Payload — bước sau đọc phần này
- do_not_redo:
  - Không cần cài đặt lại hay đặt lại mật khẩu AnyDesk trên `192.168.21.101`.
- watch_out:
  - Tiến trình AnyDesk đang lắng nghe cả qua Cloud (`ID: 1457821647`, status `online`) và qua kết nối trực tiếp LAN (`192.168.21.101:7070`).
  - Step 3.1 đã được đánh dấu Skipped theo yêu cầu của User (chỉ cài AnyDesk).
- next_inputs:
  - **Host IP**: `192.168.21.101` (Hostname: `zcu-ibox205`)
  - **AnyDesk ID**: `1457821647`
  - **AnyDesk Unattended Password**: `Kztek@123456`
  - **AnyDesk Version**: `8.1.0` (amd64)
  - **AnyDesk Server Status**: `online` (Relay: `relay-036452cf.net.anydesk.com`)
  - **Direct LAN Connection**: `192.168.21.101` (TCP Port `7070`, UDP Discovery Port `50001`)
  - **Display Session**: X11 `DISPLAY=:0` (LXQt Desktop, user `zcu`)
  - **SSH Credentials**: `zcu` / `123456` (Port `22`)

## Commit
- Hash: `6934b57`
- Đã push: Không
