---
task: kiosk-decouple-and-file-shell-merge
created: 2026-11-03
updated: 2026-11-03
status: completed
workflow: WF-REFACTOR
priority: P2
---

# PLAN MASTER: Tách Độc Lập Deploy Setup Kiosk & Hợp Nhất Quản Lý File / CMD Shell

## Mô tả
1. **Tách độc lập Deploy Kiosk**: Tab "Config máy tính" và Tab "Config phần mềm" trong `KioskDeployWindow` hiện đã tách độc lập hoàn toàn:
   - Tab "Config máy tính": Chỉ cài/sửa cấu hình máy & desktop environment (`DeployScope = MachineOnly`), không đụng chạm tới Autostart/Watchdog/Update phần mềm và không đòi hỏi AppExec.
   - Tab "Config phần mềm": Chỉ cài/sửa Autostart, Watchdog, Update cho app được chọn (`DeployScope = SoftwareOnly`), không đụng chạm các bước cấu hình máy tính.
   - Sửa thứ tự gỡ lock `dconf` trong `2-configure-system.sh` (xóa lock dconf trước khi set `enable-hot-corners` / `dynamic-workspaces`) và hỗ trợ reload GNOME Shell để nút `Activities` phản hồi tức thì.
2. **Hợp nhất Quản lý File (SFTP) & >_ CMD Shell (SSH)**:
   - Triển khai **Phương án A: 1 nút chung**: Gộp 2 nút `📁 Quản lý File` và `>_ CMD Shell` trên thẻ máy tính của `ConnectionEntryWindow` thành 1 nút `🛠️ File & CMD`.
   - Nâng cấp Tab 2 của `RemoteCommandWindow`: DataGrid 4 cột sortable (`Tên File / Thư mục`, `Loại`, `Kích thước`, `Ngày sửa`), ô lọc tìm kiếm file live (`PART_TxtFileFilter`), nút `Đi` (`PART_BtnFileGo`), tính năng `>_ CMD tại đây`, giữ nguyên Download, Sync, Xóa đệ quy SSH `rm -rf`.

## Phases & Steps
### Phase 1: Tách Độc Lập Deploy Kiosk & Fix Thứ Tự Dconf Lock
- `steps/STEP-1.1-kiosk-deploy-decouple-and-dconf-fix.md`: ✅ Hoàn thành (`2-configure-system.sh`, `1-install-software.sh`, `lubuntu-kiosk/2-configure-system.sh`, `KioskDeployService.cs`, `KioskDeployWindow.axaml.cs`).
  - Đã khắc phục lỗi `ResolveKioskScriptsDir` nạp tài nguyên script cũ trong `bin/.../Resources/`.
  - Đã đưa các hàm helper `_get_real_exec` và `_detect_dm` ra phạm vi toàn cục (top-level) trong bash.
  - Đã tự động vá lỗi `TypeError: primary is null` của Looking Glass trên GNOME 42 trong Just Perfection, đưa extension về `State: ENABLED`.

### Phase 2: Hợp Nhất UI Quản Lý File & CMD Shell (Phương Án A)
- `steps/STEP-2.1-merge-file-manager-and-cmd-shell.md`: ✅ Hoàn thành (`RemoteCommandWindow.axaml` và `.cs`, `ConnectionEntryWindow.axaml` và `.cs`).

### Phase 3: Kiểm Thử & Xác Minh (QA & Live ZCU Verification)
- `steps/STEP-3.1-verify-and-test.md`: ✅ Hoàn thành:
  - Solution build: 0 warnings, 0 errors.
  - 117/117 unit tests PASS.
  - Thực nghiệm Live trên ZCU `192.168.21.101`: Cả 2 scope `machine` và `software` chạy hoàn toàn độc lập với 0 lỗi, GNOME Shell reload mượt mà, nút Activities điều khiển chính xác.
