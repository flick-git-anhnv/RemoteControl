---
step: 1.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-11-11T09:19:00+07:00
deps: []
---

# STEP 1.1 — Tích hợp WinAgent vào Solution & Tạo bộ Bootstrap Zero-Touch cho Windows

## Input nhận
Kiến trúc đã được CTO và User phê duyệt: dùng Windows Native OpenSSH Server (`sshd` + `sftp-server.exe`, `DefaultShell = powershell.exe`) kết hợp bộ Bootstrap Zero-Touch (`autounattend.xml` cho USB cài Win mới + `1-click-enable-ssh.bat` / `bootstrap-windows-ssh.ps1` cho máy đã cài Win) và nâng cấp `install-win-agent.ps1`.

## Nhiệm vụ
1. Thêm project `IPGS.RemoteControl.WinAgent` vào `IPGS.RemoteControl.sln`.
2. Tạo `scripts/windows-agent/bootstrap-windows-ssh.ps1`, `scripts/windows-agent/1-click-enable-ssh.bat`, và `scripts/windows-agent/autounattend.xml` (đồng bộ sang `IPGS.RemoteControl.CcuUI/Resources/scripts/windows-agent/`):
   - Chuyển Network Profile sang `Private`, mở Firewall cho phép Ping (**ICMPv4/v6 Echo Request**) và cổng **TCP `22`**.
   - Cài đặt/bật dịch vụ `sshd` và `ssh-agent` (ưu tiên gói offline `OpenSSH-Win64.zip` cạnh script nếu có, fallback sang Windows Capability / dịch vụ có sẵn).
   - Đặt `DefaultShell` của OpenSSH về `powershell.exe`, đảm bảo `PasswordAuthentication yes` và `Subsystem sftp sftp-server.exe`.
   - Bật `LocalAccountTokenFilterPolicy = 1`, tắt Sleep/Monitor timeout (`powercfg`), tắt Lock Screen, hỗ trợ cấu hình `AutoAdminLogon`.
3. Nâng cấp `scripts/windows-agent/install-win-agent.ps1`:
   - Bảo toàn `agent-tls.pfx` khi cài đè (tránh lỗi G028 lệch vân tay TLS).
   - Nhận thêm tham số `-TargetFps` và `-JpegQuality` để ghi vào `appsettings.json`.
   - Tắt khóa màn hình/sleep và khởi chạy Scheduled Task `KztekWinAgent` ngay lập tức.

## Definition of Done
- [x] `dotnet build IPGS.RemoteControl.sln` biên dịch sạch cả 6 project (bao gồm `WinAgent`).
- [x] Bộ file Bootstrap (`bootstrap-windows-ssh.ps1`, `1-click-enable-ssh.bat`, `autounattend.xml`, `install-win-agent.ps1`) đầy đủ, chuẩn UTF-8, pass 100% kiểm tra AST parser PowerShell 5.1 & XML.

## Đã làm
1. **Tích hợp `IPGS.RemoteControl.WinAgent` vào `IPGS.RemoteControl.sln`**:
   - Khai báo project `IPGS.RemoteControl.WinAgent\IPGS.RemoteControl.WinAgent.csproj` (`{8E3B9D14-7A2F-4C61-B908-D4512F8A6C09}`) với đầy đủ cấu hình `Debug|Any CPU`, `Debug|x64`, `Debug|x86`, `Release|Any CPU`, `Release|x64`, `Release|x86`.
   - Chạy `dotnet build IPGS.RemoteControl.sln` đạt **0 Warning(s), 0 Error(s)** cho cả 6 project.
2. **Xây dựng bộ công cụ Bootstrap Zero-Touch cho Windows (`scripts/windows-agent/`)**:
   - `bootstrap-windows-ssh.ps1`: Tự động chuyển Network Connection Profile sang `Private`; mở Firewall cho phép Ping (`ICMPv4 Type 8` & `ICMPv6 Type 128`); cài đặt/kích hoạt dịch vụ `sshd` và `ssh-agent` (ưu tiên gói offline `OpenSSH-Win64.zip`/`OpenSSH-Win64` cạnh script, fallback sang `Add-WindowsCapability`/`dism`); cấu hình `sshd_config` (`Port`, `PasswordAuthentication yes`, `PubkeyAuthentication yes`, `Subsystem sftp sftp-server.exe`); đặt `DefaultShell = powershell.exe` (`-NoLogo -Command`); mở cổng TCP 22 trên Firewall; bật `LocalAccountTokenFilterPolicy = 1`; tắt Sleep/Monitor timeout (`powercfg`) + tắt Lock Screen (`NoLockScreen = 1`); hỗ trợ cấu hình `AutoAdminLogon`.
   - `1-click-enable-ssh.bat`: Tự động kiểm tra quyền Administrator, tự nâng quyền UAC (`RunAs`) nếu cần và chạy `bootstrap-windows-ssh.ps1`.
   - `autounattend.xml`: Cấu hình Windows 10/11 Unattended Setup (`amd64`), bỏ qua toàn bộ màn hình OOBE, tạo tài khoản Local Admin `kztek` (`kztek123`), bật `AutoLogon`, và thực thi `FirstLogonCommands` tự động chạy `bootstrap-windows-ssh.ps1` (hoặc bật trực tiếp ICMP Ping + OpenSSH + Port 22 + tắt Sleep).
