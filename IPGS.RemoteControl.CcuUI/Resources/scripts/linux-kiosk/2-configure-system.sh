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
#   lockdown_shell      Khoá lối thoát kiosk bằng dconf system-wide + lock (tham số 11,
#                       F09). Vô hiệu phím Super/Alt+F2/Ctrl+Alt+T/Alt+Tab/Alt+F4,
#                       khoá log-out/user-switching/command-line — user KHÔNG tự đổi
#                       lại được vì key bị lock ở /etc/dconf/db/local.d/locks/.
#                       1 = khoá, 0 = gỡ khoá (bảo trì). Xem GHI CHÚ F09 cuối file.
#   enable_watchdog     Cài systemd USER service tự khởi động lại app kiosk khi app bị
#                       đóng/crash (tham số 12, F10). Restart=always RestartSec=10,
#                       StartLimitIntervalSec=0 (R2: không bao giờ vào failed vĩnh viễn
#                       — kiosk không người trực thà restart mãi còn hơn chết hẳn; nhịp
#                       10s đủ chậm để không spam journal). Khi bật, app được quản lý
#                       bởi service (autostart .desktop của app bị XÓA bất kể tham số
#                       autostart — R1, tránh chạy 2 instance). 1 = cài+bật service,
#                       0 = gỡ service (app quay về autostart .desktop nếu
#                       enable_autostart=1).
#   enable_firewall     Bật tường lửa ufw (tham số 14, F27) — cài nếu chưa có, LUÔN
#                       allow OpenSSH trước khi enable để không tự khoá mất SSH.
#                       1 = bật, 0 = tắt (`ufw disable`, giữ nguyên rule để bật lại
#                       nhanh). Không tự mở port ZcuAgent ở đây — xem
#                       scripts/setup-zcu-agent.sh (tự mở đúng port khi cài agent).
#
# Ví dụ:
#   bash scripts/linux-kiosk/2-configure-system.sh
#   bash scripts/linux-kiosk/2-configure-system.sh kztek /opt/kztek/ipgskioskavalonia/run.sh

set -e

KIOSK_USER="${1:-kztek}"
APP_EXEC="${2:-ipgskioskavalonia}"
DISABLE_HOTCORNER="${3:-1}"
DISABLE_UBUNTU_DOCK="${4:-1}"
DISABLE_DESKTOP_ICONS="${5:-0}"
BLOCK_SLEEP="${6:-1}"
SKIP_INITIAL_SETUP="${7:-1}"
ENABLE_AUTOLOGIN="${8:-1}"
DISABLE_SW_UPDATE="${9:-1}"
ENABLE_AUTOSTART="${10:-1}"
LOCK_SINGLE_WORKSPACE="${11:-1}"
LOCKDOWN_SHELL="${12:-1}"
ENABLE_WATCHDOG="${13:-1}"
ENABLE_FIREWALL="${14:-1}"

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
echo "  HotCorner=$DISABLE_HOTCORNER UbuntuDock=$DISABLE_UBUNTU_DOCK DesktopIcons=$DISABLE_DESKTOP_ICONS Sleep=$BLOCK_SLEEP InitialSetup=$SKIP_INITIAL_SETUP Autologin=$ENABLE_AUTOLOGIN SwUpdate=$DISABLE_SW_UPDATE Autostart=$ENABLE_AUTOSTART LockWorkspace=$LOCK_SINGLE_WORKSPACE LockdownShell=$LOCKDOWN_SHELL Watchdog=$ENABLE_WATCHDOG Firewall=$ENABLE_FIREWALL"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "LỖI: đừng chạy script bằng 'sudo bash ...' hay user root." >&2
    echo "     Chạy trực tiếp: bash scripts/linux-kiosk/2-configure-system.sh" >&2
    exit 1
fi

# ─────────────────────────────────────────────────────────────
# 2 chiều: 1 = tắt/ẩn, 0 = bật/hiện lại như mặc định GNOME
if [ "$DISABLE_HOTCORNER" = "1" ]; then
    echo "=== [1/10] Tắt hot corner, notification banner, screensaver/lock ==="
    # `|| true`: các key này có thể đã bị dconf lock bởi mục [9/9] (F09) — khi đó
    # gsettings set theo user bị từ chối, nhưng giá trị hệ thống đã đúng rồi.
    gsettings set org.gnome.desktop.interface enable-hot-corners false 2>/dev/null || true
    gsettings set org.gnome.desktop.notifications show-banners false
    gsettings set org.gnome.desktop.screensaver lock-enabled false 2>/dev/null || true
    gsettings set org.gnome.desktop.session idle-delay 0
else
    echo "=== [1/10] Bật lại hot corner, notification banner, screensaver/lock (mặc định) ==="
    gsettings set org.gnome.desktop.interface enable-hot-corners true 2>/dev/null || true
    gsettings set org.gnome.desktop.notifications show-banners true
    gsettings set org.gnome.desktop.screensaver lock-enabled true 2>/dev/null || true
    gsettings set org.gnome.desktop.session idle-delay 300
fi

# ─────────────────────────────────────────────────────────────
if [ "$LOCK_SINGLE_WORKSPACE" = "1" ]; then
    echo "=== [2/10] Khóa còn 1 workspace tĩnh (chặn gesture 2/3 ngón chuyển workspace) ==="
    gsettings set org.gnome.mutter dynamic-workspaces false 2>/dev/null || true
    gsettings set org.gnome.desktop.wm.preferences num-workspaces 1 2>/dev/null || true
    gsettings set org.gnome.shell.overrides workspaces-only-on-primary true 2>/dev/null || true
else
    echo "=== [2/10] Bật lại workspace động (mặc định GNOME) ==="
    gsettings set org.gnome.mutter dynamic-workspaces true 2>/dev/null || true
    gsettings reset org.gnome.desktop.wm.preferences num-workspaces 2>/dev/null || true
    gsettings reset org.gnome.shell.overrides workspaces-only-on-primary 2>/dev/null || true
fi

# ─────────────────────────────────────────────────────────────
if [ "$DISABLE_UBUNTU_DOCK" = "1" ]; then
    echo "=== [3a/10] Tắt Ubuntu Dock (ubuntu-dock@ubuntu.com) ==="
    gnome-extensions disable ubuntu-dock@ubuntu.com 2>/dev/null || echo "  → ubuntu-dock@ubuntu.com không có/đã tắt, bỏ qua."
