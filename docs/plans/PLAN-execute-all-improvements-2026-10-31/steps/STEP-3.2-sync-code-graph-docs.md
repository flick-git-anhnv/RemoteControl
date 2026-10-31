---
step: 3.2
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-10-31T08:52:00+07:00
deps: ["3.1"]
---

# STEP 3.2 — Cập nhật Code Graph & Gotchas

## Input nhận
- Thay đổi mã nguồn từ các bước trước trong Phase 1, Phase 2 và kết quả test từ Phase 3.

## Nhiệm vụ
Cập nhật `code-graph/CODE-GRAPH.md` để ghi nhận các cấu trúc mới (Ping-Pong buffers, struct InputCommand, STDIN sudo, Certificate pinning, project Tests) và xuất PDF, cập nhật `GOTCHAS.md`.

## Definition of Done
- [x] `code-graph/CODE-GRAPH.md` và `.pdf` được cập nhật đồng bộ.
- [x] `GOTCHAS.md` ghi nhận bài học kinh nghiệm về LOH và Skia unmanaged memory.

## Đã làm
1. Cập nhật `code-graph/CODE-GRAPH.md` lên Version 3.0:
   - Bổ sung cấu trúc dự án thứ 4: `IPGS.RemoteControl.Tests` (xUnit .NET 9.0).
   - Mô tả chi tiết kỹ thuật Ping-Pong Double-Buffering zero-copy (`Buffer.MemoryCopy`) trong `RemoteScreenViewModel.cs`.
   - Mô tả giải pháp tái sử dụng `SKBitmap` và `malloc_trim(0)` trong `JpegEncoder.cs`.
   - Mô tả hàng đợi sự kiện `Channel<InputCommand>` với `readonly struct` và mouse coalescing trong `ClientSession.cs`.
   - Ghi nhận bảo mật nâng cấp `SSH.NET 2026.0.0` (xóa sạch NU1903) và truyền mật khẩu STDIN sudo qua `CreateInputStream()`.
   - Ghi nhận TLS Certificate Pinning và chuẩn hóa công thức CPU `100 - idle%`.
   - Xóa bỏ định dạng mã ASCII Tree chuyển sang danh sách lồng nhau theo chuẩn tài liệu KZTEK.
2. Xuất bản `code-graph/CODE-GRAPH.docx` và `code-graph/CODE-GRAPH.pdf` thông qua `md_to_docx_kztek.py`: thành công với 0 cảnh báo định dạng.
3. Cập nhật `.gemini/shared/GOTCHAS.md`:
   - Bổ sung mục lục và nội dung chi tiết mã lỗi **G009**: "Triệt tiêu rác LOH 124MB/s và giật lag GC Gen 2 bằng Ping-Pong Double-Buffering zero-copy cùng malloc_trim(0)".

## Artifact
- [CODE-GRAPH.md](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/code-graph/CODE-GRAPH.md)
- [CODE-GRAPH.docx](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/code-graph/CODE-GRAPH.docx)
- [CODE-GRAPH.pdf](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/code-graph/CODE-GRAPH.pdf)
- [GOTCHAS.md](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/.gemini/shared/GOTCHAS.md)

## Quyết định quan trọng
- Mọi tài liệu kiến trúc được đồng bộ trực tiếp với mã nguồn thực tế và xuất bản PDF sẵn sàng lưu trữ.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Tài liệu và đồ thị mã đã chuẩn hóa version 3.0.
- watch_out: Khi bổ sung message type mới cần cập nhật đồng bộ cả Code Graph và Unit Test.
- next_inputs: Cung cấp toàn bộ tài liệu cho Tech Lead thực hiện Re-Review toàn diện (Phase 4).
