---
step: 1.2
plan: ../PLAN-MASTER.md
agent: Senior Developer
status: done
completed_at: 2026-10-31T09:23:00+07:00
deps: [1.1]
---

# STEP 1.2 — TCP Socket Tuning trên ZCU TcpServer & CCU RemoteControlClient

## Input nhận
Streaming JPEG độ phân giải cao và inject sự kiện chuột đòi hỏi độ trễ cực thấp. Mặc định thuật toán Nagle trên Linux có thể gom gói tin và gây trễ 40ms, đồng thời buffer mặc định của hệ điều hành có thể gây nghẽn burst data khi màn hình có nhiều chuyển động.

## Nhiệm vụ
- Cấu hình `client.NoDelay = true` trên `TcpServer.cs` của ZCU Agent.
- Tối ưu hóa kích thước buffer truyền nhận: `SendBufferSize = 256 * 1024` trên ZCU và `ReceiveBufferSize = 256 * 1024` trên CCU Client.

## Definition of Done
- [x] Cấu hình `NoDelay = true` và `SendBufferSize = 256KB` trong `TcpServer.cs`.
- [x] Cấu hình `ReceiveBufferSize = 256KB` và `SendBufferSize = 64KB` trong `RemoteControlClient.cs`.
- [x] Biên dịch `IPGS.RemoteControl.ZcuAgent` và `IPGS.RemoteControl.CcuClient` đạt 0 Warnings, 0 Errors.

## Đã làm
Đã cập nhật cấu hình socket tại thời điểm chấp nhận kết nối trên ZCU Agent và lúc khởi tạo TCP client trên CCU Client. Gói tin JPEG và ACK được giải phóng ngay lập tức vào card mạng mà không bị giữ lại trong bộ đệm hệ điều hành.

## Artifact
- `IPGS.RemoteControl.ZcuAgent/Net/TcpServer.cs`
- `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs`

## Quyết định quan trọng
Mở rộng buffer lên 256KB giúp hấp thụ các burst frames của màn hình độ phân giải 1080p/4K mà không bị drop packet ở tầng kernel.

## Handoff Payload
- do_not_redo: Socket tuning đã hoàn thành.
- watch_out: Không có.
- next_inputs: Đấu nối hiển thị HUD lên giao diện `RemoteScreenWindow.axaml`.