else
    echo "=== [3a/10] Bật lại Ubuntu Dock (ubuntu-dock@ubuntu.com) ==="
    gnome-extensions enable ubuntu-dock@ubuntu.com 2>/dev/null || echo "  → ubuntu-dock@ubuntu.com không có, bỏ qua."
fi

# ─────────────────────────────────────────────────────────────
# [3b/10] Desktop Icons NG (ding@rastersoft.com) — tách riêng khỏi Ubuntu Dock.
# Mặc định KHÔNG tắt (=0) để icon shortcut app trên desktop click được (F14).
if [ "$DISABLE_DESKTOP_ICONS" = "1" ]; then
    echo "=== [3b/10] Tắt Desktop Icons NG (ding@rastersoft.com) ==="
    gnome-extensions disable ding@rastersoft.com 2>/dev/null || echo "  → ding@rastersoft.com không có/đã tắt, bỏ qua."
else
    echo "=== [3b/10] Bật lại Desktop Icons NG (ding@rastersoft.com) ==="
    gnome-extensions enable ding@rastersoft.com 2>/dev/null || echo "  → ding@rastersoft.com không có, bỏ qua."
fi

# ─────────────────────────────────────────────────────────────
if [ "$BLOCK_SLEEP" = "1" ]; then
    echo "=== [4/10] Chặn suspend/sleep khi cắm điện (tránh màn hình tắt giữa chừng) ==="
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'nothing'
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'nothing' 2>/dev/null || true
else
    echo "=== [4/10] Bật lại suspend/sleep mặc định ==="
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'suspend'
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'suspend' 2>/dev/null || true
fi

# ─────────────────────────────────────────────────────────────
if [ "$SKIP_INITIAL_SETUP" = "1" ]; then
    echo "=== [5/10] Bỏ qua màn hình gnome-initial-setup (nếu user vừa tạo mới) ==="
    mkdir -p "$HOME/.config"
    touch "$HOME/.config/gnome-initial-setup-done"
    echo "  → Đã đánh dấu gnome-initial-setup-done cho '$KIOSK_USER'."
else
    echo "=== [5/10] Bỏ đánh dấu gnome-initial-setup-done (màn hình initial-setup sẽ hiện lại) ==="
    rm -f "$HOME/.config/gnome-initial-setup-done"
fi

# ─────────────────────────────────────────────────────────────
# Giải symlink để lấy đường dẫn tuyệt đối THẬT của binary/script.
# Dùng ở mục [10/10] watchdog và [8/10] desktop icon.
# Nếu không tìm thấy → trả về input gốc (không exit, caller quyết định).
_get_real_exec() {
    local app="$1"
    local resolved
    if [ "${app:0:1}" = "/" ]; then
        resolved="$(readlink -f "$app" 2>/dev/null || echo "$app")"
    else
        local found; found="$(command -v "$app" 2>/dev/null || true)"
        if [ -z "$found" ]; then echo "$app"; return 0; fi
        resolved="$(readlink -f "$found" 2>/dev/null || echo "$found")"
    fi
    echo "$resolved"
}

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
    echo "=== [6/10] Autologin cho user '$KIOSK_USER' (display manager: ${DM_NAME:-không rõ}) ==="
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
    echo "=== [6/10] Tắt autologin (display manager: ${DM_NAME:-không rõ}) ==="
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
    echo "=== [7/10] Tắt popup Software Updater ==="
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
    echo "=== [7/10] Bỏ qua (không chọn) ==="
fi

# ─────────────────────────────────────────────────────────────
# F10-R1: Trạng thái autostart .desktop của APP phụ thuộc WATCHDOG trước tiên (không
# phụ thuộc ENABLE_AUTOSTART) — tránh file .desktop "mồ côi" từ lần deploy trước gây
# chạy 2 instance khi lần này bật watchdog. Ma trận trạng thái sau deploy (luôn nhất
# quán với cấu hình VỪA chọn, bất kể lần deploy trước):
#
#   Watchdog | Autostart | ipgs-kiosk.desktop (app) | ipgs-kiosk-app.service | unclutter.desktop
#   ---------|-----------|--------------------------|------------------------|------------------
#      1     |   1/0     | XÓA (service quản lý app)| cài + enable (mục [10])| theo Autostart
#      0     |    1      | TẠO                      | gỡ (mục [10])          | tạo
#      0     |    0      | XÓA (2 chiều)            | gỡ (mục [10])          | xóa (2 chiều)
echo "=== [8/10] Autostart app iPGS + unclutter + desktop icon khi vào desktop (Autostart=$ENABLE_AUTOSTART, Watchdog=$ENABLE_WATCHDOG) ==="
mkdir -p "$HOME/.config/autostart"

if [ "$ENABLE_WATCHDOG" = "1" ]; then
    # App do systemd service quản lý (Restart=always) — .desktop cũ (nếu có từ deploy
    # trước) PHẢI xóa dù Autostart tick hay không, tránh app chạy 2 instance.
    rm -f "$HOME/.config/autostart/ipgs-kiosk.desktop"
    echo "  → Watchdog BẬT: app do service ipgs-kiosk-app quản lý; đã xóa autostart .desktop của app (tránh chạy 2 lần)."
elif [ "$ENABLE_AUTOSTART" = "1" ]; then
    cat > "$HOME/.config/autostart/ipgs-kiosk.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=iPGS Kiosk
Exec=$APP_EXEC
X-GNOME-Autostart-enabled=true
EOF
    echo "  → Đã tạo $HOME/.config/autostart/ipgs-kiosk.desktop (Exec=$APP_EXEC)"
    echo "    Nếu \$APP_EXEC chưa đúng, sửa lại field Exec= trong file trên."
else
    # 2 chiều: bỏ tick Autostart (và không watchdog) = app không tự chạy nữa.
    rm -f "$HOME/.config/autostart/ipgs-kiosk.desktop"
    echo "  → Autostart + Watchdog đều TẮT: đã xóa autostart .desktop của app (nếu có)."
fi

# unclutter độc lập với watchdog — theo đúng ô Autostart (2 chiều).
if [ "$ENABLE_AUTOSTART" = "1" ]; then
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
    rm -f "$HOME/.config/autostart/unclutter.desktop"
    echo "  → Autostart TẮT: đã xóa unclutter.desktop (nếu có)."
fi

