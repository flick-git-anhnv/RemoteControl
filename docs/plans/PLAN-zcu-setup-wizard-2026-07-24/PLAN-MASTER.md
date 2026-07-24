---
task: zcu-setup-wizard
created: 2026-07-24
updated: 2026-07-24 08:58
status: completed
workflow: WF-FEATURE (rút gọn)
priority: P1
---

# PLAN MASTER: ZCU Remote Control One-Click Setup Wizard UI

## Mô tả

Xây dựng công cụ & kịch bản thiết lập tự động hóa 1-Click cho ZcuAgent (Ubuntu 22.04) từ CCU qua giao diện Wizard UI và shell script:
1. **Script cài đặt tự động (`scripts/setup-zcu-agent.sh`):** Cài đặt tự động X11 native libs, .NET 8 Runtime, ZcuAgent binary, systemd service, ufw rule, và tắt screen lock.
2. **Thư viện Installer Backend (`ZcuRemoteInstallerService` trong `IPGS.RemoteControl.CcuClient`):** Thực thi SSH kết nối tới ZCU, gửi script/lệnh cài đặt, tạo config JSON, và kiểm tra service status.
3. **Giao diện Wizard UI (`ZcuSetupWizardWindow` trong `IPGS.RemoteControl.CcuUI`):** Giao diện từng bước đơn giản (SSH Info -> Agent Settings -> Auto Install & Log -> Complete & Auto Add to Computer Profile List).

## Workflow: WF-FEATURE (rút gọn)

Agent chain:
- **Bước 1 — Tech Lead:** Thiết kế kiến trúc `ZcuRemoteInstallerService` & giao diện `ZcuSetupWizardWindow`
- **Bước 2 — Senior Developer:** Implement `setup-zcu-agent.sh`, `ZcuRemoteInstallerService` (SSH.NET), và `ZcuSetupWizardWindow` (Avalonia)
- **Bước 3 — Tech Lead:** Code review, verify build cross-platform (`win-x64`, `linux-x64`)
- **Bước 4 — UX/UI Reviewer:** Review trực quan giao diện Setup Wizard
- **Bước 5 — QA Engineer:** Verify chức năng 1-Click Setup

## Phases & Steps

| # | Bước | Agent | Status | Hoàn thành lúc |
|---|------|-------|--------|-----------------|
| 1 | Tech Lead thiết kế Installer Service & Wizard UI | Tech Lead | ✅ | 2026-07-24 08:50 |
| 2 | Senior Dev implement script + backend SSH + Avalonia Wizard UI | Senior Developer | ✅ | 2026-07-24 08:55 |
| 3 | Tech Lead code review & verify cross-platform build | Tech Lead | ✅ | 2026-07-24 08:57 |
| 4 | UX/UI Reviewer kiểm tra giao diện Wizard | UX/UI Reviewer | ✅ | 2026-07-24 08:58 |
| 5 | QA Engineer verify 1-click install | QA Engineer | ✅ | 2026-07-24 08:58 |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked
