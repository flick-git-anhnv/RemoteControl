---
step: "3.2"
title: "Fix CcuUI phần 2 (security/quality) — S3, L6, L7, Q14, Q15, Q17, Q18"
agent: senior-developer
status: done
completed_at: 2026-07-26 14:44
commit: 8b0aaa3
---

# STEP 3.2 — Fix `IPGS.RemoteControl.CcuUI` nhóm security/quality

## Nhiệm vụ

Sửa 7 mục audit trong CcuUI: S3 (password sudo lộ qua `ps`/pipe vào mọi lệnh), L6 (dispose SFTP đua với background task), L7 (bão probe theo keystroke), Q14 (xóa không xác nhận + nuốt lỗi), Q15 (progress counter không Interlocked), Q17 (leak SFTP khi connect fail), Q18 (mở SSH mới mỗi 5s).

## Đã làm

| ID | File | Fix |
|---|---|---|
| S3 🔴 | `Views/RemoteCommandWindow.axaml.cs`, `Views/BulkActionWindow.axaml.cs` | Helper cục bộ `RunSshCommandAsync`: password sudo ghi vào **STDIN của SSH channel** (`SshCommand.ExecuteAsync()` + `CreateInputStream()`, SSH.NET 2025.1.0) — mỗi lần sudo 1 dòng password. Regex `(^|[;&|(])(\s*)sudo(?=\s)` chỉ match sudo ở **vị trí lệnh** → không phá string literal chứa "sudo ", lệnh có N sudo nhận đủ N dòng. Lệnh KHÔNG có sudo → không pipe gì (lệnh đọc stdin không còn nhận nhầm password). Bỏ hẳn `KIOSK_SUDO_PASS` env + `echo '<pass>' \|` → password không còn xuất hiện trên command line (`ps -ef`) hay `/proc/*/environ`. Dùng `sudo -S -p ''` để không in prompt ra stderr. |
| L6 🟠 | `Views/FileManagerWindow.axaml.cs` | Op-counter guard: `AcquireClient()/ReleaseClient()/DisposeClient()` — `Closed` chỉ dispose khi `_activeOps == 0`, ngược lại op cuối cùng dispose trong `ReleaseClient()`. Mọi op dùng biến local `sftp` (không còn `_sftpClient!` trên thread pool). Acquire đặt **SAU** `await` picker (user có thể đóng cửa sổ khi picker mở). Connect xong mà `_closeRequested` → dispose ngay không giữ. Tất cả transition chạy trên UI thread → không cần lock. |
| L7 🟠 | `Views/ConnectionEntryWindow.axaml.cs` | `DispatcherTimer` debounce 300ms cho search box; `CancellationTokenSource` mới mỗi batch — `Cancel()` batch cũ (không Dispose ngay để tránh ObjectDisposedException trên task in-flight); batch cũ hoàn thành muộn bị chặn ghi đè (`ct.IsCancellationRequested` check cả trước và trong `Dispatcher.Post`); truyền `uiDispatch: action => Dispatcher.UIThread.Post(action)` vào `ProbeAsync` (param mới bước 1.1) → mutation `CpuUsage/RamUsage/DiskUsage` trên UI thread; toàn bộ `CheckAllStatusesAsync` bọc try/catch (OCE nuốt riêng). `Closed` → stop timer + cancel CTS. |
| Q14 🟡 | `Views/ConfirmDeleteDialog.axaml{,.cs}` (MỚI), `Views/FileManagerWindow.axaml.cs`, `Views/RemoteCommandWindow.axaml.cs` | Dialog xác nhận riêng (§20.4 — View tách file, dùng `KzButton` của KztekComponentAvalonia): liệt kê tối đa 30 đường dẫn sẽ xóa, cảnh báo đỏ riêng khi có thư mục (rm -rf đệ quy). Cả 2 window PHẢI qua dialog trước khi xóa. Lỗi xóa từng mục gom vào `failures` và hiển thị (status + log) thay vì `catch {}` nuốt; fallback `rm -rf` trong RemoteCommandWindow kiểm tra `ExitStatus` + stderr, dùng `ShQuote`. |
| Q15 🟡 | `Views/BulkActionWindow.axaml.cs` | `Interlocked.Increment(ref completed)` — dùng giá trị trả về `done` trong closure post UI. |
| Q17 🟡 | `Views/RemoteCommandWindow.axaml.cs` | `Closed` luôn dispose `_sftpClient` (Disconnect chỉ khi IsConnected, Dispose vô điều kiện, try/catch từng bước). |
| Q18 🟡 | `Views/HealthMonitorWindow.axaml.cs` | Field `_sshClient` giữ 1 kết nối xuyên suốt; `EnsureSshConnected()` chỉ tạo/connect lại khi null/rớt; exception → dispose để tick sau reconnect. Guard `_isClosed`: chặn post UI vào window đã đóng; `Closed` dispose ngay nếu không có refresh in-flight, ngược lại `finally` của refresh dispose (cả hai trên UI thread → không race). `SshCommand` bọc `using`. |

