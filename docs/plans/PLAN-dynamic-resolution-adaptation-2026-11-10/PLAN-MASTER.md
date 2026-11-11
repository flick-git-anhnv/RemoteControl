---
task: dynamic-resolution-adaptation
created: 2026-11-10
updated: 2026-11-10
status: completed
workflow: WF-FEATURE
priority: P2
---

# PLAN MASTER: Tự động điều chỉnh độ phân giải & tỷ lệ màn hình theo cấu hình máy remote (CCU & ZCU)

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả
Khắc phục tình trạng lệch tỷ lệ khung hình và độ phân giải cố định khi điều khiển từ xa giữa CCU (máy quản trị) và ZCU (máy trạm Kiosk Linux):
1. **Phía CCU Client (`RemoteScreenWindow`, `RemoteScreenViewModel`):**
   - Tự động nhận diện độ phân giải và tỷ lệ khung hình (Aspect Ratio: 4:3, 5:4, 16:9, 9:16 dọc...) của máy đích khi nhận frame đầu tiên hoặc khi máy đích đổi độ phân giải.
   - Tự động điều chỉnh kích thước cửa sổ CCU (Auto-fit window aspect ratio) vừa vặn theo tỷ lệ thực của máy đích, loại bỏ dải viền đen (letterboxing) thừa mà không gây méo/bóp dẹt hình ảnh.
   - Đảm bảo phép ánh xạ toạ độ chuột (`MapToZcuCoords`) luôn chuẩn xác 100% trong mọi kích thước và chế độ.
   - Bổ sung nút chuyển đổi chế độ hiển thị linh hoạt trên thanh công cụ TopBar (Tự động theo tỷ lệ Auto-fit / Giữ tỷ lệ viền đen Uniform / Kéo giãn đầy khung Fill).
2. **Phía ZCU Agent (`IPGS.RemoteControl.ZcuAgent`):**
   - Tự động nhận diện khi máy đích hoạt động ở chế độ không cắm màn hình vật lý (Headless / không có EDID, Linux Xorg tự rơi về chế độ an toàn VESA 1024x768).
   - Khi phát hiện trạng thái headless, ZcuAgent tự động kích hoạt chế độ hiển thị ảo tối ưu 1920x1080 (16:9) thông qua `xrandr` dynamic modeline, giúp phiên remote đạt độ phân giải sắc nét tiêu chuẩn.
   - Khi có màn hình vật lý cắm vào cổng HDMI/VGA, ZcuAgent tự động nhường quyền cho driver và giữ nguyên độ phân giải phần cứng thật theo EDID của màn hình.

## Nguồn yêu cầu
- Yêu cầu gốc: "oke, giờ sửa để nó sẽ tự điều chỉnh theo cấu hình của máy remote cho thích hợp, tự nhận diện chứ kh fix cứng nx"
- Scope Check đã xác nhận với user:
  1. Phạm vi: Cả hai phía CCU (tự nhận diện tỷ lệ để chỉnh cửa sổ/hiển thị) và ZCU (tự tối ưu độ phân giải khi không cắm màn).
  2. Giao diện CCU: Mặc định tự động điều chỉnh kích thước cửa sổ CCU theo đúng tỷ lệ màn hình máy đích (Auto-fit window aspect ratio).
- Workflow: WF-FEATURE / WF-REFACTOR
- Agent chain: Tech Lead → Senior Developer → Tech Lead (Review) → UX-UI Reflection → QA Engineer

## Phases & Steps

### Phase 1: Thiết kế kỹ thuật & Protocol
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Thiết kế cơ chế Auto-fit Aspect Ratio trên CCU & Headless Resolution Adapter trên ZCU | tech-lead | ✅ | `steps/STEP-1.1-tech-design.md` | 2026-11-10 14:58 |

### Phase 2: Triển khai mã nguồn
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Hiện thực Auto-fit Aspect Ratio & Display Mode switcher trên CCU UI | senior-developer | ✅ | `steps/STEP-2.1-ccu-autofit-ui.md` | 2026-11-10 15:05 |
| 2.2 | Hiện thực Headless Resolution Adapter tự động cấu hình 1080p khi không cắm màn trên ZcuAgent | senior-developer | ✅ | `steps/STEP-2.2-zcu-headless-adapter.md` | 2026-11-10 15:08 |
| 2.3 | Bổ sung Unit Tests kiểm thử tính toán Aspect Ratio, toạ độ chuột và parser modeline | senior-developer | ✅ | `steps/STEP-2.3-unit-tests.md` | 2026-11-10 15:09 |

### Phase 3: Kiểm soát chất lượng & Kiểm thử thực địa
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | Code review, chạy toàn bộ Unit Tests & Verify PR | tech-lead | ✅ | `steps/STEP-3.1-review-and-verify.md` | 2026-11-10 15:11 |
| 3.2 | UX-UI Reflection kiểm tra thực tế hiển thị cửa sổ, co giãn tỷ lệ và độ nhạy toạ độ chuột | ux-ui-reflection | ✅ | `steps/STEP-3.2-ux-ui-reflection.md` | 2026-11-10 15:13 |
| 3.3 | Deploy thử nghiệm lên máy ZCU thực địa `192.168.21.230` và nghiệm thu | qa-engineer | ✅ | `steps/STEP-3.3-field-testing-230.md` | 2026-11-10 15:13 |

## Artifacts dự kiến (tổng)
- `IPGS.RemoteControl.CcuUI/Views/RemoteScreenWindow.axaml(.cs)`
- `IPGS.RemoteControl.CcuUI/ViewModels/RemoteScreenViewModel.cs`
- `IPGS.RemoteControl.CcuUI/Views/RemoteScreenControl.axaml`
- `IPGS.RemoteControl.ZcuAgent/Capture/HeadlessResolutionAdapter.cs`
- `IPGS.RemoteControl.ZcuAgent/Program.cs` / `RemoteControlHostedService.cs`
- `IPGS.RemoteControl.Tests/ResolutionAdaptationTests.cs`
- `docs/plans/PLAN-dynamic-resolution-adaptation-2026-11-10/PLAN-MASTER.md`

## Blockers
Không có

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-11-10 | Khởi tạo Plan Master sau Scope Check với người dùng | task-planner |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
