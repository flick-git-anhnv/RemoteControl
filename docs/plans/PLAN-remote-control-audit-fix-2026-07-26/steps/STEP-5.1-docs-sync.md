---
step: 5.1
title: Đồng bộ tài liệu — CODE-GRAPH, GOTCHAS, lessons, BUG report, tech-debt
agent: Senior Developer
status: done
completed_at: 2026-07-26 15:10
commits: [chính commit chứa file này — "[remote-control-audit-fix] Bước 5.1", xem git log]
---

# STEP 5.1 — Đồng bộ tài liệu sau đợt audit-fix 41 phát hiện

## Đã làm

1. **`code-graph/CODE-GRAPH.md` — VIẾT LẠI v2.0** (+ PDF, DOCX): bản v1.0 mô tả workspace agent framework cũ, sai hoàn toàn với repo này (§17.5 → rewrite từ khảo sát thực tế). Bao gồm đủ 8 thay đổi interface/API từ Handoff 4.1: `ShellQuote.cs` + `SecretProtector.cs` (CcuClient mới), `SessionPickerWindow` + `ConfirmDeleteDialog` (CcuUI mới), dependency `System.Security.Cryptography.ProtectedData 8.0.0`, `ComputerStatusChecker.ProbeAsync(+uiDispatch)`, `AgentOptions.EnableDesktopIntegration`, `IFrameEncoder.EncodeJpeg → ReadOnlyMemory<byte>`, `ZcuRemoteInstallerService.ExecuteCommand → string`, `X11ErrorTracker.ShmMajorOpcode` + P/Invoke `XQueryExtension`, `SessionRecorder.Width/Height`.
2. **`.claude/GOTCHAS.md`** (+ DOCX + PDF): thêm 5 entry mới **G012–G016** (xclip daemonize/leak fd; watchdog cùng loop WriteAsync + WriteTimeout không áp dụng async; whitelist từ chối oan `~`/`%` tên .deb; printf `%` argument vs format string; DPAPI Windows-only) + 6 dòng mục lục nhanh (gồm cả G011 đã thêm ở 3.1 nhưng chưa có trong index... đã có sẵn — chỉ thêm G012-G016).
3. **Lessons toàn cục** (`C:\Users\nguye\.claude\lessons\`) — 3 lesson mới + INDEX.md + LESSONS-LOG.md (dòng 104–106) + DOCX/PDF:
   - `networking-protocol/tcp-async-write-timeout-watchdog-separate-task.md`
   - `dotnet-general/dpapi-protecteddata-cross-platform-secret-migration.md`
   - `dotnet-general/ssh-shell-quote-whitelist-printf-xclip-gotchas.md`
4. **`docs/bugs/BUG-remote-control-audit-2026-07-26.md`** (+ DOCX + PDF): status → "Đã xử lý"; thêm §9 bảng kết quả xử lý 41 mục (commit từng mục, S5/S6 ghi rõ **cố ý không sửa theo quyết định user**, S4 code-fix xong nhưng appsettings.json mẫu chờ user quyết); ghi caveat `KIOSK_SUDO_PASS`.
5. **`docs/tech-debt/TECH-DEBT.md` — FILE MỚI** (+ DOCX + PDF): TD-1 (ShellQuote public + helper sudo-stdin chung, ~45 dòng trùng ở 2 window CcuUI), TD-2 (`EncodeFrameJpeg` alloc/frame → ArrayPool, cần đổi API codec); kèm 3 ghi chú cần user quyết (appsettings.json, KIOSK_SUDO_PASS caveat, thiếu test).
6. Cập nhật PLAN-MASTER: 5.1 → ✅, status frontmatter → `completed`.

## Artifact

- `code-graph/CODE-GRAPH.md` + `.docx` + `.pdf` ✅
- `.claude/GOTCHAS.md` (G012–G016) + `.docx` + `.pdf` ✅
- `docs/bugs/BUG-remote-control-audit-2026-07-26.md` + `.docx` + `.pdf` ✅
- `docs/tech-debt/TECH-DEBT.md` + `.docx` + `.pdf` ✅ (mới)
- Lessons toàn cục: 3 file mới + `INDEX.md` + `LESSONS-LOG.md` (+ DOCX; PDF: ⚠️ riêng `tcp-async-write-timeout-watchdog-separate-task.pdf` fail 4 lần do Word COM chập chờn — DOCX có đủ, không block theo quy tắc §19.4)
- Step file này + PLAN-MASTER cập nhật

## Quyết định quan trọng

- CODE-GRAPH viết lại toàn bộ (không patch) vì bản cũ mô tả sai repo — đúng quy trình §17.5.
- Lesson KHÔNG nằm trong commit repo này (thư mục `C:\Users\nguye\.claude\lessons\` ngoài repo) — chỉ ghi file, không tự commit/push repo khác.
- Gộp 3 gotcha shell (G014+G015+xclip) vào 1 lesson `ssh-shell-quote-...` vì cùng chủ đề "chạy lệnh shell từ .NET" — tái dùng chung 1 ngữ cảnh.

## Handoff Log — bước sau cần biết

- Đã làm: Đồng bộ toàn bộ tài liệu đợt audit-fix (CODE-GRAPH v2.0, GOTCHAS G012–G016, 3 lessons toàn cục, BUG report §9 kết quả xử lý, TECH-DEBT.md mới) — plan HOÀN THÀNH toàn bộ 6 bước.
- File/module đã đọc hoặc đổi: chỉ tài liệu — KHÔNG đụng code; commits code vẫn là 5 commit cũ (`0146cb4`..`de981cf`).
- Quyết định quan trọng: xem mục trên.
- Bước sau cần biết: **(1) CHƯA PUSH** — toàn bộ 7 commit (5 code + 1 step-4.1 + 1 docs-5.1) chờ user xác nhận cuối. **(2)** Dispatcher cần hỏi user 2 việc: sửa `ZcuAgent/appsettings.json` (`AllowedClientIPs` mặc định `0.0.0.0/0`, hook chặn) hay chấp nhận file mẫu; và lên lịch TD-1/TD-2 (WF-REFACTOR riêng, xem `docs/tech-debt/TECH-DEBT.md`). **(3)** PDF của 1 lesson toàn cục fail (Word COM) — có thể xuất lại tay sau, không block.
