---
step: 3.1
title: Fix IPGS.RemoteControl.CcuUI phần 1 (critical) — A1, A2, A3, A4, A6, L5, Q13, Q16, Q19
agent: senior-developer
status: done
completed_at: 2026-07-26 14:32
commit: 58909eb
---

# STEP 3.1 — Fix CcuUI critical (9 mục audit + 2 mục gộp)

## Nhiệm vụ

Sửa 9 phát hiện critical trong `IPGS.RemoteControl.CcuUI`: A1, A2, A3, A4, A6 (mất dữ liệu/hỏng chức năng),
L5 (leak), Q13, Q16, Q19 (chất lượng). Kèm 2 mục gộp: `RemoteScreenWindow.axaml:109` (PlaceholderText)
và `RemoteScreenWindow.axaml.cs:136` (recorder lấy độ phân giải 1 lần).

## Đã làm

| Mục | Fix | File |
|---|---|---|
| A1 🔴 | Xóa cron job: bỏ hẳn `echo "...\n..."` (literal `\n` phá toàn bộ crontab) → `printf '%s\n' '<content>' \| crontab -` với helper `ShQuote` (single-quote POSIX, `'` → `'\''`, newline literal giữ nguyên trong single-quote). Danh sách rỗng → `crontab -r`. Check `ExitStatus != 0` → throw kèm stderr | `Views/CronJobWindow.axaml.cs` |
| Q13 🟡 | Thêm cron job: `(crontab -l 2>/dev/null; printf '%s\n' '<job>') \| crontab -` — `$VAR`/backtick trong lệnh cron KHÔNG còn bị shell remote expand. Check ExitStatus | `Views/CronJobWindow.axaml.cs` |
| A2 🔴 | Clone profile trong `OnAddFoundClick` copy đủ `MacAddress` + `LastAppInstallerPath` + `LastUninstallPackage` (Save() đè TOÀN BỘ field — thiếu field nào là field đó bị null hoá). Đối chiếu khớp clone chuẩn ở `ConnectionEntryWindow.OnItemEditClick` | `Views/NetworkScanWindow.axaml.cs` |
| A3 🔴 | `SessionRecorder` thread-safe: `lock (_sync)` + cờ `_disposed` cho cả `AddFrame` và `Dispose`; frame tới sau Dispose bị bỏ qua êm (không ObjectDisposedException ngược vào receive-loop). Expose `Width`/`Height`. `RemoteScreenWindow.OnClosed`: unsubscribe `FrameReceived`+`SysInfoReceived` TRƯỚC → `DisconnectAsync` → mới dispose recorder. `OnFrameReceived` snapshot `_recorder` vào local. Stop record: gán null trước, Dispose sau | `Services/SessionRecorder.cs`, `Views/RemoteScreenWindow.axaml.cs` |
| (gộp) 🟡 | Recorder resolution: chặn start khi `ScreenWidth/Height <= 0`; đang record mà frame đổi kích thước ≠ AVI header → tự dừng ghi, uncheck nút Record, báo trên Title (file AVI đã ghi vẫn hợp lệ tới thời điểm đó) | `Views/RemoteScreenWindow.axaml.cs` |
| A4 🔴 | `FindZcuAgentPublishDir` → `FindZcuAgentPublishDirAsync`: `Process.WaitForExitAsync` + `CancellationTokenSource(30s)`, timeout → `Kill(entireProcessTree)` + log; caller `await` → UI (progress bar, log console) phản hồi suốt lúc auto-publish | `Views/ZcuSetupWizardWindow.axaml.cs` |
| A6 🔴 | Tạo mới `SessionPickerWindow` (dialog chọn máy multi-select, đọc `ComputerProfileStore.Instance`, ẩn host đã có trong Dashboard, double-click chọn nhanh, trả `List<ComputerProfile>?` qua `Close(result)`). `MultiRemoteWindow.OnAddSessionClick` dùng picker + `AddSessions(picked)` — KHÔNG đụng `ConnectionEntryWindow` | `Views/SessionPickerWindow.axaml{,.cs}` (mới), `Views/MultiRemoteWindow.axaml.cs` |
| L5 🟠 | `RemoteScreenViewModel`: cờ `volatile bool _disposed` set ĐẦU TIÊN trong `Dispose()`; lambda `Dispatcher.UIThread.Post` đã nằm trong queue chạy sau Dispose → thấy cờ → `wb.Dispose()` thay vì gán `CurrentFrame` (hết leak ~8MB/bitmap; MultiRemote 3×3 Close All = 9 bitmap). `OnFrameReceived` cũng early-out khi disposed | `ViewModels/RemoteScreenViewModel.cs` |
| Q16 🟡 | `NetworkScanWindow.OnClosed` → `_scanCts?.Cancel()` (guard ObjectDisposedException do race với finally của OnScanClick) — đóng cửa sổ là dừng quét 254 IP | `Views/NetworkScanWindow.axaml.cs` |
| Q19 🟡 | Chấm trạng thái header cell: emoji `🟢` tĩnh → glyph `●` bind `Foreground=StatusBrush` + `ToolTip=StatusText` của ViewModel session (đỏ khi Faulted, cam khi connecting...). Tab ẩn: thêm `RemoteScreenViewModel.IsRenderPaused` (volatile) — frame vẫn cập nhật kích thước màn hình nhưng bỏ qua decode JPEG + alloc WriteableBitmap; `MultiRemoteWindow.UpdateRenderPauseStates()` gọi khi đổi tab (`SelectionChanged`) và cuối `RefreshLayout` (grid mode = không pause) | `ViewModels/RemoteScreenViewModel.cs`, `Views/MultiRemoteWindow.axaml.cs` |
| (gộp) 🟡 | `RemoteScreenWindow.axaml:109` `PlaceholderText`: đã verify bằng build — Avalonia 12 bản này đánh dấu `Watermark` OBSOLETE, `PlaceholderText` là tên ĐÚNG (ngược Avalonia 11). **Giữ nguyên code gốc**, ghi GOTCHA G011 | `.claude/GOTCHAS.md` (G011, chưa commit — gom bước 5.1) |

