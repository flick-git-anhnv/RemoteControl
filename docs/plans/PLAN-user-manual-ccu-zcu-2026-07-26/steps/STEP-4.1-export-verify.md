---
step: 4.1
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 21:52
---

> **Bước này chạy 2 lần:** Lần 1 (18:18) xuất BẢN NHÁP khi mới có 19/80 ảnh. Lần 2 (21:52) — sau khi Phase 2 hoàn tất 75 ảnh — thay toàn bộ marker, xuất bản chính thức v1.0. Nội dung lần 1 giữ nguyên bên dưới làm lịch sử; kết quả lần 2 ở mục "Lần chạy 2".

# STEP 4.1 — Xuất DOCX + PDF, kiểm tra Definition of Done

## Input nhận
- Handoff Log STEP-3.1 (MANUAL .md hoàn chỉnh phần chữ, 19 ảnh thật, 61 marker ⏳, danh sách TODO còn lại).

## Nhiệm vụ
Chạy `python scripts/md_to_docx_kztek.py docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` để xuất DOCX + PDF (brand KZTEK). Mở kiểm tra: ảnh hiển thị đúng, heading/mục lục đúng, không vỡ layout. Đối chiếu Definition of Done trong `.claude/agents/documentation-writer.md` + artifact bắt buộc WF-DOCS (CLAUDE.md §11). Tổng kết các mục còn BLOCK chờ thiết bị (nếu có) để báo user.

## Definition of Done (của step)
- [x] `docs/user-manuals/MANUAL-ccu-zcu-remote-control.docx` tạo thành công (`✓ DOCX hoàn thành`)
- [x] `docs/user-manuals/MANUAL-ccu-zcu-remote-control.pdf` tạo thành công (docx2pdf pass ngay lần 1, không cần retry)
- [x] Ảnh trong DOCX hiển thị đúng: 20 file trong `word/media/` (19 screenshot + 1 logo KZTEK), size từng ảnh hợp lý, không khung trống
- [x] Checklist DoD của documentation-writer đã chạy đầy đủ (kết quả bên dưới — các mục ❌ là lý do bản này là NHÁP)
- [x] Cập nhật step file này + PLAN-MASTER.md (status plan giữ `in-progress` — còn 61 ảnh chờ chụp)

## Đã làm
1. **Rà soát chất lượng trước khi xuất** (script Python đếm thực tế, không tin số trong Handoff):
   - Link ảnh `![](...)` = **19**, tất cả trỏ tới PNG tồn tại trong `docs/user-manuals/screenshots/` (0 link chết).
   - Marker `> ⏳ **[CHỜ ẢNH: ...]**` = **61** — khớp Handoff Log STEP-3.1.
   - Bảng Phụ lục A = **61 dòng dữ liệu** — khớp 1:1 với marker.
   - Đánh số hình: Hình 1–19, không trùng, max = 19 — ảnh mới sẽ bắt đầu từ Hình 20.
   - Mục lục khớp 15 chương thực tế (`## 1.` → `## 15.` + Phụ lục A), thứ tự đúng: Phần 1 CCU (ch3–10) TRƯỚC, Phần 2 ZCU (ch11–13) SAU.
   - Quét credential: 4 lần xuất hiện "password" đều là mô tả ô nhập UI/ghi chú cấu hình chung (`••••••`, `PasswordAuthentication no`) — **KHÔNG có mật khẩu/token/private key thật** trong tài liệu. Credential thật vẫn chỉ nằm ở `temp/` (gitignore).
2. **Bổ sung nhãn nháp:** thêm dòng "Ảnh minh họa hiện có: 19 (Hình 1–19) / còn 61 vị trí đang chờ chụp bổ sung. Bản chính thức sẽ thay toàn bộ marker ⏳ bằng ảnh thật." vào khối cảnh báo đầu tài liệu.
3. **Xuất DOCX + PDF:** chạy script với `PYTHONIOENCODING=utf-8` → `✓ DOCX hoàn thành` + `✓ PDF hoàn thành` (docx2pdf pass ngay, không gặp lỗi RPC lần này). File không bị lock.
4. **Kiểm tra output:**
   - DOCX 967.502 bytes — unzip `word/media/` có 20 ảnh nhúng (19 screenshot + logo), size từng ảnh khớp nguồn.
   - PDF 1.738.129 bytes.
   - Bug đánh số ordered list đã fix từ trước (text tĩnh, không dùng style "List Number") — không tái hiện.
5. Chạy checklist DoD đầy đủ của documentation-writer (bảng dưới).

## Checklist Definition of Done (documentation-writer) — kết quả

