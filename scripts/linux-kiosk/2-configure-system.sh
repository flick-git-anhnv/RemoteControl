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
#                       đóng/crash (tham số 12, F10). Restart=always + StartLimit chống
#                       vòng lặp vô hạn khi binary chưa tồn tại. Khi bật, app được quản
#                       lý bởi service (không tạo autostart .desktop cho app để tránh
#                       chạy 2 lần). 1 = cài+bật service, 0 = gỡ service (app quay về
#                       autostart .desktop nếu enable_autostart=1).
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
LOCKDOWN_SHELL="${11:-1}"
ENABLE_WATCHDOG="${12:-1}"

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
echo "  HotCorner=$DISABLE_HOTCORNER DockIcons=$DISABLE_DOCK_ICONS Sleep=$BLOCK_SLEEP InitialSetup=$SKIP_INITIAL_SETUP Autologin=$ENABLE_AUTOLOGIN SwUpdate=$DISABLE_SW_UPDATE Autostart=$ENABLE_AUTOSTART LockWorkspace=$LOCK_SINGLE_WORKSPACE LockdownShell=$LOCKDOWN_SHELL Watchdog=$ENABLE_WATCHDOG"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "LỖI: đừng chạy script bằng 'sudo bash ...' hay user root." >&2
    echo "     Chạy trực tiếp: bash scripts/linux-kiosk/2-configure-system.sh" >&2
    exit 1
fi

# ─────────────────────────────────────────────────────────────
# 2 chiều: 1 = tắt/ẩn, 0 = bật/hiện lại như mặc định GNOME
if [ "$DISABLE_HOTCORNER" = "1" ]; then
    echo "=== [1/9] Tắt hot corner, notification banner, screensaver/lock ==="
    # `|| true`: các key này có thể đã bị dconf lock bởi mục [9/9] (F09) — khi đó
    # gsettings set theo user bị từ chối, nhưng giá trị hệ thống đã đúng rồi.
    gsettings set org.gnome.desktop.interface enable-hot-corners false 2>/dev/null || true
    gsettings set org.gnome.desktop.notifications show-banners false
    gsettings set org.gnome.desktop.screensaver lock-enabled false 2>/dev/null || true
    gsettings set org.gnome.desktop.session idle-delay 0
else
    echo "=== [1/9] Bật lại hot corner, notification banner, screensaver/lock (mặc định) ==="
    gsettings set org.gnome.desktop.interface enable-hot-corners true 2>/dev/null || true
    gsettings set org.gnome.desktop.notifications show-banners true
    gsettings set org.gnome.desktop.screensaver lock-enabled true 2>/dev/null || true
    gsettings set org.gnome.desktop.session idle-delay 300
fi

# ─────────────────────────────────────────────────────────────
if [ "$LOCK_SINGLE_WORKSPACE" = "1" ]; then
    echo "=== [2/9] Khóa còn 1 workspace tĩnh (chặn gesture 2/3 ngón chuyển workspace) ==="
    gsettings set org.gnome.mutter dynamic-workspaces false 2>/dev/null || true
    gsettings set org.gnome.desktop.wm.preferences num-workspaces 1 2>/dev/null || true
    gsettings set org.gnome.shell.overrides workspaces-only-on-primary true 2>/dev/null || true
else
    echo "=== [2/9] Bật lại workspace động (mặc định GNOME) ==="
    gsettings set org.gnome.mutter dynamic-workspaces true 2>/dev/null || true
    gsettings reset org.gnome.desktop.wm.preferences num-workspaces 2>/dev/null || true
    gsettings reset org.gnome.shell.overrides workspaces-only-on-primary 2>/dev/null || true
fi

# ─────────────────────────────────────────────────────────────
if [ "$DISABLE_DOCK_ICONS" = "1" ]; then
    echo "=== [3/9] Tắt Ubuntu Dock + Desktop Icons (mặc định bật trên máy mới) ==="
    gnome-extensions disable ubuntu-dock@ubuntu.com 2>/dev/null || echo "  → ubuntu-dock@ubuntu.com không có/đã tắt, bỏ qua."
    gnome-extensions disable ding@rastersoft.com 2>/dev/null || echo "  → ding@rastersoft.com không có/đã tắt, bỏ qua."
