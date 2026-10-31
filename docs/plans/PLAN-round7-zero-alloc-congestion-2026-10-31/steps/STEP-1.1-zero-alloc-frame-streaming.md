# Bước 1.1 — Hiện Thực Hóa Zero-Allocation Frame Streaming & Header 29-byte

- **Agent:** Senior Developer
- **Status:** done
- **Mục tiêu:** Triệt tiêu hoàn toàn 1.5 – 3.0 MB/s rác LOH trên ZcuAgent khi stream video 1080p 15–30 FPS.

## Nội dung thực hiện
1. Bổ sung `FrameMetadataHeaderSize = 24` và phương thức `WriteFrameMetadataHeader(Span<byte>, ...)` vào `MessageCodec.cs` (ghi 24 bytes metadata vào span đích không phân bổ heap).
2. Tạo bộ đệm cố định per-session `_frameHeaderBuffer` (29 bytes = 5 wire header + 24 metadata) trong `ClientSession.cs`.
3. Hiện thực hóa `WriteFrameJpegDirectAsync`: Ghép header vào `_frameHeaderBuffer` rồi stream trực tiếp mảng pixel JPEG đã nén từ encoder buffer sang socket TCP.
4. Triệt tiêu hoàn toàn mảng byte cấp phát mới `new byte[24 + jpeg.Length]` cho từng khung hình.
