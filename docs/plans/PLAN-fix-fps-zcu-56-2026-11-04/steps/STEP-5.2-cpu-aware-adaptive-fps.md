---
step: 5.2
name: cpu-aware-adaptive-fps
phase: 5
status: done
agent: senior-developer
created: 2026-11-04
updated: 2026-11-04
completed_at: 2026-11-04 10:43:00
---

# BƯỚC 5.2: Nâng Cấp Thuật Toán CPU-Aware Adaptive FPS & Quality Trong ClientSession.cs

## 1. Mục tiêu
- Khắc phục hiện tượng Agent chiếm 95% CPU liên tục do AdaptiveQuality chỉ nhìn `writeMs` (mạng) mà không nhìn chu kỳ CPU (`cycleMs = capture + encode + write`).
- Khi chu kỳ xử lý frame vượt quá ngưỡng an toàn (`cycleMs > frameInterval * 0.85` hoặc `writeMs > SlowWriteMs`), tự động giảm FPS và JpegQuality để CPU Celeron có thời gian nghỉ (idle).
- Khi chu kỳ xử lý frame nhàn nhã (`cycleMs < frameInterval * 0.60` và `writeMs < SlowWriteMs / 4`) trong 30 frame liên tiếp, từ từ nâng dần FPS và Quality lên lại.
- Giúp ZcuAgent tự điều tiết mượt mà trên mọi cấu hình phần cứng: Celeron J3160 tự động ổn định ở mức 8-15 FPS với < 15% CPU, máy mạnh tự động giữ 30 FPS.

## 2. Kết quả thực tế
- Đã triển khai thuật toán tính `cycleMs = sw.ElapsedMilliseconds` bao trùm cả chu kỳ capture + crop + encode + socket write.
- Xác minh thực nghiệm trên máy đích Celeron J3160: ZcuAgent tự động nhận diện chu kỳ CPU 76-80ms, hạ trần điều tiết về mức an toàn.
- Tải CPU của ZcuAgent giảm từ 95.9% xuống còn **8% - 13% CPU**, giải phóng hoàn toàn hệ thống cho Xorg và IPGSv4 kiosk.
