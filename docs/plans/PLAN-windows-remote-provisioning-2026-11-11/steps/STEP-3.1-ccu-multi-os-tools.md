---
step: 3.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-11-11T09:55:00+07:00
deps: ["2.1"]
---

# STEP 3.1 — Mở rộng Bộ Công cụ Quản trị CCU Đa Hệ Điều Hành (Linux + Windows)

## Input nhận
Từ Step 2.1: `ZcuRemoteInstallerService` và `ShellQuote` đã có helper nhận diện HĐH từ xa (`DetectRemoteOsAsync` / `RemoteOsDetector`) và quote PowerShell.

## Nhiệm vụ
1. Nâng cấp `ComputerStatusChecker.cs`: lấy chỉ số CPU/RAM/Disk trên thẻ máy tính cho cả Linux (`top`/`free`/`df`) và Windows (PowerShell lấy CPU load, Memory sử dụng và ổ `C:`).
2. Nâng cấp `RemoteCommandWindow.axaml.cs` & `BulkActionWindow.axaml.cs`:
   - Tự nhận diện máy đích là Windows hay Linux khi thực thi lệnh SSH: trên Windows gửi thẳng lệnh vào PowerShell/CMD (không bọc `env DISPLAY=:0 ... bash -c`), ánh xạ các Snippet lệnh nhanh sang PowerShell khi đích là Windows.
   - Trên Tab Quản lý File (SFTP): lấy thư mục khởi đầu từ `_sftpClient.WorkingDirectory` thay vì hardcode `/home/{_sshUser}`; chuẩn hóa điều hướng thư mục cha (`NavigateUpAsync`), nút `>_ CMD tại đây` và xóa thư mục đệ quy cho cả Linux và Windows.
3. Nâng cấp `HealthMonitorWindow.axaml.cs` và `RemoteAppInstallService.cs`:
   - `HealthMonitorWindow`: hỗ trợ thu thập CPU, RAM, Disk, danh sách Top Processes và dừng tiến trình (`Stop-Process`) khi máy đích là Windows.
   - `RemoteAppInstallService`: hỗ trợ upload và cài đặt gói `.msi` / `.exe` silent trên máy đích Windows.

## Definition of Done
- [x] `ComputerStatusChecker`, `RemoteCommandWindow`, `BulkActionWindow`, `HealthMonitorWindow`, `RemoteAppInstallService` hoạt động mượt mà với cả máy đích Linux và Windows.
- [x] Build sạch 0 lỗi.

## Đã làm
1. **Nâng cấp `IPGS.RemoteControl.CcuClient/ComputerStatusChecker.cs`**:
   - Bổ sung `LinuxMetricsCommand`, `WindowsMetricsPowerShellCommand`, `BuildWindowsMetricsCommand()`, `ParseMetricsOutput(string? output)` và `FetchSystemMetricsOverSshAsync(SshClient ssh, CancellationToken cancellationToken)`.
   - Chuẩn hóa đầu ra 3 dòng (`CPU %`, `usedMb totalMb`, `usedG totalG pct%`) dùng chung cho cả Linux và Windows (`Get-CimInstance Win32_Processor`, `Win32_OperatingSystem`, `Win32_LogicalDisk -Filter 'DeviceID=''C:''''`).
   - Tự động thử lệnh Linux trước; nếu đầu ra trống hoặc không khớp định dạng (máy đích là Windows), tự động fallback sang lệnh PowerShell của Windows.
2. **Nâng cấp `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml.cs` & `SshCommandHints.cs`**:
   - Bổ sung `TranslateQuickCommandForWindows(string? command)` tự động ánh xạ toàn bộ các lệnh mẫu Linux (`sudo reboot`, `sudo poweroff`, `sudo shutdown -h now`, `free -m && echo '' && df -h /`, `free -h && echo '---' && df -h`, `top -bn1 | head -n 15`, `ps aux --sort=-%mem | head -n 15`, `ip a && echo '---' && ip route`, `ip a && ping -c 4 8.8.8.8`, `sudo systemctl restart ssh`, `tail -n 50 /var/log/syslog`, `apt clean`/`apt update`) sang câu lệnh PowerShell tương đương trên Windows.
   - Nâng cấp `RunSshCommandAsync` tự động nhận diện HĐH qua `RemoteOsDetector.DetectRemoteOsAsync(ssh)`: trên Windows thực thi trực tiếp (kèm fallback `-EncodedCommand` nếu `DefaultShell` là `cmd.exe`), trên Linux giữ nguyên 100% cơ chế `env DISPLAY=:0 ... bash -c` và `sudo -S` qua STDIN.
   - Nâng cấp Tab Quản lý File (SFTP): lấy thư mục làm việc mặc định từ `ShellQuote.NormalizeSftpPath(_sftpClient.WorkingDirectory)` (hỗ trợ cả `/home/<user>` lẫn `/C:/Users/<user>`), chuẩn hóa `LoadDirectoryAsync`, `NavigateUpAsync`, `ProcessUploadFilesAsync`, `OnFileSyncClick`, bổ sung `BuildOpenTerminalHereCommand` (`Set-Location -LiteralPath '...' ; Get-ChildItem` cho Windows, `cd '...' && ls -la` cho Linux) và `BuildRecursiveDeleteCommand` (`Remove-Item -LiteralPath '...' -Recurse -Force` cho Windows, `rm -rf` cho Linux).
   - Mở rộng `SshCommandHints.ShutdownPattern` nhận diện cả `Restart-Computer` và `Stop-Computer`.
