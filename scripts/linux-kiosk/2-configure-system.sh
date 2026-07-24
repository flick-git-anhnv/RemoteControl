#!/bin/bash
# 2-configure-system.sh — Các tinh chỉnh hệ thống còn lại cho kiosk iPGS
# (KHÔNG cài thêm phần mềm nào — phần cài đặt nằm ở 1-install-software.sh).
#
# Bao gồm:
#   - Tắt hot corner
#   - Tắt notification banner
#   - Tắt khóa màn hình / screensaver
#   - Tắt Ubuntu Dock + desktop icons (dock trái, icon Trash/Home) — quan trọng
#     với máy mới cài Ubuntu Desktop vì 2 extension này bật mặc định
#   - Chặn suspend/sleep khi cắm điện (tránh màn hình tắt giữa chừng)
#   - Tắt popup Software Updater (tránh gián đoạn kiosk khi có bản vá mới)
#   - Bỏ qua màn hình gnome-initial-setup (hữu ích khi user kiosk vừa tạo mới)
#   - Autologin GDM cho user kiosk
#   - Autostart app iPGS fullscreen + unclutter khi vào desktop
#
# BẮT BUỘC chạy SAU khi đã chạy xong 1-install-software.sh (cần unclutter đã
# cài để autostart unclutter.desktop hoạt động).
#
# Chạy (mặc định bật tất cả):
#   bash scripts/linux-kiosk/2-configure-system.sh [kiosk_user] [app_exec]
#
# Chạy có chọn lọc từng mục (tham số 3-9: 1=bật, 0=bỏ qua, mặc định 1 nếu không truyền):
#   bash scripts/linux-kiosk/2-configure-system.sh <kiosk_user> <app_exec> \
#        <disable_hotcorner> <disable_dock_icons> <block_sleep> \
#        <skip_initial_setup> <enable_autologin> <disable_sw_update> <enable_autostart>
#
# Tham số (đều có default):
#   kiosk_user          Mặc định: kztek — user dùng để autologin GDM.
#   app_exec            Mặc định: ipgskioskavalonia (lệnh có sẵn trong PATH sau khi
#                       cài .deb từ scripts/linux-deb/build-deb.sh)
#   disable_hotcorner   Tắt hot corner/notification banner/khóa màn hình/idle-delay
#   disable_dock_icons  Tắt Ubuntu Dock + Desktop Icons
#   block_sleep         Chặn suspend/sleep khi cắm điện
#   skip_initial_setup  Bỏ qua màn hình gnome-initial-setup
#   enable_autologin    Autologin GDM cho kiosk_user
#   disable_sw_update   Tắt popup + auto-download Software Updater
#   enable_autostart    Autostart app iPGS + unclutter khi vào desktop
#   lock_single_workspace  Khóa còn 1 workspace tĩnh (tham số 10) — chặn triệt để lỗi
#                       cử chỉ 2/3 ngón trên màn cảm ứng bị Mutter hiểu thành gesture
#                       chuyển workspace, làm app fullscreen "biến mất" sang workspace
#                       khác. Set dynamic-workspaces=false + num-workspaces=1 thì
#                       gesture vẫn kích hoạt nhưng không còn workspace nào để chuyển tới.
#
# Ví dụ:
#   bash scripts/linux-kiosk/2-configure-system.sh
#   bash scripts/linux-kiosk/2-configure-system.sh kztek /opt/kztek/ipgskioskavalonia/run.sh

set -e

KIOSK_USER="${1:-kztek}"
APP_EXEC="${2:-ipgskioskavalonia}"
DISABLE_HOTCORNER="${3:-1}"
DISABLE_DOCK_ICONS="${4:-1}"
BLOCK_SLEEP="${5:-1}"
SKIP_INITIAL_SETUP="${6:-1}"
ENABLE_AUTOLOGIN="${7:-1}"
DISABLE_SW_UPDATE="${8:-1}"
ENABLE_AUTOSTART="${9:-1}"
LOCK_SINGLE_WORKSPACE="${10:-1}"

# Helper sudo cho SSH session không có TTY.
# C# truyền KIOSK_SUDO_PASS qua môi trường; nếu không có thì dùng sudo bình thường
# (sẽ hỏi password nếu có TTY, hoặc fail nếu không có TTY).
_sudo() {
    if [ -n "${KIOSK_SUDO_PASS:-}" ]; then
        echo "$KIOSK_SUDO_PASS" | sudo -S "$@" 2>/dev/null
    else
        sudo "$@"
    fi
}

