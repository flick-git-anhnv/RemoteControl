---
step: 1.2
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31T08:37:00+07:00
deps: ["1.1"]
---

# STEP 1.2 — Input Queue Optimization & Fix UI Thread

## Input nhận
- Đề xuất 1.4 và 1.5 trong [PROPOSAL-remote-control-improvements-2026-10-31.md](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md).
- Vấn đề C1, C2, C3.

## Nhiệm vụ
1. Tối ưu hóa vòng lặp inject input trong `ClientSession.cs`: loại bỏ `Task.Run` thừa thãi trên từng sự kiện chuột, triển khai coalescing di chuột (chỉ giữ lại vị trí mới nhất khi hàng đợi bận).
2. Sửa vi phạm luồng UI Thread trong `RemoteScreenControl.axaml.cs`: bọc `PasteStatus?.Invoke(null)` qua `Dispatcher.UIThread.Post`.

## Definition of Done
- [x] Loại bỏ `Task.Run` trung gian trong `RunInputInjectorLoopAsync`.
- [x] Bọc `PasteStatus` an toàn 100% trên luồng UI.
- [x] Build thành công `ZcuAgent` và `CcuUI`.

## Đã làm
1. Trong `IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs`:
   - Định nghĩa `readonly struct InputCommand` để chuyển dữ liệu input mà không sinh allocation trên GC heap.
   - Thay thế `Channel<Action>` bằng `Channel<InputCommand>` với dung lượng `BoundedChannelOptions(512)` và `BoundedChannelFullMode.DropOldest`.
   - Triển khai mouse move coalescing trong `EnqueueMouseMove`: nếu hàng đợi đang có sự kiện di chuột chưa xử lý, ghi đè tọa độ mới nhất và bỏ qua các tọa độ cũ.
   - Trong `RunInputInjectorLoopAsync`, thực thi tuần tự trực tiếp trên background thread của channel reader, loại bỏ triệt để `Task.Run` trên ThreadPool.
2. Trong `IPGS.RemoteControl.CcuUI/Views/RemoteScreenControl.axaml.cs`:
   - Bọc lệnh `PasteStatus?.Invoke(null)` bên trong `Avalonia.Threading.Dispatcher.UIThread.Post(...)` đảm bảo an toàn tuyệt đối với luồng giao diện Avalonia.

## Artifact
- [ClientSession.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs)
- [RemoteScreenControl.axaml.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuUI/Views/RemoteScreenControl.axaml.cs)

## Quyết định quan trọng
- Dùng `struct InputCommand` giúp triệt tiêu hoàn toàn closure object allocation của `Action`.
- Coalescing tọa độ chuột giải quyết dứt điểm hiện tượng chuột bị trễ (lag) tích lũy khi di chuyển nhanh.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Không can thiệp lại cơ chế Channel của ClientSession.
- watch_out: Các sự kiện phím (Key Event) không được drop; chỉ drop/coalesce sự kiện MouseMove.
- next_inputs: Cung cấp codebase sạch cảnh báo thread an toàn cho Junior Dev nâng cấp dependency.
