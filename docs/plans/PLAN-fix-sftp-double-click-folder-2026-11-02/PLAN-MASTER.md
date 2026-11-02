---
task: fix-sftp-double-click-folder
created: 2026-11-02
updated: 2026-11-02
status: completed
workflow: WF-BUGFIX
priority: P2
---

# PLAN MASTER: Sửa Lỗi Không Nhấn Đúp Chuột Được Để Vào Thư Mục Trong Quản Lý File SFTP

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md` tương ứng.

## Mô tả
Trong cửa sổ "Quản lý File & Lệnh (SFTP / SSH)" (`RemoteCommandWindow`), người dùng không thể nhấn đúp chuột (Double-Click / Double-Tapped) vào các thư mục con trong danh sách `PART_FileListBox` để mở và duyệt thư mục bên trong. Nguyên nhân do hàm `OnFileItemDoubleTapped` kiểm tra sai `sender.DataContext` (vốn là ListBox chứ không phải item), dẫn đến điều kiện mở thư mục luôn bị bỏ qua.

## Nguồn yêu cầu
- Yêu cầu gốc: "sửa phần này ko nhấn được đúp để vào folder trong" kèm ảnh chụp màn hình tab SFTP.
- Workflow: WF-BUGFIX — Sửa lỗi tính năng SFTP File Browser
- Agent chain: Senior Developer → Tech Lead → UX-UI Reflection → QA Engineer

## Phases & Steps

### Phase 1: Triage Root Cause & Sửa Code (Senior Developer)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Chẩn đoán nguyên nhân gốc rễ và sửa logic DoubleTapped + KeyDown trên ListBox SFTP | senior-developer | ✅ | `steps/STEP-1.1-triage-root-cause-fix.md` | 2026-11-02 08:55:28 |

### Phase 2: Review Code & Kiểm tra Biên (Tech Lead)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Rà soát code review, đảm bảo an toàn null-safety, POSIX navigation và build sạch | tech-lead | ✅ | `steps/STEP-2.1-tech-lead-code-review.md` | 2026-11-02 08:56:20 |

### Phase 3: Phản biện UI/UX (UX-UI Reflection)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | Đánh giá trải nghiệm tương tác chuột & phím (Double click, Enter, Backspace, hit testing) | ux-ui-reviewer | ✅ | `steps/STEP-3.1-ux-reflection-review.md` | 2026-11-02 08:56:50 |

### Phase 4: Kiểm thử Xác minh & Đóng Bug (QA Engineer)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 4.1 | Chạy test suite, kiểm tra kết nối SFTP thật tới 192.168.21.230 và xác nhận fix | qa-engineer | ✅ | `steps/STEP-4.1-qa-verify.md` | 2026-11-02 08:57:04 |

## Artifacts dự kiến (tổng)
- [x] `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml`
- [x] `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml.cs`
- [x] `IPGS.RemoteControl.Tests/RemoteFileItemTests.cs` (9 unit tests mới)
- [x] Entry G018 trong `.gemini/shared/GOTCHAS.md`

## Blockers
Không có

## Quyết định / Ghi chú tổng
- `e.Source` và `sender` trong Avalonia ListBox: `sender` là chính ListBox, `e.Source` là element được click (`TextBlock`, `StackPanel`, `ListBoxItem`).
- Bổ sung cả 4 cơ chế nhận diện DataContext: `(sender as Control)?.DataContext`, `(e.Source as StyledElement)?.DataContext`, `(e.Source as Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext`, và `listBox?.SelectedItem`.
- Bổ sung phím tắt trực quan: Phím `Enter` mở thư mục đang chọn, phím `Backspace` lên thư mục cha (`NavigateUpAsync`).
- Chuẩn hóa hàm `NavigateUpAsync` theo định dạng đường dẫn POSIX `/` chuẩn Linux.
- 117/117 unit tests đạt 100% Pass.

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-11-02 | Tạo plan sửa lỗi double-click folder trong SFTP tab | task-planner |
| 2026-11-02 | Hoàn thành sửa code, review kiến trúc, UX-UI reflection và QA verify | qa-engineer |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