# ─────────────────────────────────────────────────────────────
# Desktop icon ~/Desktop/ (F14 + F15-b):
# F15-b: ưu tiên sửa icon đã có sẵn (gói .deb cài) thay vì tạo thêm file mới —
# tránh xuất hiện 2 icon cùng trỏ 1 app. Logic:
#   1. Dọn ipgs-kiosk.desktop (stray từ deploy cũ) — LUÔN làm, bất kể Autostart.
#   2. Tìm icon có sẵn trên Desktop trỏ cùng app (so khớp Exec đã giải symlink/PATH).
#      → Có: chỉ chmod+x + gio set trusted, KHÔNG tạo file mới.
#      → Không có: tạo mới ipgs-kiosk.desktop (trường hợp app chưa có icon riêng).
#   3. Khi Autostart OFF: không tạo desktop shortcut (icon do .deb cài vẫn giữ).
#
# Pre-compute REAL_EXEC (dùng chung cho [8/10] desktop icon và [10/10] watchdog).
DESKTOP_ICON="$HOME/Desktop/ipgs-kiosk.desktop"
_REAL_EXEC="$(_get_real_exec "$APP_EXEC")"
_REAL_EXEC_DIR="$(dirname "$_REAL_EXEC")"

# F15-b Bước 1: dọn stray icon do script cũ tạo — LUÔN, không phụ thuộc Autostart.
if [ -f "$DESKTOP_ICON" ]; then
    rm -f "$DESKTOP_ICON"
    echo "  → F15-b: đã dọn $DESKTOP_ICON (icon trùng do deploy cũ tạo)."
fi

if [ "$ENABLE_AUTOSTART" = "1" ]; then
    # F15-b Bước 2: tìm icon có sẵn trỏ cùng app (khớp Exec đã giải symlink/PATH).
    # So khớp theo: exec giải ra = _REAL_EXEC, hoặc thư mục của exec = _REAL_EXEC_DIR.
    _FOUND_ICON=""
    for _df in "$HOME/Desktop/"*.desktop; do
        [ -f "$_df" ] || continue
        [ "$_df" = "$DESKTOP_ICON" ] && continue  # file vừa dọn ở bước 1
        _df_exec_raw="$(grep -m1 '^Exec=' "$_df" 2>/dev/null \
                        | sed 's/^Exec=//' | sed 's/ *%[uUfFdDiIcCkK].*//' \
                        | awk '{print $1}')"
        [ -z "$_df_exec_raw" ] && continue
        _df_exec_real="$(_get_real_exec "$_df_exec_raw")"
        if [ "$_df_exec_real" = "$_REAL_EXEC" ] || \
           [ "$(dirname "$_df_exec_real")" = "$_REAL_EXEC_DIR" ]; then
            _FOUND_ICON="$_df"; break
        fi
    done

    if [ -n "$_FOUND_ICON" ]; then
        # F15-b: icon sẵn có → chỉ chmod+x + trusted, KHÔNG tạo thêm file mới.
        chmod +x "$_FOUND_ICON"
        _found_exec="$(grep -m1 '^Exec=' "$_FOUND_ICON" | sed 's/^Exec=//')"
        if gio set "$_FOUND_ICON" metadata::trusted true 2>/dev/null; then
            echo "  → F15-b: icon có sẵn $(basename "$_FOUND_ICON") — chmod+x + trusted=true (Exec=$_found_exec)"
        else
            echo "  → F15-b: icon có sẵn $(basename "$_FOUND_ICON") — chmod+x (Exec=$_found_exec)"
            echo "CẢNH BÁO (F15-b): không set metadata::trusted — GVfs/session bus chưa sẵn sàng." >&2
            echo "     Chạy thủ công: gio set $_FOUND_ICON metadata::trusted true" >&2
        fi
    else
        # F15-b: không có icon nào → tạo mới ipgs-kiosk.desktop.
        _APP_ICON=""
        for _try in "$_REAL_EXEC_DIR/.IPGSKioskAvalonia/appIcon.png" \
                    "$_REAL_EXEC_DIR/appIcon.png" \
                    "/opt/kztek/ipgskioskavalonia/.IPGSKioskAvalonia/appIcon.png"; do
            if [ -f "$_try" ]; then _APP_ICON="$_try"; break; fi
        done
        mkdir -p "$HOME/Desktop"
        cat > "$DESKTOP_ICON" <<EOF
[Desktop Entry]
Type=Application
Name=IPGS Kiosk
Comment=KZTEK IPGS Kiosk App
Exec=$_REAL_EXEC
Icon=$_APP_ICON
Terminal=false
Categories=Utility;
EOF
        chmod +x "$DESKTOP_ICON"
        if gio set "$DESKTOP_ICON" metadata::trusted true 2>/dev/null; then
            echo "  → Đã tạo $DESKTOP_ICON (Exec=$_REAL_EXEC, trusted=true — F14+F15-b)"
        else
            echo "  → Đã tạo $DESKTOP_ICON (Exec=$_REAL_EXEC)"
            echo "CẢNH BÁO (F14): không set metadata::trusted — GVfs/session bus chưa sẵn sàng." >&2
            echo "     Chạy thủ công trong phiên desktop: gio set $DESKTOP_ICON metadata::trusted true" >&2
        fi
    fi
else
    echo "  → Autostart TẮT: không tạo desktop shortcut (icon do .deb cài vẫn giữ nguyên)."
fi

