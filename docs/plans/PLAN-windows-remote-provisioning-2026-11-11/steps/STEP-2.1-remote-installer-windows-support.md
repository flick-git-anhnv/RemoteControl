---
step: 2.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-11-11T09:33:00+07:00
deps: ["1.1"]
---

# STEP 2.1 — Nâng cấp Cài đặt Remote Agent Từ Xa Đa Nền Tảng (Linux + Windows)

## Input nhận
Từ Step 1.1: `WinAgent` đã nằm trong `.sln` và `scripts/windows-agent/install-win-agent.ps1` đã hỗ trợ đầy đủ tham số `-Port`, `-Token`, `-AllowedClientIPs`, `-TargetFps`, `-JpegQuality` cùng bảo toàn `agent-tls.pfx`.

## Nhiệm vụ
1. Mở rộng `ShellQuote.cs`: thêm `QuotePowerShell(string value)` và `ValidateWindowsUsername` / chuẩn hóa đường dẫn từ xa cho cả Linux & Windows.
2. Nâng cấp `ZcuRemoteInstallerService.cs`:
   - Thêm `DetectRemoteOsAsync(SshClient ssh)` để tự nhận diện máy đích là Windows hay Linux qua SSH.
   - Nâng cấp `ReadAgentConfigAsync` và `RestartAgentAsync` hoạt động trên cả Linux (`/home/<user>/ipgs/remote-agent/appsettings.json`) và Windows (`C:\Program Files\KztekWinAgent\appsettings.json`, Scheduled Task `KztekWinAgent`).
   - Nâng cấp `InstallZcuAgentAsync`: nếu máy đích là Windows, tự động tìm hoặc publish `IPGS.RemoteControl.WinAgent` (`win-x64`), đẩy lên máy đích qua SFTP (xử lý chuẩn đường dẫn SFTP Windows `/C:/...` hoặc `C:/...`), cấu hình `appsettings.json`, mở Firewall cổng Agent + ICMP Ping, tắt Sleep/Lock Screen, tạo và khởi chạy Scheduled Task `KztekWinAgent`.
3. Cập nhật `ZcuSetupWizardWindow.axaml.cs` để hiển thị trạng thái và tìm/publish binary phù hợp.

## Definition of Done
- [x] `ZcuRemoteInstallerService` tự động nhận diện Linux vs Windows và cài đặt thành công `ZcuAgent` hoặc `WinAgent`.
- [x] `ReadAgentConfigAsync` và `RestartAgentAsync` hỗ trợ cả 2 HĐH.
- [x] Build sạch 0 lỗi.

