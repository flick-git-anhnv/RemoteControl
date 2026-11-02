---
step: 2.1
plan: ../PLAN-MASTER.md
agent: Junior Developer
status: done
completed_at: 2026-10-31T09:33:45+07:00
deps: [1.2]
---

# STEP 2.1 — Xây dựng bộ Unit Test tự động cho SessionRecorder trong RAM

## Input nhận
`SessionRecorder` đã hỗ trợ constructor nhận `Stream`, cho phép kiểm thử toàn diện trên bộ nhớ RAM mà không tạo file rác trên ổ đĩa cứng.

## Nhiệm vụ
- Thêm tham chiếu `CcuUI` vào dự án `IPGS.RemoteControl.Tests`.
- Tạo file kiểm thử mới `SessionRecorderTests.cs` kiểm tra:
  - Khởi tạo hợp lệ với `MemoryStream` và kích thước màn hình.
  - Quăng `ArgumentNullException` khi stream null.
  - Ghi khung hình có số byte lẻ được căn lề chẵn với byte pad = 0.
  - Khi Dispose, patch chính xác số lượng frame tại offset 48 và 140, đồng thời ghi cấu trúc chỉ mục `idx1`.
  - Guard thread-safe sau khi Dispose hoạt động êm dịu, không ném exception.

## Definition of Done
- [x] Tạo `SessionRecorderTests.cs` gồm 5 test cases chi tiết.
- [x] Toàn bộ các test cases đều đỗ 100%.

## Đã làm
Đã tạo tệp `SessionRecorderTests.cs` và cấu hình tham chiếu dự án thành công. Toàn bộ 5 bài kiểm tra đều vượt qua, xác thực tính chuẩn xác của cấu trúc file video AVI do hệ thống tạo ra.

## Artifact
- `IPGS.RemoteControl.Tests/SessionRecorderTests.cs`
- `IPGS.RemoteControl.Tests/IPGS.RemoteControl.Tests.csproj`

## Quyết định quan trọng
Kiểm thử bộ ghi hình hoàn toàn trên `MemoryStream` giúp tốc độ chạy test đạt cực nhanh (dưới 50ms) và không phụ thuộc vào quyền ghi đĩa của hệ thống.

## Handoff Payload
- do_not_redo: Test suite cho SessionRecorder đã hoàn thiện.
- watch_out: Không có.
- next_inputs: Bổ sung test kiểm thử ranh giới cho `MessageCodec`.
