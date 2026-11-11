---
step: 4.1
plan: ../PLAN-MASTER.md
agent: qa-engineer
status: done
completed_at: 2026-11-11 10:02
deps: ["3.1"]
---

# STEP 4.1 — Bổ sung Unit Tests Đa Nền Tảng, Review & Đồng bộ Tài liệu CODE-GRAPH

## Input nhận
Từ Step 1.1, 2.1, 3.1: Toàn bộ tính năng Bootstrap Windows, Cài đặt WinAgent từ xa qua SSH/SFTP và Bộ công cụ quản trị đa nền tảng trên CCU đã hoàn tất.

## Nhiệm vụ
1. Tạo `IPGS.RemoteControl.Tests/WindowsRemoteProvisioningTests.cs` kiểm thử tự động:
   - Nhận diện HĐH từ chuỗi probe (`RemoteOsDetector` / `ZcuRemoteInstallerService`).
   - Escape & quote tham số PowerShell (`ShellQuote.QuotePowerShell`), chuẩn hóa đường dẫn SFTP Windows (`/C:/Users/...` ↔ `C:\Users\...`) và tính toán thư mục cha (`NavigateUp`).
   - Parse thông số CPU/RAM/Disk và danh sách tiến trình từ output Windows PowerShell.
2. Chạy `dotnet build IPGS.RemoteControl.sln` và `dotnet test IPGS.RemoteControl.sln` đảm bảo 100% test pass, 0 lỗi.
3. Cập nhật `code-graph/CODE-GRAPH.md` phản ánh đầy đủ kiến trúc Windows Remote Provisioning và xuất DOCX/PDF cho các file `.md` đã tạo/sửa bằng `scripts/md_to_docx_kztek.py`.

## Definition of Done
- [x] Toàn bộ Unit Tests (cũ + mới: 264/264 tests) pass 100%, 0 Warning, 0 Error.
- [x] `code-graph/CODE-GRAPH.md` được cập nhật lên v4.1 và xuất DOCX theo Rule R1 & R3.

## Đã làm
1. **Xây dựng bộ kiểm thử tự động `IPGS.RemoteControl.Tests/WindowsRemoteProvisioningTests.cs` (95 test cases mở rộng, nâng tổng số test toàn solution lên 264 tests):**
   - Kiểm thử `ShellQuote.QuotePowerShell` (escape dấu nháy đơn `' -> ''`, trung hòa ký tự command injection `; | & $(...)` và cắt bỏ `\r\n`) và `ShellQuote.ValidateWindowsUsername` (cho phép `DOMAIN\user`, `user@domain`, khoảng trắng hợp lệ; từ chối mọi payload command injection).
   - Kiểm thử bộ chuẩn hóa & điều hướng đường dẫn SFTP đa nền tảng: `IsWindowsSftpPath`, `NormalizeSftpPath` (`C:\Users\kztek\` → `/C:/Users/kztek`), `ToWindowsLocalPath` (`/C:/Users/kztek/app.msi` → `C:\Users\kztek\app.msi`), `GetParentSftpPath` và `CombineSftpPath`.
   - Kiểm thử `RemoteOsDetector.ParseOsDetectionOutput` (nhận diện chính xác Linux `uname -s` và Windows `Windows_NT`, `Microsoft Windows [Version 10.0...]`, PowerShell/cmd error output) và `ZcuRemoteInstallerService.ParseAgentConfigJson` (hỗ trợ UTF-8 BOM, comment, trailing comma).
   - Kiểm thử `ComputerStatusChecker.ParseMetricsOutput` (cả dấu chấm và dấu phẩy thập phân locale) và `BuildWindowsMetricsCommand`.
   - Kiểm thử `RemoteCommandWindow` (`TranslateQuickCommandForWindows`, `BuildOpenTerminalHereCommand`, `BuildRecursiveDeleteCommand`), `HealthMonitorWindow` (`ParseHealthOutput` 5 section Windows PowerShell, `BuildKillProcessCommand`), và `RemoteAppInstallService` (`ValidateInstallerPackagePath` chặn cài nhầm gói chéo HĐH, `BuildWindowsInstallCommand` cho `.msi` và `.exe`).
   - Kiểm thử tính toàn vẹn của bộ script Bootstrap Windows (`bootstrap-windows-ssh.ps1`, `1-click-enable-ssh.bat`, `autounattend.xml` well-formed XML, `install-win-agent.ps1`).
2. **Thực thi Build & Test toàn diện (`IPGS.RemoteControl.sln`):**
   - `dotnet build IPGS.RemoteControl.sln`: **0 Warning(s), 0 Error(s)** trên cả 6 projects.
   - `dotnet test IPGS.RemoteControl.sln`: **264/264 Passed (100%)**.
3. **Code Review (Tech Lead) & Đồng bộ `code-graph/CODE-GRAPH.md` (v4.1):**
   - Cập nhật đầy đủ kiến trúc Zero-Touch Windows Remote Provisioning & Multi-OS CCU Management vào `code-graph/CODE-GRAPH.md` và xuất DOCX chuẩn KZTEK.

## Artifact
- `IPGS.RemoteControl.Tests/WindowsRemoteProvisioningTests.cs`
- `code-graph/CODE-GRAPH.md` & `code-graph/CODE-GRAPH.docx`

## Quyết định quan trọng
- Kiểm tra chéo (Cross-OS package guard) trong `RemoteAppInstallService.ValidateInstallerPackagePath` được đưa vào unit test tự động để đảm bảo người vận hành không thể cài nhầm gói `.deb` lên máy Windows hoặc `.msi`/`.exe` lên máy Linux.
- Tự động kiểm tra tính hợp lệ cấu trúc XML (`XDocument.Load`) của `scripts/windows-agent/autounattend.xml` ngay trong xUnit test suite để ngăn ngừa lỗi cú pháp XML làm gián đoạn quá trình cài đặt Windows Zero-Touch.

## Handoff Payload — bước sau đọc phần này (chỉ phần này, không cần đọc "Đã làm")
- do_not_redo: Toàn bộ 4 Phase (Step 1.1 → 4.1) của kế hoạch `PLAN-windows-remote-provisioning-2026-11-11` đã hoàn thành 100%, 264/264 unit tests pass, 0 warnings, 0 errors.
- watch_out: Không có.
- next_inputs: Sẵn sàng bàn giao tổng kết cho Dispatcher và User.

## Commit
- Hash: (cập nhật trong git history của branch `main`)
- Đã push: local commit

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
