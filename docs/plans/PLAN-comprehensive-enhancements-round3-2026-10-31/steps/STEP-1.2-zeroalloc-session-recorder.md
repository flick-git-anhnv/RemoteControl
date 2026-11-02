---
step: 1.2
plan: ../PLAN-MASTER.md
agent: Senior Developer
status: done
completed_at: 2026-10-31T09:11:00+07:00
deps: [1.1]
---

# STEP 1.2 — Zero-Allocation AVI Frame Recording trong SessionRecorder

## Input nhận
`SessionRecorder.RecordFrame` ghi từng khung hình JPEG vào tệp AVI. Việc gọi `Encoding.ASCII.GetBytes("00dc")` và `BitConverter.GetBytes(int)` tạo ra các mảng heap rác ở mỗi khung hình với tốc độ 30-60 khung hình/giây.

## Nhiệm vụ
Áp dụng tính năng C# UTF-8 string literals (`"00dc"u8`) và `stackalloc byte[4]` kết hợp `BinaryPrimitives.WriteInt32LittleEndian` để triệt tiêu toàn bộ heap allocation trong vòng lặp ghi khung hình.

## Definition of Done
- [x] Thay thế mã hóa chuỗi ASCII bằng UTF-8 byte literal `"00dc"u8`.
- [x] Thay thế `BitConverter.GetBytes(int)` bằng bộ nhớ ngăn xếp stackalloc 4 bytes và `BinaryPrimitives`.
- [x] Đảm bảo không còn allocation nào trên heap trong hàm `RecordFrame`.
- [x] Biên dịch `IPGS.RemoteControl.CcuUI` đạt 0 Warnings, 0 Errors.

## Đã làm
Đã tái cấu trúc `SessionRecorder.cs`, chuyển mã ghi chunk ID `"00dc"` sang `ReadOnlySpan<byte>` tĩnh thông qua `"00dc"u8`, và dùng `Span<byte> sizeBytes = stackalloc byte[4]` để ghi độ dài khung hình. Khung hình được ghi trực tiếp vào `_stream` mà không qua trung gian heap.

## Artifact
- `IPGS.RemoteControl.CcuUI/Services/SessionRecorder.cs`

## Quyết định quan trọng
Sử dụng C# 11+ UTF-8 literal `"00dc"u8` giúp lưu trữ chuỗi 4 ký tự vào vùng data tĩnh của assembly, thời gian truy xuất là tức thì và allocation bằng 0.

## Handoff Payload
- do_not_redo: Không can thiệp lại định dạng chunk AVI trong `SessionRecorder`.
- watch_out: Chú ý padding chẵn byte khi `jpegData.Length % 2 != 0`.
- next_inputs: Tiến hành đồng bộ TLS Cert Fingerprint sang Multi-Remote và Discovery.