else
    echo "=== [3/9] Bật lại Ubuntu Dock + Desktop Icons ==="
    gnome-extensions enable ubuntu-dock@ubuntu.com 2>/dev/null || echo "  → ubuntu-dock@ubuntu.com không có, bỏ qua."
    gnome-extensions enable ding@rastersoft.com 2>/dev/null || echo "  → ding@rastersoft.com không có, bỏ qua."
fi

# ─────────────────────────────────────────────────────────────
if [ "$BLOCK_SLEEP" = "1" ]; then
    echo "=== [4/9] Chặn suspend/sleep khi cắm điện (tránh màn hình tắt giữa chừng) ==="
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'nothing'
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'nothing' 2>/dev/null || true
else
    echo "=== [4/9] Bật lại suspend/sleep mặc định ==="
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'suspend'
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'suspend' 2>/dev/null || true
fi

# ─────────────────────────────────────────────────────────────
if [ "$SKIP_INITIAL_SETUP" = "1" ]; then
    echo "=== [5/9] Bỏ qua màn hình gnome-initial-setup (nếu user vừa tạo mới) ==="
    mkdir -p "$HOME/.config"
    touch "$HOME/.config/gnome-initial-setup-done"
    echo "  → Đã đánh dấu gnome-initial-setup-done cho '$KIOSK_USER'."
else
    echo "=== [5/9] Bỏ đánh dấu gnome-initial-setup-done (màn hình initial-setup sẽ hiện lại) ==="
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
    echo "=== [6/9] Autologin cho user '$KIOSK_USER' (display manager: ${DM_NAME:-không rõ}) ==="
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
    echo "=== [6/9] Tắt autologin (display manager: ${DM_NAME:-không rõ}) ==="
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
    echo "=== [7/9] Tắt popup Software Updater ==="
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
    echo "=== [7/9] Bỏ qua (không chọn) ==="
fi

# ─────────────────────────────────────────────────────────────
if [ "$ENABLE_AUTOSTART" = "1" ]; then
    echo "=== [8/9] Autostart app iPGS fullscreen + unclutter khi vào desktop ==="
    mkdir -p "$HOME/.config/autostart"

    # F10: khi watchdog systemd bật, app do service quản lý (Restart=always). KHÔNG
    # tạo autostart .desktop cho app nữa để tránh chạy 2 instance song song. Nếu có
    # file .desktop cũ (deploy trước), gỡ đi. unclutter vẫn qua autostart .desktop.
    if [ "$ENABLE_WATCHDOG" = "1" ]; then
        rm -f "$HOME/.config/autostart/ipgs-kiosk.desktop"
        echo "  → Watchdog systemd BẬT: app do service ipgs-kiosk-app quản lý; bỏ autostart .desktop của app (tránh chạy 2 lần)."
    else
        cat > "$HOME/.config/autostart/ipgs-kiosk.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=iPGS Kiosk
Exec=$APP_EXEC
X-GNOME-Autostart-enabled=true
EOF
        echo "  → Đã tạo $HOME/.config/autostart/ipgs-kiosk.desktop (Exec=$APP_EXEC)"
        echo "    Nếu \$APP_EXEC chưa đúng, sửa lại field Exec= trong file trên."
    fi

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
    echo "=== [8/9] Bỏ qua (không chọn) ==="
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
# - KHÔNG khoá được bằng dconf: cử chỉ cảm ứng vuốt 3 ngón lên (GNOME 40+) vẫn mở
#   được Activities overview — hard-code trong GNOME Shell, không có gsettings key.
#   Giảm nhẹ: đã ẩn Activities button + search (Just Perfection), workspace khoá 1.
#   Chặn triệt để cần session gnome-kiosk / WM khác (chưa có gói cho Ubuntu 22.04).
#
# GỠ KHOÁ ĐỂ BẢO TRÌ (quản trị viên, qua SSH):
#   sudo rm /etc/dconf/db/local.d/00-kiosk-lockdown \
#           /etc/dconf/db/local.d/locks/00-kiosk-lockdown && sudo dconf update
#   (hoặc chạy lại script này với tham số 11 = 0). Khoá lại: chạy script, tham số 11 = 1.
DCONF_PROFILE="/etc/dconf/profile/user"
DCONF_LOCAL_D="/etc/dconf/db/local.d"
DCONF_SETTINGS="$DCONF_LOCAL_D/00-kiosk-lockdown"
DCONF_LOCKS="$DCONF_LOCAL_D/locks/00-kiosk-lockdown"
BAK_SUFFIX="bak-$(date +%Y%m%d%H%M%S)"

