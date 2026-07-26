---
step: 2.2
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: blocked
completed_at:
---

# STEP 2.2 — Screenshot nhóm Remote & Điều khiển

## Input nhận
- Handoff Log STEP-2.1 (cách chạy app, gotcha đã gặp, ảnh đã có).
- Screen Inventory `_workspace/00_docwriter_screen-inventory.md` — mục nhóm 2.2.
- User báo đã bật lại máy ảo ZCU `192.168.1.4` → kiểm tra kết nối đầu bước.

## Nhiệm vụ
Chạy app thật, chụp screenshot các màn hình: **RemoteScreenWindow, RemoteScreenControl, MultiRemoteWindow, RemoteCommandWindow, FileManagerWindow** (+ ConfirmDeleteDialog, SystemInventoryWindow — chuyển từ nhóm khác vào 2.2 theo bước 0.2) + chụp bù 2 ảnh nợ của 2.1 (`network-scan-results`, `connection-entry-default` P01 online).

## Definition of Done
- [ ] Đủ ảnh .png theo checklist inventory cho 5 màn hình (hoặc ghi rõ từng ảnh thiếu + lý do thiếu thiết bị) — **CHƯA ĐẠT: 4/28, ZCU offline**
- [x] Ảnh từ app thật, đúng quy ước tên, tại `docs/user-manuals/screenshots/` (4 ảnh đã chụp đều thật, verify bằng Read)
- [x] Handoff Log: ảnh đã chụp / thiếu, trạng thái kết nối ZCU thực tế khi chụp
- [x] Cập nhật step file này + PLAN-MASTER.md (mục Blockers đã cập nhật)

## Đã làm
1. **Kiểm tra ZCU `192.168.1.4` (2026-07-26 17:35–17:41):** dù user báo đã bật lại VM, kết quả vẫn **KHÔNG kết nối được**:
   - Ping thất bại liên tục 14 lần trong ~6 phút (ICMP "Destination host unreachable" từ gateway 192.168.1.9), có chờ VM boot giữa các lần thử.
   - Test TCP cổng 22 và 17600 tới 192.168.1.4: đều FAIL.
   - Quét TOÀN BỘ subnet 192.168.1.1–254 tìm cổng 22 và 17600 (đề phòng VM nhận IP khác qua DHCP): **KHÔNG máy nào mở 2 cổng này** → VM ZCU thực sự chưa lên mạng (khả năng: adapter VM đang ở chế độ NAT thay vì Bridged, hoặc VM chưa boot xong/treo).
2. **Tận dụng chụp 4 ảnh nhóm 2.2 KHÔNG cần ZCU** (từ app Release đang chạy, verify từng ảnh bằng Read tool):
   - `remote-screen-connecting.png` — double-click máy P01 → RemoteScreenWindow status vàng "Đang kết nối..." (trạng thái thật trong lúc client retry).
   - `remote-screen-ssh-help.png` — banner vàng "💡 Cài SSH (nếu máy chưa có)" + lệnh apt install openssh-server (hiện vì P01 có SshReachable=false), status "Đã ngắt kết nối" giữa 2 lần retry.
   - `remote-screen-faulted.png` — sau 10 lần reconnect (MaxReconnectAttempts=10, ~10 phút): status đỏ "Lỗi kết nối" + banner đỏ "Không thể kết nối sau nhiều lần thử. Kiểm tra địa chỉ và kết nối mạng."
   - `multi-remote-empty.png` — mở Multi-Remote Dashboard → Ngắt tất cả → trạng thái rỗng "Chưa có máy tính nào trong phiên Remote Multi-Dashboard".
3. Tạo tool bổ sung `temp/user-manual-ccu-zcu/actions-22.ps1` (double-click item thật bằng mouse_event, click nút theo AutoId/Name trong cửa sổ con qua Win32→UIA).
4. **24/28 ảnh còn lại + 2 ảnh bù 2.1 + chuẩn bị dữ liệu demo SSH: 🛑 BLOCK** — tất cả cần ZCU online (stream thật, SFTP, SSH). KHÔNG bịa ảnh, KHÔNG placeholder.

