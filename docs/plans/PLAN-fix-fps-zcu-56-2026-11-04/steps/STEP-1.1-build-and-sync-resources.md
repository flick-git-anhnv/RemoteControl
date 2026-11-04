---
step: 1.1
task: fix-fps-zcu-56
agent: senior-developer
status: done
deps: []
created: 2026-11-04
completed_at: 2026-11-04 09:23:00
---

# STEP 1.1 — Publish Release linux-x64 & Đồng Bộ Vào Resources

## Nhiệm vụ
Biên dịch Release `linux-x64` cho `IPGS.RemoteControl.ZcuAgent` (đảm bảo code chứa bản vá G020: `nuint shmseg` trong `XShmInterop.cs` và `Marshal.AllocHGlobal` trong `X11ScreenCapturer.cs`). Copy toàn bộ artifact output vào thư mục `IPGS.RemoteControl.CcuUI/Resources/zcu-agent/linux-x64/` và các thư mục bin output của CcuUI.

## Definition of Done
- [x] `dotnet publish IPGS.RemoteControl.ZcuAgent -c Release -r linux-x64 --self-contained false` thành công 0 errors.
- [x] Thư mục `IPGS.RemoteControl.CcuUI/Resources/zcu-agent/linux-x64/` chứa `IPGS.RemoteControl.ZcuAgent.dll` mới nhất (133,632 bytes, timestamp 2026-11-04).

## Đã làm
- Chạy lệnh publish tạo output tại `bin/publish/zcu-agent-linux-x64/`.
- Đồng bộ toàn bộ các file DLL và runtime sang `IPGS.RemoteControl.CcuUI/Resources/zcu-agent/linux-x64/` và `bin/Debug/net8.0/Resources/zcu-agent/linux-x64/`.

## Artifacts
- `bin/publish/zcu-agent-linux-x64/` (36 files)
- `IPGS.RemoteControl.CcuUI/Resources/zcu-agent/linux-x64/IPGS.RemoteControl.ZcuAgent.dll` (133,632 bytes)

## Handoff Payload
- do_not_redo: Đã biên dịch xong và đồng bộ Resource, không cần publish lại.
- watch_out: Khi upload lên ZCU cần chmod +x IPGS.RemoteControl.ZcuAgent và giữ nguyên Token xác thực.
- next_inputs: "Thư mục nguồn bin/publish/zcu-agent-linux-x64/"
