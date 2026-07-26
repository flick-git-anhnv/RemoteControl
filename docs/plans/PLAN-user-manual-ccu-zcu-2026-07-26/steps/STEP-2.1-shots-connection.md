---
step: 2.1
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 17:28
---

# STEP 2.1 — Screenshot nhóm Kết nối & Quản lý thiết bị

## Input nhận
- Handoff Log STEP-1.1 (đường dẫn exe Release + cách chạy app).
- Screen Inventory `_workspace/00_docwriter_screen-inventory.md` — mục nhóm 2.1.

## Nhiệm vụ
Chạy app thật (build Release từ STEP-1.1), chụp screenshot các màn hình: **MainWindow (màn hình chính), ConnectionEntryWindow, ComputerEditWindow, NetworkScanWindow, SessionPickerWindow, ConfirmDeleteDialog** — theo đúng danh sách ảnh + tên file trong Screen Inventory. Lưu vào `docs/user-manuals/screenshots/`. Trạng thái cần ZCU thật/SSH mà không có → ghi rõ ảnh nào bị thiếu vào Handoff Log + đánh dấu 🛑 partial-block, KHÔNG bịa/placeholder.

## Definition of Done
- [ ] Đủ ảnh .png cho 6 màn hình nhóm này theo checklist inventory (hoặc ghi rõ ảnh thiếu + lý do thiết bị)
- [ ] Ảnh chụp từ app thật, đúng quy ước đặt tên, đặt tại `docs/user-manuals/screenshots/`
- [ ] Handoff Log: danh sách ảnh đã chụp / ảnh thiếu-vì-sao, gotcha khi mở từng màn hình
- [ ] Cập nhật step file này + PLAN-MASTER.md

## Đã làm
- Backup profile store thật của user đã có sẵn tại `temp/user-manual-ccu-zcu/profiles.backup.json` (4 máy: Kiosk, Kien, VietAnh-VirtualMachine, ZCU 192.168.21.230 — token ANHNV). Store `%APPDATA%\iPGS\RemoteControl\profiles.json` được nạp dữ liệu mẫu 3 máy P01/P02/P03 từ `temp/user-manual-ccu-zcu/profiles.sample.json`.
- Chụp `connection-entry-empty` (store rỗng, phiên trước) → nạp 3 máy mẫu → restart app Release → chụp 14 ảnh còn lại bằng UIA (`temp/user-manual-ccu-zcu/actions-21.ps1`) + script chụp `capture-window.ps1`. Mỗi ảnh đã verify bằng Read tool (đúng cửa sổ/trạng thái).
- **ZCU thật 192.168.1.4 KHÔNG phản hồi cổng 22/17600** (ping OK nhưng TCP timeout, thử nhiều lần) → các trạng thái "P01 online" không tạo được trong bước này.

## Artifact
**15/16 ảnh tại `docs/user-manuals/screenshots/`:**
- ConnectionEntry (9/9): `connection-entry-empty.png`, `connection-entry-default.png` ⚠️, `connection-entry-offline.png`, `connection-entry-search.png`, `connection-entry-tab-recent.png`, `connection-entry-bulk-selected.png`, `connection-entry-quick-filled.png`, `connection-entry-wol-error-no-mac.png`, `connection-entry-wol-success.png`
- ComputerEdit (3/3): `computer-edit-default.png`, `computer-edit-filled.png`, `computer-edit-edit-mode.png`
- NetworkScan (2/3): `network-scan-default.png`, `network-scan-scanning.png`
- SessionPicker (1/1): `session-picker-default.png`

**Ảnh còn thiếu / cần chụp lại:**
- 🛑 `network-scan-results.png` — THIẾU: ZCU 192.168.1.4 không mở cổng 17600 (agent không chạy/thiết bị khác giữ IP), quét thật ra "0 máy". Chụp bổ sung ở bước 2.2/2.3 khi ZCU online (sau khi cài agent qua ZcuSetupWizard).
- ⚠️ `connection-entry-default.png` — ĐÃ chụp (3 máy mẫu, bố cục đúng) nhưng P01 hiển thị OFFLINE thay vì online + badge CPU/RAM/Disk như spec. Chụp lại ở bước 2.2 khi ZCU online (chỉ cần 1 lệnh capture, dữ liệu mẫu giữ nguyên).

## Quyết định quan trọng
- MAC của P01 dùng giá trị demo `A0:B1:C2:D3:E4:F5` (không phải MAC thật) → không cần che MAC trong mọi ảnh; dialog WOL success vẫn hiện bình thường.
- Token thật `ANHNV` giữ trong store (cần cho kết nối thật ở 2.2) nhưng mọi ảnh hiển thị token đều được set tạm `demo-****` qua UIA trước khi chụp (không lưu) — không lộ token trong ảnh.
- `connection-entry-offline.png` = crop cận cảnh dòng P02 từ ảnh default (System.Drawing) — đúng yêu cầu "cận cảnh".
- Giữ NGUYÊN dữ liệu mẫu P01/P02/P03 trong profile store sau bước này (bước 2.2–2.4 còn dùng P01); khôi phục store thật từ backup thực hiện ở bước 4.1.

## Handoff Log — bước sau cần biết
- Đã làm: chụp 15/16 ảnh nhóm 2.1 từ app Release đang chạy; thiếu `network-scan-results` + cần chụp lại `connection-entry-default` khi ZCU online.
- File/module đã đọc hoặc đổi: `docs/user-manuals/screenshots/*.png` (15 file); tool trong `temp/user-manual-ccu-zcu/`: `actions-21.ps1` (UIA: click nút theo tên/AutomationId, điền TextBox, tick checkbox bằng SetFocus+Space, chọn ListItem), `close-win32.ps1` (WM_CLOSE dialog), `list-windows.ps1`, `capture-window.ps1`.
- Quyết định quan trọng: dữ liệu mẫu GIỮ NGUYÊN trong `%APPDATA%\iPGS\RemoteControl\profiles.json` (3 máy P01/P02/P03, P01 token thật ANHNV + SSH kztek); backup store thật (4 máy) tại `temp/user-manual-ccu-zcu/profiles.backup.json` — khôi phục ở bước 4.1 bằng copy đè ngược lại (app phải tắt khi copy).
- Bước sau cần biết: (1) **ZCU 192.168.1.4 hiện KHÔNG mở cổng 22/17600 — bước 2.2 phải kiểm tra lại thiết bị/nhờ user bật trước khi chụp nhóm Remote**; (2) Gotcha UIA-Avalonia: dialog con KHÔNG xuất hiện trong UIA RootElement.Children — tìm qua Win32 EnumWindows rồi `AutomationElement.FromHandle` (đã có sẵn `Get-WindowElByWin32` trong actions-21.ps1); so sánh tiêu đề cửa sổ tiếng Việt có thể fail do Unicode normalization — LUÔN dùng phần title thuần ASCII; script .ps1 có tiếng Việt PHẢI lưu UTF-8 **có BOM**; app chạy PID mới (restart trong bước này), giữ nguyên đang chạy.

## Commit
- Hash: [điền sau khi commit]
- Đã push: không (theo chỉ thị bước — không push)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