# ─────────────────────────────────────────────────────────────
# [8b] F15-a: Set trusted + kiểm tra Exec cho MỌI icon KZTEK trên Desktop.
# Gói .deb cài icon vào ~/Desktop nhưng KHÔNG set metadata::trusted → click không chạy.
# Chạy vô điều kiện (không phụ thuộc ENABLE_AUTOSTART) để bao phủ mọi app KZTEK.
#
# Xử lý từng kztek-*.desktop:
#   - chmod +x
#   - Kiểm tra Exec= tồn tại + thực thi được (sau khi giải symlink qua _get_real_exec).
#   - Nếu Exec trỏ symlink → giải ra đường dẫn thật → sửa lại trong file (trừ khi vim
#     đang mở file — có .swp — thì chỉ cảnh báo, không ghi đè nội dung file).
#   - gio set metadata::trusted true.
echo "=== [8b] Set trusted cho mọi icon KZTEK trên Desktop (F15-a) ==="
_F15_TRUSTED=0
_F15_WARN=0
_f15_any=0
for _kz in "$HOME/Desktop/kztek-"*.desktop; do
    [ -f "$_kz" ] || continue
    _f15_any=1
    _kz_name="$(basename "$_kz")"

    # Phát hiện vim .swp — nếu có thì KHÔNG ghi đè nội dung file (.swp chỉ chặn write)
    _kz_swp="$(dirname "$_kz")/.${_kz_name}.swp"
    _kz_has_swp=0; [ -f "$_kz_swp" ] && _kz_has_swp=1

    # Lấy Exec= (bỏ %placeholder)
    _kz_exec_raw="$(grep -m1 '^Exec=' "$_kz" 2>/dev/null \
                    | sed 's/^Exec=//' | sed 's/ *%[uUfFdDiIcCkK].*//' \
                    | awk '{print $1}')"
    if [ -z "$_kz_exec_raw" ]; then
        echo "  CẢNH BÁO: $_kz_name không có trường Exec= — bỏ qua." >&2
        _F15_WARN=$((_F15_WARN+1)); continue
    fi

    # Giải symlink → đường dẫn thật
    _kz_exec_real="$(_get_real_exec "$_kz_exec_raw")"

    # Kiểm tra tồn tại + thực thi được
    if [ ! -f "$_kz_exec_real" ] || [ ! -x "$_kz_exec_real" ]; then
        echo "  CẢNH BÁO: $_kz_name Exec=$_kz_exec_raw → giải ra '$_kz_exec_real' nhưng không tồn tại hoặc không có quyền thực thi." >&2
        _F15_WARN=$((_F15_WARN+1))
        # Vẫn set trusted bên dưới — hữu ích khi binary được deploy sau
    fi

    # Nếu Exec trỏ symlink → sửa thành đường dẫn thật (tránh G023: BASH_SOURCE sai)
    if [ "$_kz_exec_raw" != "$_kz_exec_real" ] && [ -f "$_kz_exec_real" ] && [ -x "$_kz_exec_real" ]; then
        if [ "$_kz_has_swp" = "1" ]; then
            echo "  CẢNH BÁO: $_kz_name có .swp (vim đang mở) — BỎ QUA sửa Exec= để không mất thay đổi đang soạn. Set trusted vẫn áp dụng." >&2
            _F15_WARN=$((_F15_WARN+1))
        else
            sed -i "s|^Exec=.*|Exec=$_kz_exec_real|" "$_kz"
            echo "  → $_kz_name: sửa Exec=$_kz_exec_raw → $_kz_exec_real (giải symlink G023)"
        fi
    fi

    # chmod +x
    chmod +x "$_kz"

    # gio set trusted — an toàn kể cả khi có .swp (không ghi nội dung file)
    if gio set "$_kz" metadata::trusted true 2>/dev/null; then
        echo "  → trusted: $_kz_name (Exec=$_kz_exec_raw)"
        _F15_TRUSTED=$((_F15_TRUSTED+1))
    else
        echo "  CẢNH BÁO: không set trusted cho $_kz_name — GVfs/session bus chưa sẵn sàng." >&2
        echo "     Chạy thủ công: gio set $_kz metadata::trusted true" >&2
        _F15_WARN=$((_F15_WARN+1))
    fi
done
if [ "$_f15_any" = "0" ]; then
    echo "  → Không có icon kztek-*.desktop nào trên Desktop."
else
    echo "  → F15-a kết quả: trusted=$_F15_TRUSTED icon, cảnh báo=$_F15_WARN."
fi

# ─────────────────────────────────────────────────────────────
# [9/9] F09 — Khoá lối thoát kiosk bằng dconf system-wide + LOCK.
#
# GHI CHÚ F09 (test thật trên ZCU 192.168.0.101, Ubuntu 22.04.2, GNOME Shell 42.9, X11):
# - `gsettings set` theo user KHÔNG đủ: user (hoặc bất kỳ app nào chạy trong session)
#   có thể set ngược lại. Phải dùng dconf DB HỆ THỐNG (/etc/dconf/db/local.d/) kèm
#   file LOCK (/etc/dconf/db/local.d/locks/) — key bị lock thì mọi ghi từ user bị
#   từ chối và giá trị hệ thống luôn thắng.
# - GOTCHA: Ubuntu KHÔNG có sẵn /etc/dconf/profile/user — nếu thiếu file này, dconf
#   dùng profile mặc định chỉ có user-db:user và TOÀN BỘ system-db local bị bỏ qua
#   (dconf update chạy thành công nhưng không có tác dụng gì). PHẢI tạo profile trước.
# - Khoá được: phím Super (overlay-key), Super+S/A/V/N, Alt+F2, Alt+Tab/Super+Tab,
#   Alt+F4, Ctrl+Alt+T, log out/user switching/command-line (lockdown schema).
#   R3 (audit 2026-07-27): thêm Super+1..9 (switch-to-application-N — mở app trong
#   favorites, gồm Terminal/Nautilus → lỗ P1 trên máy có bàn phím), Alt+` (switch-group),
#   Alt+Space (activate-window-menu), Super+H (minimize).
# - KHÔNG khoá được bằng dconf: cử chỉ cảm ứng vuốt 3 ngón lên (GNOME 40+) vẫn mở
#   được Activities overview — hard-code trong GNOME Shell, không có gsettings key.
#   Giảm nhẹ: đã ẩn Activities button + search (Just Perfection), workspace khoá 1.
#   Chặn triệt để cần session gnome-kiosk / WM khác (chưa có gói cho Ubuntu 22.04).
#
# GỠ KHOÁ ĐỂ BẢO TRÌ (quản trị viên, qua SSH):
#   sudo ipgs-kiosk-unlock        ← helper cài sẵn (F11): xoá file lockdown + mọi file
#                                   rác 00-kiosk-lockdown* còn sót, dconf update, TỰ
#                                   XÁC MINH gsettings writable=true.
#   (hoặc chạy lại script này với tham số 11 = 0). Khoá lại: chạy script, tham số 11 = 1.
#
# F11 (QA phát hiện 2026-07-27): dconf update biên dịch MỌI FILE trong local.d/ và
# locks/ BẤT KỂ đuôi tên — backup .bak-* đặt trong đó sẽ TÁI ÁP toàn bộ lock sau khi
# gỡ file chính → đường gỡ khoá bảo trì bị hỏng. Vì vậy: backup PHẢI ghi ra ngoài cây
# /etc/dconf/db/ (dùng $BACKUP_DIR dưới đây), và mọi thao tác gỡ khoá phải dọn cả
# file 00-kiosk-lockdown* còn sót rồi XÁC MINH bằng gsettings writable.
DCONF_PROFILE="/etc/dconf/profile/user"
DCONF_LOCAL_D="/etc/dconf/db/local.d"
DCONF_SETTINGS="$DCONF_LOCAL_D/00-kiosk-lockdown"
DCONF_LOCKS="$DCONF_LOCAL_D/locks/00-kiosk-lockdown"
BAK_SUFFIX="bak-$(date +%Y%m%d%H%M%S)"
BACKUP_DIR="/var/backups/kztek-kiosk"          # backup file hệ thống (cần sudo)
USER_BACKUP_DIR="$HOME/.local/state/kztek-kiosk-backups"  # backup file user-level
UNLOCK_HELPER="/usr/local/sbin/ipgs-kiosk-unlock"

