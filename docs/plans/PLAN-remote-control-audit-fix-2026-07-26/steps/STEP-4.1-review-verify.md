---
step: 4.1
title: Tech Lead review toàn bộ + build verify 3 project
agent: Tech Lead
status: done
completed_at: 2026-07-26 14:53
commits: [de981cf]
---

# STEP 4.1 — Tech Lead review + build verify (4 commit: 0146cb4, 1ab4f03, 58909eb, 8b0aaa3)

## 1. Kết quả build (verify độc lập, không tin build tự báo)

| Project | Cấu hình | Error | Warning | Ghi chú |
|---|---|---|---|---|
| `IPGS.RemoteControl.CcuClient` | Release | 0 | 0 | Build lại sau review fix `de981cf` — vẫn 0/0 |
| `IPGS.RemoteControl.ZcuAgent` | Release | 0 | 0 | |
| `IPGS.RemoteControl.ZcuAgent` | **publish `-r linux-x64`** | 0 | 0 | Publish thành công ra `temp/review-4.1/zcuagent-publish` |
| `IPGS.RemoteControl.CcuUI` | Release (default) | 0 | 456* | |
| `IPGS.RemoteControl.CcuUI` | Release `-r linux-x64` | 0 | 456* | |
| `IPGS.RemoteControl.CcuUI` | Release `-r win-x64` | 0 | 456* | |

\* **456 warning ≠ regression.** Breakdown: 870 lượt `CS1591` (thiếu XML doc — phần lớn từ project tham chiếu ngoài `E:\KZTEK\Code_Git\5.BaseUI\KztekComponentAvalonia`), 34 `AVLN5001` (Watermark obsolete — pre-existing, xem GOTCHA G011), 6 `CS8602/8603/8604` (2 trong KztekComponentAvalonia, 1 trong `LicenseManagerService.cs` — file KHÔNG đụng theo quyết định user). **Không có warning mới nào phát sinh từ 4 commit fix.** Con số "19 warning baseline" của bước 3.1/3.2 là đếm theo incremental build; full rebuild đếm cả project tham chiếu ngoài.

## 2. Bảng review từng nhóm (thứ tự: correctness > security > behavior parity > maintainability > style)