### Completeness
| Mục | Kết quả | Ghi chú |
|---|---|---|
| Ứng dụng chạy thật trong suốt quá trình | ✅ | CcuUI Release chạy từ exe (Phase 1–2); phần ZCU viết từ code/script thật |
| Mọi screenshot chụp từ app đang chạy thật | ✅ | 19/19 ảnh hiện có đều chụp thật (PrintWindow), không placeholder |
| Build Release, chạy từ exe Release | ✅ | STEP-1.1 (app Avalonia desktop, không phải WinForms nhưng áp dụng cùng nguyên tắc) |
| Số màn hình ghi lại = Screen Inventory | ⚠️ | 18/18 màn hình đều có MỤC HƯỚNG DẪN chữ đầy đủ; nhưng nhiều màn hình mới có marker thay ảnh |
| Mỗi màn hình đủ screenshot mọi trạng thái | ❌ | **19/80 ảnh (79 checklist + 1 bù)** — 61 vị trí còn marker ⏳ vì ZCU offline. **Đây là lý do bản này là NHÁP** |
| Mỗi màn hình đủ hướng dẫn mọi thao tác | ✅ | Nội dung chữ phủ 100% (ch3–13 + FAQ) |
| Mỗi trạng thái mô tả có ảnh chèn ngay tại chỗ | ❌ | 61 chỗ đang là marker ⏳ đúng vị trí — thay bằng ảnh khi VM hoạt động |
| Mỗi ảnh có caption *Hình X* | ✅ | 19/19 |
| Số PNG = số `![]()` (1:1) | ✅ | 19 = 19; thư mục screenshots không có ảnh mồ côi |

### Brand KZTEK
| Mục | Kết quả | Ghi chú |
|---|---|---|
| Logo KZTEK header góc trái | ✅ | Script tự chèn (`Kztek_Logo.png` — có trong `word/media/`) |
| Heading Navy #251C53, accent Cam #F05922, bảng nền Navy chữ trắng | ✅ | Script `md_to_docx_kztek.py` áp brand tự động |
| Screenshot hiển thị rõ, không cắt | ✅ | Verify media nhúng đủ size |

### Kỹ thuật
| Mục | Kết quả | Ghi chú |
|---|---|---|
| DOCX mở được, font không lỗi | ✅ | File hợp lệ (zip đúng cấu trúc, media đủ), 967 KB |
| PDF xuất đúng, không vỡ bố cục | ✅ | 1,7 MB, xuất bằng docx2pdf từ chính DOCX |
| Không có thông tin nhạy cảm | ✅ | Đã quét — chỉ có ví dụ giả `<user>`, `••••••` |
| Ordered list đánh số đúng | ✅ | Fix text tĩnh vẫn hiệu lực |
| File output không bị lock trước khi chạy | ✅ | Không có lock file |

> **Kết luận DoD:** Chưa đạt DoD đầy đủ của WF-DOCS (2 mục ❌ về coverage ảnh). Đúng như user đã chốt: đây là **BẢN NHÁP**, phát hành nội bộ để review nội dung chữ; bản chính thức chỉ ra sau khi bổ sung 61 ảnh.