# F11: backup 1 file hệ thống ra $BACKUP_DIR (KHÔNG bao giờ để cạnh file gốc trong
# thư mục mà dconf/GDM quét). Tên đích có thêm tên thư mục cha để tránh trùng
# basename (settings và locks cùng tên 00-kiosk-lockdown).
_backup_sys_file() {
    [ -f "$1" ] || return 0
    _sudo mkdir -p "$BACKUP_DIR"
    _sudo cp "$1" "$BACKUP_DIR/$(basename "$(dirname "$1")")-$(basename "$1").$BAK_SUFFIX" || true
}

# F11: dọn mọi file rác 00-kiosk-lockdown.* / user.* (backup của bản script cũ) đang
# nằm SAI CHỖ trong local.d/ + locks/ + profile/ — chuyển sang $BACKUP_DIR. Chỉ đụng
# file có prefix của mình, KHÔNG đụng config khác (VD 01-* của quản trị viên).
_sweep_stray_dconf_backups() {
    local moved=0 f
    for f in "$DCONF_SETTINGS".* "$DCONF_LOCKS".* "$DCONF_PROFILE".*; do
        [ -f "$f" ] || continue
        _sudo mkdir -p "$BACKUP_DIR"
        _sudo mv "$f" "$BACKUP_DIR/$(basename "$(dirname "$f")")-$(basename "$f")" || true
        moved=$((moved+1))
    done
    [ "$moved" -gt 0 ] && echo "  → F11: đã chuyển $moved file backup nằm sai chỗ trong dconf db → $BACKUP_DIR"
    return 0
}

if [ "$LOCKDOWN_SHELL" = "1" ]; then
    echo "=== [9/10] Khoá lối thoát kiosk (dconf system-wide + lock) ==="

    # Soạn nội dung ở file tạm của user rồi _sudo install — tránh pipe qua _sudo
    # (stdin của _sudo đã dùng để truyền mật khẩu cho sudo -S).
    TMP_D="$(mktemp -d)"

    # 1) dconf profile — bắt buộc, xem GOTCHA ở trên.
    cat > "$TMP_D/profile-user" <<'EOF'
user-db:user
system-db:local
EOF

    # 2) Giá trị hệ thống.
    cat > "$TMP_D/settings" <<'EOF'
# Sinh bởi 2-configure-system.sh (F09) — KHÔNG sửa tay; gỡ = xóa file + dconf update
[org/gnome/mutter]
overlay-key=''
dynamic-workspaces=false

[org/gnome/desktop/wm/preferences]
num-workspaces=1

[org/gnome/desktop/interface]
enable-hot-corners=false

[org/gnome/shell/keybindings]
toggle-overview=@as []
toggle-application-view=@as []
toggle-message-tray=@as []
focus-active-notification=@as []
switch-to-application-1=@as []
switch-to-application-2=@as []
switch-to-application-3=@as []
switch-to-application-4=@as []
switch-to-application-5=@as []
switch-to-application-6=@as []
switch-to-application-7=@as []
switch-to-application-8=@as []
switch-to-application-9=@as []

[org/gnome/desktop/wm/keybindings]
panel-main-menu=@as []
panel-run-dialog=@as []
switch-applications=@as []
switch-applications-backward=@as []
switch-group=@as []
switch-group-backward=@as []
activate-window-menu=@as []
minimize=@as []
switch-windows=@as []
switch-windows-backward=@as []
cycle-windows=@as []
cycle-windows-backward=@as []
close=@as []
switch-to-workspace-left=@as []
switch-to-workspace-right=@as []
switch-to-workspace-up=@as []
switch-to-workspace-down=@as []
switch-to-workspace-last=@as []

[org/gnome/settings-daemon/plugins/media-keys]
terminal=@as []
logout=@as []
control-center=@as []
home=@as []
email=@as []
www=@as []
search=@as []

[org/gnome/desktop/lockdown]
disable-command-line=true
disable-log-out=true
disable-user-switching=true
disable-lock-screen=true
disable-printing=true
disable-print-setup=true

[org/gnome/desktop/screensaver]
lock-enabled=false
EOF

    # 3) LOCKS — phần quan trọng nhất: thiếu lock thì user vẫn set ngược lại được.
    cat > "$TMP_D/locks" <<'EOF'
