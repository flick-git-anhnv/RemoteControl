---
step: 2.2
plan: ../PLAN-MASTER.md
agent: Junior Developer
status: done
completed_at: 2026-10-31T09:34:15+07:00
deps: [2.1]
---

# STEP 2.2 — Mở rộng kiểm thử ranh giới cho MessageCodec và Clipboard

## Input nhận
Cần bổ sung các bài kiểm tra ranh giới khắt khe đối với giải mã frame JPEG, phản hồi AuthFail, thông điệp Unicode tiếng Việt và ngưỡng cắt clipboard 1MB.

## Nhiệm vụ
- Bổ sung 5 bài kiểm thử mới trong `MessageCodecTests.cs`:
  - `DecodeFrameJpeg_ValidPayload_DecodesProperly`: Kiểm tra parse đầy đủ FrameId, Timestamp, Width, Height, JpegData.
  - `DecodeFrameJpeg_ExcessiveDimensions_ThrowsProtocolException`: Kiểm tra quăng ngoại lệ khi Width > 8192.
  - `DecodeAuthFail_StandardAndTruncated_ReturnsExpectedStrings`: Kiểm tra parse lý do lỗi xác thực.
  - `EncodeDecode_StringMessage_RoundtripsUnicodeCorrectly`: Kiểm tra chuỗi tiếng Việt có dấu.
  - `MaxClipboardTextLength_IsEnforcedAtOneMegabyte`: Xác thực hằng số an toàn 1MB.

## Definition of Done
- [x] Nâng tổng số bài kiểm tra trong `MessageCodecTests.cs` từ 13 lên 18 tests.
- [x] Nâng tổng số tests trong giải pháp lên đúng **75 tests passed 100%**.

## Đã làm
Đã cập nhật `MessageCodecTests.cs` với 5 bài kiểm tra ranh giới dữ liệu chi tiết. Toàn bộ 75 ca kiểm thử trong hệ thống đều đỗ 100%.

## Artifact
- `IPGS.RemoteControl.Tests/MessageCodecTests.cs`

## Quyết định quan trọng
Chốt chặn kiểm thử tự động tại các trường hợp ranh giới (edge cases) đảm bảo giải pháp không bao giờ bị crash bởi các dữ liệu mạng bất thường.

## Handoff Payload
- do_not_redo: Test suite ranh giới đã hoàn tất.
- watch_out: Không có.
- next_inputs: QA Engineer chạy kiểm thử tổng hợp và xác thực clean build.
