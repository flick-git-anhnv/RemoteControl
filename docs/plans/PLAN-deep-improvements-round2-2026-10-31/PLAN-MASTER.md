---
task: TASK-deep-improvements-round2-2026-10-31
created: 2026-10-31
updated: 2026-10-31
status: done
workflow: WF-REFACTOR
priority: P1
---

# PLAN MASTER: Thực Thi Cải Tiến Tầng 2 & Vòng Lặp Đánh Giá Triệt Để Đến Khi Hết Tính Năng

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả
Hiện thực hóa toàn bộ 7 đề xuất cải tiến sâu (F-NEW-1 đến F-NEW-7) từ đợt phản biện độc lập:
1. Sửa lỗi ngầm bỏ sót `TlsCertFingerprint` trong `ComputerProfileStore.Save()` và `CloneForStorage`.
2. Khép kín luồng bảo mật TLS Pinning và TOFU (Trust On First Use) tự động trên CCU UI.
3. An toàn luồng tuyệt đối khi thay đổi độ phân giải màn hình trong `RemoteScreenViewModel`.
4. Loại bỏ blocking `X11.XSync` trên từng tọa độ chuột và phím trong `MouseInjector` và `KeyboardInjector`.
5. Tối ưu Zero-Copy chụp màn hình trực tiếp sang Skia trên ZCU Agent, tiết kiệm 250 MB/s RAM copy.
6. Mở rộng Unit Test Suite cho `SecretProtector` và `ComputerProfileStore` (tăng lên 41 tests).
7. Hiển thị trực quan trạng thái mã hóa TLS 🔒 trên thanh tiêu đề Remote Screen.
Sau khi hoàn thành, tiếp tục thực hiện đợt Re-Review Vòng 3 để tìm kiếm và tối ưu đến khi không còn điểm nào có thể cải tiến thêm.

## Nguồn yêu cầu
- Yêu cầu người dùng: "có thực hiện cải tiến => review => Cải tiến tới khi hết cải tiến hết tất cả tính năng"
- Workflow: WF-REFACTOR
- Agent chain: Senior Developer → Junior Developer → QA Engineer → Documentation Writer → Tech Lead (Re-Review loop)

## Phases & Steps

### Phase 1: Sửa Lỗi Ngầm Bảo Mật & Đa Luồng UI (P0 & P1)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 1.1 | Fix `TlsCertFingerprint` trong `ComputerProfileStore` & Nối TOFU UI | Senior Developer | ✅ | `steps/STEP-1.1-fix-profile-tls-tofu.md` | 2026-10-31T08:58 |
| 1.2 | An toàn luồng khi đổi độ phân giải & Loại bỏ blocking `XSync` | Senior Developer | ✅ | `steps/STEP-1.2-threadsafe-resize-remove-xsync.md` | 2026-10-31T08:59 |

### Phase 2: Tối ưu Bộ nhớ Zero-Copy ZCU & Mở rộng Kiểm thử (P2 & P3)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 2.1 | Tối ưu Zero-Copy Capture sang Skia & Icon Ổ Khóa TLS UI | Senior Developer | ✅ | `steps/STEP-2.1-zerocopy-capture-tls-badge.md` | 2026-10-31T09:00 |
| 2.2 | Mở rộng Unit Test Suite cho SecretProtector & ProfileStore | Junior Developer | ✅ | `steps/STEP-2.2-unit-tests-secrets-profiles.md` | 2026-10-31T09:01 |

### Phase 3: Kiểm thử Toàn diện & Đồng bộ Tài liệu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 3.1 | Test Suite Run & Solution Build Verification | QA Engineer | ✅ | `steps/STEP-3.1-qa-test-run.md` | 2026-10-31T09:01 |
| 3.2 | Cập nhật Code Graph v3.1 & Gotchas | Documentation Writer | ✅ | `steps/STEP-3.2-sync-code-graph-docs.md` | 2026-10-31T09:02 |

### Phase 4: Re-Review Vòng 3 & Kiểm tra Triệt để Tính Năng
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 4.1 | Re-Review Phản biện Vòng 3 & Nghiệm thu Triệt để | Tech Lead | ✅ | `steps/STEP-4.1-tech-lead-re-review-round3.md` | 2026-10-31T09:02 |

## Artifacts dự kiến
- [x] Mã nguồn cập nhật tại `ComputerProfileStore.cs`, `RemoteScreenViewModel.cs`, `RemoteScreenWindow.axaml(.cs)`, `MouseInjector.cs`, `KeyboardInjector.cs`, `JpegEncoder.cs`.
- [x] Thêm file test `SecretProtectorTests.cs` và `ComputerProfileStoreTests.cs` (41/41 passed).
- [x] Báo cáo Re-Review Vòng 3 `RE-REVIEW-verification-round3-2026-10-31.md` (+ DOCX, PDF).
- [x] Cập nhật `code-graph/CODE-GRAPH.md` (+ DOCX, PDF).
- [x] Cập nhật `.gemini/shared/GOTCHAS.md` (mã lỗi G010, G011).
