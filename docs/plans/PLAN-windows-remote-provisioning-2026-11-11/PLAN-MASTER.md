---
task: windows-remote-provisioning
created: 2026-11-11
updated: 2026-11-11
status: done
workflow: WF-FEATURE
priority: P1
---

# PLAN MASTER: Triển khai Zero-Touch Windows Remote Provisioning & Quản trị Đa Nền Tảng (Linux + Windows) trên CCU

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước (mô tả đầy đủ, Handoff Log, artifact chi tiết) nằm ở `steps/STEP-[N.M]-[tên].md` tương ứng — xem cột "Step file" bên dưới.

## Mô tả
Triển khai kiến trúc **Windows Native OpenSSH Server + Zero-Touch Unattend Bootstrap** kết hợp **WinAgent** để máy tính Windows sau khi cài xong HĐH và có IPv4 có thể được cấu hình từ xa 100% từ CCU (bắt đầu từ Cài Remote, Quản lý File SFTP, Chạy lệnh từ xa, Giám sát sức khỏe, Cài đặt phần mềm) giống hệt trải nghiệm trên Ubuntu/Lubuntu.

## Nguồn yêu cầu
- Yêu cầu gốc: Triển khai remote trên Windows hoạt động toàn trình qua IPv4 như Ubuntu/Lubuntu (tương đương SSH, cấu hình từ xa bắt đầu từ cài remote mà không cần đụng chạm vào máy Windows sau khi cài HĐH).
- Workflow: WF-FEATURE / WF-ARCH — Thiết kế & Triển khai tính năng
- Agent chain: TECH LEAD → CTO → SENIOR DEVELOPER → TECH LEAD (Review) → QA ENGINEER

## Phases & Steps

> **Session isolation (GEMINI.md §16.5):** Mỗi bước ⬜/🔄 PHẢI chạy tách session — LOCAL dùng `Agent` subagent, WEB dùng `RemoteTrigger`. Agent/trigger tự tạo/cập nhật step file riêng, commit+push, rồi cập nhật đúng 1 dòng status ở bảng dưới đây.

### Phase 1: Bootstrap Zero-Touch cho Windows & Tích hợp Solution
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Tích hợp WinAgent vào `.sln`, tạo bộ Bootstrap OpenSSH/ICMP/AutoLogon (`autounattend.xml`, `bootstrap-windows-ssh.ps1`, `1-click-enable-ssh.bat`) và nâng cấp `install-win-agent.ps1` | senior-developer | ✅ | `steps/STEP-1.1-windows-bootstrap-and-sln.md` | 2026-11-11 09:19 |

### Phase 2: Nâng cấp Cài đặt Remote Agent Từ Xa Đa Nền Tảng (Linux + Windows)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Nâng cấp `ZcuRemoteInstallerService`, `ShellQuote` và `ZcuSetupWizardWindow` tự động nhận diện HĐH (Linux/Windows), đẩy và cài đặt `WinAgent` từ xa qua SSH/SFTP | senior-developer | ✅ | `steps/STEP-2.1-remote-installer-windows-support.md` | 2026-11-11 09:33 |

### Phase 3: Mở rộng Bộ Công cụ Quản trị CCU Đa Hệ Điều Hành (Linux + Windows)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | Nâng cấp `ComputerStatusChecker`, `RemoteCommandWindow` (SFTP + PowerShell), `BulkActionWindow`, `HealthMonitorWindow` và `RemoteAppInstallService` hỗ trợ máy đích Windows | senior-developer | ✅ | `steps/STEP-3.1-ccu-multi-os-tools.md` | 2026-11-11 09:55 |

### Phase 4: Kiểm thử Tự động, Code Review & Đồng bộ Tài liệu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 4.1 | Bổ sung Unit Tests đa nền tảng, chạy full test suite, Tech Lead review và cập nhật `CODE-GRAPH.md` | qa-engineer | ✅ | `steps/STEP-4.1-tests-review-and-docs.md` | 2026-11-11 10:02 |

## Artifacts dự kiến (tổng)
- [x] `IPGS.RemoteControl.sln` (bổ sung project `IPGS.RemoteControl.WinAgent`)
- [x] `scripts/windows-agent/bootstrap-windows-ssh.ps1`
- [x] `scripts/windows-agent/1-click-enable-ssh.bat`
- [x] `scripts/windows-agent/autounattend.xml`
- [x] `scripts/windows-agent/install-win-agent.ps1`
- [x] `IPGS.RemoteControl.CcuClient/ZcuRemoteInstallerService.cs`
- [x] `IPGS.RemoteControl.CcuClient/ShellQuote.cs`
- [x] `IPGS.RemoteControl.CcuClient/ComputerStatusChecker.cs`
- [x] `IPGS.RemoteControl.CcuClient/RemoteAppInstallService.cs`
- [x] `IPGS.RemoteControl.CcuUI/Views/ZcuSetupWizardWindow.axaml.cs`
- [x] `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml.cs`
- [x] `IPGS.RemoteControl.CcuUI/Views/BulkActionWindow.axaml.cs`
- [x] `IPGS.RemoteControl.CcuUI/Views/HealthMonitorWindow.axaml.cs`
- [x] `IPGS.RemoteControl.Tests/WindowsRemoteProvisioningTests.cs`
- [x] `code-graph/CODE-GRAPH.md`

## Blockers
Không có

## Quyết định / Ghi chú tổng
- Sử dụng **Windows Native OpenSSH Server** (`sshd` + `sftp-server.exe`) với `DefaultShell = powershell.exe` để giữ nguyên 100% kiến trúc `SSH.NET` (`Renci.SshNet`) trên CCU.
- Cung cấp cả `autounattend.xml` (Zero-Touch 100% khi cài Windows từ USB) và `1-click-enable-ssh.bat` (cho máy đã cài sẵn Windows) để mở cổng TCP `22` + cho phép Ping ICMPv4 + bật AutoLogon.
- Tự động phát hiện HĐH máy đích qua SSH (`DetectRemoteOsAsync`) để điều phối luồng cài đặt (`ZcuAgent` cho Linux, `WinAgent` cho Windows) và câu lệnh quản trị mà người dùng không cần cấu hình phức tạp.

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-11-11 | Khởi tạo Plan Master & 4 Step files theo phê duyệt của User | task-planner |
| 2026-11-11 | Hoàn thành toàn bộ 4 Phases (1.1 → 4.1): Bootstrap Windows OpenSSH, Remote Installer WinAgent, Multi-OS CCU Tools, 264/264 Unit Tests Pass 100% & CODE-GRAPH v4.1 | senior-developer, tech-lead, qa-engineer |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
**Cách đọc nhanh:** đọc MASTER trước → nếu cần chi tiết bước cụ thể mới mở step file tương ứng.
