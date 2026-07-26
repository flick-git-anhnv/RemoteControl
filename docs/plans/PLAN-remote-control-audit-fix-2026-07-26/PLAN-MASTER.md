---
task: remote-control-audit-fix
created: 2026-07-26
updated: 2026-07-26 15:10
status: completed
workflow: WF-REFACTOR (mở rộng — bao gồm cả bug fix bảo mật)
priority: P1
---

# PLAN MASTER: Khảo sát toàn hệ thống Remote Control — Sửa 41 phát hiện

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả

Sau khi khảo sát chỉ-đọc toàn bộ 3 project của hệ thống Remote Control CCU↔ZCU
(`IPGS.RemoteControl.ZcuAgent`, `IPGS.RemoteControl.CcuClient`, `IPGS.RemoteControl.CcuUI`),
đã phát hiện **41 vấn đề** chia 4 nhóm. User duyệt phạm vi: **sửa toàn bộ 41 mục**.

**Quyết định phạm vi từ user (2026-07-26):**

| Vấn đề | Quyết định |
|---|---|
| Phạm vi fix | **Toàn bộ 41 mục** (Nhóm 1+2+3+4) |
| License (backdoor `"ANHNV"` + không enforce) | **Chỉ báo cáo, KHÔNG đụng code** — ghi nhận vào tài liệu |
| SSH password plaintext | **Mã hoá bằng DPAPI** + tự migrate file cũ |

**Ràng buộc kỹ thuật DPAPI:** app build cross-platform (`win-x64;linux-x64`) nhưng
`System.Security.Cryptography.ProtectedData` chỉ chạy trên Windows → implement DPAPI cho
Windows, trên Linux phải báo lỗi/cảnh báo rõ ràng, TUYỆT ĐỐI không im lặng ghi plaintext.

## Nguồn yêu cầu

- User: "Tiến hành khảo sát, sau đó đề xuất cải tiến và sửa lỗi"
- Khảo sát thực hiện bởi 3 subagent `senior-developer` chạy song song (chỉ-đọc)
- Workflow: WF-REFACTOR mở rộng — SD (khảo sát) → SD (fix theo project) → TL (review + build) → Docs

## Danh mục 41 phát hiện

### 🔴 Nhóm 1 — Mất dữ liệu / hỏng chức năng (6)
| ID | Vị trí | Vấn đề |
|---|---|---|
| A1 | `CcuUI/Views/CronJobWindow.axaml.cs:195` | Xóa cron job → `echo "a\nb"` không dịch `\n` → hỏng toàn bộ crontab |
| A2 | `CcuUI/Views/NetworkScanWindow.axaml.cs:126` | Clone profile quên copy `MacAddress` → mất MAC, hỏng WoL |
| A3 | `CcuUI/Services/SessionRecorder.cs:39` + `RemoteScreenWindow.axaml.cs:83` | Race Dispose(UI) vs AddFrame(TCP thread) → ObjectDisposedException giết receive-loop |
| A4 | `CcuUI/Views/ZcuSetupWizardWindow.axaml.cs:197` | `WaitForExit(30000)` đồng bộ trên UI thread → treo UI 30s |
| A5 | `CcuClient/RemoteAppInstallService.cs:77` | `./~/{file}` — `~` không expand → cài `.sh`/`.run` luôn fail |
| A6 | `CcuUI/Views/MultiRemoteWindow.axaml.cs:311` | "Add Session" mở main window làm dialog, không nhận kết quả → không thêm được session |

### 🔴 Nhóm 2 — Bảo mật (7)
| ID | Vị trí | Vấn đề |
|---|---|---|
| S1 | `CcuClient/RemoteAppInstallService.cs:73,114,117,128`, `ZcuRemoteInstallerService.cs:89,161`, `KioskDeployService.cs:178` | Command injection — tham số nội suy vào `bash -c '...'` không escape, chạy dưới sudo |
| S2 | `ZcuAgent/Net/ClientSession.cs:251-280` | `Process.Start` với input client (chat/clipboard), quoting ad-hoc, không cap độ dài |
| S3 | `CcuUI/Views/RemoteCommandWindow.axaml.cs:213` + `BulkActionWindow.axaml.cs:179` | Password pipe vào mọi lệnh kể cả không sudo; lộ qua `ps -ef` |
| S4 | `ZcuAgent/appsettings.json` + `Auth/AuthManager.cs:44` | Mặc định `0.0.0.0/0`; list rỗng = allow-all; không fail-fast token placeholder |
| S5 | `CcuUI/Services/LicenseManagerService.cs:62` | Backdoor key cứng `"ANHNV"` — **KHÔNG SỬA** theo quyết định user, chỉ ghi tài liệu |
| S6 | `CcuUI/App.axaml.cs:12` | License không được enforce — **KHÔNG SỬA**, chỉ ghi tài liệu |
| S7 | `CcuClient/ComputerProfileStore.cs:189` + `ComputerProfile.cs:40` | SSH password + token plaintext → **mã hoá DPAPI + migrate** |

