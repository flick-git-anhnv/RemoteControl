---
task: TASK-apex-boundary-hardening-round5-2026-10-31
created: 2026-10-31
updated: 2026-10-31
status: done
workflow: WF-REFACTOR
priority: P1
---

# PLAN MASTER: Thực Thi Cải Tiến Đỉnh Cao Vòng 5 (Apex Boundary Hardening & Automated Testing Expansion)

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả
Tiếp tục chu trình lặp vô hạn theo tôn chỉ: "Cải tiến tới khi hết tất cả tính năng". Vòng 5 tập trung vào gia cố phòng thủ ranh giới dữ liệu và mở rộng bộ kiểm thử tự động toàn diện:
1. **F-ROUND5-1:** Hỗ trợ `Stream` trừu tượng & tối ưu hóa FourCC tĩnh `"idx1"u8` trong `SessionRecorder.cs`.
2. **F-ROUND5-2:** Clipboard Safety Shield giới hạn 1MB văn bản chống nghẽn socket và tràn RAM trong `RemoteControlClient.cs`.
3. **F-ROUND5-3:** Boundary Hardening cho `DecodeFrameJpeg` và `DecodeAuthFail` trong `MessageCodec.cs`.
4. **F-ROUND5-4:** Mở rộng Unit Test Suite với `SessionRecorderTests` và test ranh giới `MessageCodecTests` (đạt 75/75 tests passed 100%).

## Nguồn yêu cầu
- Yêu cầu người dùng: "tiep tuc" — tiếp tục tối ưu hóa tới khi không còn điểm nào có thể cải tiến thêm
- Workflow: WF-REFACTOR
- Agent chain: Senior Developer → Junior Developer → QA Engineer → Documentation Writer → Tech Lead

## Phases & Steps

### Phase 1: Gia cố Ranh giới Dữ liệu & Trừu tượng hóa Stream (P1)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 1.1 | Nâng cấp `SessionRecorder` với Stream Constructor & FourCC literal tĩnh | Senior Developer | ✅ | `steps/STEP-1.1-session-recorder-stream-fourcc.md` | 2026-10-31T09:32:00+07:00 |
| 1.2 | Clipboard Safety Shield (1MB limit) & MessageCodec Hardening | Senior Developer | ✅ | `steps/STEP-1.2-clipboard-codec-hardening.md` | 2026-10-31T09:33:00+07:00 |

### Phase 2: Mở rộng Test Suite Lên Đỉnh Cao 75 Tests (P2)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 2.1 | Xây dựng bộ Unit Test tự động cho `SessionRecorder` trong RAM | Junior Developer | ✅ | `steps/STEP-2.1-unit-tests-session-recorder.md` | 2026-10-31T09:33:45+07:00 |
| 2.2 | Mở rộng kiểm thử ranh giới cho `MessageCodec` và Clipboard | Junior Developer | ✅ | `steps/STEP-2.2-unit-tests-codec-boundaries.md` | 2026-10-31T09:34:15+07:00 |

### Phase 3: Kiểm thử Toàn diện & Đồng bộ Tài liệu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 3.1 | Test Suite Run & Solution Clean Build Verification | QA Engineer | ✅ | `steps/STEP-3.1-qa-test-run.md` | 2026-10-31T09:34:30+07:00 |
| 3.2 | Cập nhật Code Graph v3.4 & Gotchas G015 | Documentation Writer | ✅ | `steps/STEP-3.2-sync-code-graph-docs.md` | 2026-10-31T09:34:50+07:00 |

### Phase 4: Re-Review Vòng 6 & Nghiệm Thu Tuyệt Đối
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 4.1 | Re-Review Phản biện Vòng 6 & Nghiệm thu Chung cuộc | Tech Lead | ✅ | `steps/STEP-4.1-tech-lead-re-review-round6.md` | 2026-10-31T09:35:00+07:00 |

## Artifacts đạt được
- Mã nguồn cập nhật tại `SessionRecorder.cs`, `RemoteControlClient.cs`, `MessageCodec.cs`.
- Thêm file test `SessionRecorderTests.cs` và mở rộng `MessageCodecTests.cs` (tổng 75/75 tests passed 100%).
- Báo cáo Re-Review Vòng 6 `RE-REVIEW-verification-round6-2026-10-31.md` (+ DOCX, PDF).
- Cập nhật `code-graph/CODE-GRAPH.md` (+ DOCX, PDF) lên v3.4.
- Thêm bài học kinh nghiệm G015 vào `.gemini/shared/GOTCHAS.md`.
