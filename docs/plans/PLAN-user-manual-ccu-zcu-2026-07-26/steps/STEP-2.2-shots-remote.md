---
step: 2.2
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 19:10
---

# STEP 2.2 — Screenshot nhóm Remote & Điều khiển

## Input nhận
- Handoff Log STEP-2.1 (cách chạy app, gotcha đã gặp, ảnh đã có).
- Screen Inventory `_workspace/00_docwriter_screen-inventory.md` — mục nhóm 2.2.
- User báo đã bật lại máy ảo ZCU `192.168.1.4` → kiểm tra kết nối đầu bước.

## Nhiệm vụ
Chạy app thật, chụp screenshot các màn hình: **RemoteScreenWindow, RemoteScreenControl, MultiRemoteWindow, RemoteCommandWindow, FileManagerWindow** (+ ConfirmDeleteDialog, SystemInventoryWindow — chuyển từ nhóm khác vào 2.2 theo bước 0.2) + chụp bù 2 ảnh nợ của 2.1 (`network-scan-results`, `connection-entry-default` P01 online).

## Definition of Done
- [x] Đủ ảnh .png theo checklist inventory cho 5 màn hình — **ĐẠT: 27/28 nhóm 2.2** (chỉ thiếu `system-inventory-data` vì agent ZCU cũ, xem F02) + 2/2 ảnh bù 2.1
- [x] Ảnh từ app thật, đúng quy ước tên, tại `docs/user-manuals/screenshots/` (mọi ảnh verify bằng Read tool)
- [x] Handoff Log: ảnh đã chụp / thiếu, trạng thái kết nối ZCU thực tế khi chụp
- [x] Cập nhật step file này + PLAN-MASTER.md (gỡ blocker ZCU offline)
- [x] Ghi nhận lỗi chức năng phát hiện khi thao tác → `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md` (F01, F02, F03)

## Đã làm
### Phiên 1 (17:35–17:41, ZCU offline) — chụp 4 ảnh không cần thiết bị
1. ZCU offline (ping/22/17600 đều fail, quét cả subnet không thấy) → chỉ chụp được 4 ảnh trạng thái lỗi/rỗng:
   - `remote-screen-connecting.png`, `remote-screen-ssh-help.png`, `remote-screen-faulted.png`, `multi-remote-empty.png` (4 ảnh chính thức, phiên 2 KHÔNG chụp lại).
2. Tạo tool `temp/user-manual-ccu-zcu/actions-22.ps1` (dblclick-item bằng mouse_event, click theo AutoId/Name qua Win32→UIA).

### Phiên 2 (18:20–19:10, ZCU ĐÃ ONLINE) — hoàn tất 23 ảnh mới + 2 bù 2.1
3. **Chuẩn bị dữ liệu demo qua SSH** (plink, mật khẩu đọc từ `temp/.../zcu-connection.md`): tạo `/home/kztek/kztek-demo/` (bao-cao-thang.txt, config-mau.json, backup.sh +x, logs/); `demo-upload.txt` trên Desktop Windows; thư mục local `sync-demo/` cho Đồng bộ.
4. **Bù 2.1 (2):** `network-scan-results.png` (quét subnet 192.168.1. → tìm thấy ZcuAgent/1.0 @192.168.1.4); `connection-entry-default.png` (ghi đè — P01 online, badge CPU 1.5%/RAM 16%/Disk 38%).
5. **RemoteScreen (5 mới):** `streaming` (đang xem desktop ZCU thật), `privacy-on` (nút Privacy sáng), `record-on` (nút đổi "⏹ Stop" — đang ghi), `chat` (gõ tin nhắn), `disconnected` (status "Đã ngắt kết nối"). Best-effort: `clipboard-sync` (chụp được nhưng agent cũ không phản hồi).
6. **MultiRemote (3):** `grid-2x2` (P01 live + P02/P03 đen), `custom-grid` (lưới 1x2), `tab-view` (thẻ tab 3 máy).
7. **FileManager (7):** `default` (/home/kztek), `navigate` (vào kztek-demo), `filter` (lọc "bao"), `upload-success` (demo-upload.txt), `sync-result` (thêm du-lieu/sync-config), `after-delete` (xóa du-lieu.txt còn 6 mục), `error-connect` (best-effort — điều hướng /root/... → status lỗi quyền).
8. **ConfirmDelete (2):** `default` (xóa file), `dir-warning` (chọn thư mục logs → cảnh báo đỏ rm -rf, đã bấm Hủy giữ lại logs).
9. **RemoteCommand (5):** `console-default`, `snippet` (gõ RAM — dropdown KHÔNG mở, xem F03), `console-output` (uname -a && df -h thật), `sftp-tab` (tab Truyền nhận File), `error` (command not found).
10. **SystemInventory (0/1): 🛑 KHÔNG chụp được** — agent ZCU (build 2026-07-24) CŨ hơn commit thêm SysInfo (`52e93ae`, 2026-07-25) → SysInfoReq không được agent trả lời, cửa sổ không mở. Ghi nhận F02.
11. **Kiểm thử phát hiện lỗi:** ghi nhận F01 (record tự dừng "độ phân giải thay đổi"), F02 (SysInfo/Privacy/Chat/Clipboard fail âm thầm với agent cũ), F03 (snippet dropdown không mở) vào `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md`.

