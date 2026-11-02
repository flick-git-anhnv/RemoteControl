---
step: 1.1
plan: ../PLAN-MASTER.md
agent: devops-engineer
status: done
completed_at: 2026-11-02 14:42
deps: []
---

# STEP 1.1 — Kết nối SSH Thăm dò Môi trường Thiết bị 192.168.21.101

## Input nhận
- Thông tin SSH: IP `192.168.21.101`, User `zcu`, Password `123456`, Port `22`.

## Nhiệm vụ
Kết nối SSH tới máy đích `192.168.21.101` để thăm dò thông số hệ điều hành, kiến trúc CPU, kết nối Internet (để tải AnyDesk), phiên bản .NET runtime, trạng thái Display X11, các thư viện đồ họa (`libx11-6`, `libxext6`, `libxtst6`, `xclip`), và kiểm tra xem AnyDesk hay ZCU Agent đã được cài đặt trước đó chưa.

## Definition of Done
- [x] Kết nối SSH thành công tới `192.168.21.101` với user `zcu`.
- [x] Xác định hệ điều hành, phiên bản distro và kiến trúc CPU (`uname -m`).
- [x] Kiểm tra kết nối Internet từ thiết bị (`curl -I https://keys.anydesk.com` hoặc `deb.anydesk.com`).
- [x] Kiểm tra trạng thái X11 Display (`DISPLAY`, `/tmp/.X11-unix/`), `XDG_SESSION_TYPE` và desktop environment.
- [x] Kiểm tra trạng thái .NET 8 runtime và dịch vụ `ipgs-remote-agent` / `anydesk` hiện tại.

## Đã làm
- Viết và thực thi script thăm dò `probe_101.py` qua `paramiko` kết nối SSH tới `zcu@192.168.21.101:22`.
- Thu thập đầy đủ thông tin hệ thống trên `192.168.21.101`:
  - **Hostname & OS**: `zcu-ibox205` — `Ubuntu 22.04.5 LTS (Jammy Jellyfish)`, Kernel `6.8.0-138-generic`, kiến trúc `amd64` (`x86_64`), RAM `3.8 GB` (trống ~3.0 GB), ổ cứng `117 GB` (trống `99 GB`).
  - **Kết nối Internet & Công cụ tải**: Có kết nối Internet ổn định (`ping 8.8.8.8` đạt `50.7 ms`, `wget --spider https://keys.anydesk.com/repos/DEB-GPG-KEY` trả về OK). Máy có sẵn `/usr/bin/wget`, `/usr/bin/gpg`, `/usr/bin/apt-get` (chưa cài `curl`).
  - **X11 / Display / Session**:
    - Display Manager: `sddm` đang chạy với chế độ autologin cho user `zcu` vào môi trường desktop `LXQt` (`/usr/bin/startlxqt`).
    - Socket X11: `/tmp/.X11-unix/X0` đang hoạt động (`DISPLAY=:0`, `Type=x11`, `Active=yes`, `State=active` trên `seat0`, độ phân giải ảo hiện tại `1024x768` headless).
    - Xauthority: `/home/zcu/.Xauthority` (của user `zcu`) và `/var/run/sddm/{23d5d0af-111a-4698-b8b4-88497abd133b}` (của `sddm`).
  - **Trạng thái AnyDesk**: Chưa được cài đặt (`which anydesk` rỗng, `anydesk.service` chưa tồn tại).
  - **Các thư viện phụ thuộc**: Đã cài sẵn `libx11-6`, `libxext6`, `libxtst6`, `libxcb-shm0`, `libminizip1`, `xclip`.
  - **Trạng thái .NET & ZCU Agent**:
    - Đã có `.NET 8.0.20` tại `/home/zcu/.dotnet`.
    - Đang có bản cũ của `IPGS.RemoteControl.ZcuAgent` (từ ngày `16/09`) tại `/home/zcu/ipgs/remote-agent/`, chạy dưới dạng **systemd user service** (`~/.config/systemd/user/ipgs-remote-agent.service`, PID 1107, lắng nghe port `17600`, Token `123456`). Bản cũ đang phát sinh cảnh báo `[X11Error] error_code=128 request_code=130 minor_code=2` và chưa có các cải tiến mới nhất (G009–G017).
  - **Mạng & Firewall**: Interface `enp2s0` có 2 IP (`192.168.21.101/24` và `192.168.1.82/24`). `ufw` đang `inactive`.

## Artifact
- `C:\Users\tam01\.gemini\antigravity\brain\c5905275-fa6c-40fe-bc54-8e152402e5b9\scratch\probe_101.py`

## Quyết định quan trọng
- **Cài đặt AnyDesk (Step 2.1)**: Sử dụng `wget` (vì máy chưa có `curl`) để tải GPG key và thêm APT repository chính thức của AnyDesk (`http://deb.anydesk.com/ all main`) hoặc tải trực tiếp gói `.deb` `amd64` và cài đặt bằng `apt-get install -y`, sau đó cấu hình mật khẩu Unattended Access `Kztek@123456` qua `echo 'Kztek@123456' | sudo anydesk --set-password`.
- **Triển khai ZCU Agent (Step 3.1)**: Dừng user service hiện tại (`systemctl --user stop ipgs-remote-agent`), cập nhật bản build mới nhất của `IPGS.RemoteControl.ZcuAgent` vào `/home/zcu/ipgs/remote-agent/`, giữ nguyên cấu hình `Port: 17600` và `Token: "123456"`, khởi động lại service và bật `loginctl enable-linger zcu` để đảm bảo service luôn chạy kể cả khi chưa có phiên SSH.

## Handoff Payload — bước sau đọc phần này
- do_not_redo:
  - Không cần kiểm tra lại OS, kiến trúc CPU hay kết nối mạng: máy là `Ubuntu 22.04.5 LTS amd64 (x86_64)`, có Internet đầy đủ.
  - Không cần cài lại `libx11-6`, `libxext6`, `libxtst6`, `xclip` vì đã có sẵn trên máy.
- watch_out:
  - Máy đích chưa cài `curl`, hãy dùng `wget` khi tải key/package hoặc cài thêm `curl` qua `apt-get`.
  - X11 đang chạy trên `DISPLAY=:0` với desktop `LXQt` (`sddm` autologin user `zcu`), file Xauthority của user là `/home/zcu/.Xauthority`.
  - `ipgs-remote-agent` hiện đang được quản lý bằng **systemd user service** (`~/.config/systemd/user/ipgs-remote-agent.service`) chứ không phải `/etc/systemd/system/ipgs-remote-agent.service`. Khi cập nhật ở Step 3.1 cần dừng hoặc đồng bộ đúng service này để tránh xung đột cổng `17600` (`Address already in use`).
- next_inputs:
  - Host: `192.168.21.101`, Port: `22`, User: `zcu`, Pass: `123456`, Arch: `amd64`, OS: `Ubuntu 22.04.5 LTS`.
  - AnyDesk Unattended Password cần đặt: `Kztek@123456`.

## Commit
- Hash: pending
- Đã push: Không
