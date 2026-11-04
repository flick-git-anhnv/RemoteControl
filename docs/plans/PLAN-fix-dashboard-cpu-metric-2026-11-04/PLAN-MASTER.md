---
task: fix-dashboard-cpu-metric
created: 2026-11-04
updated: 2026-11-04
status: completed
workflow: WF-BUGFIX
priority: P2
---

# PLAN MASTER: Sửa Lỗi Parse Tải CPU Trên Dashboard (Hỗ Trợ Locale vi_VN & Lấy Mẫu top Steady-State)

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả
1. Khắc phục lỗi đọc số thập phân trên Dashboard (`HealthMonitorWindow.axaml.cs` dòng 380): hỗ trợ cả dấu phẩy `,` của locale `vi_VN` và dấu chấm `.`, chuẩn hoá `.Replace(',', '.')` trước khi parse.
2. Nâng cấp lệnh `top` từ lấy mẫu 1 lần (`top -b -n 1`) sang lệnh 2 lần lấy mẫu với cờ `LC_ALL=C` (`LC_ALL=C top -b -n 2 -d 0.5`), trích xuất số liệu từ lần lấy mẫu thứ 2 để triệt tiêu startup spike và độ lệch 11% của chính lệnh `top`.
3. Bổ sung unit tests kiểm thử toàn diện các trường hợp format `top` (dấu chấm, dấu phẩy, 1 mẫu, 2 mẫu).

## Nguồn yêu cầu
- Yêu cầu gốc: "oke, vậy tiến hành sửa đi để hiển thị chính xác"
- Workflow: WF-BUGFIX
- Agent chain: Senior Developer (fix) → Tech Lead (review) → QA Engineer (verify & sign-off)

## Phases & Steps

### Phase 1: Sửa Mã Nguồn & Viết Unit Tests
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Sửa regex & parser trong HealthMonitorWindow.axaml.cs, tách TopParser helper | senior-developer | ✅ | `steps/STEP-1.1-fix-health-monitor-parsing.md` | 2026-11-04 13:49:00 |
| 1.2 | Viết unit tests kiểm thử TopParser với locale tiếng Việt & steady-state mẫu 2 | senior-developer | ✅ | `steps/STEP-1.2-unit-tests-cpu-parsing.md` | 2026-11-04 13:50:00 |

### Phase 2: Tech Lead Review
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Code review giải pháp xử lý locale, lệnh top, backward-compatibility | tech-lead | ✅ | `steps/STEP-2.1-tech-lead-review.md` | 2026-11-04 13:50:30 |

### Phase 3: QA Kiểm Thử & Nghiệm Thu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | Chạy 100% unit tests, kiểm chứng parse số liệu thực tế từ máy 192.168.21.56 | qa-engineer | ✅ | `steps/STEP-3.1-qa-verify-and-signoff.md` | 2026-11-04 13:51:00 |

## Artifacts đã hoàn thành
- `IPGS.RemoteControl.CcuClient/TopParser.cs` (mới)
- `IPGS.RemoteControl.CcuUI/Views/HealthMonitorWindow.axaml.cs` (cập nhật)
- `IPGS.RemoteControl.Tests/TopParserTests.cs` (5 tests mới, 140/140 tests pass)
