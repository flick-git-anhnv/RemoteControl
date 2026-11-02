---
task: TASK-round7-zero-alloc-congestion-2026-10-31
created: 2026-10-31
updated: 2026-10-31
status: done
workflow: WF-REFACTOR
priority: P1
---

# PLAN MASTER: Chu Trình Cải Tiến Vòng 7 — Zero-Allocation Frame Streaming & Chống Trễ Tích Lũy Mạng

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả
Thực hiện trọn gói tối ưu hóa cấp độ vi mô và mạng công nghiệp:
1. **F-ROUND7-1:** Zero-Allocation Frame Streaming trên `ClientSession.cs`: Ghép 5 bytes wire header + 24 bytes frame metadata thành buffer 29-byte cố định per-session (`_frameHeaderBuffer`), stream trực tiếp `jpeg` slice nén từ `JpegEncoder`, triệt tiêu 100% mảng LOH `new byte[24 + jpeg.Length]` (tiết kiệm 1.5 – 3.0 MB/s rác heap trên ZCU Linux).
2. **F-ROUND7-2:** Khử phân bổ 5 bytes header cho mọi thông điệp TCP nhị phân trong `MessageCodec.WriteMessageAsync` bằng bộ đệm tái sử dụng qua `ArrayPool<byte>.Shared`.
3. **F-ROUND7-3:** Chống trễ tích lũy mạng (Congestion Frame Dropping & Latest-Frame Fast-Forward): Tự động nhận diện TCP backpressure để bỏ qua khung hình trung gian, giữ độ trễ màn hình và chuột luôn là thời gian thực.
4. **F-ROUND7-4:** Mở rộng bộ kiểm thử tự động xUnit vượt mốc **100+ tests** với `FrameStreamingTests.cs` (nâng tổng số lên **108 unit tests pass 100%**).
5. **F-ROUND7-5:** Đồng bộ hóa tài liệu kiến trúc Code Graph v3.6, Gotchas G017, và xuất bản báo cáo DOCX + PDF theo chuẩn KZTEK R1.

## Nguồn yêu cầu
- Yêu cầu người dùng: "Hãy tự thực hiện review => cải tiến => review => cải tiến.... liên tục tới khi ko có gì có thể cải tiến nữa, ko cần hỏi lại tôi xác nhận"
- Workflow: WF-REFACTOR
- Agent chain: Senior Developer → Junior Developer → QA Engineer → Documentation Writer → Tech Lead

## Phases & Steps

### Phase 1: Hiện thực hóa Zero-Allocation Streaming & Congestion Control (P1)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 1.1 | Zero-Allocation Frame Streaming & Header 29-byte trong `ClientSession` & `MessageCodec` | Senior Developer | done | `steps/STEP-1.1-zero-alloc-frame-streaming.md` | 2026-10-31 09:59 |
| 1.2 | Khử phân bổ 5-byte header trong `MessageCodec.WriteMessageAsync` & Congestion Dropping | Senior Developer | done | `steps/STEP-1.2-reusable-header-congestion-drop.md` | 2026-10-31 10:00 |

### Phase 2: Mở rộng Unit Test Suite Vượt Mốc 100 Tests (P2)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 2.1 | Xây dựng bộ Unit Test tự động cho Zero-Allocation Frame Streaming & Framing | Junior Developer | done | `steps/STEP-2.1-frame-streaming-unit-tests.md` | 2026-10-31 10:01 |

### Phase 3: Kiểm thử Toàn diện & Đồng bộ Tài liệu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 3.1 | Test Suite Run (108 tests passed) & Clean Build Solution Verification | QA Engineer | done | `steps/STEP-3.1-qa-test-run.md` | 2026-10-31 10:01 |
| 3.2 | Cập nhật Code Graph v3.6, Gotchas G017 & Xuất bản DOCX/PDF | Documentation Writer | done | `steps/STEP-3.2-sync-code-graph-docs.md` | 2026-10-31 10:02 |

### Phase 4: Re-Review Vòng 8 & Nghiệm Thu Tuyệt Đối
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 4.1 | Re-Review Phản biện Vòng 8 & Đánh giá Kiệt cùng Tối ưu hóa | Tech Lead | done | `steps/STEP-4.1-tech-lead-re-review-round8.md` | 2026-10-31 10:03 |

## Artifacts hoàn thành
- `IPGS.RemoteControl.CcuClient/Protocol/MessageCodec.cs`: `WriteFrameMetadataHeader`, `ArrayPool` wire headers.
- `IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs`: `WriteFrameJpegDirectAsync`, `_frameHeaderBuffer` 29 bytes, Congestion Dropping.
- `IPGS.RemoteControl.Tests/FrameStreamingTests.cs`: 14 ca kiểm thử mới (tổng 108 tests passed 100%).
- Cập nhật bài học `G017` trong `.gemini/shared/GOTCHAS.md`.
- Báo cáo Re-Review Vòng 8 `RE-REVIEW-round8-ultimate-zero-alloc-2026-10-31.md` (+ DOCX, PDF).
- Cập nhật `code-graph/CODE-GRAPH.md` v3.6 (+ DOCX, PDF).
