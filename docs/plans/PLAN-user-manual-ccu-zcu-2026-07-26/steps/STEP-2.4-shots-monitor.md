---
step: 2.4
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 21:28
---

# STEP 2.4 — Screenshot nhóm Giám sát & Hệ thống (+ terminal ZCU)

## Input nhận
- Handoff Log STEP-2.3 (cách chạy app, trạng thái thiết bị, ảnh đã có/thiếu).
- Screen Inventory `_workspace/00_docwriter_screen-inventory.md` — mục nhóm 2.4.

## Nhiệm vụ
Chạy app thật, chụp screenshot các màn hình: **HealthMonitorWindow, SystemInventoryWindow, LicenseWindow**. Nếu có thiết bị ZCU/máy Linux truy cập được: chụp thêm ảnh terminal phía ZCU cho phần triển khai (cài `.deb`, `systemctl status zcuagent`, kiểm tra SSH key) — phục vụ STEP-3.1. Không có thiết bị → ghi 🛑 BLOCK cho các ảnh đó, báo user, KHÔNG bịa.

## Definition of Done
- [ ] Đủ ảnh .png theo checklist inventory cho 3 màn hình CCU (hoặc ghi rõ ảnh thiếu + lý do)
- [ ] Ảnh terminal ZCU (nếu có thiết bị) hoặc ghi rõ BLOCK
- [ ] Ảnh từ app/máy thật, đúng quy ước tên, tại `docs/user-manuals/screenshots/`
- [ ] Handoff Log tổng kết Phase 2: danh sách TOÀN BỘ ảnh đã có / còn thiếu (gom cả 2.1–2.4) để Phase 3 biết chèn gì
- [ ] Cập nhật step file này + PLAN-MASTER.md (Blockers nếu có)

## Đã làm
Chụp **14/15 ảnh nhóm 2.4** từ app/thiết bị thật, mỗi ảnh verify bằng Read tool:
- **HealthMonitorWindow (2/2):** `health-monitor-loading` (mở tức thì, "--%" + "Đang tải..."), `health-monitor-data` (CPU 0.7% / RAM 984-5876MB / Ổ đĩa 24G-67G + Top Process thật của ZCU).
- **LicenseWindow (4/4):** mở bằng **harness dev tạm** `temp/user-manual-ccu-zcu/license-harness/` (WinExe tham chiếu CcuUI, dùng `AppBuilder...Start()` → KHÔNG kích hoạt lifetime nên KHÔNG mở ConnectionEntryWindow; KHÔNG sửa code sản phẩm). Chụp `license-default`, `license-error-empty` ("Vui lòng nhập License Key."), `license-error-invalid` (key giả → "Chữ ký số License không hợp lệ"), `license-success` (kích hoạt bằng backdoor `ANHNV` → "Kích hoạt thành công!"). Hardware ID + ô key đều đã che.
- **KeyGen (1/1):** `keygen-output` (đổi tên từ keygen-console) — build+run `dotnet run KeyGen`, in Public/Private Key RSA-2048, **CHE TOÀN BỘ private key** bằng mask-region.
- **Terminal ZCU (7/8):** `zcu-terminal-session-x11` (loginctl → Type=x11), `-ssh-install` (dpkg -l + apt-get -s: openssh 8.9p1 already newest), `-appsettings` (Port 17600/Token che/AllowedClientIPs 0.0.0.0/0), `-service-status` (`systemctl --user status ipgs-remote-agent` → active running, PID 3471), `-journalctl` (`journalctl --user` 20 dòng cuối), `-ssh-keygen` (ssh-keygen ed25519 + copy pubkey lên ZCU, fingerprint che, key demo đã xóa cả 2 phía sau khi chụp), `-ufw-status` (bù cho MANUAL).

**KHÔNG chụp được (1):** `zcu-terminal-setup-script.png` — yêu cầu chạy lại `setup-zcu-agent.sh` (ghi đè + restart agent đang chạy), ngoài ranh giới "chỉ đọc/giám sát" của bước 2.4 → BLOCK có lý do.