echo "=== [2] Cấu hình hệ thống cho Kiosk iPGS — Ubuntu 22.04 ==="
echo "  Kiosk user : $KIOSK_USER"
echo "  App exec   : $APP_EXEC"
echo "  HotCorner=$DISABLE_HOTCORNER DockIcons=$DISABLE_DOCK_ICONS Sleep=$BLOCK_SLEEP InitialSetup=$SKIP_INITIAL_SETUP Autologin=$ENABLE_AUTOLOGIN SwUpdate=$DISABLE_SW_UPDATE Autostart=$ENABLE_AUTOSTART LockWorkspace=$LOCK_SINGLE_WORKSPACE"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "LỖI: đừng chạy script bằng 'sudo bash ...' hay user root." >&2
    echo "     Chạy trực tiếp: bash scripts/linux-kiosk/2-configure-system.sh" >&2
    exit 1
fi

# ─────────────────────────────────────────────────────────────
# 2 chiều: 1 = tắt/ẩn, 0 = bật/hiện lại như mặc định GNOME
if [ "$DISABLE_HOTCORNER" = "1" ]; then
    echo "=== [1/8] Tắt hot corner, notification banner, screensaver/lock ==="
    gsettings set org.gnome.desktop.interface enable-hot-corners false
    gsettings set org.gnome.desktop.notifications show-banners false
    gsettings set org.gnome.desktop.screensaver lock-enabled false
    gsettings set org.gnome.desktop.session idle-delay 0
else
    echo "=== [1/8] Bật lại hot corner, notification banner, screensaver/lock (mặc định) ==="
    gsettings set org.gnome.desktop.interface enable-hot-corners true
    gsettings set org.gnome.desktop.notifications show-banners true
    gsettings set org.gnome.desktop.screensaver lock-enabled true
    gsettings set org.gnome.desktop.session idle-delay 300
fi

# ─────────────────────────────────────────────────────────────
if [ "$LOCK_SINGLE_WORKSPACE" = "1" ]; then
    echo "=== [2/8] Khóa còn 1 workspace tĩnh (chặn gesture 2/3 ngón chuyển workspace) ==="
    gsettings set org.gnome.mutter dynamic-workspaces false
    gsettings set org.gnome.desktop.wm.preferences num-workspaces 1 2>/dev/null || true
    gsettings set org.gnome.shell.overrides workspaces-only-on-primary true 2>/dev/null || true
else
    echo "=== [2/8] Bật lại workspace động (mặc định GNOME) ==="
    gsettings set org.gnome.mutter dynamic-workspaces true
    gsettings reset org.gnome.desktop.wm.preferences num-workspaces 2>/dev/null || true
    gsettings reset org.gnome.shell.overrides workspaces-only-on-primary 2>/dev/null || true
fi

# ─────────────────────────────────────────────────────────────
if [ "$DISABLE_DOCK_ICONS" = "1" ]; then
    echo "=== [3/8] Tắt Ubuntu Dock + Desktop Icons (mặc định bật trên máy mới) ==="
    gnome-extensions disable ubuntu-dock@ubuntu.com 2>/dev/null || echo "  → ubuntu-dock@ubuntu.com không có/đã tắt, bỏ qua."
    gnome-extensions disable ding@rastersoft.com 2>/dev/null || echo "  → ding@rastersoft.com không có/đã tắt, bỏ qua."
else
    echo "=== [3/8] Bật lại Ubuntu Dock + Desktop Icons ==="
    gnome-extensions enable ubuntu-dock@ubuntu.com 2>/dev/null || echo "  → ubuntu-dock@ubuntu.com không có, bỏ qua."
    gnome-extensions enable ding@rastersoft.com 2>/dev/null || echo "  → ding@rastersoft.com không có, bỏ qua."
fi

# ─────────────────────────────────────────────────────────────
if [ "$BLOCK_SLEEP" = "1" ]; then
    echo "=== [4/8] Chặn suspend/sleep khi cắm điện (tránh màn hình tắt giữa chừng) ==="
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'nothing'
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'nothing' 2>/dev/null || true
else
    echo "=== [4/8] Bật lại suspend/sleep mặc định ==="
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'suspend'
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'suspend' 2>/dev/null || true
fi

# ─────────────────────────────────────────────────────────────
if [ "$SKIP_INITIAL_SETUP" = "1" ]; then
    echo "=== [5/8] Bỏ qua màn hình gnome-initial-setup (nếu user vừa tạo mới) ==="
    mkdir -p "$HOME/.config"
    touch "$HOME/.config/gnome-initial-setup-done"
    echo "  → Đã đánh dấu gnome-initial-setup-done cho '$KIOSK_USER'."
else
    echo "=== [5/8] Bỏ đánh dấu gnome-initial-setup-done (màn hình initial-setup sẽ hiện lại) ==="
    rm -f "$HOME/.config/gnome-initial-setup-done"
