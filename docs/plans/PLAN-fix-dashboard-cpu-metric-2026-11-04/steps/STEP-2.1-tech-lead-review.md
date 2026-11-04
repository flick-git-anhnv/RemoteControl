---
step: 2.1
task: fix-dashboard-cpu-metric
agent: tech-lead
status: done
deps: [1.1, 1.2]
created: 2026-11-04
completed_at: 2026-11-04 13:50:30
---

# STEP 2.1 — Tech Lead Code Review

## Kết quả Review
1. **Kiến trúc & Tách lớp:**
   - Việc tách `TopParser.cs` thành static helper thuần túy trong `IPGS.RemoteControl.CcuClient` giúp tách rời hoàn toàn logic parse text khỏi Avalonia UI, cho phép kiểm thử tự động độc lập 100%.
2. **Khắc phục lỗi Locale:**
   - Sử dụng `LC_ALL=C` trong lệnh SSH đảm bảo hệ điều hành Linux trả về các keyword chuẩn tiếng Anh và dấu chấm thập phân.
   - Kết hợp xử lý regex `([\d.,]+)\s*id` và `.Replace(',', '.')` trong C# tạo cơ chế "phòng thủ kép" (Defense in Depth), kể cả khi target machine thiếu locale C hoặc lệnh được chạy thủ công với locale khác.
3. **Triệt tiêu Startup Spike:**
   - Chạy 2 lần lấy mẫu (`-n 2 -d 0.5`) và dùng `LastOrDefault` / `FindLastIndex` giúp loại bỏ hoàn toàn hiện tượng vọt CPU 11% của chính lệnh `top`.
4. **Đánh giá Unit Tests:**
   - Bộ 5 test cases bao phủ đầy đủ các góc cạnh (null/empty, dot, comma, multi-iteration, fallback).
   - Đạt chuẩn PASS 100%. Ký duyệt merge.