3. **Nâng cấp `IPGS.RemoteControl.CcuUI/Views/BulkActionWindow.axaml` & `BulkActionWindow.axaml.cs`**:
   - Dùng chung `RemoteCommandWindow.RunSshCommandAsync` để tự động nhận diện HĐH từng máy trong danh sách thực thi hàng loạt và ánh xạ lệnh nhanh sang PowerShell cho các máy Windows.
   - Chuẩn hóa thư mục đích khi Upload file hàng loạt theo `sftp.WorkingDirectory` của từng máy và mở rộng bộ chọn file cài đặt ứng dụng hỗ trợ `.deb, .sh, .run, .msi, .exe`.
4. **Nâng cấp `IPGS.RemoteControl.CcuUI/Views/HealthMonitorWindow.axaml.cs`**:
   - Bổ sung `WindowsHealthProbeCommand`, `ParseHealthOutput(string? output, int maxProcesses)` (parse 5 section chuẩn `===UPTIME===`, `===CPU===`, `===MEM===`, `===DISK===`, `===PROCS===`) và `BuildKillProcessCommand(int pid, string sig, RemoteOsType osType)`.
   - Nhận diện HĐH máy đích khi mở kết nối SSH giám sát: thu thập CPU/RAM/Disk/Top 15 Processes bằng PowerShell trên Windows và dừng tiến trình bằng `Stop-Process -Id <pid> -Force`.
5. **Nâng cấp `IPGS.RemoteControl.CcuClient/RemoteAppInstallService.cs`, `RemoteAppInstallWindow` & `RemoteFilePickerWindow`**:
   - Bổ sung `ValidateInstallerPackagePath` / `ValidateDebPackagePath` và `BuildWindowsInstallCommand(string windowsLocalPath)`.
   - Tự động nhận diện HĐH máy đích trong `InstallAsync` / `InstallDebPackageAsync`: chặn chọn nhầm gói khác hệ điều hành với thông báo rõ ràng; trên Windows hỗ trợ upload và cài đặt silent cho `.msi` (`msiexec.exe /i ... /qn /norestart`) và `.exe` (`& ... /S`), kiểm tra mã thoát (`0` / `3010`) và tự động dọn file tạm sau khi cài xong.

## Artifact
- `IPGS.RemoteControl.CcuClient/ComputerStatusChecker.cs`
- `IPGS.RemoteControl.CcuClient/RemoteAppInstallService.cs`
- `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml.cs`
- `IPGS.RemoteControl.CcuUI/Views/SshCommandHints.cs`
- `IPGS.RemoteControl.CcuUI/Views/BulkActionWindow.axaml`
- `IPGS.RemoteControl.CcuUI/Views/BulkActionWindow.axaml.cs`
- `IPGS.RemoteControl.CcuUI/Views/HealthMonitorWindow.axaml.cs`
- `IPGS.RemoteControl.CcuUI/Views/RemoteAppInstallWindow.axaml`
- `IPGS.RemoteControl.CcuUI/Views/RemoteAppInstallWindow.axaml.cs`
- `IPGS.RemoteControl.CcuUI/Views/RemoteFilePickerWindow.cs`

## Quyết định quan trọng
- Chuẩn hóa đầu ra 3 dòng của `WindowsMetricsPowerShellCommand` trùng khớp hoàn toàn với `LinuxMetricsCommand` (`CPU %`, `usedMb totalMb`, `usedG totalG pct%`) để dùng chung bộ phân tích `ComputerStatusChecker.ParseMetricsOutput`.
- Tách các hàm thuần túy (`ParseMetricsOutput`, `BuildWindowsMetricsCommand`, `TranslateQuickCommandForWindows`, `BuildOpenTerminalHereCommand`, `BuildRecursiveDeleteCommand`, `ParseHealthOutput`, `BuildKillProcessCommand`, `ValidateInstallerPackagePath`, `BuildWindowsInstallCommand`) thành `public static` để phục vụ kiểm thử đơn vị toàn diện ở Step 4.1.

## Handoff Payload — bước sau đọc phần này (chỉ phần này, không cần đọc "Đã làm")
- do_not_redo: Đã hoàn thành nâng cấp đa nền tảng (Linux + Windows) cho `ComputerStatusChecker`, `RemoteCommandWindow`, `BulkActionWindow`, `HealthMonitorWindow`, `RemoteAppInstallService`, `RemoteAppInstallWindow`, `RemoteFilePickerWindow` và `SshCommandHints`.
- watch_out: Các hàm static cần kiểm thử tự động trong Step 4.1 gồm: `ShellQuote` (`QuotePowerShell`, `ValidateWindowsUsername`, `IsWindowsSftpPath`, `NormalizeSftpPath`, `ToWindowsLocalPath`, `GetParentSftpPath`, `CombineSftpPath`), `RemoteOsDetector.ParseOsDetectionOutput`, `ZcuRemoteInstallerService.ParseAgentConfigJson`, `ComputerStatusChecker` (`ParseMetricsOutput`, `BuildWindowsMetricsCommand`), `RemoteCommandWindow` (`TranslateQuickCommandForWindows`, `BuildOpenTerminalHereCommand`, `BuildRecursiveDeleteCommand`), `HealthMonitorWindow` (`ParseHealthOutput`, `BuildKillProcessCommand`), và `RemoteAppInstallService` (`ValidateInstallerPackagePath`, `ValidateDebPackagePath`, `BuildWindowsInstallCommand`).
- next_inputs: Bước 4.1 viết bộ unit tests `IPGS.RemoteControl.Tests/WindowsRemoteProvisioningTests.cs` bao phủ toàn bộ logic đa nền tảng của Phase 1–3, chạy full test suite, thực hiện code review và cập nhật `code-graph/CODE-GRAPH.md`.

## Commit
- Hash: pending
- Đã push: không (local branch)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