fi

# ─────────────────────────────────────────────────────────────
if [ "$ENABLE_AUTOLOGIN" = "1" ]; then
    echo "=== [6/8] Autologin GDM cho user '$KIOSK_USER' ==="
    GDM_CONF="/etc/gdm3/custom.conf"
    if [ -f "$GDM_CONF" ]; then
        _sudo cp "$GDM_CONF" "$GDM_CONF.bak-$(date +%Y%m%d%H%M%S)" 2>/dev/null || true
        if _sudo grep -q "^AutomaticLoginEnable" "$GDM_CONF"; then
            _sudo sed -i "s/^AutomaticLoginEnable.*/AutomaticLoginEnable = true/" "$GDM_CONF"
        else
            _sudo sed -i "/^\[daemon\]/a AutomaticLoginEnable = true" "$GDM_CONF"
        fi
        if _sudo grep -q "^AutomaticLogin " "$GDM_CONF"; then
            _sudo sed -i "s/^AutomaticLogin .*/AutomaticLogin = $KIOSK_USER/" "$GDM_CONF"
        else
            _sudo sed -i "/^AutomaticLoginEnable/a AutomaticLogin = $KIOSK_USER" "$GDM_CONF"
        fi
        echo "  → Đã cập nhật $GDM_CONF (backup: $GDM_CONF.bak-*)"
    else
        echo "CẢNH BÁO: không tìm thấy $GDM_CONF — bỏ qua bước autologin, cấu hình thủ công sau." >&2
    fi
else
    echo "=== [6/8] Tắt autologin GDM ==="
    GDM_CONF="/etc/gdm3/custom.conf"
    if [ -f "$GDM_CONF" ]; then
        _sudo cp "$GDM_CONF" "$GDM_CONF.bak-$(date +%Y%m%d%H%M%S)" 2>/dev/null || true
        _sudo sed -i "s/^AutomaticLoginEnable.*/AutomaticLoginEnable = false/" "$GDM_CONF" 2>/dev/null || true
        echo "  → Đã tắt autologin trong $GDM_CONF (backup: $GDM_CONF.bak-*)"
    else
        echo "CẢNH BÁO: không tìm thấy $GDM_CONF — bỏ qua." >&2
    fi
fi

# ─────────────────────────────────────────────────────────────
if [ "$DISABLE_SW_UPDATE" = "1" ]; then
    echo "=== [7/8] Tắt popup Software Updater ==="
    mkdir -p "$HOME/.config/autostart"
    if [ -f /etc/xdg/autostart/update-notifier.desktop ]; then
        cat > "$HOME/.config/autostart/update-notifier.desktop" <<EOF
[Desktop Entry]
Hidden=true
EOF
        echo "  → Đã ẩn autostart update-notifier cho user hiện tại."
    else
        echo "  → Không thấy update-notifier.desktop, bỏ qua."
    fi
    gsettings set org.gnome.software download-updates false 2>/dev/null || true
else
    echo "=== [7/8] Bỏ qua (không chọn) ==="
fi

# ─────────────────────────────────────────────────────────────
if [ "$ENABLE_AUTOSTART" = "1" ]; then
    echo "=== [8/8] Autostart app iPGS fullscreen + unclutter khi vào desktop ==="
    mkdir -p "$HOME/.config/autostart"

    cat > "$HOME/.config/autostart/ipgs-kiosk.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=iPGS Kiosk
Exec=$APP_EXEC
X-GNOME-Autostart-enabled=true
EOF
    echo "  → Đã tạo $HOME/.config/autostart/ipgs-kiosk.desktop (Exec=$APP_EXEC)"
    echo "    Nếu \$APP_EXEC chưa đúng, sửa lại field Exec= trong file trên."

    if command -v unclutter >/dev/null 2>&1; then
        cat > "$HOME/.config/autostart/unclutter.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Unclutter
Exec=unclutter -idle 1
X-GNOME-Autostart-enabled=true
EOF
        echo "  → Đã tạo $HOME/.config/autostart/unclutter.desktop"
    else
        echo "CẢNH BÁO: chưa thấy lệnh 'unclutter' — chạy 1-install-software.sh trước khi chạy script này." >&2
    fi
else
    echo "=== [8/8] Bỏ qua (không chọn) ==="
fi

# ─────────────────────────────────────────────────────────────
echo ""
echo "✓ HOÀN THÀNH. Cần LOG OUT / RESTART để áp dụng đầy đủ (đặc biệt autologin GDM + autostart)."
echo "  Kiểm tra sau khi restart:"
echo "    - Máy tự vào thẳng user '$KIOSK_USER' không cần đăng nhập"
echo "    - App '$APP_EXEC' tự chạy fullscreen"
