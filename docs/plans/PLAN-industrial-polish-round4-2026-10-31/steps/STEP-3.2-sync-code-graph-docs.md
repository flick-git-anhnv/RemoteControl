---
step: 3.2
plan: ../PLAN-MASTER.md
agent: Documentation Writer
status: done
completed_at: 2026-10-31T09:25:00+07:00
deps: [3.1]
---

# STEP 3.2 — Cập nhật Code Graph v3.3 & Gotchas G014

## Input nhận
Kết quả triển khai 4 hạng mục cải tiến Vòng 4 và xác nhận kiểm thử tự động 65 tests passed từ QA Engineer.

## Nhiệm vụ
- Cập nhật tài liệu kiến trúc tổng thể `code-graph/CODE-GRAPH.md` lên Version 3.3.
- Bổ sung entry G014 vào `.gemini/shared/GOTCHAS.md` về tối ưu TCP Socket Nagle algorithm và buffer size trên Linux ZCU.
- Biên dịch tài liệu sang DOCX và PDF thông qua script chuẩn `scripts/md_to_docx_kztek.py`.

## Definition of Done
- [x] Cập nhật `code-graph/CODE-GRAPH.md` phản ánh đầy đủ kiến trúc hiện tại và bộ test 65 ca kiểm thử.
- [x] Cập nhật `.gemini/shared/GOTCHAS.md` với mục G014.
- [x] Xuất bản file `.docx` và `.pdf` cho CODE-GRAPH.

## Đã làm
Đã cập nhật chi tiết các thành phần mới và chỉ số hiệu năng vào `CODE-GRAPH.md` phiên bản 3.3. Đã thêm bài học kinh nghiệm G014 vào kho tri thức ngầm `.gemini/shared/GOTCHAS.md`. Đã chạy script chuyển đổi xuất bản tài liệu đạt chuẩn format KZTEK R1.

## Artifact
- `code-graph/CODE-GRAPH.md`, `code-graph/CODE-GRAPH.docx`, `code-graph/CODE-GRAPH.pdf`
- `.gemini/shared/GOTCHAS.md`

## Quyết định quan trọng
Duy trì tính đồng bộ 100% giữa mã nguồn thực tế và tài liệu kiến trúc giúp các kỹ sư bảo trì trong tương lai nắm bắt chính xác hệ thống mà không cần đọc lại toàn bộ code.

## Handoff Payload
- do_not_redo: Không cần cập nhật lại sơ đồ kiến trúc cho các tính năng Vòng 4.
- watch_out: Tuân thủ quy tắc định dạng tài liệu không để sót ký tự LaTeX thô.
- next_inputs: Chuyển giao sang Tech Lead thực hiện Re-Review và nghiệm thu chung cuộc.