**Kiểm tra bổ sung (không cần sửa):** `RemoteAppInstallWindow` và `KioskDeployWindow` đã có `catch (Exception ex)` hiển thị `ex.Message` lên `PART_StatusMsg` + log → `ArgumentException` whitelist mới từ bước 1.1 được hiển thị cho user, không crash/không nuốt.

## Verification

```
Lệnh:   dotnet build IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj
Output: Build succeeded. 19 Warning(s), 0 Error(s)
Kết luận: Pass — 0 error; 19 warning đúng baseline có sẵn (AVLN5001 Watermark obsolete
          trong file KHÔNG thuộc scope bước này + CS8604 LicenseManagerService), không tăng.
```

## Artifact

- `IPGS.RemoteControl.CcuUI/Views/ConfirmDeleteDialog.axaml` + `.axaml.cs` (MỚI)
- `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml.cs` (S3, Q14, Q17)
- `IPGS.RemoteControl.CcuUI/Views/BulkActionWindow.axaml.cs` (S3, Q15)
- `IPGS.RemoteControl.CcuUI/Views/FileManagerWindow.axaml.cs` (L6, Q14)
- `IPGS.RemoteControl.CcuUI/Views/ConnectionEntryWindow.axaml.cs` (L7)
- `IPGS.RemoteControl.CcuUI/Views/HealthMonitorWindow.axaml.cs` (Q18)
- Commit: `8b0aaa3` (chưa push, chờ review 4.1)

## Quyết định quan trọng

1. **S3 dùng `SshCommand.CreateInputStream()` (SSH.NET 2025.1.0)** thay vì `echo pass |` — cách duy nhất để password hoàn toàn không xuất hiện trên command line remote. Pattern: `var t = cmd.ExecuteAsync(); using (var input = cmd.CreateInputStream()) { write; } await t;` — CreateInputStream phải gọi SAU khi execution bắt đầu.
2. **Bỏ `KIOSK_SUDO_PASS` env**: env này trước đây được set qua command line nên vẫn lộ qua `ps` lúc chạy; script remote nào đọc biến này sẽ không còn nhận được — cần QA xác nhận ở bước 5.1/QA rằng không có script kiosk phụ thuộc (khảo sát repo không thấy nơi nào đọc).
3. **ShQuote + SudoPattern nhân bản cục bộ ở 2 file** (theo Handoff 1.1 — `ShellQuote` bên CcuClient là `internal`). Đề xuất cho 4.1 bên dưới.
4. **L6 không dùng lock/CTS** mà dùng op-counter thuần UI-thread — đơn giản, đủ đúng vì mọi acquire/release/close đều trên UI thread (trước `Task.Run` / sau `await`).

## Handoff Log — bước sau cần biết (4.1 Tech Lead review + build toàn solution)

- **Đã làm:** Fix đủ 7 mục S3, L6, L7, Q14, Q15, Q17, Q18 + tạo `ConfirmDeleteDialog` dùng chung; xác nhận RemoteAppInstall/KioskDeploy đã hiển thị `ArgumentException` whitelist từ bước 1.1. Build CcuUI 0 error / 19 warning (baseline). Commit `8b0aaa3`, chưa push.
- **File/module đã đọc hoặc đổi:** 6 file Views kể trên + 2 file dialog mới; đọc (không sửa) `CcuClient/ComputerStatusChecker.cs`, `RemoteAppInstallWindow`, `KioskDeployWindow`, `CronJobWindow` (tham chiếu pattern ShQuote).
- **Quyết định quan trọng:** (1) S3 dùng `CreateInputStream` — cần SSH.NET ≥ 2024.0, repo đang 2025.1.0; (2) đã BỎ `KIOSK_SUDO_PASS` khỏi env lệnh remote — nếu có script kiosk nào đọc biến này sẽ mất, khảo sát không thấy consumer nhưng QA nên xác nhận; (3) `sudo` giờ được nhận diện bằng regex vị-trí-lệnh — lệnh dạng `xargs sudo ...` hay `find -exec sudo ...` (sudo không ở đầu segment) sẽ KHÔNG được cấp password tự động (chấp nhận: hiếm, an toàn hơn false-positive).
- **Đề xuất cho 4.1 quyết định:** Nâng `ShellQuote` (CcuClient) thành `public` và bổ sung helper `SudoStdinCommandRunner` dùng chung, rồi gỡ 2 bản helper trùng lặp trong `RemoteCommandWindow`/`BulkActionWindow` — hiện trùng ~45 dòng × 2 file.
- **Bước sau KHÔNG cần làm lại:** Không sửa lùi `PlaceholderText` → `Watermark` (G011); 19 warning AVLN5001 còn lại nằm ở các file ngoài scope 3.2 (ComputerEditWindow, ZcuSetupWizardWindow, LicenseWindow, ...) — dọn dần khi chạm vào, không phải lỗi mới; `ConfirmDeleteDialog.ShowAsync` trả `false` khi đóng bằng X (mặc định an toàn).
