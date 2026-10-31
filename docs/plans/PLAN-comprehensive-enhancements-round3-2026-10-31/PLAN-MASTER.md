---
task: TASK-comprehensive-enhancements-round3-2026-10-31
created: 2026-10-31
updated: 2026-10-31
status: done
workflow: WF-REFACTOR
priority: P1
---

# PLAN MASTER: Thực Thi Cải Tiến Toàn Diện Vòng 3 & Tối Ưu Hóa Mọi Tính Năng Còn Lại

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả
Tiếp tục mở rộng tối ưu hóa trên toàn bộ các tính năng phụ trợ của hệ thống Remote Control Tool (File Transfer, Session Recorder, Multi-Remote Dashboard, Network Discovery, Wake-on-LAN):
1. **F-ROUND3-1:** Zero-Copy Chunk Streaming trong `FileTransferHandler.cs` và `ClientSession.WriteAsync`.
2. **F-ROUND3-2:** Zero-Allocation AVI Writing trong `SessionRecorder.cs` (loại bỏ allocation trong vòng lặp ghi video).
3. **F-ROUND3-3:** Đồng bộ TLS Certificate Pinning & TOFU sang `MultiRemoteWindow`.
4. **F-ROUND3-4:** Tự động bắt vân tay TLS chứng chỉ trong `ZcuAgentDiscoveryService`.
5. **F-ROUND3-5:** Mở rộng Unit Test Suite cho `WakeOnLanService` (đạt 55/55 tests passed).

## Nguồn yêu cầu
- Yêu cầu người dùng: "tiep tuc" — tiếp tục tối ưu hóa tới khi không còn điểm nào có thể cải tiến thêm
- Workflow: WF-REFACTOR
- Agent chain: Senior Developer → Junior Developer → QA Engineer → Documentation Writer → Tech Lead

## Phases & Steps

### Phase 1: Tối ưu Bộ nhớ Truyền File & Ghi Hình AVI (P1)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 1.1 | Zero-Copy File Download trong `FileTransferHandler` | Senior Developer | ✅ | `steps/STEP-1.1-zerocopy-file-download.md` | 2026-10-31T09:10:00+07:00 |
| 1.2 | Zero-Allocation AVI Frame Recording trong `SessionRecorder` | Senior Developer | ✅ | `steps/STEP-1.2-zeroalloc-session-recorder.md` | 2026-10-31T09:11:00+07:00 |

### Phase 2: Đồng bộ TLS Cho Multi-Remote & Discovery (P2)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 2.1 | TLS Pinning trong MultiRemote & Discovery TLS Fingerprint | Senior Developer | ✅ | `steps/STEP-2.1-tls-multiremote-discovery.md` | 2026-10-31T09:12:00+07:00 |
| 2.2 | Mở rộng Unit Test Suite cho Wake-on-LAN Service | Junior Developer | ✅ | `steps/STEP-2.2-unit-tests-wol.md` | 2026-10-31T09:12:30+07:00 |

### Phase 3: Kiểm thử Toàn diện & Đồng bộ Tài liệu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 3.1 | Test Suite Run & Solution Build Verification | QA Engineer | ✅ | `steps/STEP-3.1-qa-test-run.md` | 2026-10-31T09:13:00+07:00 |
| 3.2 | Cập nhật Code Graph v3.2 & Gotchas | Documentation Writer | ✅ | `steps/STEP-3.2-sync-code-graph-docs.md` | 2026-10-31T09:14:00+07:00 |

### Phase 4: Re-Review Vòng 4 & Nghiệm Thu Cuối Cùng
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 4.1 | Re-Review Phản biện Vòng 4 & Nghiệm thu Chung cuộc | Tech Lead | ✅ | `steps/STEP-4.1-tech-lead-re-review-round4.md` | 2026-10-31T09:15:00+07:00 |

## Artifacts đạt được
- Mã nguồn cập nhật tại `FileTransferHandler.cs`, `ClientSession.cs`, `SessionRecorder.cs`, `MultiRemoteWindow.axaml.cs`, `ZcuAgentDiscoveryService.cs`.
- Thêm file test `WakeOnLanServiceTests.cs` (tổng 55/55 tests passed, 100% pass rate).
- Báo cáo Re-Review Vòng 4 `RE-REVIEW-verification-round4-2026-10-31.md` (+ DOCX, PDF).
- Cập nhật `code-graph/CODE-GRAPH.md` (+ DOCX, PDF) lên v3.2.
- Thêm bài học kinh nghiệm G012 vào `.gemini/shared/GOTCHAS.md`.
