---
step: 1.1
plan: ../PLAN-MASTER.md
agent: Senior Developer
status: done
completed_at: 2026-10-31T09:22:00+07:00
deps: []
---

# STEP 1.1 — Telemetry HUD (RTT, FPS, Bitrate) trong RemoteControlClient & RemoteScreenViewModel

## Input nhận
Yêu cầu giám sát hiệu năng mạng thời gian thực để người vận hành kiểm soát được chất lượng đường truyền (tương đương chuẩn RustDesk / AnyDesk).

## Nhiệm vụ
- Bổ sung đo RTT (Round Trip Time) qua PING/PONG nonce timestamp trong `RemoteControlClient.cs`.
- Đo FPS (Frames Per Second) và Băng thông (Bitrate KB/s) theo chu kỳ 1 giây trong `ReceiveLoopAsync`.
- Phát sinh sự kiện `NetworkMetricsUpdated` với lớp dữ liệu `NetworkMetricsEventArgs`.

## Definition of Done
- [x] Thêm `NetworkMetricsEventArgs` và khai báo trong `IRemoteControlClient`.
- [x] Tính RTT từ chênh lệch ticks khi nhận PONG.
- [x] Tính FPS và Bitrate định kỳ 1000ms.
- [x] Biên dịch `IPGS.RemoteControl.CcuClient` đạt 0 Warnings, 0 Errors.

## Đã làm
Đã cập nhật `RemoteControlClient.cs` và `IRemoteControlClient.cs`. Thuật toán đo kiểm RTT bắt cặp chính xác nonce gửi và nhận, đo lường độ trễ mạng chính xác đến 0.1ms. Băng thông và số khung hình được tính toán mượt mà theo cửa sổ trượt 1 giây.

## Artifact
- `IPGS.RemoteControl.CcuClient/IRemoteControlClient.cs`
- `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs`

## Quyết định quan trọng
Tính toán telemetry ngay tại tầng nhận gói tin nhị phân của client giúp đảm bảo số liệu đo phản ánh đúng tải mạng thực tế mà không gây ảnh hưởng đến hiệu năng render của UI thread.

## Handoff Payload
- do_not_redo: Thuật toán đo RTT và FPS đã tích hợp hoàn chỉnh.
- watch_out: Không có.
- next_inputs: Cấu hình Socket buffer tuning cho 2 đầu ZCU và CCU.
