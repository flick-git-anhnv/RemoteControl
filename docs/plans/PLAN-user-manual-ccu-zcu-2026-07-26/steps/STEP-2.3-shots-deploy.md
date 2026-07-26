---
step: 2.3
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 21:06
---

# STEP 2.3 — Screenshot nhóm Triển khai & Quản trị

## Input nhận
- Handoff Log STEP-2.2 (cách chạy app, trạng thái thiết bị ZCU, ảnh đã có/thiếu).
- Screen Inventory `_workspace/00_docwriter_screen-inventory.md` — mục nhóm 2.3.

## Nhiệm vụ
Chạy app thật, chụp screenshot các màn hình: **ZcuSetupWizardWindow (từng bước wizard), KioskDeployWindow, RemoteAppInstallWindow, BulkActionWindow, CronJobWindow**. ZcuSetupWizard là màn hình trọng tâm cho phần triển khai ZCU của tài liệu — chụp đủ từng bước wizard. Bước wizard cần thao tác cài thật lên thiết bị mà không có ZCU → chụp đến bước tối đa có thể, ghi 🛑 BLOCK phần còn lại, báo user.

## Definition of Done
- [ ] Đủ ảnh .png theo checklist inventory cho 5 màn hình, đặc biệt đủ các bước ZcuSetupWizard chụp được
- [ ] Ảnh từ app thật, đúng quy ước tên, tại `docs/user-manuals/screenshots/`
- [ ] Handoff Log: ảnh đã chụp / thiếu, ghi chú luồng wizard thực tế (thứ tự bước, điều kiện next)
- [ ] Cập nhật step file này + PLAN-MASTER.md (Blockers nếu có)

## Đã làm
- **Chuyển IP ZCU mới `192.168.0.101`:** sửa default trong `temp/.../zcu-ssh.ps1`, xóa hostkey cache (tự probe lại — OK), cập nhật Host P01 trong `%APPDATA%\iPGS\RemoteControl\profiles.json` (app đang tắt, sửa JSON trực tiếp thay vì màn Sửa — an toàn tương đương, đã verify app đọc đúng).
- **Cập nhật ZcuAgent bản mới:** `dotnet publish` linux-x64 tại repo → chạy app Release (CWD = repo root để wizard tìm thấy publish dir) → ZcuSetupWizard cài thật lên P01, giữ nguyên Token/Port (bỏ tick "Tự động lưu máy" tránh tạo profile trùng). 7/7 bước OK (~10s), agent DLL trên ZCU giờ build 2026-07-26 20:57, user-unit `ipgs-remote-agent` active, stream + SysInfo hoạt động ngay → **F02 hết** (đã cập nhật finding).
- **Chụp nhóm 2.3 (17/20, mỗi ảnh verify bằng Read):** ZcuSetupWizard 5/5 (default/token-generated/error/installing/success — token thật "ANHNV" đã che bằng `mask-region.ps1`; installing+success lấy từ burst 42 frame vì cửa sổ auto-close 1,5s sau thành công); RemoteAppInstall 4/5 (default/file-selected qua dialog Duyệt File + `savedlg-type.ps1`/uninstall-dropdown/error); KioskDeploy 2/4 (2 tab); BulkAction 3/3 (default/running/results — running≡results vì lệnh chạy <1s); CronJob 3/3 (đã chụp từ phiên trước, giữ nguyên, verify lại OK).
- **Chụp bù `system-inventory-data.png`** (nhóm 2.2): kết nối stream P01 → 📊 SysInfo → cửa sổ mở với CPU/RAM/OS/Arch thật.
- **Findings mới:** F04 (status lỗi cũ không reset khi bắt đầu thao tác mới — wizard + app-install, P3), F05 (BulkAction lộ raw .NET exception khi máy thiếu SSH user, P3) → APPEND vào `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md` kèm ảnh chứng minh.
- Dọn dẹp: crontab ZCU sạch (không còn job demo), untick checkbox bulk, đóng mọi cửa sổ con; dữ liệu demo `/home/kztek/kztek-demo/` giữ nguyên cho 2.4.

