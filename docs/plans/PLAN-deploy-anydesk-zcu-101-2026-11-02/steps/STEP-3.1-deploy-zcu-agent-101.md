---
step: 3.1
plan: ../PLAN-MASTER.md
agent: devops-engineer
status: skipped
completed_at: 2026-11-02 14:41
deps: [2.1]
---

# STEP 3.1 — Triển khai IPGS ZCU Remote Control Agent lên 192.168.21.101

## Input nhận
- Bản build `IPGS.RemoteControl.ZcuAgent` (`bin/publish/zcu-agent-linux-x64` hoặc publish self-contained nếu máy đích chưa có .NET 8).
- Thông tin môi trường từ Step 1.1 & 2.1.

## Nhiệm vụ
Cài đặt các gói phụ thuộc X11 (`libx11-6`, `libxext6`, `libxtst6`, `xclip`), triển khai bản build mới nhất của `IPGS.RemoteControl.ZcuAgent` lên `/home/zcu/ipgs/remote-agent/`, cấu hình `appsettings.json` (`Port: 17600`, `Token: "123456"`), tạo và kích hoạt systemd service (`ipgs-remote-agent.service`) cùng `loginctl enable-linger zcu`, và mở cổng tường lửa `17600/tcp`.

## Definition of Done
- [ ] Đảm bảo đầy đủ thư viện X11 (`libx11-6`, `libxext6`, `libxtst6`, `xclip`) và .NET runtime (hoặc bản self-contained).
- [ ] Upload đầy đủ binary `IPGS.RemoteControl.ZcuAgent` lên `/home/zcu/ipgs/remote-agent/`.
- [ ] Cấu hình `appsettings.json` với Port `17600` và Token `"123456"`.
- [ ] Dịch vụ `ipgs-remote-agent.service` hoạt động (`active (running)`) và lắng nghe trên cổng `17600`.
- [ ] Mở port `17600/tcp` trên firewall (nếu ufw active).

## Đã làm


## Artifact


## Quyết định quan trọng


## Handoff Payload — bước sau đọc phần này
- do_not_redo:
- watch_out:
- next_inputs:

## Commit
- Hash: —
- Đã push: Không