**[YÊU CẦU USER] Kiểm tra chức năng — thêm F06** vào file BUG: backdoor cứng `ANHNV` bỏ qua toàn bộ kiểm tra license + agent `AllowedClientIPs: 0.0.0.0/0` với token yếu `ANHNV` + ufw inactive (P2 bảo mật). Không tự sửa code.

## Artifact
- 14 ảnh mới `docs/user-manuals/screenshots/`: health-monitor-{loading,data}, license-{default,error-empty,error-invalid,success}, keygen-output, zcu-terminal-{session-x11,ssh-install,appsettings,service-status,journalctl,ssh-keygen,ufw-status}.
- `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md` — thêm **F06** (P2 bảo mật).
- Harness tạm `temp/user-manual-ccu-zcu/license-harness/` + toolkit mới (`actions-24.ps1`, `zcu-term-shot.ps1`, `term-drive.ps1`, `find-title.ps1`) — **KHÔNG commit** (trong temp/).

## Quyết định quan trọng
- LicenseWindow không có entry UI → harness `AppBuilder.Configure<App>().Start(AppMain)` mở trực tiếp window (styles/resources đầy đủ) mà không bật ClassicDesktopLifetime → tránh mở MainWindow. Build **Debug** (Release bị lock DLL do app CCU đang chạy giữ `KztekComponentAvalonia.dll`).
- 3 file đổi tên cho khớp marker MANUAL (MANUAL là nguồn chuẩn tên nhúng): keygen-console→**keygen-output**, zcu-terminal-x11-check→**zcu-terminal-session-x11**, zcu-terminal-systemctl-status→**zcu-terminal-service-status**.
- ufw **inactive** trên ZCU thật — KHÔNG có luật `17600/tcp ALLOW` như MANUAL ch12.6 kỳ vọng. Giữ ảnh thật (trung thực), đây là bằng chứng thêm cho F06 (agent không có firewall).
- Terminal ZCU chụp qua cửa sổ WindowsTerminal (title `ZCU-TERM`) chạy plink SSH thật — không dùng ảnh giả.

## Handoff Log — bước sau cần biết
- **Đã làm:** 14/15 ảnh nhóm 2.4 (2 health-monitor + 4 license + 1 keygen + 7 terminal ZCU), verify từng ảnh bằng Read; thêm F06 (P2 bảo mật) vào file BUG. Phase 2 hoàn tất.
- **Tổng ảnh toàn dự án: 75 file PNG** trong `docs/user-manuals/screenshots/`.
- **Marker MANUAL còn thiếu file (4 — cho bước thay marker):** (1) `zcu-terminal-setup-script.png` — nhóm 2.4, BLOCK (chạy lại setup ghi đè agent, ngoài scope đọc); (2) `kiosk-deploy-log.png`, (3) `kiosk-deploy-error.png`, (4) `remote-app-install-output.png` — nhóm 2.3 để lại, BLOCK vĩnh viễn theo lệnh cấm user (destructive). → 4 marker này bước thay-marker phải thay bằng MÔ TẢ CHỮ, không có ảnh. **57/61 marker MANUAL đã có file khớp.** Ngoài ra 3 ảnh best-effort transient (remote-screen-connecting/clipboard-sync, file-manager-error-connect) đã có file — không thiếu.
- **Bước sau cần biết:** (a) MANUAL ch12.6 mô tả ufw có luật 17600/tcp ALLOW nhưng thiết bị thật ufw INACTIVE → bước thay-marker nên chỉnh câu chữ thành "nếu bật ufw…" hoặc gắn với F06. (b) Bước thay-marker xong PHẢI khôi phục profile store từ `temp/user-manual-ccu-zcu/profiles.backup.json` rồi chạy lại xuất DOCX/PDF (4.1). (c) Harness license + toolkit nằm trong temp/, KHÔNG commit; license.key ghi trong AppData\Kztek\RemoteControl đã bị xóa sau khi chụp success.

## Commit
- Hash: 8b29c12
- Đã push: không (theo lệnh — chỉ commit)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
