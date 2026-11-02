---
step: 1.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31 08:35:00
deps: []
---

# STEP 1.1 — Rà soát Kỹ thuật Toàn diện Codebase & Tính năng mới

## Input nhận
- Yêu cầu người dùng: Review toàn diện sau đó đề xuất cải tiến hệ thống IPGS Remote Control.
- Codebase hiện tại trên nhánh `feature/remote-improve-rustdesk`.
- `code-graph/CODE-GRAPH.md` và `_workspace/CONTEXT-HINTS.md`.

## Nhiệm vụ
Rà soát chi tiết mã nguồn 3 module (`ZcuAgent`, `CcuClient`, `CcuUI`) cùng các commit mới gần đây. Phát hiện các lỗi tiềm ẩn (race condition, memory leaks, unhandled exceptions, resource disposal) và nợ kỹ thuật tồn đọng.

## Definition of Done
- [x] Khảo sát toàn bộ các luồng: Streaming/Capture, Input Injection, Clipboard Sync, File Transfer, Installer & Hardware Monitor.
- [x] Lập danh mục chi tiết các phát hiện kỹ thuật (Bugs, Performance bottlenecks, Memory leaks, Concurrency issues).
- [x] Bàn giao báo cáo kỹ thuật làm đầu vào cho Tech Lead.

## Đã làm
- Khảo sát mã nguồn chi tiết tại các tệp lõi: `ClientSession.cs`, `X11ScreenCapturer.cs`, `JpegEncoder.cs`, `RemoteControlClient.cs`, `RemoteScreenViewModel.cs`, `RemoteScreenControl.axaml.cs`, `HealthMonitorWindow.axaml.cs`, `RemoteAppInstallService.cs`.
- Phát hiện 12 vấn đề kỹ thuật lớn:
  + M1: LOH Garbage 124 MB/s do cấp phát WriteableBitmap và byte[] mỗi frame tại `RemoteScreenViewModel`.
  + M2: Cấp phát và hủy `SKBitmap` unmanaged liên tục 15 fps tại `JpegEncoder`, gây phân mảnh glibc malloc.
  + M3: Cấp phát mảng 64 KB không cần thiết mỗi chunk upload file tại `RemoteControlClient`.
  + C1: Nghẽn ThreadPool do `Task.Run` thừa trong `RunInputInjectorLoopAsync`.
  + C2: Vi phạm luồng UI Thread trong `PasteStatus` tại `RemoteScreenControl`.
  + C3: Hàng đợi `_inputQueue` unbounded thiếu coalescing di chuột.
  + S1: Cảnh báo compiler NU1903 High Severity Vulnerability trong thư viện `SSH.NET` 2025.1.0.
  + S2: Lộ mật khẩu sudo trong biến môi trường `KIOSK_SUDO_PASS`.
  + S3: Thiếu Certificate Fingerprint Pinning cho kết nối TLS.
  + U1: Tính toán sai CPU % trong `HealthMonitorWindow` (chỉ lấy %us, bỏ qua %sy và %wa).
  + U2: 32 cảnh báo XAML Obsolete `TextBox.Watermark` của Avalonia 12.
  + T1: Thiếu hoàn toàn bộ Unit Test tự động cho các thư viện cốt lõi.
- Xuất bản báo cáo kỹ thuật: `docs/reviews/REVIEW-remote-control-full-audit-2026-10-31.md`.

## Artifact
- `docs/reviews/REVIEW-remote-control-full-audit-2026-10-31.md`
- `docs/reviews/REVIEW-remote-control-full-audit-2026-10-31.docx`
- `docs/reviews/REVIEW-remote-control-full-audit-2026-10-31.pdf`

## Quyết định quan trọng
- Không chỉ rà soát cú pháp mà tập trung sâu vào hành vi cấp phát bộ nhớ tầng thấp (LOH, unmanaged heap glibc) và tranh chấp luồng mạng/UI.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Khảo sát và lập bảng thống kê lỗi kỹ thuật đã hoàn tất, không cần audit lại từ đầu.
- watch_out: Chú ý lỗi M1 (LOH phình 124MB/s) và M2 (phân mảnh heap glibc SkiaSharp theo GOTCHAS G002) là hai nguyên nhân hàng đầu khiến app giật lag.
- next_inputs: Dùng tệp `docs/reviews/REVIEW-remote-control-full-audit-2026-10-31.md` làm căn cứ để Tech Lead xây dựng đề xuất cải tiến phân tầng.