## Việc còn lại để hoàn thiện (khi VM ZCU hoạt động trở lại)
1. **Chạy lại STEP-2.2** phần còn thiếu: 24/28 ảnh nhóm Remote & Điều khiển + 2 ảnh bù nhóm 2.1 (`network-scan-results.png`, `connection-entry-default.png` bản P01 online) — cần ZCU `192.168.1.4` mở cổng 22/17600.
2. **Chạy STEP-2.3** (đang ⏭️ HOÃN): nhóm Triển khai & Quản trị — 20 ảnh; nhớ ràng buộc user đã chốt: ZcuSetupWizard được cài thật; KioskDeploy KHÔNG bấm Deploy thật; RemoteAppInstall KHÔNG cài .deb thật.
3. **Chạy STEP-2.4** (đang ⏭️ HOÃN): nhóm Giám sát & Hệ thống — 15 ảnh (HealthMonitor có dữ liệu, SystemInventory, License qua harness `temp/`, ảnh terminal ZCU #53–61 Phụ lục A).
4. **Thay 61 marker ⏳** trong MANUAL bằng ảnh thật, đánh số tiếp từ **Hình 20**, cập nhật/xóa Phụ lục A + gỡ nhãn "BẢN NHÁP", nâng phiên bản 0.9 → 1.0.
5. **Khôi phục profile store thật** từ `temp/user-manual-ccu-zcu/profiles.backup.json` (hiện vẫn là dữ liệu mẫu P01/P02/P03 — CHƯA khôi phục ở bước này vì Phase 2 còn dở, cần dữ liệu mẫu để chụp tiếp).
6. **Xuất lại DOCX + PDF** và chạy lại checklist DoD — lúc đó mới đổi status plan → done.

## Artifact
- `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` — cập nhật nhãn nháp (19/61)
- `docs/user-manuals/MANUAL-ccu-zcu-remote-control.docx` — 967 KB, 20 ảnh nhúng ✅ (BẢN NHÁP)
- `docs/user-manuals/MANUAL-ccu-zcu-remote-control.pdf` — 1,7 MB ✅ (BẢN NHÁP)

## Quyết định quan trọng
- Số liệu Handoff STEP-3.1 được xác minh đúng bằng đếm thực tế: 19 ảnh / 61 marker / 61 dòng Phụ lục A — không phải sửa.
- Giữ status plan `in-progress` (KHÔNG `done`): DoD coverage ảnh chưa đạt, Phase 2 còn 2.2 dở + 2.3/2.4 hoãn.
- KHÔNG khôi phục profile store ở bước này — dữ liệu mẫu P01/P02/P03 còn cần cho phiên chụp bổ sung.

## Handoff Log lần 1 (lịch sử — đã được lần 2 thay thế)
- Đã làm: Rà soát MANUAL (19 ảnh/61 marker/61 dòng Phụ lục A khớp, 0 link chết, 0 credential thật, mục lục khớp), thêm dòng thống kê ảnh vào nhãn nháp, xuất DOCX (967 KB, 20 ảnh nhúng) + PDF (1,7 MB) thành công ngay lần 1.
- File/module đã đọc hoặc đổi: đổi `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` (nhãn nháp) + sinh `.docx`/`.pdf`; đổi step file này + `PLAN-MASTER.md`.
- Quyết định quan trọng: plan giữ `in-progress`; profile store CHƯA khôi phục (giữ P01/P02/P03 cho phiên chụp bổ sung); ảnh mới đánh số từ Hình 20.
- Bước sau cần biết: khi VM ZCU online → chạy lại 2.2 (24 ảnh + 2 bù), 2.3, 2.4 theo ràng buộc đã chốt trong PLAN-MASTER; thay 61 marker; khôi phục store từ `temp/user-manual-ccu-zcu/profiles.backup.json`; xuất lại DOCX/PDF rồi mới đóng plan.

---

# Lần chạy 2 — BẢN CHÍNH THỨC (2026-07-26 21:52)

## Đã làm (lần 2)
1. **Read từng ảnh mới (57 file) trước khi chèn** để viết alt/caption đúng nội dung thật — phát hiện và sửa theo thực tế: `remote-command-error` là lỗi [STDERR] trong Console Output (không phải status cam); `file-manager-error-connect` là lỗi quyền truy cập/đường dẫn; `health-monitor-loading` báo "đang tải dữ liệu"; `zcu-terminal-session-x11` kiểm tra qua `loginctl` (SSH); `zcu-terminal-ssh-install` xác nhận bằng `dpkg -l`; `zcu-terminal-ssh-keygen` dùng cú pháp Windows thay `ssh-copy-id`; `zcu-setup-wizard-error` là lỗi thiếu IP/SSH/Token.
2. **Thay 57 marker ⏳ bằng ảnh thật** (script `temp/user-manual-ccu-zcu/finalize_manual.py` — assert từng chuỗi, không thay mù) + cập nhật caption Hình 2 (`connection-entry-default` bản P01 online). **4 marker không có ảnh** (`zcu-terminal-setup-script`, `kiosk-deploy-log`, `kiosk-deploy-error`, `remote-app-install-output`) → thay bằng mô tả chữ tự nhiên, không lộ lý do kỹ thuật nội bộ.
3. **Đánh số Hình lại liên tục 1→75** toàn tài liệu; verify script: 75 `![]()` = 75 caption, số liên tục không trùng/nhảy, 0 marker còn lại, 75/75 PNG tồn tại, 0 ảnh dùng trùng, 0 PNG mồ côi.
4. **Sửa nội dung lệch thực tế:** ch12.6 mục 3 viết lại — ufw `Status: inactive` là mặc định Ubuntu (thiết bị thật đang inactive), thêm khuyến nghị bật ufw + mở đúng cổng (gắn với finding F06); cập nhật dòng UFW ở bảng 13.5; thêm lưu ý kiểm tra phiên X11 qua SSH (11.2) và ssh-copy-id trên Windows (13.1).
5. **Bỏ nhãn BẢN NHÁP** → phiên bản 1.0 chính thức (2026-07-26); Phụ lục A đổi thành "Ghi chú về ảnh minh họa" (bảng 4 thao tác mô tả bằng lời), mục lục cập nhật theo.
6. **Khôi phục profile store thật:** app CCU không chạy → copy `temp/user-manual-ccu-zcu/profiles.backup.json` đè `%APPDATA%\iPGS\RemoteControl\profiles.json` → verify đủ 4 máy thật: Kiosk (192.168.21.230), Kien (192.168.21.68), VietAnh-VirtualMachine (192.168.0.101), ZCU (192.168.21.230).
7. **Xuất DOCX + PDF chính thức:** DOCX 4.952.540 bytes (75 ảnh trong `word/media/`), PDF 5.101.623 bytes (%PDF-1.7). docx2pdf convert 100% thành công — chỉ báo lỗi phụ `Word.Application.Quit` SAU khi PDF đã ghi xong (verify timestamp + header PDF), không ảnh hưởng kết quả.

## Checklist Definition of Done (documentation-writer) — lần 2
| Nhóm | Mục | Kết quả |
|---|---|---|
| Completeness | Mọi screenshot chụp từ app/thiết bị thật | ✅ 75/75 (Phase 1–2, PrintWindow + terminal SSH thật) |
| Completeness | Số màn hình = Screen Inventory (18/18) | ✅ đủ hướng dẫn + ảnh |
| Completeness | Mỗi màn hình đủ trạng thái theo checklist | ✅ 75/79 ảnh checklist + 4 thao tác mô tả bằng chữ (lệnh cấm destructive của user — ghi Phụ lục A) |
| Completeness | Ảnh chèn ngay tại chỗ mô tả, không gom cuối | ✅ 0 marker còn lại |
| Completeness | Caption `*Hình X:*` từng ảnh, số liên tục | ✅ Hình 1–75 |
| Completeness | Số PNG = số `![]()` (1:1) | ✅ 75 = 75, 0 mồ côi |
| Brand | Logo KZTEK, Navy/Cam, bảng Navy chữ trắng | ✅ script tự áp |
| Kỹ thuật | DOCX mở được, 75 ảnh nhúng | ✅ 4,95 MB |
| Kỹ thuật | PDF xuất đúng | ✅ 5,1 MB (%PDF-1.7) |
| Kỹ thuật | Không credential thật (password/token/key) | ✅ quét lại — chỉ giá trị minh họa/đã che |
| Kỹ thuật | Ordered list đánh số đúng (fix text tĩnh) | ✅ không tái hiện |
| Kỹ thuật | Profile store thật khôi phục + verify | ✅ 4 máy thật |

> **Kết luận DoD lần 2:** ĐẠT đầy đủ WF-DOCS. Mục duy nhất có điều kiện: 4/79 ảnh checklist thay bằng mô tả chữ theo lệnh cấm destructive của user (đã minh bạch tại Phụ lục A của MANUAL).

## Artifact (lần 2 — chính thức)
- `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` — v1.0, 75 hình, 0 marker
- `docs/user-manuals/MANUAL-ccu-zcu-remote-control.docx` — 4,95 MB, 75 ảnh nhúng ✅
- `docs/user-manuals/MANUAL-ccu-zcu-remote-control.pdf` — 5,1 MB ✅
- `%APPDATA%\iPGS\RemoteControl\profiles.json` — khôi phục dữ liệu thật (ngoài repo)

## Handoff Log — bước sau cần biết
- Đã làm: Thay 57 marker bằng ảnh thật (Read từng ảnh trước khi viết caption) + 4 marker thành mô tả chữ; đánh số Hình 1–75 liên tục; sửa ch12.6 ufw theo thực tế inactive + khuyến nghị bật (F06); bỏ nhãn nháp → v1.0; khôi phục profile store thật (4 máy verify OK); xuất DOCX 4,95 MB (75 ảnh) + PDF 5,1 MB.
- File/module đã đọc hoặc đổi: `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md/.docx/.pdf`; script tạm `temp/user-manual-ccu-zcu/finalize_manual.py` (không commit); step file này + PLAN-MASTER.
- Quyết định quan trọng: PDF hợp lệ dù docx2pdf báo lỗi `Word.Application.Quit` (lỗi xảy ra sau khi convert 100% — verify bằng timestamp/header); plan đóng `done` — việc còn lại (F01–F06) thuộc WF-BUGFIX, ngoài plan này.
- Bước sau cần biết: 6 finding F01–F06 trong `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md` chờ senior-developer xử lý (WF-BUGFIX riêng); harness license + toolkit nằm trong `temp/` — KHÔNG commit; nếu chụp bổ sung sau này, backup store thật trước rồi khôi phục lại như quy trình bước 2.1/4.1.

## Commit
- Lần 1: commit `[user-manual-ccu-zcu] Bước 4.1` (bản nháp, hash `8231819`)
- Lần 2: commit `[user-manual-ccu-zcu] Bước 4.1 (lần 2)` trên nhánh main, 2026-07-26 — hash ghi ở message tổng kết
- Đã push: không (theo yêu cầu bước này)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
