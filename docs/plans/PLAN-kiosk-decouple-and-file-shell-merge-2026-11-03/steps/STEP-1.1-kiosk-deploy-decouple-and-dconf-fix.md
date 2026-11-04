# STEP 1.1: Tách Độc Lập Deploy Kiosk & Sửa Thứ Tự Gỡ Lock Dconf

## Mục tiêu
- Hỗ trợ biến môi trường `DEPLOY_SCOPE` (`machine` | `software` | `all`) trong:
  - `scripts/linux-kiosk/2-configure-system.sh`
  - `scripts/lubuntu-kiosk/2-configure-system.sh`
- Sửa thứ tự gỡ lock dconf: nếu `LOCKDOWN_SHELL=0` và scope là machine/all, thực hiện gỡ lock và `dconf update` TRƯỚC TIÊN (Step 0), đồng thời reset các keybindings / overview keys để `Activities` mở được ngay.
- Thêm bước reload GNOME Shell an toàn trên X11 trong `1-install-software.sh` để extension `just-perfection` và `block-caribou` được kích hoạt ngay.
- Cập nhật `KioskDeployOptions` và `KioskDeployService` với enum `DeployScope`.
- Cập nhật `KioskDeployWindow.axaml.cs`: `OnDeployMachineClick` chỉ chạy scope Machine; `OnDeploySoftwareClick` chỉ chạy scope Software.