if [ "$LOCKDOWN_SHELL" = "1" ]; then
    echo "=== [9/9] Khoá lối thoát kiosk (dconf system-wide + lock) ==="

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

[org/gnome/desktop/wm/keybindings]
panel-main-menu=@as []
panel-run-dialog=@as []
switch-applications=@as []
switch-applications-backward=@as []
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
/org/gnome/desktop/wm/keybindings/panel-main-menu
/org/gnome/desktop/wm/keybindings/panel-run-dialog
/org/gnome/desktop/wm/keybindings/switch-applications
/org/gnome/desktop/wm/keybindings/switch-applications-backward
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

    # Backup file cũ (nếu có) rồi cài đặt.
    [ -f "$DCONF_PROFILE" ] && _sudo cp "$DCONF_PROFILE" "$DCONF_PROFILE.$BAK_SUFFIX" || true
    [ -f "$DCONF_SETTINGS" ] && _sudo cp "$DCONF_SETTINGS" "$DCONF_SETTINGS.$BAK_SUFFIX" || true
    [ -f "$DCONF_LOCKS" ] && _sudo cp "$DCONF_LOCKS" "$DCONF_LOCKS.$BAK_SUFFIX" || true

    _sudo mkdir -p "$DCONF_LOCAL_D/locks"
    _sudo cp "$TMP_D/profile-user" "$DCONF_PROFILE"
    _sudo cp "$TMP_D/settings" "$DCONF_SETTINGS"
    _sudo cp "$TMP_D/locks" "$DCONF_LOCKS"
    _sudo dconf update
    rm -rf "$TMP_D"

    # KIỂM CHỨNG THẬT — không tin _sudo (sudo sai mật khẩu → lệnh âm thầm không chạy).
    if [ ! -f "$DCONF_SETTINGS" ] || [ ! -f "$DCONF_LOCKS" ] || ! grep -q "^system-db:local" "$DCONF_PROFILE" 2>/dev/null; then
        echo "LOCKDOWN-FAILED: không ghi được dconf DB hệ thống (kiểm tra sudo password)." >&2
        exit 1
    fi
    EFFECTIVE_OVERLAY="$(gsettings get org.gnome.mutter overlay-key 2>/dev/null || echo '?')"
    if [ "$EFFECTIVE_OVERLAY" = "''" ]; then
        echo "  → LOCKDOWN-VERIFIED: overlay-key='' (đã khoá), files: $DCONF_SETTINGS + $DCONF_LOCKS"
    else
        echo "CẢNH BÁO: overlay-key hiện là $EFFECTIVE_OVERLAY — session có thể cần đăng nhập lại để dconf profile mới có hiệu lực." >&2
    fi
    echo "  → Gỡ khoá bảo trì: sudo rm $DCONF_SETTINGS $DCONF_LOCKS && sudo dconf update"