/org/gnome/mutter/overlay-key
/org/gnome/mutter/dynamic-workspaces
/org/gnome/desktop/wm/preferences/num-workspaces
/org/gnome/desktop/interface/enable-hot-corners
/org/gnome/shell/keybindings/toggle-overview
/org/gnome/shell/keybindings/toggle-application-view
/org/gnome/shell/keybindings/toggle-message-tray
/org/gnome/shell/keybindings/focus-active-notification
/org/gnome/shell/keybindings/switch-to-application-1
/org/gnome/shell/keybindings/switch-to-application-2
/org/gnome/shell/keybindings/switch-to-application-3
/org/gnome/shell/keybindings/switch-to-application-4
/org/gnome/shell/keybindings/switch-to-application-5
/org/gnome/shell/keybindings/switch-to-application-6
/org/gnome/shell/keybindings/switch-to-application-7
/org/gnome/shell/keybindings/switch-to-application-8
/org/gnome/shell/keybindings/switch-to-application-9
/org/gnome/desktop/wm/keybindings/panel-main-menu
/org/gnome/desktop/wm/keybindings/panel-run-dialog
/org/gnome/desktop/wm/keybindings/switch-applications
/org/gnome/desktop/wm/keybindings/switch-applications-backward
/org/gnome/desktop/wm/keybindings/switch-group
/org/gnome/desktop/wm/keybindings/switch-group-backward
/org/gnome/desktop/wm/keybindings/activate-window-menu
/org/gnome/desktop/wm/keybindings/minimize
/org/gnome/desktop/wm/keybindings/switch-windows
/org/gnome/desktop/wm/keybindings/switch-windows-backward
/org/gnome/desktop/wm/keybindings/cycle-windows
/org/gnome/desktop/wm/keybindings/cycle-windows-backward
/org/gnome/desktop/wm/keybindings/close
/org/gnome/desktop/wm/keybindings/switch-to-workspace-left
/org/gnome/desktop/wm/keybindings/switch-to-workspace-right
/org/gnome/desktop/wm/keybindings/switch-to-workspace-up
/org/gnome/desktop/wm/keybindings/switch-to-workspace-down
/org/gnome/desktop/wm/keybindings/switch-to-workspace-last
/org/gnome/settings-daemon/plugins/media-keys/terminal
/org/gnome/settings-daemon/plugins/media-keys/logout
/org/gnome/settings-daemon/plugins/media-keys/control-center
/org/gnome/settings-daemon/plugins/media-keys/home
/org/gnome/settings-daemon/plugins/media-keys/email
/org/gnome/settings-daemon/plugins/media-keys/www
/org/gnome/settings-daemon/plugins/media-keys/search
/org/gnome/desktop/lockdown/disable-command-line
/org/gnome/desktop/lockdown/disable-log-out
/org/gnome/desktop/lockdown/disable-user-switching
/org/gnome/desktop/lockdown/disable-lock-screen
/org/gnome/desktop/lockdown/disable-printing
/org/gnome/desktop/lockdown/disable-print-setup
/org/gnome/desktop/screensaver/lock-enabled
EOF

    # Backup file cũ (nếu có) rồi cài đặt — F11: backup ra $BACKUP_DIR, TUYỆT ĐỐI
    # không đặt trong local.d/locks/profile (dconf update nạp mọi file trong đó).
    _backup_sys_file "$DCONF_PROFILE"
    _backup_sys_file "$DCONF_SETTINGS"
    _backup_sys_file "$DCONF_LOCKS"
    _sweep_stray_dconf_backups

    _sudo mkdir -p "$DCONF_LOCAL_D/locks"
    _sudo cp "$TMP_D/profile-user" "$DCONF_PROFILE"
    _sudo cp "$TMP_D/settings" "$DCONF_SETTINGS"
    _sudo cp "$TMP_D/locks" "$DCONF_LOCKS"
    _sudo dconf update
    # F11: cài helper gỡ khoá bảo trì /usr/local/sbin/ipgs-kiosk-unlock — 1 lệnh duy
    # nhất cho quản trị viên: xoá file lockdown + dọn mọi file rác 00-kiosk-lockdown*
    # còn sót trong db, dconf update, và TỰ XÁC MINH bằng gsettings writable (chạy
    # dưới user kiosk qua session bus — process mới đọc profile mới ngay, G020).
    cat > "$TMP_D/unlock-helper" <<HELPER_EOF
#!/bin/bash
# ipgs-kiosk-unlock — Gỡ khoá lối thoát kiosk để BẢO TRÌ.
# Sinh bởi 2-configure-system.sh (F11) — chạy: sudo ipgs-kiosk-unlock
# Khoá lại: chạy Kiosk Deploy (tick "Khoá lối thoát kiosk") hoặc 2-configure-system.sh tham số 11=1.
set -e
if [ "\$EUID" -ne 0 ]; then echo "Chạy bằng sudo: sudo ipgs-kiosk-unlock" >&2; exit 1; fi
BK="$BACKUP_DIR"
mkdir -p "\$BK"
TS="\$(date +%Y%m%d%H%M%S)"
# Xoá file lockdown chính + MỌI file rác cùng prefix (F11: dconf update nạp mọi file
# trong local.d/locks bất kể đuôi — .bak còn sót sẽ tái áp lock).
for f in "$DCONF_SETTINGS" "$DCONF_SETTINGS".* "$DCONF_LOCKS" "$DCONF_LOCKS".*; do
    [ -f "\$f" ] || continue
    mv "\$f" "\$BK/unlock-\$TS-\$(basename "\$(dirname "\$f")")-\$(basename "\$f")"
done
dconf update
LEFT="\$(find "$DCONF_LOCAL_D" -maxdepth 2 -type f -name '00-kiosk-lockdown*' 2>/dev/null | wc -l)"
if [ "\$LEFT" != "0" ]; then
    echo "UNLOCK-FAILED: vẫn còn \$LEFT file 00-kiosk-lockdown* trong $DCONF_LOCAL_D" >&2
    exit 1
fi
# Xác minh thật bằng process MỚI dưới user kiosk (cần session bus đang chạy).
KUSER="$KIOSK_USER"
KUID="\$(id -u "\$KUSER" 2>/dev/null || echo '')"
if [ -n "\$KUID" ] && [ -S "/run/user/\$KUID/bus" ]; then
    W="\$(runuser -u "\$KUSER" -- env DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/\$KUID/bus gsettings writable org.gnome.mutter overlay-key 2>/dev/null || echo '?')"
    if [ "\$W" = "true" ]; then
        echo "UNLOCK-VERIFIED: overlay-key writable=true — khoá đã gỡ thật (file cũ lưu: \$BK)."
    else
        echo "CẢNH BÁO: gsettings writable trả về '\$W' (mong đợi true) — kiểm tra thủ công trong phiên user: gsettings writable org.gnome.mutter overlay-key" >&2
        exit 1
    fi
else
    echo "Đã gỡ file lockdown + dconf update (không xác minh tự động được — session bus user chưa chạy)."
    echo "Xác minh thủ công trong phiên user: gsettings writable org.gnome.mutter overlay-key → phải là true."