### 🟠 Nhóm 3 — Leak / Race / DoS (9)
| ID | Vị trí | Vấn đề |
|---|---|---|
| L1 | `CcuClient/RemoteControlClient.cs:224-300` | Rò socket + NetworkStream mỗi lần reconnect fail (không dispose ở path lỗi handshake) |
| L2 | `ZcuAgent/Net/ClientSession.cs:263` | xclip daemonize → WaitForExit timeout, Process không dispose → leak fd; block thread-pool 1s |
| L3 | `ZcuAgent/Net/ClientSession.cs:149-208` | Không write timeout; heartbeat trong loop bị chặn → slow-reader chiếm slot, timeout không fire |
| L4 | `CcuClient/Protocol/MessageCodec.cs:184-235` | 6 hàm Decode* không validate độ dài payload → rớt session / log-spam |
| L5 | `CcuUI/ViewModels/RemoteScreenViewModel.cs:331-359` | Dispatcher.Post queue chạy sau Dispose → leak ~8MB WriteableBitmap/session |
| L6 | `CcuUI/Views/FileManagerWindow.axaml.cs:125` | Closed dispose `_sftpClient` khi background task còn dùng → NRE/ObjectDisposed race |
| L7 | `CcuUI/Views/ConnectionEntryWindow.axaml.cs:41,110` | Mỗi keystroke → probe lại toàn bộ máy, không debounce/cancel → bão probe, kết quả cũ đè mới |
| L8 | `ZcuAgent/Interop/X11ErrorTracker.cs:92` | Cờ `ShmErrorOccurred` toàn cục bị lỗi XTest thread khác set → false fallback XGetImage |
| L9 | `ZcuAgent/Capture/X11ScreenCapturer.cs:141` | Torn read `ScreenSize` giữa capture/receive thread → clamp toạ độ sai |

### 🟡 Nhóm 4 — Chất lượng / hiệu năng (19)
| ID | Vị trí | Vấn đề |
|---|---|---|
| Q1 | `ZcuAgent/Net/ClientSession.cs:260,279` | `catch {}` nuốt lỗi im lặng |
| Q2 | `ZcuAgent/Net/ClientSession.cs:255` | Dead code nhánh Win32 trong service Linux |
| Q3 | `ZcuAgent` capture/encode path | Alloc `byte[]` mỗi frame → GC pressure, nên ArrayPool |
| Q4 | `CcuClient/ZcuRemoteInstallerService.cs:120-137` | Config JSON dựng bằng nội suy chuỗi trong heredoc → hỏng JSON nếu token có `"`/newline |
| Q5 | `CcuClient/ZcuRemoteInstallerService.cs:309` | `Process.Start` không dispose; WaitForExit timeout → orphan process |
| Q6 | `CcuClient/ZcuRemoteInstallerService.cs:216` | `using var cmd` rồi `return cmd` → trả object đã Dispose |
| Q7 | `CcuClient/ComputerProfileStore.cs:180` | `File.WriteAllText` không atomic → crash = hỏng/mất toàn bộ profile |
| Q8 | `CcuClient/ComputerProfileStore.cs:156` | `Load` nuốt exception im lặng → mất profile không cảnh báo |
| Q9 | `CcuClient/Protocol/MessageCodec.cs:88,101,111,121` | Length prefix cast `(ushort)` → truncation khi >65535 byte |
| Q10 | `CcuClient/Services/WakeOnLanService.cs:35` | MAC validate ném `FormatException` thay vì `ArgumentException` |
| Q11 | `CcuClient/ComputerStatusChecker.cs:28-52` | Mutation cross-thread → PropertyChanged raise off UI-thread |
| Q12 | `CcuClient` (3 service) | Logic escape password lặp 3 nơi → gom về helper `ShellQuote` dùng chung |
| Q13 | `CcuUI/Views/CronJobWindow.axaml.cs:140,153` | Thêm cron job bọc `"..."` → `$`/backtick bị shell expand |
| Q14 | `CcuUI/Views/FileManagerWindow.axaml.cs:351` + `RemoteCommandWindow` | Xóa file/`rm -rf` không có dialog xác nhận; lỗi bị `catch {}` nuốt |
| Q15 | `CcuUI/Views/BulkActionWindow.axaml.cs:255` | `completed++` không `Interlocked` → progress đếm sai |
| Q16 | `CcuUI/Views/NetworkScanWindow.axaml.cs` | Đóng window không `Cancel()` CTS → scan 254 IP chạy ngầm |
| Q17 | `CcuUI/Views/RemoteCommandWindow.axaml.cs:105` | Chỉ dispose `_sftpClient` khi `IsConnected` → connect fail thì leak |
| Q18 | `CcuUI/Views/HealthMonitorWindow.axaml.cs` | Mở kết nối SSH mới mỗi 5s thay vì giữ 1 kết nối |
| Q19 | `CcuUI/Views/MultiRemoteWindow.axaml.cs:101` + tab ẩn | Chấm trạng thái là text tĩnh luôn xanh; tab ẩn vẫn decode JPEG full-rate |

