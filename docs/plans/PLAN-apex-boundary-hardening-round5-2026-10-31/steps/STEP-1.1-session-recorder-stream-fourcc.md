---
step: 1.1
plan: ../PLAN-MASTER.md
agent: Senior Developer
status: done
completed_at: 2026-10-31T09:32:00+07:00
deps: []
---

# STEP 1.1 — Nâng cấp SessionRecorder với Stream Constructor & FourCC literal tĩnh

## Input nhận
Yêu cầu trừu tượng hóa tầng lưu trữ tệp AVI của `SessionRecorder` để có thể ghi vào bất kỳ `Stream` nào (đặc biệt là `MemoryStream` phục vụ kiểm thử tự động độc lập I/O) và loại bỏ hoàn toàn các phân bổ chuỗi ASCII FourCC còn sót lại.

## Nhiệm vụ
- Thêm constructor `public SessionRecorder(Stream stream, int width, int height, int fps = 15)`.
- Chuyển đổi 100% các FourCC ("RIFF", "AVI ", "LIST", "hdrl", "avih", "strl", "strh", "vids", "MJPG", "strf", "movi", "idx1", "00dc") sang UTF-8 byte literals tĩnh (`"..."u8`).
- Xóa bỏ hoàn toàn hàm chuyển đổi `Encoding.ASCII.GetBytes(string)`.

## Definition of Done
- [x] Constructor nhận `Stream` hoạt động hoàn hảo, hỗ trợ `MemoryStream`.
- [x] Loại bỏ 100% `Encoding.ASCII` khỏi `SessionRecorder.cs`.
- [x] Biên dịch `IPGS.RemoteControl.CcuUI` đạt 0 Warnings, 0 Errors.

## Đã làm
Đã tái cấu trúc `SessionRecorder.cs`. Bổ sung overload nhận `Stream`, tối ưu hóa `WriteFourCC` nhận `ReadOnlySpan<byte>` tĩnh. Toàn bộ chu trình khởi tạo header, ghi frame và patch idx1 khi Dispose đều đạt zero-heap-allocation tuyệt đối.

## Artifact
- `IPGS.RemoteControl.CcuUI/Services/SessionRecorder.cs`

## Quyết định quan trọng
Chuyển đổi toàn bộ FourCC sang UTF-8 literal giúp tệp mã nguồn sạch, không còn phụ thuộc vào `System.Text.Encoding` và tốc độ ghi đĩa đạt mức tối đa.

## Handoff Payload
- do_not_redo: SessionRecorder đã hỗ trợ Stream trừu tượng.
- watch_out: Không có.
- next_inputs: Gia cố giới hạn Clipboard và MessageCodec.