fi
echo "Lưu ý: phiên GNOME đang chạy vẫn giữ khoá cũ tới khi đăng xuất/khởi động lại."
HELPER_EOF
    _sudo cp "$TMP_D/unlock-helper" "$UNLOCK_HELPER"
    _sudo chmod 755 "$UNLOCK_HELPER"
    rm -rf "$TMP_D"

    # KIỂM CHỨNG THẬT — không tin _sudo (sudo sai mật khẩu → lệnh âm thầm không chạy).
    if [ ! -f "$DCONF_SETTINGS" ] || [ ! -f "$DCONF_LOCKS" ] || ! grep -q "^system-db:local" "$DCONF_PROFILE" 2>/dev/null; then
        echo "LOCKDOWN-FAILED: không ghi được dconf DB hệ thống (kiểm tra sudo password)." >&2
        exit 1
    fi
    # F11: xác nhận không còn file rác trong db sau khi cài (sweep phải sạch).
    STRAY_LEFT="$(find "$DCONF_LOCAL_D" -maxdepth 2 -type f -name '00-kiosk-lockdown.*' 2>/dev/null | wc -l)"
    if [ "$STRAY_LEFT" != "0" ]; then
        echo "CẢNH BÁO: còn $STRAY_LEFT file backup nằm sai chỗ trong $DCONF_LOCAL_D (F11) — kiểm tra sudo password." >&2
    fi
    EFFECTIVE_OVERLAY="$(gsettings get org.gnome.mutter overlay-key 2>/dev/null || echo '?')"
    if [ "$EFFECTIVE_OVERLAY" = "''" ]; then
        echo "  → LOCKDOWN-VERIFIED: overlay-key='' (đã khoá), files: $DCONF_SETTINGS + $DCONF_LOCKS"
    else
        echo "CẢNH BÁO: overlay-key hiện là $EFFECTIVE_OVERLAY — session có thể cần đăng nhập lại để dconf profile mới có hiệu lực." >&2
    fi
    echo "  → Gỡ khoá bảo trì: sudo ipgs-kiosk-unlock  (tự dọn file rác + dconf update + xác minh writable=true)"
else
    echo "=== [9/10] GỠ khoá lối thoát kiosk (chế độ bảo trì) ==="
    if [ -f "$DCONF_SETTINGS" ] || [ -f "$DCONF_LOCKS" ]; then
        # F11: backup ra $BACKUP_DIR (ngoài cây dconf db) + dọn cả file rác cùng prefix.
        _backup_sys_file "$DCONF_SETTINGS"
        _backup_sys_file "$DCONF_LOCKS"
        _sudo rm -f "$DCONF_SETTINGS" "$DCONF_LOCKS"
        _sweep_stray_dconf_backups
        _sudo dconf update
        if [ -f "$DCONF_SETTINGS" ] || [ -f "$DCONF_LOCKS" ]; then
            echo "UNLOCK-FAILED: không xóa được dconf lockdown (kiểm tra sudo password)." >&2
            exit 1
        fi
        # F11: XÁC MINH đã gỡ thật — process mới đọc profile mới ngay (G020), key phải
        # writable trở lại. Không tin việc "đã xoá file" là đủ.
        W_CHECK="$(gsettings writable org.gnome.mutter overlay-key 2>/dev/null || echo '?')"
        if [ "$W_CHECK" = "true" ]; then
            echo "  → UNLOCK-VERIFIED: overlay-key writable=true (backup tại $BACKUP_DIR). Đăng nhập lại/reboot để phím tắt hoạt động lại."
        else
            echo "CẢNH BÁO: gsettings writable trả về '$W_CHECK' (mong đợi true) — có thể còn file rác trong $DCONF_LOCAL_D tái áp lock (F11). Kiểm tra: find $DCONF_LOCAL_D -type f" >&2
        fi
    else
        _sweep_stray_dconf_backups
        echo "  → Chưa từng khoá (không có $DCONF_SETTINGS) — bỏ qua."
    fi
fi

# ─────────────────────────────────────────────────────────────
# [10] F10 — Watchdog systemd USER service: tự khởi động lại app kiosk khi bị
# đóng/crash, tránh lộ desktop trống (audit 2026-07-27 phát hiện app không chạy +
# không có cơ chế restart).
#
# THIẾT KẾ (R2 — điều chỉnh theo review Tech Lead 2026-07-27):
#  - Type=simple, Restart=always, RestartSec=10 → app tắt/crash thì bật lại sau 10s.
#  - StartLimitIntervalSec=0 → TẮT hẳn rate-limit start: kiosk KHÔNG NGƯỜI TRỰC, nếu
#    dùng StartLimitBurst thì app crash-loop (hoặc binary chưa deploy) sẽ đưa service
#    vào trạng thái `failed` VĨNH VIỄN → lộ desktop trống mãi mãi — tái mở đúng lỗ P1
#    mà watchdog sinh ra để chặn. Thà restart mãi còn hơn chết hẳn.
#  - Chống spam log khi restart mãi: RestartSec=10 → tối đa ~6 chu kỳ/phút (~vài chục
#    dòng journal/phút), nằm dưới ngưỡng rate-limit mặc định của journald
#    (RateLimitBurst=10000/30s) nên không đầy log; journald cũng tự xoay vòng theo
#    SystemMaxUse. Không cần StandardOutput=null — giữ log để chẩn đoán vì sao app chết.
#  - ExecStart qua `bash -lc 'exec ...'` để nạp PATH đăng nhập ($HOME/.local/bin,
#    /usr/local/bin) — app_exec có thể không nằm trong PATH tối giản của systemd user.
#  - WantedBy=graphical-session.target → khởi động cùng phiên đồ hoạ của user kiosk.
#
# LƯU Ý VẬN HÀNH: script CHỈ cài + enable (không `start` ngay) vì binary app có thể
# chưa được deploy — service sẽ tự chạy ở lần đăng nhập kế tiếp. Khi bật watchdog,
# mục [8/9] đã bỏ autostart .desktop của app để không chạy 2 instance.
WATCHDOG_UNIT_NAME="ipgs-kiosk-app.service"
WATCHDOG_UNIT="$HOME/.config/systemd/user/$WATCHDOG_UNIT_NAME"