> Ghi chú: `CcuUI/Views/RemoteScreenWindow.axaml:109` (`PlaceholderText` trên plain `TextBox`)
> và `RemoteScreenWindow.axaml.cs:136` (recorder lấy độ phân giải 1 lần) được gộp xử lý trong Bước 4.2.

## Phases & Steps

> **Session isolation (CLAUDE.md §16.5):** Mỗi bước ⬜/🔄 chạy tách session bằng `Agent` subagent (môi trường LOCAL).
> Chạy **tuần tự** — không song song hoá, vì 3 project phụ thuộc nhau (`ZcuAgent` → `CcuClient`, `CcuUI` → `CcuClient`),
> build đồng thời sẽ tranh chấp thư mục `obj/` của project chung.

| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Fix `CcuClient` + Protocol — L1, L4, A5, S1(phần CcuClient), S7(DPAPI), Q4-Q12 | Senior Developer | ✅ | `steps/STEP-1.1-ccuclient-fix.md` | 2026-07-26 14:08 |
| 2.1 | Fix `ZcuAgent` — S2, S4, L2, L3, L8, L9, Q1, Q2, Q3 | Senior Developer | ✅ | `steps/STEP-2.1-zcuagent-fix.md` | 2026-07-26 14:19 |
| 3.1 | Fix `CcuUI` phần 1 (critical) — A1, A2, A3, A4, A6, L5, Q13, Q16, Q19 | Senior Developer | ✅ | `steps/STEP-3.1-ccuui-critical-fix.md` | 2026-07-26 14:32 |
| 3.2 | Fix `CcuUI` phần 2 (security/quality) — S3, L6, L7, Q14, Q15, Q17, Q18 | Senior Developer | ✅ | `steps/STEP-3.2-ccuui-quality-fix.md` | 2026-07-26 14:44 |
| 4.1 | Tech Lead review toàn bộ + build sạch cả 3 project (win-x64 + linux-x64) | Tech Lead | ✅ | `steps/STEP-4.1-review-verify.md` | 2026-07-26 14:53 |
| 5.1 | Đồng bộ tài liệu — GOTCHAS, lessons, CODE-GRAPH, ghi nhận vấn đề License không sửa | Senior Developer | ✅ | `steps/STEP-5.1-docs-sync.md` | 2026-07-26 15:10 |

## Artifacts dự kiến