| Nhóm | Verdict | Ghi chú review |
|---|---|---|
| **L3** watchdog PONG tách task riêng (ZcuAgent) | ✅ PASS | Không race mới: `_lastPongTicks` Interlocked cả 2 phía; watchdog chỉ return (không write) → `WhenAny` + `CancelAsync` + `WhenAll` đóng sạch cả 3 task; `sessionCts` là `using`, không leak. WriteAsync CancelAfter 10s convert timeout → `IOException` (phân biệt đúng với session-cancel qua `when (!ct.IsCancellationRequested)`). |
| **L8** scope lỗi X11 SHM theo opcode | ✅ PASS | **Khả năng chống crash 2026-07-23 KHÔNG mất**: (1) opcode chưa biết (−1) → flag mọi error (conservative); (2) parse XErrorEvent fail → flag (catch đặt `ShmErrorOccurred = true`); (3) handler vẫn return 0 chặn `exit()` của Xlib. Chỉ khi opcode ĐÃ đăng ký qua `XQueryExtension("MIT-SHM")` mới lọc — đúng thiết kế. |
| **S7** DPAPI `SecretProtector` | ✅ PASS | Migrate đúng: plaintext cũ (không prefix `enc:v1:`) đọc nguyên vẹn → tự mã hoá ở lần Persist kế (CloneForStorage — không mutate object runtime). Linux: cảnh báo NỔI BẬT 1 lần (Interlocked guard), unprotect giá trị `enc:v1:` trên Linux → trả rỗng + log, KHÔNG im lặng. Không log password ở bất kỳ path nào (chỉ log `ex.Message` của DPAPI). Decrypt fail → trả rỗng, không ném → không mất cả danh sách profile. Q7 atomic write (tmp + Move overwrite) + Q8 backup `.corrupt-*` đạt. |
| **S3** sudo password qua STDIN channel (CcuUI ×2 window) | ✅ PASS | Password không còn xuất hiện trong command line lẫn env → không lộ qua `ps -ef`/`/proc/*/environ`. `SudoPattern` chỉ match `sudo` ở vị trí lệnh (`^` hoặc sau `; & \| (`) — không phá lệnh chứa chuỗi "sudo" trong literal. Mỗi `sudo -S -p ''` được feed đúng 1 dòng. Edge case chấp nhận được: lệnh không-sudo tự đọc stdin không bị pipe nhầm password (chỉ pipe khi có sudo). |
| **S1/Q12** `ShellQuote` whitelist | ⚠️ PASS-sau-fix | **1 lỗi thật (đã tự fix, commit `de981cf`)**: `FileNameRegex` từ chối `~` — tên `.deb` chuẩn Debian chứa `~` trong version (`pkg_1.0~rc1_amd64.deb`) → nhánh cài đặt fail oan. Fix: cho phép `~` và `%` (trừ ký tự đầu — vẫn alphanumeric, không tilde-expand/option-injection; cả 2 vô hại trong `"$HOME/..."` bên trong `bash -c '...'`). `g++` với `+` đã pass từ đầu. Username regex khớp chuẩn useradd. `Quote()` strip newline có chủ đích (SSH command 1 dòng) — documented. |
| **A1/Q13** crontab qua `printf` | ✅ PASS | KHÔNG dính bẫy `%` của printf: chuỗi user nằm ở vị trí **argument** (`printf '%s\n' '<arg>'`), format string cố định — `%` trong argument là literal. Single-quote giữ nguyên `$`/backtick/newline. Xóa job cuối cùng → `crontab -r` (đúng, `printf ''` sẽ fail crontab). ExitStatus được kiểm tra, lỗi không còn nuốt im lặng. |
| **L5/A3** dispose bitmap + recorder | ✅ PASS | Không double-dispose: `SessionRecorder.Dispose` idempotent (`_disposed` guard trong lock); window gán `_recorder = null` TRƯỚC rồi dispose, receive-thread đọc snapshot local. Không deadlock: 1 lock duy nhất, không callback trong lock. L5: cờ `_disposed` volatile set trước unsubscribe; lambda Dispatcher.Post tự dispose bitmap khi VM chết. Bonus hợp lệ: dừng ghi khi ZCU đổi độ phân giải (AVI header cố định). |
| **A5** `./~/{file}` → `"$HOME/{file}"` | ✅ PASS | Kết hợp validate filename — đúng và an toàn. |
| **S4** deny-by-default + fail-fast token (ZcuAgent) | ✅ PASS | List rỗng → deny-all + warning; token placeholder → LogCritical + throw trước khi mở listener; `0.0.0.0/0` tường minh → warning nổi bật. |
| **S2/L2/Q1/Q2** notify-send/xclip qua ArgumentList (ZcuAgent) | ✅ PASS | ArgumentList = 1 argv element, không quoting tay; Process luôn dispose; xclip daemonize → release handle KHÔNG kill tree (giữ clipboard) — gotcha documented đúng. Cap chat 4K/clipboard 256K. Có cờ `EnableDesktopIntegration` tắt hẳn. |
| **L9/Q3** ScreenSize atomic + buffer reuse (ZcuAgent) | ✅ PASS | Holder record + volatile = publish atomic. Buffer reuse (pixel + jpeg) an toàn vì single-threaded capture loop, consume đồng bộ — GOTCHA ghi rõ trong `Interfaces.cs`. |
| **L1/L4/Q9** reconnect leak + decode validate (CcuClient) | ✅ PASS | try/catch bọc từ ConnectAsync → CloseConnectionAsync idempotent (Interlocked.Exchange null). 8 decoder có RequireMinLength + length-consistency → `ProtocolException`; 4 encoder có RequireU16Length. `DecodeAuthFail` giữ lenient (Math.Min) — chấp nhận được, path thông báo lỗi. |
| **L6/L7/Q14–Q18** CcuUI quality | ✅ PASS | L6 acquire/release đếm op trên UI thread — đúng vì Acquire trước Task.Run, Release sau await. L7 debounce 300ms + cancel batch cũ, không dispose CTS in-flight (tránh ObjectDisposedException) — đúng. Q14 ConfirmDeleteDialog tách file riêng (§20.4 tuân thủ). Q15 Interlocked.Increment dùng giá trị trả về. Q18 giữ 1 SSH connection + tự reconnect khi rớt + dispose ở op cuối. |
| **A2/A4/A6/Q16/Q19** CcuUI critical | ✅ PASS | A2 clone đủ mọi field persist (đối chiếu đúng chủng bug d1ab288). A4 WaitForExitAsync + kill tree khi timeout. A6 SessionPickerWindow mới — đúng hướng (không biến main window thành dialog). Q19 IsRenderPaused volatile, chấm trạng thái bind StatusBrush thật. |

