---
step: 1.2
plan: ../PLAN-MASTER.md
agent: Senior Developer
status: done
completed_at: 2026-10-31T09:33:00+07:00
deps: [1.1]
---

# STEP 1.2 — Clipboard Safety Shield (1MB limit) & MessageCodec Hardening

## Input nhận
Người dùng có thể sao chép văn bản kích thước cực lớn (ví dụ log file 100MB) khiến thông điệp `CLIP_DATA` vượt ngưỡng an toàn của TCP stream, gây nguy cơ Out-Of-Memory hoặc đơ socket. Đồng thời `MessageCodec.DecodeFrameJpeg` cần kiểm soát chặt chẽ biên giới kích thước màn hình để tránh crash Skia.

## Nhiệm vụ
- Thêm hằng số bảo vệ `MaxClipboardTextLength = 1024 * 1024` (1MB) trong `RemoteControlClient.cs` và tự động cắt chuỗi nếu vượt quá ngưỡng.
- Trong `MessageCodec.DecodeFrameJpeg`: Kiểm tra ràng buộc `width >= 0 && width <= 8192 && height >= 0 && height <= 8192`, văng `ProtocolException` nếu dữ liệu bất thường.

## Definition of Done
- [x] Áp dụng cắt chuỗi an toàn 1MB trong `SendClipboardTextAsync`.
- [x] Thêm bounds checking 8192px cho `DecodeFrameJpeg`.
- [x] Biên dịch `IPGS.RemoteControl.CcuClient` đạt 0 Warnings, 0 Errors.

## Đã làm
Đã bổ sung chốt chặn an toàn `MaxClipboardTextLength` và kiểm soát biên độ kích thước khung hình trong `MessageCodec.cs`. Ngăn chặn hoàn toàn nguy cơ DoS hoặc crash bộ đệm khi nhận dữ liệu rác từ mạng.

## Artifact
- `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs`
- `IPGS.RemoteControl.CcuClient/Protocol/MessageCodec.cs`

## Quyết định quan trọng
Giới hạn 1MB văn bản là hoàn toàn đủ cho mọi nhu cầu copy/paste văn bản và mã nguồn thông thường, đồng thời bảo vệ tuyệt đối sự ổn định của kết nối điều khiển từ xa.

## Handoff Payload
- do_not_redo: Giới hạn an toàn đã tích hợp.
- watch_out: Không có.
- next_inputs: Viết unit tests tự động cho `SessionRecorder` và các ranh giới mới.
