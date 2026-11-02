# Bước 3.2 — Cập Nhật Code Graph v3.5 & Gotchas G016

- **Agent:** Documentation Writer
- **Status:** done
- **Mục tiêu:** Đồng bộ hóa tài liệu kiến trúc kỹ thuật và bài học kinh nghiệm hệ thống.

## Nội dung thực hiện
1. Bổ sung bài học **G016** vào `.gemini/shared/GOTCHAS.md`:
   - Ghi nhận chi tiết nguyên nhân sập tiến trình khi thiếu `XDG_SESSION_TYPE=x11`, màn hình đen DPMS screensaver trong môi trường headless, và giải pháp phân giải tự động `X11DisplayResolver`.
2. Cập nhật `code-graph/CODE-GRAPH.md` phản ánh module mới `X11DisplayResolver` và tương tác giữa Capturer, Injectors với X11 Server.
3. Biên dịch tài liệu sang DOCX và PDF theo format chuẩn KZTEK R1.