## 3. Lỗi Tech Lead tự fix

| # | Commit | Mô tả |
|---|---|---|
| 1 | `de981cf` | `ShellQuote.FileNameRegex` thêm `~` `%` vào whitelist (trừ ký tự đầu) — bản cũ từ chối oan tên `.deb` hợp lệ chứa `~` trong version. Build lại CcuClient: 0 error / 0 warning. |

## 4. Quyết định 3 đề xuất tồn đọng

| # | Đề xuất | Quyết định | Lý do |
|---|---|---|---|
| 1 | Nâng `ShellQuote` → `public` + helper sudo-stdin dùng chung (bỏ ~45 dòng trùng ở 2 window CcuUI) | **TECH-DEBT — TD-1**, không làm trong bước này | Refactor đụng 3 file ngay trước push làm tăng rủi ro regression cho code vừa review PASS; duplication đã được comment tham chiếu chéo rõ ràng. Giao thành task refactor riêng (WF-REFACTOR nhỏ) sau khi đợt fix này lên. |
| 2 | Q3 tồn đọng: `MessageCodec.EncodeFrameJpeg` alloc `byte[24+len]` mỗi frame | **TECH-DEBT — TD-2**, không làm | Fix đúng cách cần đổi ownership/signature `WriteMessageAsync` (ArrayPool rent/return xuyên async boundary) — thay đổi API protocol codec, vượt scope review. Mức alloc này (1 mảng/frame, đã có buffer reuse ở capture+encode) không phải hot-spot nghiêm trọng. |
| 3 | Bỏ `KIOSK_SUDO_PASS` env ở CcuUI có phá deploy script? | **XÁC NHẬN AN TOÀN** | Grep toàn repo: chỉ `scripts/linux-kiosk/2-configure-system.sh` đọc biến này, và nó chạy qua `KioskDeployService` (CcuClient) — service này **vẫn set** `KIOSK_SUDO_PASS`. CcuUI chỉ bỏ ở RemoteCommand/BulkAction (lệnh ad-hoc). ⚠️ Caveat nhỏ: user chạy TAY script kiosk qua RemoteCommandWindow sẽ không còn env này — hàm `_sudo()` trong script fallback `sudo` thường (có thể hỏi TTY). Ghi vào tài liệu bước 5.1. |

## 5. Tình trạng `appsettings.json` (ZcuAgent) — CẦN USER QUYẾT

File `IPGS.RemoteControl.ZcuAgent/appsettings.json` **vẫn còn** `"AllowedClientIPs": ["0.0.0.0/0"]` + token placeholder — hook `config-protection` chặn sửa, đã xác nhận lại hiện trạng (không bypass hook). Code đã phòng thủ đủ: token placeholder → **fail-fast từ chối start**; `0.0.0.0/0` → warning nổi bật; list rỗng → deny-all. **Khuyến nghị cho Dispatcher hỏi user:** cho phép sửa file này thành `"AllowedClientIPs": []` (deny-by-default thật sự) hoặc CIDR mạng nội bộ cụ thể — hoặc chấp nhận giữ nguyên vì đây là file mẫu, installer (`ZcuRemoteInstallerService`) luôn ghi đè config thật khi deploy.

