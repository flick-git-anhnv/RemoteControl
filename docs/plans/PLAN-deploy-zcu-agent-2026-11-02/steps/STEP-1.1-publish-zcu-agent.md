---
step: 1.1
plan: ../PLAN-MASTER.md
agent: devops-engineer
status: done
completed_at: 2026-11-02 08:43:12
deps: []
---

# STEP 1.1 — Biên dịch Release linux-x64 cho IPGS.RemoteControl.ZcuAgent

## Input nhận
- Mã nguồn mới nhất của dự án `IPGS.RemoteControl.ZcuAgent/IPGS.RemoteControl.ZcuAgent.csproj` trên nhánh `feature/remote-improve-rustdesk`.
- Toàn bộ 42 hạng mục cải tiến hiệu năng qua 7 vòng (Zero-Allocation frame streaming, Ping-pong double buffering, X11DisplayResolver, DPMS anti-blanking, socket buffer tuning).

## Nhiệm vụ
Biên dịch dự án ZcuAgent sang cấu hình Release cho nền tảng `linux-x64` và xuất ra thư mục publish chuẩn bị triển khai.

## Definition of Done
- [x] Lệnh `dotnet publish` chạy thành công với 0 Errors.
- [x] Thư mục `bin/publish/zcu-agent-linux-x64/` chứa đầy đủ file thực thi `IPGS.RemoteControl.ZcuAgent`, `appsettings.json`, các thư viện SkiaSharp và dependencies.

## Đã làm
- Chạy lệnh `dotnet publish IPGS.RemoteControl.ZcuAgent/IPGS.RemoteControl.ZcuAgent.csproj -c Release -r linux-x64 --self-contained false -o bin/publish/zcu-agent-linux-x64`.
- Quá trình biên dịch thành công 100%, sinh ra file thực thi và các thư viện phụ thuộc (`IPGS.RemoteControl.ZcuAgent.dll` kích thước 133,120 bytes, tích hợp toàn bộ codec tối ưu).

## Artifact
- `bin/publish/zcu-agent-linux-x64/` (36 files bao gồm `IPGS.RemoteControl.ZcuAgent`, `libSkiaSharp.so`, `SkiaSharp.dll`, `appsettings.json`).

## Quyết định quan trọng
- Sử dụng `--self-contained false` vì máy đích Ubuntu đã có sẵn .NET 8.0 Runtime tại `/home/kztek/.dotnet` (tiết kiệm dung lượng và thời gian upload).

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Đã biên dịch xong bản publish Release linux-x64, không cần publish lại.
- watch_out: Bản publish yêu cầu .NET 8 runtime có sẵn trên máy đích hoặc đặt `DOTNET_ROOT=/home/kztek/.dotnet`.
- next_inputs: Toàn bộ thư mục `bin/publish/zcu-agent-linux-x64/`.

## Commit
- Hash: b952e20 (baseline commit)
- Đã push: Không
