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
# Dò display manager THẬT đang dùng trên máy — KHÔNG hardcode gdm3.
# Trả về: gdm3 / gdm / lightdm / sddm / <khác> / "" (không xác định được).
_detect_dm() {
    local dm=""
    if [ -f /etc/X11/default-display-manager ]; then
        dm="$(basename "$(cat /etc/X11/default-display-manager)" 2>/dev/null)"
    fi
    if [ -z "$dm" ]; then
        dm="$(basename "$(readlink -f /etc/systemd/system/display-manager.service 2>/dev/null)" .service 2>/dev/null)"
    fi
    echo "$dm"
}

if [ "$ENABLE_AUTOLOGIN" = "1" ]; then
    DM_NAME="$(_detect_dm)"
    echo "=== [6/8] Autologin cho user '$KIOSK_USER' (display manager: ${DM_NAME:-không rõ}) ==="
    AUTOLOGIN_OK=0
    case "$DM_NAME" in
        gdm3|gdm)
            # Ubuntu dùng /etc/gdm3, distro khác (Fedora/Arch...) dùng /etc/gdm
            GDM_CONF="/etc/gdm3/custom.conf"
            [ -f "$GDM_CONF" ] || GDM_CONF="/etc/gdm/custom.conf"
            if [ -f "$GDM_CONF" ]; then
                _sudo cp "$GDM_CONF" "$GDM_CONF.bak-$(date +%Y%m%d%H%M%S)" 2>/dev/null || true
                # Xóa các dòng cũ rồi ghi lại 1 lần ngay dưới [daemon] — idempotent.
                # TimedLogin* = fallback: GDM có race hiếm (nhất là boot chậm/bất thường
                # trên VM) khiến AutomaticLogin bị bỏ qua và greeter hiện ra — khi đó
                # TimedLogin sẽ tự đăng nhập user sau 5 giây tại greeter, máy vẫn tự vào
                # desktop mà không cần gõ mật khẩu (F08).
                _sudo sed -i '/^AutomaticLoginEnable/d;/^AutomaticLogin /d;/^TimedLoginEnable/d;/^TimedLogin /d;/^TimedLoginDelay/d' "$GDM_CONF" || true
                _sudo sed -i "/^\[daemon\]/a AutomaticLoginEnable = true\nAutomaticLogin = $KIOSK_USER\nTimedLoginEnable = true\nTimedLogin = $KIOSK_USER\nTimedLoginDelay = 5" "$GDM_CONF" || true
                # KIỂM CHỨNG: đọc lại file thật, không tin kết quả sed (sudo sai mật
                # khẩu → sed âm thầm không chạy do _sudo nuốt stderr).
                if grep -q "^AutomaticLoginEnable = true" "$GDM_CONF" && \
                   grep -q "^AutomaticLogin = $KIOSK_USER" "$GDM_CONF"; then
                    echo "  → AUTOLOGIN-VERIFIED: $GDM_CONF (AutomaticLogin=$KIOSK_USER + TimedLogin fallback 5s, backup: $GDM_CONF.bak-*)"
                    AUTOLOGIN_OK=1
                fi
            else
                echo "LỖI: display manager là $DM_NAME nhưng không thấy custom.conf ở /etc/gdm3 hay /etc/gdm." >&2
            fi
            ;;
        lightdm)
            LDM_DIR="/etc/lightdm/lightdm.conf.d"
            LDM_CONF="$LDM_DIR/60-kiosk-autologin.conf"
            _sudo mkdir -p "$LDM_DIR" || true
            # KHÔNG dùng `... | _sudo tee` — _sudo đã chiếm stdin để truyền mật khẩu sudo -S.
            _sudo bash -c "printf '[Seat:*]\nautologin-user=%s\nautologin-user-timeout=0\n' '$KIOSK_USER' > '$LDM_CONF'" || true
            if grep -q "^autologin-user=$KIOSK_USER" "$LDM_CONF" 2>/dev/null; then
                echo "  → AUTOLOGIN-VERIFIED: $LDM_CONF (autologin-user=$KIOSK_USER)"
                echo "  Lưu ý: user cần thuộc group autologin/nopasswdlogin trên một số distro." >&2
                AUTOLOGIN_OK=1
            fi
            ;;
        sddm)
            SDDM_DIR="/etc/sddm.conf.d"
            SDDM_CONF="$SDDM_DIR/60-kiosk-autologin.conf"
            _sudo mkdir -p "$SDDM_DIR" || true
            _sudo bash -c "printf '[Autologin]\nUser=%s\nSession=plasma\n' '$KIOSK_USER' > '$SDDM_CONF'" || true
            if grep -q "^User=$KIOSK_USER" "$SDDM_CONF" 2>/dev/null; then
                echo "  → AUTOLOGIN-VERIFIED: $SDDM_CONF (User=$KIOSK_USER)"
                AUTOLOGIN_OK=1
            fi
            ;;
        *)
            echo "LỖI: không xác định được display manager (đọc /etc/X11/default-display-manager + display-manager.service đều thất bại)." >&2
            ;;
    esac
    if [ "$AUTOLOGIN_OK" != "1" ]; then
        # BÁO LỖI THẬT thay vì cảnh báo suông rồi kết thúc "HOÀN THÀNH" (F08:
        # trước đây script chỉ warning và vẫn exit 0 → app báo thành công giả).
        echo "AUTOLOGIN-FAILED: cấu hình autologin CHƯA được áp dụng (kiểm tra sudo password / display manager). Máy sẽ vẫn hỏi đăng nhập sau khi restart." >&2
        exit 1
    fi
else
    DM_NAME="$(_detect_dm)"
    echo "=== [6/8] Tắt autologin (display manager: ${DM_NAME:-không rõ}) ==="
    case "$DM_NAME" in
        gdm3|gdm)
            GDM_CONF="/etc/gdm3/custom.conf"
            [ -f "$GDM_CONF" ] || GDM_CONF="/etc/gdm/custom.conf"
            if [ -f "$GDM_CONF" ]; then
                _sudo cp "$GDM_CONF" "$GDM_CONF.bak-$(date +%Y%m%d%H%M%S)" 2>/dev/null || true
                _sudo sed -i "s/^AutomaticLoginEnable.*/AutomaticLoginEnable = false/;s/^TimedLoginEnable.*/TimedLoginEnable = false/" "$GDM_CONF" 2>/dev/null || true
                echo "  → Đã tắt autologin trong $GDM_CONF (backup: $GDM_CONF.bak-*)"
            else
                echo "CẢNH BÁO: không tìm thấy custom.conf — bỏ qua." >&2
            fi
            ;;
        lightdm) _sudo rm -f /etc/lightdm/lightdm.conf.d/60-kiosk-autologin.conf || true; echo "  → Đã gỡ 60-kiosk-autologin.conf (lightdm)." ;;
        sddm)    _sudo rm -f /etc/sddm.conf.d/60-kiosk-autologin.conf || true; echo "  → Đã gỡ 60-kiosk-autologin.conf (sddm)." ;;
        *)       echo "CẢNH BÁO: không xác định được display manager — bỏ qua tắt autologin." >&2 ;;
    esac
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