## 6. Verdict tổng

**PASS** — approve merge (sau khi user duyệt). 4 commit fix + 1 commit review fix, cả 3 project build 0 error trên mọi RID áp dụng, không warning mới. **Chưa push** — chờ user xác nhận cuối cùng.

License S5/S6: xác nhận code license KHÔNG bị đụng — đúng quyết định user (kiểm tra diff: `LicenseManagerService.cs`, `App.axaml.cs` không xuất hiện trong changeset).

## Handoff Log — bước sau cần biết (5.1 đồng bộ tài liệu)

- **Đã làm:** Build verify 3 project (Release + linux-x64 publish ZcuAgent + win-x64/linux-x64 CcuUI) đều 0 error; review toàn bộ diff `9586c4b..HEAD` theo 7 trọng tâm rủi ro cao — tất cả PASS; tự fix 1 lỗi ShellQuote (`de981cf`); quyết định TD-1/TD-2 tech-debt, xác nhận bỏ `KIOSK_SUDO_PASS` ở CcuUI an toàn.
- **File/module đã đọc hoặc đổi:** Đổi duy nhất `IPGS.RemoteControl.CcuClient/ShellQuote.cs` (regex nội bộ, KHÔNG đổi signature). Đọc toàn bộ diff 36 file của 4 commit.
- **Thay đổi interface/API cần cập nhật CODE-GRAPH:** CÓ — từ các bước trước (không phải 4.1): (1) `IFrameEncoder.EncodeJpeg` đổi trả về `byte[]?` → `ReadOnlyMemory<byte>`; (2) `ComputerStatusChecker.ProbeAsync` thêm param `Action<Action>? uiDispatch`; (3) `ZcuRemoteInstallerService.ExecuteCommand` trả `SshCommand` → `string`; (4) class mới: `ShellQuote`, `SecretProtector` (CcuClient), `SessionPickerWindow`, `ConfirmDeleteDialog` (CcuUI); (5) `AgentOptions` thêm `EnableDesktopIntegration`; (6) dependency mới `System.Security.Cryptography.ProtectedData` 8.0.0 (CcuClient); (7) `X11ErrorTracker.ShmMajorOpcode` + P/Invoke `XQueryExtension` mới; (8) `SessionRecorder` thêm property `Width`/`Height`. Lưu ý: `code-graph/CODE-GRAPH.md` hiện **chưa tồn tại** — bước 5.1 tạo mới theo §17.1.
- **Đáng ghi GOTCHAS/lessons:** (a) Bẫy printf `%` KHÔNG áp dụng khi user string ở vị trí argument của `printf '%s\n'` — chỉ nguy hiểm khi làm format string; (b) tên file `.deb` Debian hợp lệ chứa `~`/`%` — whitelist filename phải tính đến (lỗi de981cf); (c) full rebuild đếm warning cả project tham chiếu ngoài (`KztekComponentAvalonia`) — baseline warning phải đo bằng cùng chế độ build; (d) xclip daemonize → không được kill process tree sau timeout (mất clipboard); (e) `NetworkStream.WriteTimeout` KHÔNG áp dụng cho async write — phải CancelAfter.
- **Bước sau cần biết:** KHÔNG push — chờ user. Vấn đề `appsettings.json` (mục 5) cần Dispatcher hỏi user. TD-1/TD-2 ghi vào tài liệu tech-debt. Caveat `KIOSK_SUDO_PASS` (mục 4.3) ghi vào docs. License S5/S6 giữ nguyên — ghi nhận tài liệu tại `docs/bugs/BUG-remote-control-audit-2026-07-26.md`.
