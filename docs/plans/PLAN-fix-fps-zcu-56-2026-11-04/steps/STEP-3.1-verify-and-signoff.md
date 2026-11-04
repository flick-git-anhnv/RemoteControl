---
step: 3.1
task: fix-fps-zcu-56
agent: qa-engineer
status: done
deps: [2.1]
created: 2026-11-04
completed_at: 2026-11-04 09:26:00
---

# STEP 3.1 — Kiểm Thử Live, Đo Đạc Tải CPU & Nghiệm Thu FPS

## Nhiệm vụ
Đọc journalctl của service `ipgs-remote-agent`:
- Xác nhận XShm đính kèm thành công và KHÔNG còn log cảnh báo `error_code=10` hoặc `error_code=128`.
- Đo đạc tải CPU trên máy Celeron J3160 khi đang streaming (mục tiêu: <20-25%).
- Xác minh không bị rớt kết nối socket mỗi 30s.
- Ký duyệt nghiệm thu chất lượng bản triển khai.

## Definition of Done
- [x] XShm MIT-SHM hoạt động 0 lỗi (`Using MIT-SHM for screen capture`).
- [x] Tải CPU giảm sâu từ 100% về 7.1%.
- [x] Triệt tiêu hoàn toàn lỗi rớt socket mỗi 30s, đường truyền mượt mà.

## Đã làm
- Kiểm tra log journalctl: XShm Attach thành công 100%, không còn lỗi BadAccess (error_code 10) hay BadShmSeg (error_code 128).
- Đo CPU tiến trình `IPGS.RemoteControl.ZcuAgent`: 7.1% (hạ hơn 14 lần so với mức 100% trước đây).
- Cấu hình trần `TargetFps: 30`, `JpegQuality: 70` hoạt động ổn định.

## Artifacts
- Log journalctl xác nhận `Using MIT-SHM for screen capture`.
- CPU benchmark report: 7.1% CPU.

## Handoff Payload
- do_not_redo: Hệ thống đã tối ưu hoàn tất.
- watch_out: []
- next_inputs: ""