3. **Nâng cấp `scripts/windows-agent/install-win-agent.ps1` & đồng bộ sang `CcuUI/Resources`**:
   - Bổ sung tham số `-TargetFps` (mặc định 30), `-JpegQuality` (mặc định 70), `-StartImmediately` (mặc định `$true`).
   - Bảo toàn cả `appsettings.json` và chứng chỉ TLS tự ký `agent-tls.pfx` khi cài đè (tránh mất vân tay TLS ghim trên CCU theo Gotcha G028).
   - Mở Firewall cho cả cổng TCP `$Port` (`Kztek WinAgent`) và rule `KZTEK Allow ICMPv4 Ping`.
   - Tự động dò tìm user đang đăng nhập Desktop (`Win32_ComputerSystem.UserName` -> `explorer.exe` owner -> `quser.exe` -> fallback `$env:USERNAME`) để đăng ký Scheduled Task `KztekWinAgent` chạy trong phiên Interactive của user đó ngay cả khi cài đặt từ xa qua SSH.
   - Đồng bộ toàn bộ `scripts/windows-agent/*` sang `IPGS.RemoteControl.CcuUI/Resources/scripts/windows-agent/`.

## Artifact
- `IPGS.RemoteControl.sln`
- `scripts/windows-agent/bootstrap-windows-ssh.ps1`
- `scripts/windows-agent/1-click-enable-ssh.bat`
- `scripts/windows-agent/autounattend.xml`
- `scripts/windows-agent/install-win-agent.ps1`
- `IPGS.RemoteControl.CcuUI/Resources/scripts/windows-agent/bootstrap-windows-ssh.ps1`
- `IPGS.RemoteControl.CcuUI/Resources/scripts/windows-agent/1-click-enable-ssh.bat`
- `IPGS.RemoteControl.CcuUI/Resources/scripts/windows-agent/autounattend.xml`
- `IPGS.RemoteControl.CcuUI/Resources/scripts/windows-agent/install-win-agent.ps1`

## Quyết định quan trọng
- Đảm bảo toàn bộ script `.ps1` tương thích 100% với bộ parser AST của Windows PowerShell 5.1 cả ở chế độ UTF-8 lẫn ANSI CP1252 (không chứa byte `0x93`/`0x94` như ký tự `ồ` bên trong chuỗi ngoặc kép `"..."`, hoặc `0x91`/`0x92` như `ố` bên trong chuỗi ngoặc đơn `'...'`).

## Handoff Payload — bước sau đọc phần này (chỉ phần này, không cần đọc "Đã làm")
- do_not_redo: Đã tích hợp `IPGS.RemoteControl.WinAgent` vào `IPGS.RemoteControl.sln` và đã hoàn thiện + đồng bộ toàn bộ `scripts/windows-agent/` sang `IPGS.RemoteControl.CcuUI/Resources/scripts/windows-agent/`.
- watch_out: Khi gọi `install-win-agent.ps1` từ xa qua SSH trong Bước 2.1 (`ZcuRemoteInstallerService`), truyền đầy đủ `-Port`, `-Token`, `-AllowedClientIPs`, `-TargetFps`, `-JpegQuality` và đọc lại chứng chỉ/fingerprint hoặc log từ `$env:ProgramFiles\KztekWinAgent`. Lưu ý trên Windows PowerShell 5.1, tránh dùng ký tự `ồ` trong `"..."` hoặc `ố` trong `'...'` nếu truyền script không có BOM.
- next_inputs: Bước 2.1 sử dụng `scripts/windows-agent/install-win-agent.ps1` (hoặc bản trong `Resources/scripts/windows-agent/`) và project `IPGS.RemoteControl.WinAgent` để nâng cấp `ZcuRemoteInstallerService`, `ShellQuote` và `ZcuSetupWizardWindow` hỗ trợ tự động nhận diện Windows và đẩy/cài đặt `WinAgent` qua SSH/SFTP.

## Commit
- Hash: pending
- Đã push: không (local branch)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