## Đã làm
1. **Mở rộng `IPGS.RemoteControl.CcuClient/ShellQuote.cs`** (chuyển sang `public static class ShellQuote` để tái sử dụng xuyên suốt `CcuClient`, `CcuUI` và `Tests`):
   - Bổ sung `QuotePowerShell(string value)` bọc chuỗi trong `'...'`, escape `'` thành `''` và loại bỏ `\r`, `\n`.
   - Bổ sung `ValidateWindowsUsername(string username, string paramName)` hỗ trợ tài khoản Windows (`DOMAIN\user`, `user@domain`, khoảng trắng trong tên user) và chặn hoàn toàn ký tự command injection (`; | & $ ` " ' < > ( ) \r \n ..`).
   - Bổ sung bộ hàm chuẩn hóa đường dẫn SFTP đa nền tảng: `IsWindowsSftpPath`, `NormalizeSftpPath`, `ToWindowsLocalPath`, `GetParentSftpPath`, `CombineSftpPath` xử lý mượt mà cả đường dẫn Linux (`/home/kztek`) lẫn Windows OpenSSH SFTP (`/C:/Users/kztek` ↔ `C:\Users\kztek`).
2. **Nâng cấp `IPGS.RemoteControl.CcuClient/ZcuRemoteInstallerService.cs`**:
   - Bổ sung enum `RemoteOsType { Linux, Windows }`, lớp `RemoteOsDetector`, và các phương thức `DetectRemoteOs`, `DetectRemoteOsAsync`, `ParseOsDetectionOutput`, `ParseAgentConfigJson`.
   - Nâng cấp `ReadAgentConfigAsync` và `RestartAgentAsync` tự động nhận diện HĐH: hỗ trợ đọc `C:\Program Files\KztekWinAgent\appsettings.json` và khởi động lại Scheduled Task `KztekWinAgent` (trả về `"active"` tương thích 100% với UI hiện tại).
   - Nâng cấp `InstallZcuAgentAsync` hỗ trợ 2 nhánh độc lập:
     - **Linux**: giữ nguyên toàn bộ quy trình 8 bước cài đặt `ZcuAgent` (`linux-x64`), kèm chốt chặn G024 (xóa `libhost*`/`libclr*` nếu framework-dependent) và G028 (giữ `agent-tls.pfx` và `appsettings.json`).
     - **Windows**: tự động kiểm tra phiên bản Windows & `.NET 8.0` runtime trên máy đích; gọi `FindOrPublishWinAgentFolder` để tìm hoặc tự động `dotnet publish` gói `IPGS.RemoteControl.WinAgent` (`win-x64`, tự chọn `--self-contained true` nếu máy đích chưa cài `.NET 8`); upload bộ cài cùng `install-win-agent.ps1` (kèm UTF-8 BOM) lên thư mục staging qua SFTP; thực thi `install-win-agent.ps1` với đầy đủ tham số `-Port`, `-Token`, `-AllowedClientIPs`, `-TargetFps`, `-JpegQuality`, `-StartImmediately`; kiểm tra tiến trình/Scheduled Task `KztekWinAgent` và dọn sạch thư mục staging.
3. **Cập nhật `ZcuSetupWizardWindow.axaml` & `ZcuSetupWizardWindow.axaml.cs`**:
   - Cập nhật mô tả giao diện và trạng thái hiển thị hỗ trợ cả Linux và Windows.
   - Chuyển `FindZcuAgentPublishDirAsync` sang `FindPrebuiltZcuAgentPublishDir` không chặn luồng trước khi kết nối SSH, để `ZcuRemoteInstallerService` tự động chọn/publish đúng binary (`ZcuAgent` hoặc `WinAgent`) sau khi nhận diện HĐH máy đích.

## Artifact
- `IPGS.RemoteControl.CcuClient/ShellQuote.cs`
- `IPGS.RemoteControl.CcuClient/ZcuRemoteInstallerService.cs`
- `IPGS.RemoteControl.CcuUI/Views/ZcuSetupWizardWindow.axaml`
- `IPGS.RemoteControl.CcuUI/Views/ZcuSetupWizardWindow.axaml.cs`

## Quyết định quan trọng
- Khi upload `install-win-agent.ps1` qua SFTP lên máy đích Windows, tự động gắn tiền tố UTF-8 BOM (`0xEF, 0xBB, 0xBF`) trong `GetWinAgentInstallerScriptBytesWithBom()` để đảm bảo Windows PowerShell 5.1 đọc chuẩn mã UTF-8 100% (tránh lỗi CP1252 theo Gotcha G022).
- Khi máy đích Windows chưa cài `.NET 8.0` runtime, `FindOrPublishWinAgentFolder` tự động publish `IPGS.RemoteControl.WinAgent` với `--self-contained true` (`win-x64-sc`) để máy Windows mới cài hoạt động ngay lập tức không cần Internet.

## Handoff Payload — bước sau đọc phần này (chỉ phần này, không cần đọc "Đã làm")
- do_not_redo: Đã hoàn thiện `ShellQuote` (`public static`, có `QuotePowerShell`, `ValidateWindowsUsername`, `IsWindowsSftpPath`, `NormalizeSftpPath`, `ToWindowsLocalPath`, `GetParentSftpPath`, `CombineSftpPath`) và `ZcuRemoteInstallerService` / `RemoteOsDetector` (`RemoteOsType`, `DetectRemoteOs`, `DetectRemoteOsAsync`, `ParseOsDetectionOutput`, `ReadAgentConfigAsync`, `RestartAgentAsync`, `InstallZcuAgentAsync` cho cả Linux và Windows).
- watch_out: Trên Windows OpenSSH SFTP, `sftp.WorkingDirectory` trả về dạng `/C:/Users/<user>` — luôn dùng `ShellQuote.NormalizeSftpPath`, `ShellQuote.GetParentSftpPath`, `ShellQuote.CombineSftpPath` khi thao tác SFTP và `ShellQuote.ToWindowsLocalPath` khi chuyển sang đường dẫn truyền vào PowerShell/cmd.
- next_inputs: Bước 3.1 sử dụng `RemoteOsDetector.DetectRemoteOs(ssh)` / `DetectRemoteOsAsync(ssh)` và các hàm trong `ShellQuote` để nâng cấp `ComputerStatusChecker`, `RemoteCommandWindow`, `BulkActionWindow`, `HealthMonitorWindow` và `RemoteAppInstallService` hỗ trợ máy đích Windows.

## Commit
- Hash: pending
- Đã push: không (local branch)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
