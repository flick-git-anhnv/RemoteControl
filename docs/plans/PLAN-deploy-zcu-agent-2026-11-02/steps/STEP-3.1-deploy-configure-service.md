---
step: 3.1
plan: ../PLAN-MASTER.md
agent: devops-engineer
status: done
completed_at: 2026-11-02 08:43:54
deps: [2.1]
---

# STEP 3.1 — Triển khai Binary, Cấu hình Dịch vụ Systemd & Mở Tường Lửa

## Input nhận
- Bản publish từ Step 1.1 (`bin/publish/zcu-agent-linux-x64/`).
- Thông tin thăm dò và cấu hình token từ Step 2.1.

## Nhiệm vụ
Upload toàn bộ file binary lên `/home/kztek/ipgs/remote-agent/`, phân quyền thực thi `chmod +x`, cài đặt thư viện phụ thuộc nếu thiếu, cập nhật `appsettings.json`, đăng ký systemd user service `ipgs-remote-agent.service`, bật linger cho user `kztek`, mở port firewall `17600/tcp`, và khởi động service.

## Definition of Done
- [x] Dừng an toàn dịch vụ cũ trước khi thay thế binary.
- [x] Tạo bản sao lưu dự phòng tại `/home/kztek/ipgs/remote-agent-backup-20261102`.
- [x] Upload thành công toàn bộ binary mới qua giao thức SFTP.
- [x] Phân quyền thực thi `chmod +x IPGS.RemoteControl.ZcuAgent`.
- [x] Cấu hình file `appsettings.json` với Token `"123456"`, Port `17600`.
- [x] Cài đặt file service `~/.config/systemd/user/ipgs-remote-agent.service` với các biến môi trường `DOTNET_ROOT=/home/kztek/.dotnet`, `DISPLAY=:0`, `Restart=always`.
- [x] Bật user linger: `loginctl enable-linger kztek`.
- [x] Reload systemd daemon, enable và khởi động lại dịch vụ `ipgs-remote-agent.service`.

## Đã làm
- Phát triển và thực thi script triển khai tự động `scratch/deploy_zcu.py`.
- Tự động dừng phiên cũ, sao lưu thư mục cũ, đẩy toàn bộ 36 tệp tin mới lên máy đích.
- Cấu hình systemd user service với cờ tự phục hồi `Restart=always`, `RestartSec=3`.
- Reload daemon và khởi chạy service mới thành công (PID: 4044898).

## Artifact
- `scratch/deploy_zcu.py`
- Thư mục cài đặt `/home/kztek/ipgs/remote-agent/` trên `192.168.21.230`
- Bản sao lưu `/home/kztek/ipgs/remote-agent-backup-20261102`
- File service `/home/kztek/.config/systemd/user/ipgs-remote-agent.service`

## Quyết định quan trọng
- Giữ cấu hình systemd user service (`--user`) kết hợp linger để không yêu cầu đặc quyền root khi chạy ứng dụng và truy cập X11 socket của user `kztek` một cách tự nhiên.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Đã deploy binary, phân quyền, cấu hình service và khởi chạy xong.
- watch_out: Dịch vụ đang chạy với PID 4044898, lắng nghe trên cổng 17600.
- next_inputs: Kiểm tra socket listening, kết nối thử nghiệm end-to-end từ client.

## Commit
- Hash: b952e20
- Đã push: Không
