---
step: 2.1
task: fix-fps-zcu-56
agent: devops-engineer
status: done
deps: [1.1]
created: 2026-11-04
completed_at: 2026-11-04 09:25:35
---

# STEP 2.1 — Triển Khai ZcuAgent Mới Lên Máy Đích 192.168.21.56

## Nhiệm vụ
Dừng `ipgs-remote-agent`, upload binary mới từ bước 1.1 lên `/home/ccu/ipgs/remote-agent/` qua SFTP, cấu hình `appsettings.json` đặt `TargetFps: 30`, `JpegQuality: 70`, `chmod +x IPGS.RemoteControl.ZcuAgent`, và khởi động lại service.

## Definition of Done
- [x] File binary trên máy đích có timestamp mới nhất (`IPGS.RemoteControl.ZcuAgent.dll` 133,632 bytes).
- [x] File `appsettings.json` có `TargetFps: 30`, `JpegQuality: 70`.
- [x] Service `ipgs-remote-agent` ở trạng thái `active (running)`.

## Đã làm
- Dừng `systemctl --user stop ipgs-remote-agent`.
- Làm sạch thư mục `/home/ccu/ipgs/remote-agent/` (loại bỏ tệp rác `libhost*`, bảo toàn cấu hình `appsettings.json` và chứng chỉ TLS).
- Đẩy binary mới đã vá G020 qua SFTP.
- Cấu hình `appsettings.json`: `TargetFps: 30`, `JpegQuality: 70`.
- Khởi động service và xác nhận running (PID 1932).

## Artifacts
- `/home/ccu/ipgs/remote-agent/` trên 192.168.21.56
- `appsettings.json` (TargetFps 30, JpegQuality 70)

## Handoff Payload
- do_not_redo: Đã deploy xong ZcuAgent mới lên máy đích.
- watch_out: Không sao chép các tệp `libhost*` vào thư mục app trên Linux vì máy đích đã có .NET runtime tại `/home/ccu/.dotnet`.
- next_inputs: "Dịch vụ đã active tại port 17600"
