---
step: 3.1
plan: ../PLAN-MASTER.md
agent: engineering-manager
status: done
completed_at: 2026-10-31 08:36:00
deps: ["2.1"]
---

# STEP 3.1 — Đánh giá Khả thi, Phân bổ Nguồn lực & Lập Lộ trình Release

## Input nhận
- Danh mục đề xuất cải tiến phân tầng từ Tech Lead: `docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md`.

## Nhiệm vụ
Đánh giá tính khả thi về mặt tài nguyên, rủi ro dự án, phân bổ công việc theo Sprint và thiết lập các mốc bàn giao (milestones) chuẩn bị release.

## Definition of Done
- [x] Phân bổ độ ưu tiên và ước lượng nguồn lực (effort/capacity).
- [x] Lập kế hoạch release các giai đoạn (Phase Release Plan) khả thi.
- [x] Trình báo cáo lên CTO xem xét và phê duyệt.

## Đã làm
- Đánh giá khả thi: Tổng effort gói P1 là ~5.7 ngày công. Đây là khối lượng công việc hoàn toàn khả thi trong 1 Sprint ngắn (1 tuần) trước đợt release ổn định v1.2.
- Phân bổ tài nguyên nhân sự:
  + Senior Developer: Phụ trách Đề xuất 1.1 (Double-buffering WriteableBitmap), Đề xuất 1.2 (Tái sử dụng SKBitmap & malloc_trim), Đề xuất 1.4 (Input Queue & Coalescing chuột).
  + Junior Developer: Phụ trách Đề xuất 1.3 (Nâng cấp SSH.NET), Đề xuất 1.5 (Fix Dispatcher UI Thread) và Đề xuất 3.3 (Chuẩn hóa XAML Watermark).
  + QA Team: Tham gia viết và nghiệm thu Test Suite theo Đề xuất 1.6 song song với quá trình dev.
- Kế hoạch mốc thời gian:
  + Mốc 1 (Sprint 1 — 5 ngày): Hoàn thành toàn bộ Gói P1, đóng gói v1.2-RC1.
  + Mốc 2 (Sprint 2 — 3 ngày): Triển khai Gói P2 (STDIN sudo password, upload chunk zero-copy, Cert Pinning).
  + Mốc 3 (Backlog dài hạn): Nghiên cứu và hiện thực hóa Gói P3 (Wayland PipeWire & Cursor shape).

## Artifact
- Kế hoạch triển khai tích hợp trong `docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md`.

## Quyết định quan trọng
- Khóa toàn bộ các tính năng mới chưa ổn định để tập trung 100% nguồn lực giải quyết triệt để Gói P1 trước khi release chính thức.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Phân bổ nhân sự và mốc thời gian release đã chốt.
- watch_out: Cần sự phê duyệt định hướng kiến trúc cấp cao từ CTO trước khi kích hoạt quy trình WF-REFACTOR/WF-BUGFIX cho Gói P1.
- next_inputs: Bàn giao kế hoạch cho CTO phê duyệt.
