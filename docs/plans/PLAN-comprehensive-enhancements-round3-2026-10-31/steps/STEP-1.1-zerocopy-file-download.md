---
step: 1.1
plan: ../PLAN-MASTER.md
agent: Senior Developer
status: done
completed_at: 2026-10-31T09:10:00+07:00
deps: []
---

# STEP 1.1 — Zero-Copy Chunk Streaming trong FileTransferHandler và ClientSession

## Input nhận
Yêu cầu tối ưu hiệu năng I/O tải tệp từ ZCU Agent sang CCU Client, loại bỏ việc cấp phát mảng byte tạm `ToArray()` ở mỗi chunk truyền tệp.

## Nhiệm vụ
Nâng cấp `ClientSession.WriteAsync` để hỗ trợ nạp trực tiếp `ReadOnlyMemory<byte>` thay vì chỉ nhận `byte[]`, và tái cấu trúc `FileTransferHandler.HandleDownload` sử dụng `buf.AsMemory(0, 1 + n)` nhằm stream chunk zero-copy.

## Definition of Done
- [x] Thêm overload `WriteAsync(MessageType type, ReadOnlyMemory<byte> payload, CancellationToken ct)` trong `ClientSession.cs`.
- [x] Thay thế phân bổ `buf.Take(1 + n).ToArray()` bằng `buf.AsMemory(0, 1 + n)` trong `FileTransferHandler.cs`.
- [x] Biên dịch `IPGS.RemoteControl.ZcuAgent` thành công không có lỗi hoặc cảnh báo.

## Đã làm
Đã triển khai overload mới cho `ClientSession.WriteAsync` nhận `ReadOnlyMemory<byte>`, ghi trực tiếp header 5 bytes và payload qua `Stream.WriteAsync(ReadOnlyMemory<byte>)` trên luồng socket. Trong `FileTransferHandler.HandleDownload`, đã tái sử dụng mảng chunk 64KB và slice `ReadOnlyMemory<byte>` không tạo bản sao bộ nhớ.

## Artifact
- `IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs`
- `IPGS.RemoteControl.ZcuAgent/Net/FileTransferHandler.cs`

## Quyết định quan trọng
Sử dụng `ReadOnlyMemory<byte>` thay vì `ArraySegment<byte>` vì tương thích tự nhiên với các API async của .NET Socket/Stream và Skia memory slicing.

## Handoff Payload
- do_not_redo: Overload `ClientSession.WriteAsync` và logic chunk slice trong `FileTransferHandler` đã hoàn thành.
- watch_out: Cần lưu ý byte đầu tiên của buffer là transferId, các byte sau là dữ liệu tệp thô.
- next_inputs: Mã nguồn đã sẵn sàng cho tối ưu hóa `SessionRecorder`.