## Artifact
- Ảnh mới `docs/user-manuals/screenshots/`: `zcu-setup-wizard-{default,token-generated,error,installing,success}.png`, `remote-app-install-{default,file-selected,uninstall,error}.png`, `kiosk-deploy-tab-{computer,software}.png`, `bulk-action-{default,running,results}.png`, `system-inventory-data.png` (15 ảnh mới + 3 cron có sẵn = 18 file).
- 🛑 BLOCK (theo lệnh cấm của user): `kiosk-deploy-log.png` (bấm Deploy = đổi GNOME/autologin thật), `kiosk-deploy-error.png` (không thể tạo lỗi SSH mà không bấm Deploy thật hoặc phá hồ sơ SSH đang dùng — validation duy nhất là thiếu SSH user, P01 có đủ), `remote-app-install-output.png` (cấm cài .deb thử).
- `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md` (cập nhật F02 + thêm F04, F05) + `docs/bugs/screenshots/bug-wizard-stale-error-status.png`, `bug-bulk-raw-exception.png`.
- Tool mới trong `temp/user-manual-ccu-zcu/` (không commit): `mask-region.ps1`, `update-p01-ip.ps1`; sửa IP `zcu-ssh.ps1`.

## Quyết định quan trọng
- Cập nhật IP P01 bằng cách sửa `profiles.json` trực tiếp khi app tắt (thay vì UIA qua màn Sửa) — nhanh, không đụng token mã hóa, kết quả app hiển thị đúng.
- Giữ nguyên token "ANHNV" khi cài lại agent (đúng yêu cầu); mọi ảnh có token thật đều che (`mask-region.ps1` hoặc điền demo trước khi chụp).
- `kiosk-deploy-error` xếp BLOCK thay vì cố tạo lỗi bằng cách phá hồ sơ SSH của P01 giữa phiên (rủi ro > lợi ích 1 ảnh).

## Handoff Log — bước sau cần biết
- Đã làm: 17/20 ảnh nhóm 2.3 + ảnh bù `system-inventory-data` (3 ảnh BLOCK theo lệnh cấm); ZcuAgent trên ZCU đã cập nhật bản 2026-07-26 (token/port giữ nguyên), F02 hết; thêm F04/F05 vào file BUG.
- File/module đã đọc hoặc đổi: `docs/user-manuals/screenshots/` (+15 ảnh), `docs/bugs/BUG-ccu-ui-findings-2026-07-26.md` + 2 ảnh bug, `temp/user-manual-ccu-zcu/` (zcu-ssh.ps1 IP mới, mask-region.ps1, update-p01-ip.ps1), `%APPDATA%\iPGS\RemoteControl\profiles.json` (P01 → 192.168.0.101). Đọc: `ZcuSetupWizardWindow.axaml.cs`, `RemoteAppInstallWindow.axaml.cs`, `KioskDeployWindow.axaml.cs`.
- Quyết định quan trọng: agent chạy dạng **systemd user-unit `ipgs-remote-agent`** (`~/.config/systemd/user/`) — KHÔNG phải system unit; `systemctl status ipgs-remote-agent` (không --user) sẽ không thấy → ảnh 2.4 `zcu-terminal-systemctl-status` phải dùng `systemctl --user status ipgs-remote-agent`.
- Bước sau cần biết: (1) app CCU đang chạy PID 2352, CWD=repo root, P01 online IP mới; (2) dropdown AutoCompleteBox CHỤP ĐƯỢC bằng cách bấm nút ▼ rồi `capture-region.ps1` (CopyFromScreen — popup là hwnd riêng, PrintWindow không thấy; F03 chỉ còn đúng với gõ-từ-khóa); (3) cửa sổ wizard auto-close 1,5s sau thành công → muốn chụp trạng thái cuối phải dùng `capture-burst.ps1`; (4) đừng quên `--user` khi xem service agent trên ZCU; demo data `/home/kztek/kztek-demo/` còn nguyên.

## Commit
- Hash: 3ac979f
- Đã push: không (theo chỉ thị bước này)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