## Artifact
**Nhóm 2.2 — 27/28 ảnh (verify từng ảnh bằng Read tool):**
- RemoteScreen (9/9): `remote-screen-connecting`, `-ssh-help`, `-faulted` (phiên 1); `-streaming`, `-privacy-on`, `-record-on`, `-chat`, `-clipboard-sync`, `-disconnected` (phiên 2).
- MultiRemote (4/4): `multi-remote-empty` (phiên 1); `-grid-2x2`, `-custom-grid`, `-tab-view`.
- FileManager (7/7): `file-manager-default`, `-navigate`, `-filter`, `-upload-success`, `-sync-result`, `-after-delete`, `-error-connect`.
- ConfirmDelete (2/2): `confirm-delete-default`, `-dir-warning`.
- RemoteCommand (5/5): `remote-command-console-default`, `-snippet`, `-console-output`, `-sftp-tab`, `-error`.
- SystemInventory (0/1): 🛑 `system-inventory-data` — KHÔNG chụp được (agent ZCU cũ, F02).

**Bù 2.1 (2/2):** `network-scan-results`, `connection-entry-default` (P01 online).

**Findings:** `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md` (F01, F02, F03) + `docs/bugs/screenshots/bug-record-stopped-resolution.png`.

**Tool mới (temp/, gitignore):** `zcu-ssh.ps1` (SSH qua plink), `restore-window.ps1`, `savedlg-type.ps1`, `type-in-el.ps1`, `type-slow.ps1`, `snippet-open.ps1`, `tap-xy.ps1`, `capture-nofg.ps1`, `verify-token.ps1`, `show-profiles.ps1`; bổ sung action cho `actions-22.ps1` (toggle-in-window, set-in-window, select-tab, select-rows, mouse-click-row/el, click-main-named, click-name-any).

## Quyết định quan trọng
- `system-inventory-data`: BLOCK theo môi trường (agent ZCU build 2026-07-24 cũ hơn feature SysInfo commit 2026-07-25) — KHÔNG bịa ảnh. Chụp được sau khi bước 2.3 cập nhật/deploy lại agent. 27/28 > gate 26/28 nên step vẫn Done.
- Record-on: chụp được trên phiên kết nối MỚI (lúc đó recorder khởi tạo thành công); nhưng ghi hình có thể tự dừng nếu resolution frame khác snapshot (F01).
- Che thông tin: mọi ảnh dùng dữ liệu demo (token "demo-****", mật khẩu ô ••••); IP nội bộ 192.168.1.4 được phép hiển thị.

## Handoff Log — bước sau cần biết
- Đã làm: ZCU online → hoàn tất 27/28 ảnh nhóm 2.2 + 2/2 ảnh bù 2.1, tạo dữ liệu demo SSH, ghi 3 findings (F01/F02/F03). Chỉ thiếu `system-inventory-data` do agent ZCU cũ.
- File/module đã đọc hoặc đổi: `docs/user-manuals/screenshots/` (+23 ảnh mới, ghi đè `connection-entry-default`); `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md` (mới) + `docs/bugs/screenshots/bug-record-stopped-resolution.png`; nhiều tool trong `temp/user-manual-ccu-zcu/`. Đọc: `RemoteScreenWindow.axaml.cs`, `RemoteCommandWindow.axaml.cs`, `ComputerProfileStore.cs`.
- Quyết định quan trọng: agent ZCU đang chạy (build 07-24) CŨ hơn client → SysInfo/Privacy/Chat/Clipboard không hoạt động thật. Muốn chụp `system-inventory-data` + kiểm chứng đủ Privacy → bước 2.3 phải deploy lại agent bản mới trước.
- Bước sau cần biết: (1) SSH tới ZCU dùng `temp/.../zcu-ssh.ps1 -Cmd "..."` (đã cache hostkey ở `zcu-hostkey.txt`); (2) MSYS_NO_PATHCONV=1 khi truyền path Linux `/home/...` qua PowerShell tránh Git-Bash chuyển thành `C:\Program Files\...`; (3) file .ps1 KHÔNG có BOM → PowerShell đọc sai emoji/tiếng Việt trong literal → dùng wildcard ASCII (`-like "*File*"`) thay khớp tên có emoji; (4) AutoCompleteBox dropdown (F03) không mở được bằng UIA/keyboard/mouse — coi như best-effort; (5) app vẫn chạy PID 31064; dữ liệu demo `/home/kztek/kztek-demo/` (logs/, backup.sh, bao-cao-thang.txt, config-mau.json, demo-upload.txt, sync-config.txt) còn nguyên trên ZCU cho bước sau.

## Commit
- Hash: 1bc4d8e
- Đã push: không

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
