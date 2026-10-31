---
step: 2.1
plan: ../PLAN-MASTER.md
agent: tech-lead
status: done
completed_at: 2026-10-31 08:35:30
deps: ["1.1"]
---

# STEP 2.1 — Thẩm định Kiến trúc, Đánh giá Bảo mật & Đề xuất Cải tiến

## Input nhận
- Báo cáo kỹ thuật chi tiết từ Senior Developer: `docs/reviews/REVIEW-remote-control-full-audit-2026-10-31.md`.
- Nghiên cứu đối chuẩn RustDesk: `docs/research/RESEARCH-rustdesk-2026-09-29.md`.

## Nhiệm vụ
Thẩm định kiến trúc hệ thống, đánh giá mô hình bảo mật STRIDE, rà soát khoảng trống công nghệ và xây dựng danh mục đề xuất cải tiến phân tầng (P1/P2/P3, Effort, Giải pháp kiến trúc).

## Definition of Done
- [x] Hoàn thành mô hình đánh giá an toàn thông tin & độ tin cậy kết nối.
- [x] Xây dựng danh mục đề xuất cải tiến phân tầng rõ ràng (P1/P2/P3) với kiến trúc giải pháp cụ thể.
- [x] Bàn giao báo cáo đề xuất cho Engineering Manager và CTO.

## Đã làm
- Đánh giá kiến trúc hệ thống theo 4 nguyên tắc cốt lõi: Zero-Copy & Buffer Reuse, Deterministic Threading, Hardened Security, Resilient Testing.
- Xây dựng 13 giải pháp kiến trúc phân thành 3 gói ưu tiên:
  + Gói P1 (Release Blockers):
    1.1 Ping-Pong Double-Buffering & Zero-Copy giải mã (triệt tiêu 100% rác LOH 124MB/s).
    1.2 Tái sử dụng `SKBitmap` qua `InstallPixels` + `malloc_trim` định kỳ (khống chế RAM Agent < 65MB).
    1.3 Cập nhật bản vá lỗ hổng `SSH.NET` (giải quyết triệt để cảnh báo NU1903).
    1.4 Thay thế `Channel<Action>` bằng `struct InputCommand` + Coalescing chuột.
    1.5 Điều chuyển `PasteStatus` về `Dispatcher.UIThread.Post`.
    1.6 Thiết lập project Unit Test Suite `IPGS.RemoteControl.Tests`.
  + Gói P2 (Nâng cao độ tin cậy & bảo mật):
    2.1 Chuẩn hóa mật khẩu sudo qua STDIN Stream (bỏ `KIOSK_SUDO_PASS`).
    2.2 Tối ưu hóa buffer Zero-Copy khi upload file chunking.
    2.3 Bổ sung TLS Certificate Fingerprint Pinning.
    2.4 Sửa công thức đo lường tải CPU trên `HealthMonitorWindow`.
  + Gói P3 (Tiến hóa công nghệ dài hạn):
    3.1 Hỗ trợ Wayland Native Capture qua PipeWire / Portal D-Bus API.
    3.2 Đồng bộ hình dạng con trỏ chuột (Cursor Shape Sync).
    3.3 Chuẩn hóa 32 thuộc tính XAML Watermark obsolete Avalonia 12.
- Xuất bản đề xuất kiến trúc: `docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md`.

## Artifact
- `docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md`
- `docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.docx`
- `docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.pdf`

## Quyết định quan trọng
- Đặt kỹ thuật Ping-Pong Double-Buffering làm ưu tiên P1 số 1 vì đây là nút thắt cổ chai lớn nhất ảnh hưởng trực tiếp đến trải nghiệm người dùng và độ ổn định của CCU.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Ma trận phân tầng P1/P2/P3 và phân tích kiến trúc đã hoàn thiện, không cần lập lại bảng đề xuất.
- watch_out: Gói P1 cần tổng cộng ~5.7 ngày công dev, được chia thành các task độc lập có thể phân bổ cho Senior và Junior dev.
- next_inputs: Dùng `docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md` làm đầu vào để EM lập kế hoạch release và CTO phê duyệt kiến trúc.