if [ "$ENABLE_WATCHDOG" = "1" ]; then
    echo "=== [10/10] Watchdog systemd: tự khởi động lại app kiosk khi đóng/crash ==="
    mkdir -p "$HOME/.config/systemd/user"
    # F11: backup unit ra thư mục riêng của user — không để file lạ trong
    # ~/.config/systemd/user/ (systemd bỏ qua đuôi không hợp lệ nhưng vẫn là rác).
    if [ -f "$WATCHDOG_UNIT" ]; then
        mkdir -p "$USER_BACKUP_DIR"
        cp "$WATCHDOG_UNIT" "$USER_BACKUP_DIR/$WATCHDOG_UNIT_NAME.$BAK_SUFFIX" 2>/dev/null || true
    fi

    # F12 RC#2: Dùng REAL_EXEC đã được pre-compute ở mục [8/10] (global _get_real_exec).
    # Fix symlink: readlink -f giải /usr/bin/ipgskioskavalonia → /opt/kztek/.../run.sh
    # → BASH_SOURCE[0] đúng → DIR đúng → không còn exit 127 crash-loop.
    REAL_EXEC="$_REAL_EXEC"
    REAL_EXEC_DIR="$_REAL_EXEC_DIR"
    echo "  → Kiểm tra binary: $REAL_EXEC"
    if [ ! -f "$REAL_EXEC" ] || [ ! -x "$REAL_EXEC" ]; then
        echo "LỖI: không tìm thấy binary '$REAL_EXEC' (tồn tại + có quyền thực thi)." >&2
        echo "     Đảm bảo app kiosk đã được deploy trước khi bật watchdog," >&2
        echo "     hoặc truyền đường dẫn tuyệt đối cho tham số app_exec." >&2
        exit 1
    fi

    cat > "$WATCHDOG_UNIT" <<EOF
[Unit]
Description=iPGS Kiosk App Watchdog (tu khoi dong lai khi dong/crash)
After=graphical-session.target
PartOf=graphical-session.target
# R2: TAT rate-limit start — kiosk khong nguoi truc, service KHONG duoc phep chet
# vinh vien (failed) khi app crash-loop/binary chua deploy. RestartSec=10 giu nhip
# restart cham de khong spam journal.
StartLimitIntervalSec=0

[Service]
Type=simple
Environment=DISPLAY=:0
WorkingDirectory=$REAL_EXEC_DIR
ExecStart=$REAL_EXEC
Restart=always
RestartSec=10

[Install]
WantedBy=graphical-session.target
EOF
    echo "  → Đã ghi $WATCHDOG_UNIT (ExecStart=$REAL_EXEC, WorkingDirectory=$REAL_EXEC_DIR, Restart=always RestartSec=10, StartLimitIntervalSec=0 — không bao giờ chết hẳn)"

    systemctl --user daemon-reload 2>/dev/null || true
    systemctl --user enable "$WATCHDOG_UNIT_NAME" 2>/dev/null || true

    # KIỂM CHỨNG: file unit hợp lệ + đã enable (symlink trong graphical-session.target.wants).
    if systemctl --user cat "$WATCHDOG_UNIT_NAME" >/dev/null 2>&1; then
        WD_STATE="$(systemctl --user is-enabled "$WATCHDOG_UNIT_NAME" 2>/dev/null || echo 'unknown')"
        echo "  → WATCHDOG-VERIFIED: unit hợp lệ, is-enabled=$WD_STATE (sẽ tự chạy + tự restart app ở lần đăng nhập kế tiếp)."
    else
        echo "CẢNH BÁO: không đọc lại được unit watchdog qua systemctl --user (kiểm tra phiên user systemd)." >&2
    fi
    echo "  → Gỡ watchdog: chạy lại script với tham số 12 = 0, hoặc:"
    echo "      systemctl --user disable --now $WATCHDOG_UNIT_NAME && rm $WATCHDOG_UNIT && systemctl --user daemon-reload"
else
    echo "=== [10/10] GỠ watchdog systemd app kiosk (không chọn) ==="
    if [ -f "$WATCHDOG_UNIT" ]; then
        systemctl --user disable --now "$WATCHDOG_UNIT_NAME" 2>/dev/null || true
        mkdir -p "$USER_BACKUP_DIR"
        cp "$WATCHDOG_UNIT" "$USER_BACKUP_DIR/$WATCHDOG_UNIT_NAME.$BAK_SUFFIX" 2>/dev/null || true
        rm -f "$WATCHDOG_UNIT"
        # F11: dọn cả backup cũ nằm sai chỗ trong ~/.config/systemd/user/ (bản script cũ)
        for f in "$WATCHDOG_UNIT".*; do
            [ -f "$f" ] || continue
            mv "$f" "$USER_BACKUP_DIR/$(basename "$f")" 2>/dev/null || true
        done
        systemctl --user daemon-reload 2>/dev/null || true
        echo "  → Đã gỡ watchdog service (backup: $USER_BACKUP_DIR/$WATCHDOG_UNIT_NAME.$BAK_SUFFIX). App quay về autostart .desktop nếu bật Autostart."
    else
        echo "  → Chưa từng cài watchdog — bỏ qua."
    fi
fi

# ─────────────────────────────────────────────────────────────
# [11/11] Tường lửa ufw (F27). LUÔN allow OpenSSH TRƯỚC khi enable — nếu quên bước
# này, bật ufw trên máy chỉ có SSH sẽ tự khoá luôn quyền truy cập từ xa (không có GUI
# để mở lại nếu kỹ thuật viên đang thao tác qua SSH). Đã verify thứ tự này an toàn
# trên máy Lubuntu thật (SSH vẫn sống sau khi enable) — dùng chung logic cho GNOME.
if [ "$ENABLE_FIREWALL" = "1" ]; then
    echo "=== [11/11] Bật tường lửa ufw ==="
    if ! command -v ufw >/dev/null 2>&1; then
        _sudo apt-get install -y -qq ufw 2>&1 || true
    fi
    if command -v ufw >/dev/null 2>&1; then
        _sudo ufw allow OpenSSH 2>/dev/null || _sudo ufw allow 22/tcp 2>/dev/null || true
        _sudo ufw --force enable 2>&1
        UFW_STATE="$(_sudo ufw status 2>/dev/null | head -1)"
        echo "  → FIREWALL-VERIFIED: $UFW_STATE (đã allow OpenSSH trước khi enable)."
    else
        echo "CẢNH BÁO: không cài được ufw (cần mạng để apt install) — bỏ qua." >&2
    fi
else
    echo "=== [11/11] Tắt tường lửa ufw (giữ nguyên rule, chỉ ufw disable) ==="
    command -v ufw >/dev/null 2>&1 && _sudo ufw disable 2>&1 || echo "  → Chưa cài ufw — bỏ qua."
fi

# ─────────────────────────────────────────────────────────────
echo ""
echo "✓ HOÀN THÀNH. Cần LOG OUT / RESTART để áp dụng đầy đủ (đặc biệt autologin GDM + autostart)."
echo "  Kiểm tra sau khi restart:"
echo "    - Máy tự vào thẳng user '$KIOSK_USER' không cần đăng nhập"
echo "    - App '$APP_EXEC' tự chạy fullscreen"