else
    echo "=== [9/9] GỠ khoá lối thoát kiosk (chế độ bảo trì) ==="
    if [ -f "$DCONF_SETTINGS" ] || [ -f "$DCONF_LOCKS" ]; then
        [ -f "$DCONF_SETTINGS" ] && _sudo cp "$DCONF_SETTINGS" "$DCONF_SETTINGS.$BAK_SUFFIX" || true
        [ -f "$DCONF_LOCKS" ] && _sudo cp "$DCONF_LOCKS" "$DCONF_LOCKS.$BAK_SUFFIX" || true
        _sudo rm -f "$DCONF_SETTINGS" "$DCONF_LOCKS"
        _sudo dconf update
        if [ -f "$DCONF_SETTINGS" ] || [ -f "$DCONF_LOCKS" ]; then
            echo "UNLOCK-FAILED: không xóa được dconf lockdown (kiểm tra sudo password)." >&2
            exit 1
        fi
        echo "  → Đã gỡ khoá (backup: *.$BAK_SUFFIX). Đăng nhập lại/reboot để phím tắt hoạt động lại."
    else
        echo "  → Chưa từng khoá (không có $DCONF_SETTINGS) — bỏ qua."
    fi
fi

# ─────────────────────────────────────────────────────────────
# [10] F10 — Watchdog systemd USER service: tự khởi động lại app kiosk khi bị
# đóng/crash, tránh lộ desktop trống (audit 2026-07-27 phát hiện app không chạy +
# không có cơ chế restart).
#
# THIẾT KẾ:
#  - Type=simple, Restart=always, RestartSec=3 → app tắt/crash thì bật lại sau 3s.
#  - StartLimitIntervalSec=60 + StartLimitBurst=5 (ở [Unit], systemd 249) → nếu binary
#    CHƯA tồn tại (như hiện trạng: chưa deploy app), service fail 5 lần trong 60s rồi
#    DỪNG hẳn kèm log "start request repeated too quickly" — KHÔNG lặp vô hạn ghi đầy log.
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
    echo "=== [10] Watchdog systemd: tự khởi động lại app kiosk khi đóng/crash ==="
    mkdir -p "$HOME/.config/systemd/user"
    [ -f "$WATCHDOG_UNIT" ] && cp "$WATCHDOG_UNIT" "$WATCHDOG_UNIT.$BAK_SUFFIX" 2>/dev/null || true

    # Escape dấu ' trong APP_EXEC để nhúng an toàn vào 'exec ...' của bash -lc.
    APP_EXEC_ESC="$(printf '%s' "$APP_EXEC" | sed "s/'/'\\\\''/g")"

    cat > "$WATCHDOG_UNIT" <<EOF
[Unit]
Description=iPGS Kiosk App Watchdog (tu khoi dong lai khi dong/crash)
After=graphical-session.target
PartOf=graphical-session.target
# Chong vong lap restart vo han khi binary chua ton tai: dung sau 5 lan fail/60s.
StartLimitIntervalSec=60
StartLimitBurst=5

[Service]
Type=simple
ExecStart=/bin/bash -lc 'exec $APP_EXEC_ESC'
Restart=always
RestartSec=3

[Install]
WantedBy=graphical-session.target
EOF
    echo "  → Đã ghi $WATCHDOG_UNIT (ExecStart=$APP_EXEC, Restart=always RestartSec=3, StartLimitBurst=5/60s)"

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
    echo "=== [10] GỠ watchdog systemd app kiosk (không chọn) ==="
    if [ -f "$WATCHDOG_UNIT" ]; then
        systemctl --user disable --now "$WATCHDOG_UNIT_NAME" 2>/dev/null || true
        cp "$WATCHDOG_UNIT" "$WATCHDOG_UNIT.$BAK_SUFFIX" 2>/dev/null || true
        rm -f "$WATCHDOG_UNIT"
        systemctl --user daemon-reload 2>/dev/null || true
        echo "  → Đã gỡ watchdog service (backup: $WATCHDOG_UNIT.$BAK_SUFFIX). App quay về autostart .desktop nếu bật Autostart."
    else
        echo "  → Chưa từng cài watchdog — bỏ qua."
    fi
fi

# ─────────────────────────────────────────────────────────────
echo ""
echo "✓ HOÀN THÀNH. Cần LOG OUT / RESTART để áp dụng đầy đủ (đặc biệt autologin GDM + autostart)."
echo "  Kiểm tra sau khi restart:"
echo "    - Máy tự vào thẳng user '$KIOSK_USER' không cần đăng nhập"
echo "    - App '$APP_EXEC' tự chạy fullscreen"
