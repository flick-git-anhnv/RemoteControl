---
step: 2.1
plan: ../PLAN-MASTER.md
agent: Senior Developer
status: done
completed_at: 2026-10-31T09:23:30+07:00
deps: [1.2]
---

# STEP 2.1 — Tích hợp HUD Badge & Auto-Reconnect UX trên RemoteScreenWindow.axaml

## Input nhận
Dữ liệu telemetry đo kiểm hiệu năng mạng đã được Client phát sinh qua event `NetworkMetricsUpdated`. Cần đưa lên giao diện hiển thị cho người dùng một cách trực quan, tinh gọn.

## Nhiệm vụ
- Thêm thuộc tính `NetworkHudText` và `IsHudVisible` trong `RemoteScreenViewModel.cs`.
- Thêm Badge HUD trên thanh công cụ `RemoteScreenWindow.axaml` hiển thị FPS, RTT và Bitrate với Tooltip chi tiết.
- Cải thiện trạng thái reconnect hiển thị rõ ràng số lần retry và thời gian chờ.

## Definition of Done
- [x] ViewModel subscribe sự kiện và cập nhật định kỳ lên luồng UI qua `Dispatcher.UIThread.Post`.
- [x] Thêm UI Border Badge trên `RemoteScreenWindow.axaml` cạnh TLS badge.
- [x] Biên dịch `IPGS.RemoteControl.CcuUI` đạt 0 Warnings, 0 Errors.

## Đã làm
Đã cập nhật `RemoteScreenViewModel.cs` tự động định dạng tốc độ truyền tải thành KB/s hoặc MB/s. Đã bổ sung control `Border` màu tối bo góc với text block hiển thị `⚡ FPS | 📶 RTT | 📊 Bitrate` trên Toolbar của `RemoteScreenWindow.axaml`.

## Artifact
- `IPGS.RemoteControl.CcuUI/ViewModels/RemoteScreenViewModel.cs`
- `IPGS.RemoteControl.CcuUI/Views/RemoteScreenWindow.axaml`

## Quyết định quan trọng
Ẩn Badge khi mất kết nối (`IsHudVisible = false`) để tránh hiển thị thông tin rác của phiên cũ.

## Handoff Payload
- do_not_redo: Giao diện HUD đã hoàn thiện.
- watch_out: Không có.
- next_inputs: Viết unit tests cho `ComputerStatusChecker` và `ZcuAgentDiscoveryService`.