- [ ] Code fix trong 3 project (không tạo project mới)
- [ ] `.claude/GOTCHAS.md` — entry mới cho các lỗi ngầm phát hiện
- [ ] `C:\Users\nguye\.claude\lessons\` — lesson mới + cập nhật `LESSONS-LOG.md`
- [ ] `code-graph/CODE-GRAPH.md` + `.pdf` — cập nhật nếu đổi interface/API
- [ ] `docs/bugs/BUG-remote-control-audit-2026-07-26.md` — báo cáo khảo sát đầy đủ 41 mục
- [ ] Ghi nhận vấn đề License (S5, S6) vào tài liệu — không sửa code

## Blockers

Không có.

## Quyết định / Ghi chú tổng

1. **License (S5, S6) KHÔNG sửa** theo quyết định user — có thể backdoor `"ANHNV"` là cố ý cho nội bộ.
   Chỉ ghi nhận vào `docs/bugs/` để user tự quyết sau.
2. **DPAPI cross-platform:** Windows dùng `ProtectedData`; Linux KHÔNG có DPAPI →
   phải báo lỗi/cảnh báo rõ, không im lặng fallback plaintext.
3. **Chạy tuần tự, không song song:** 3 project phụ thuộc nhau qua `CcuClient`,
   build song song tranh chấp `obj/`.
4. **Mỗi bước phải build sạch project của mình trước khi commit** — không đẩy lỗi build sang bước sau.

## Lịch sử cập nhật

| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-07-26 13:57 | Plan tạo mới sau khảo sát 3 project (41 phát hiện); user duyệt phạm vi toàn bộ + giữ license + DPAPI | Dispatcher |
| 2026-07-26 14:08 | Bước 1.1 Done — fix 15 mục CcuClient, build 0 error/0 warning, commit 0146cb4 (chưa push, chờ review 4.1) | Senior Developer |
| 2026-07-26 14:19 | Bước 2.1 Done — fix 9 mục ZcuAgent + xác nhận L4, build 0 error/0 warning, commit 1ab4f03 (chưa push). Lưu ý: appsettings.json bị hook chặn — default AllowedClientIPs cần user duyệt ở bước 4.1 | Senior Developer |
| 2026-07-26 14:32 | Bước 3.1 Done — fix 9 mục critical CcuUI (A1, A2, A3, A4, A6, L5, Q13, Q16, Q19) + 2 mục gộp; tạo mới SessionPickerWindow (A6, không đụng ConnectionEntryWindow); PlaceholderText verify-giữ-nguyên (GOTCHA G011: Avalonia 12 Watermark obsolete); build 0 error, commit 58909eb (chưa push) | Senior Developer |
| 2026-07-26 14:53 | Bước 4.1 Done — review PASS toàn bộ 4 commit; build 0 error cả 3 project (kể cả ZcuAgent publish linux-x64, CcuUI win-x64/linux-x64); tự fix 1 lỗi ShellQuote từ chối oan `~`/`%` trong tên .deb (de981cf); TD-1 (ShellQuote public) + TD-2 (EncodeFrameJpeg alloc) ghi tech-debt; xác nhận bỏ KIOSK_SUDO_PASS ở CcuUI an toàn (script kiosk đi qua KioskDeployService vẫn set env); appsettings.json ZcuAgent còn default 0.0.0.0/0 do hook chặn — cần user quyết. CHƯA PUSH — chờ user | Tech Lead |
| 2026-07-26 14:44 | Bước 3.2 Done — fix 7 mục security/quality CcuUI (S3, L6, L7, Q14, Q15, Q17, Q18); S3: password sudo qua STDIN channel (CreateInputStream), bỏ KIOSK_SUDO_PASS; tạo mới ConfirmDeleteDialog (Q14, §20.4); build 0 error / 19 warning baseline, commit 8b0aaa3 (chưa push). Đề xuất 4.1: nâng ShellQuote public + helper sudo dùng chung | Senior Developer |
| 2026-07-26 15:10 | Bước 5.1 Done — CODE-GRAPH viết lại v2.0 (8 thay đổi API) + PDF; GOTCHAS G012–G016; 3 lessons toàn cục + INDEX/LESSONS-LOG; BUG report §9 kết quả xử lý (S5/S6 cố ý giữ nguyên) → status Đã xử lý; TECH-DEBT.md mới (TD-1, TD-2). **PLAN HOÀN THÀNH — status: completed. CHƯA PUSH, chờ user; còn 2 việc cần user quyết: appsettings.json ZcuAgent + lịch TD-1/TD-2** | Senior Developer |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