## Verification

```
Lệnh:   dotnet build IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj
Output: Build succeeded.  19 Warning(s)  0 Error(s)
Kết luận: Pass
```

19 warning đều CÓ SẴN từ trước (CS1591 XML doc của KztekComponentAvalonia, AVLN5001 Watermark obsolete
trong library + các view khác, CS8604 trong `LicenseManagerService.cs` — file cấm sửa). Không warning mới
từ code bước này.

## Quyết định quan trọng

1. **A6 giải quyết trọn trong bước này, KHÔNG cần đụng `ConnectionEntryWindow`** — tạo `SessionPickerWindow` mới thay vì tái dùng main window làm dialog.
2. **PlaceholderText giữ nguyên** — nghi vấn trong plan là theo trí nhớ Avalonia 11; Avalonia 12 của repo này dùng `PlaceholderText` là chuẩn, `Watermark` obsolete (GOTCHA G011).
3. **Q19 phần tab ẩn: pause DECODE, không pause stream** — vẫn nhận frame TCP (giữ heartbeat + cập nhật ScreenSize) nhưng bỏ decode SkiaSharp + alloc bitmap. Chuyển tab là có hình ngay ở frame kế (15fps → trễ ≤ ~66ms), không cần re-handshake.
4. **Đổi độ phân giải giữa lúc record → DỪNG ghi** (không cố re-header): AVI MJPEG header cố định width/height, ghi tiếp là file rác; dừng sớm giữ được phần video hợp lệ đã ghi.
5. Commit chỉ gồm 9 file source + 2 file mới; GOTCHAS.md/plan docs không commit theo pattern bước 1.1/2.1 (gom về bước 5.1 docs-sync).

## Artifact

- 7 file source đã sửa + 2 file mới (`SessionPickerWindow.axaml{,.cs}`) trong `IPGS.RemoteControl.CcuUI/`
- `.claude/GOTCHAS.md` — thêm entry G011 (working tree, chưa commit)
- Commit: `58909eb` (chưa push — chờ review bước 4.1)

## Handoff Log — bước sau cần biết

- **Đã làm:** Fix xong 9 mục critical CcuUI (A1, A2, A3, A4, A6, L5, Q13, Q16, Q19) + 2 mục gộp (PlaceholderText verify-giữ-nguyên, recorder resolution guard). Build CcuUI 0 error, 19 warning có sẵn. Commit `58909eb`, chưa push. KHÔNG mục nào phải chuyển sang bước 3.2 — A6 xử lý xong bằng `SessionPickerWindow` mới, không đụng `ConnectionEntryWindow`.
- **File/module đã đổi:** `Views/CronJobWindow.axaml.cs`, `Views/NetworkScanWindow.axaml.cs`, `Services/SessionRecorder.cs`, `Views/RemoteScreenWindow.axaml.cs`, `Views/ZcuSetupWizardWindow.axaml.cs`, `Views/MultiRemoteWindow.axaml.cs`, `ViewModels/RemoteScreenViewModel.cs`; mới: `Views/SessionPickerWindow.axaml{,.cs}`. `Views/RemoteScreenWindow.axaml` không đổi (revert về gốc sau verify).
- **Quyết định quan trọng:** (1) GOTCHA G011 — Avalonia 12 repo này: `Watermark` obsolete, dùng `PlaceholderText`; bước 3.2 đừng "sửa lùi" các `PlaceholderText` có sẵn trong ConnectionEntryWindow/FileManagerWindow... (2) `RemoteScreenViewModel` có thêm 2 API: `IsRenderPaused` (bool, volatile) và cờ `_disposed` nội bộ — nếu bước sau tạo VM chỗ khác, dispose xong đừng tái sử dụng. (3) `SessionRecorder` giờ thread-safe, có `Width`/`Height` public.
- **Bước sau cần biết:**
  - Bước 3.2: nhóm file của bạn (`RemoteCommandWindow`, `BulkActionWindow`, `FileManagerWindow`, `ConnectionEntryWindow`, `HealthMonitorWindow`) KHÔNG bị bước này chạm vào — không có xung đột. Pattern `ShQuote` (single-quote POSIX local helper) trong `CronJobWindow.axaml.cs` có thể copy dùng cho S3 (password pipe) — `ShellQuote.cs` bên CcuClient là `internal`, CcuUI KHÔNG truy cập được.
  - Bước 4.1 (review): (a) verify chuỗi lệnh crontab bằng SSH thật nếu có máy Linux (printf + single-quote đa dòng); (b) `SessionPickerWindow` là UI mới → cần UXR theo workflow nếu áp dụng; (c) 19 warning tồn đọng của CcuUI (Watermark obsolete trong KztekComponentAvalonia + các view, CS8604 LicenseManagerService) là nợ có sẵn, ngoài scope.
  - Bước 5.1 (docs): commit GOTCHAS.md (G011) + cập nhật CODE-GRAPH (2 file view mới `SessionPickerWindow`, API mới `IsRenderPaused`, `SessionRecorder.Width/Height`).