## Artifact
**Đã chụp (4, verify bằng Read):**
- `docs/user-manuals/screenshots/remote-screen-connecting.png`
- `docs/user-manuals/screenshots/remote-screen-ssh-help.png`
- `docs/user-manuals/screenshots/remote-screen-faulted.png`
- `docs/user-manuals/screenshots/multi-remote-empty.png`

**Còn thiếu — lý do: ZCU 192.168.1.4 offline (không ping/không mở cổng 22, 17600):**
- RemoteScreen (5): `remote-screen-streaming`, `-privacy-on`, `-record-on`, `-chat`, `-clipboard-sync`, `-disconnected` (cần phiên stream thật)
- MultiRemote (3): `multi-remote-grid-2x2`, `-custom-grid`, `-tab-view` (cần phiên live)
- FileManager (7): toàn bộ `file-manager-*` (cần SFTP)
- ConfirmDelete (2): `confirm-delete-default`, `-dir-warning` (cần phiên FileManager)
- RemoteCommand (5): toàn bộ `remote-command-*` (cần SSH — nút CMD Shell disabled khi SSH đỏ)
- SystemInventory (1): `system-inventory-data` (cần đang stream)
- Bù 2.1 (2): `network-scan-results`, `connection-entry-default` (bản P01 online)
- Chưa tạo được dữ liệu demo `/home/kztek/kztek-demo/` trên ZCU (SSH fail) — làm ngay đầu phiên chụp lại.

## Quyết định quan trọng
- Không đánh dấu Done: 4/28 < gate 80% → step 🛑 BLOCKED, chờ user đưa ZCU online rồi chạy lại phần còn lại (4 ảnh đã có không cần chụp lại).
- `remote-screen-ssh-help` chụp ở trạng thái retry (status "Đã ngắt kết nối") — banner SSH-help là chủ thể ảnh, hợp lệ; khi có ZCU online KHÔNG cần chụp lại ảnh này.
- Nghi vấn chuyển cho user: VM ZCU có thể đang ở network mode NAT (không thấy từ LAN) — đề nghị kiểm tra chế độ Bridged + `ip a` trong VM.

## Handoff Log — bước sau cần biết
- Đã làm: ZCU vẫn offline sau ~6 phút retry + quét cả subnet (không IP nào mở 22/17600) → chỉ chụp được 4/28 ảnh nhóm 2.2 (các trạng thái lỗi/rỗng không cần ZCU), step BLOCKED chờ thiết bị.
- File/module đã đọc hoặc đổi: `docs/user-manuals/screenshots/` (+4 png); tool mới `temp/user-manual-ccu-zcu/actions-22.ps1` (action `dblclick-item` mở RemoteScreen bằng double-click chuột thật — nút "Kết nối" bị disabled khi AgentReachable≠true nhưng double-tap ListItem KHÔNG bị chặn).
- Quyết định quan trọng: 4 ảnh đã có là bản chính thức, không chụp lại; khi ZCU online chạy tiếp 24 ảnh còn lại + 2 ảnh bù 2.1 + tạo dữ liệu demo SSH (inventory §5.2) trước khi chụp FileManager/RemoteCommand.
- Bước sau cần biết: (1) Faulted chỉ hiện sau ĐỦ 10 lần reconnect ≈ 10 phút với host unreachable — muốn faulted nhanh dùng token sai (AUTH_FAIL → Faulted ngay, không retry); (2) app đang chạy PID 31064, profile store giữ nguyên P01/P02/P03; (3) gotcha PowerShell: `sleep N; lệnh` bị harness chặn — dùng `powershell -Command "Start-Sleep -Seconds N"` tách riêng.

## Commit
- Hash: [điền sau khi commit]
- Đã push: không

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
