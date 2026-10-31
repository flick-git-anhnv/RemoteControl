# Bước 3.2 — Cập Nhật Code Graph v3.6, Gotchas G017 & Xuất Bản DOCX/PDF

- **Agent:** Documentation Writer
- **Status:** done
- **Mục tiêu:** Cập nhật kiến trúc hệ thống, bài học kinh nghiệm và xuất bản tài liệu chuẩn KZTEK R1.

## Nội dung thực hiện
1. Bổ sung bài học **G017** vào `.gemini/shared/GOTCHAS.md`:
   - Phân tích nguyên nhân rác LOH 1.5–3.0 MB/s do cấp phát mảng byte cho từng khung hình trong `EncodeFrameJpeg`.
   - Ghi nhận cơ chế Zero-Allocation Frame Streaming và Congestion Relief chống trễ tích lũy.
2. Cập nhật `code-graph/CODE-GRAPH.md` lên version 3.6 phản ánh:
   - Zero-Allocation Frame Streaming (`WriteFrameJpegDirectAsync` và `WriteFrameMetadataHeader`).
   - Reusable rented header trong `MessageCodec`.
   - Bộ test suite đạt mốc 108 unit tests pass 100%.
3. Biên dịch tài liệu sang định dạng DOCX và PDF theo chuẩn KZTEK R1.
