---
task: rebuild-ccuui-verify-zcuagent
created: 2026-11-06
updated: 2026-11-06
status: done
workflow: WF-DEVOPS
priority: P2
---

# PLAN MASTER: Rebuild CcuUI & Verify ZcuAgent Binary Artifacts

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md` tương ứng.

## Mô tả
CcuUI đang chứa bản build cũ và thư mục chạy `bin\Debug` và `bin\Release` đang giữ `IPGS.RemoteControl.ZcuAgent.dll` cũ (165.376 byte Debug ngày 05/11 và 133.632 byte Release ngày 04/11). Cần tắt app CcuUI đang mở, clean/rebuild cả bản Debug và Release của `IPGS.RemoteControl.CcuUI` để cơ chế `PreserveNewest` chép đè bản ZcuAgent mới (152.064 byte, cập nhật ngày 06/11) vào output, sau đó kiểm tra và đối soát kích thước chính xác.

## Nguồn yêu cầu
- Yêu cầu gốc: Tắt app CcuUI đang chạy, Rebuild CcuUI (Debug và Release), kiểm tra file bin\Debug và bin\Release ZcuAgent.dll có đúng 152.064 byte.
- Workflow: WF-DEVOPS — CI/CD pipeline / Infrastructure & Build Packaging
- Agent chain: DEVOPS ENGINEER → DEVOPS LEAD

## Phases & Steps

### Phase 1: Build & Artifact Verification
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Kiểm tra process & Clean/Rebuild Debug CcuUI | DEVOPS ENGINEER | ✅ | `steps/STEP-1.1-check-process-and-rebuild-debug.md` | 2026-11-06 14:35 |
| 1.2 | Rebuild Release CcuUI & Kiểm tra kích thước ZcuAgent.dll | DEVOPS ENGINEER | ✅ | `steps/STEP-1.2-build-release-and-verify-artifacts.md` | 2026-11-06 14:36 |

### Phase 2: Thẩm định & Phê duyệt
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Thẩm định kết quả build và phê duyệt bàn giao | DEVOPS LEAD | ✅ | `steps/STEP-2.1-devops-lead-audit-and-signoff.md` | 2026-11-06 14:36 |

## Artifacts dự kiến (tổng)
- [x] `IPGS.RemoteControl.CcuUI/bin/Debug/net8.0/IPGS.RemoteControl.CcuUI.dll` (981.504 bytes, 14:34)
- [x] `IPGS.RemoteControl.CcuUI/bin/Release/net8.0/IPGS.RemoteControl.CcuUI.dll` (894.976 bytes, 14:36)
- [x] `IPGS.RemoteControl.CcuUI/bin/Debug/net8.0/Resources/zcu-agent/linux-x64/IPGS.RemoteControl.ZcuAgent.dll` (152.064 bytes)
- [x] `IPGS.RemoteControl.CcuUI/bin/Release/net8.0/Resources/zcu-agent/linux-x64/IPGS.RemoteControl.ZcuAgent.dll` (152.064 bytes)
- [x] `docs/plans/PLAN-rebuild-ccuui-verify-zcuagent-2026-11-06/PLAN-MASTER.md`

## Blockers
Không có

## Quyết định / Ghi chú tổng
- Tiến trình CcuUI (PID 23092) chạy trước đó đã bị đóng để giải phóng khóa file `IPGS.RemoteControl.CcuUI.exe`.
- Cả hai bản Debug và Release đã được build thành công (0 errors).
- Bản agent `IPGS.RemoteControl.ZcuAgent.dll` đóng gói trong cả 2 cấu hình đều đạt chuẩn 152.064 byte.

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-11-06 11:15 | Khởi tạo plan WF-DEVOPS | task-planner |
| 2026-11-06 11:18 | Build lần 1 và verify | devops-engineer |
| 2026-11-06 14:36 | Tắt tiến trình PID 23092, rebuild lại Debug & Release tích hợp code mới | devops-engineer |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
