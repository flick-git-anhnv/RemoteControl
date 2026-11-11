#!/bin/bash
# 2-configure-system.sh (Lubuntu/LXQt) — Các tinh chỉnh hệ thống còn lại cho kiosk iPGS
# trên Lubuntu (LXQt + Openbox + SDDM/LightDM), KHÔNG phải GNOME/GDM.
#
# ĐÂY LÀ BẢN SONG SONG của scripts/linux-kiosk/2-configure-system.sh (bản gốc chỉ đúng
# trên Ubuntu Desktop/GNOME). Không dùng chung được vì: GDM→SDDM/LightDM, gsettings
# org.gnome.*→không tồn tại, dconf lockdown GNOME→Openbox rc.xml, Ubuntu Dock/Desktop
# Icons NG (extension GNOME)→autostart pcmanfm-qt (xử lý ở 1-install-software.sh).
#
# CHƯA TEST TRÊN MÁY LUBUNTU THẬT — chạy thử + verify trên 1 máy thật trước khi dùng
# sản xuất, rồi ghi lesson theo quy trình chuẩn (C:\Users\nguye\.claude\lessons\).
#
# BẮT BUỘC chạy SAU khi đã chạy xong 1-install-software.sh (cần unclutter đã cài).
#
# Chạy (mặc định bật tất cả):
#   bash scripts/lubuntu-kiosk/2-configure-system.sh [kiosk_user] [app_exec]
#
# Chạy có chọn lọc (tham số 3-10: 1=áp dụng, 0=HOÀN TÁC về mặc định, mặc định 1 nếu không truyền):
#   bash scripts/lubuntu-kiosk/2-configure-system.sh <kiosk_user> <app_exec> \
#        <block_sleep> <enable_autologin> <disable_sw_update> <enable_autostart> \
#        <lock_single_desktop> <lockdown_shell> <enable_watchdog> <enable_firewall>
#
# Tham số:
#   kiosk_user           Mặc định: kztek — user autologin SDDM/LightDM.
#   app_exec             Mặc định: ipgskioskavalonia
#   block_sleep          Mask systemd sleep/suspend targets + tắt DPMS/screensaver X11
#                         (KHÔNG phụ thuộc DE — dùng chung được cho cả GNOME lẫn LXQt,
#                         nên đáng tin hơn gsettings power-plugin riêng của GNOME).
#   enable_autologin     Autologin SDDM (ưu tiên trên Lubuntu 20.04+) hoặc LightDM
#                         (Lubuntu cũ hơn dùng LXDE/LightDM) cho kiosk_user.
#   disable_sw_update    Ẩn popup update-notifier (Lubuntu vẫn dựa trên Ubuntu nên có
#                         gói này).
#   enable_autostart     Autostart app iPGS khi vào desktop (XDG chuẩn, LXQt đọc y hệt
#                         GNOME). unclutter do 1-install-software.sh quản lý.
#   lock_single_desktop  Khoá còn 1 desktop/workspace tĩnh trong Openbox (tương đương
#                         num-workspaces=1 bản GNOME) — giảm bề mặt thoát kiosk.
#   lockdown_shell       Khoá phím tắt thoát kiosk — 2 lớp: (1) Openbox rc.xml (Alt+F4,
#                         Alt+Tab...) + (2) lxqt-globalkeyshortcuts.conf — LỚP QUAN
#                         TRỌNG HƠN, xác minh thật trên máy Lubuntu test: Ctrl+Alt+T mở
#                         Terminal, Ctrl+Alt+Delete mở Task Manager, Meta+E mở File
#                         Manager, Meta+R mở Runner, phím Super mở Main Menu — đây mới
#                         là lối thoát kiosk thật, Openbox rc.xml không khoá được các
#                         phím này. KHÔNG có cơ chế "lock" cấp hệ thống như dconf GNOME
#                         — chỉ có thể ghi đè config của user rồi set read-only (chattr
#                         +i cho rc.xml) để cản GUI settings ghi đè lại; user có sudo
#                         vẫn gỡ được (như F09 gốc, đây là control thêm chứ không phải
#                         tuyệt đối).
#   enable_watchdog      Cài systemd USER service tự khởi động lại app kiosk khi
#                         crash — CƠ CHẾ Y HỆT bản GNOME (systemd --user không phụ
#                         thuộc desktop environment).
#   enable_firewall      Bật tường lửa ufw (cài nếu chưa có, LUÔN allow OpenSSH trước
#                         khi enable để không tự khoá mất SSH) — 1 = bật, 0 = tắt
#                         (`ufw disable`, giữ nguyên rule đã cấu hình để bật lại nhanh).
#                         Không tự mở port ZcuAgent ở đây — xem setup-zcu-agent.sh
#                         (tự mở đúng port khi cài agent).
#
# Ví dụ:
#   bash scripts/lubuntu-kiosk/2-configure-system.sh
#   bash scripts/lubuntu-kiosk/2-configure-system.sh kztek /opt/kztek/ipgskioskavalonia/run.sh

set -e

KIOSK_USER="${1:-kztek}"
APP_EXEC="${2:-ipgskioskavalonia}"
BLOCK_SLEEP="${3:-1}"
ENABLE_AUTOLOGIN="${4:-1}"
DISABLE_SW_UPDATE="${5:-1}"
ENABLE_AUTOSTART="${6:-1}"
LOCK_SINGLE_DESKTOP="${7:-1}"
LOCKDOWN_SHELL="${8:-1}"
ENABLE_WATCHDOG="${9:-1}"
ENABLE_FIREWALL="${10:-1}"

DEPLOY_SCOPE="${DEPLOY_SCOPE:-all}"
_sudo() {
    if [ -n "${KIOSK_SUDO_PASS:-}" ]; then
        echo "$KIOSK_SUDO_PASS" | sudo -S "$@" 2>/dev/null
    else
        sudo "$@"
    fi
}

# ─────────────────────────────────────────────────────────────
# Giải symlink để lấy đường dẫn tuyệt đối THẬT của binary/script.
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


echo "=== [2] Cấu hình hệ thống cho Kiosk iPGS — Lubuntu (LXQt) ==="
echo "  Kiosk user : $KIOSK_USER"
echo "  App exec   : $APP_EXEC"
echo "  Sleep=$BLOCK_SLEEP Autologin=$ENABLE_AUTOLOGIN SwUpdate=$DISABLE_SW_UPDATE Autostart=$ENABLE_AUTOSTART LockDesktop=$LOCK_SINGLE_DESKTOP LockdownShell=$LOCKDOWN_SHELL Watchdog=$ENABLE_WATCHDOG Firewall=$ENABLE_FIREWALL"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "LỖI: đừng chạy script bằng 'sudo bash ...' hay user root." >&2
    echo "     Chạy trực tiếp: bash scripts/lubuntu-kiosk/2-configure-system.sh" >&2
    exit 1
fi

# ─────────────────────────────────────────────────────────────
# [1/9] Chặn suspend/sleep + tắt DPMS/screensaver X11 — cơ chế DE-agnostic (systemd +
# X11 core, không phụ thuộc GNOME hay LXQt) nên dùng được thay cho gsettings power.
if [ "$DEPLOY_SCOPE" != "software" ]; then
if [ "$BLOCK_SLEEP" = "1" ]; then
    echo "=== [1/9] Chặn suspend/sleep + tắt DPMS/screensaver ==="
    _sudo systemctl mask sleep.target suspend.target hibernate.target hybrid-sleep.target 2>/dev/null || true
    xset s off 2>/dev/null || true
    xset -dpms 2>/dev/null || true
    xset s noblank 2>/dev/null || true
else
    echo "=== [1/9] Bật lại suspend/sleep + DPMS/screensaver mặc định ==="
    _sudo systemctl unmask sleep.target suspend.target hibernate.target hybrid-sleep.target 2>/dev/null || true
    xset s on 2>/dev/null || true
    xset +dpms 2>/dev/null || true
fi

# ─────────────────────────────────────────────────────────────
# [2/9] Khoá còn 1 desktop tĩnh trong Openbox (tương đương num-workspaces=1 GNOME) —
# chặn gesture/phím chuyển desktop làm app fullscreen "biến mất" sang desktop khác.
OB_DIR="$HOME/.config/openbox"
OB_RC="$OB_DIR/rc.xml"
mkdir -p "$OB_DIR"

# XÁC MINH THẬT trên Lubuntu 22.04 (192.168.21.39, gói lxqt-session 0.17, openbox
# 3.6.1): package "openbox" chỉ ship /etc/xdg/openbox/rc.xml — KHÔNG có file nào tên
# "lxqt-rc.xml" trên toàn hệ thống, và không có symlink openbox-lxqt (kiểu
# openbox-lxde của LXDE) để đổi tên rc theo desktop. lxqt-session gọi thẳng binary
# "openbox" (xem /usr/share/lxqt/windowmanagers.conf mục "openbox") nên Openbox dùng
# đúng quy ước mặc định của nó: $XDG_CONFIG_HOME/openbox/rc.xml. (Giả định ban đầu
# "lxqt-rc.xml" là SAI — đã sửa sau khi SSH kiểm tra máy thật, xem lesson đã ghi.)
if [ ! -f "$OB_RC" ]; then
    for _tmpl in /etc/xdg/openbox/rc.xml /usr/share/lxqt/openbox/rc.xml; do
        if [ -f "$_tmpl" ]; then
            cp "$_tmpl" "$OB_RC"
            echo "  → Đã khởi tạo $OB_RC từ mẫu hệ thống $_tmpl."
            break
        fi
    done
fi

# XÁC MINH THẬT (chạy lại script lần 2 trên 192.168.21.39): nếu lần chạy TRƯỚC đã bật
# lockdown_shell=1, bước [6/9] bên dưới đã set $OB_RC thành 444 + chattr +i (immutable).
# Bước [2/9] này chạy TRƯỚC bước [6/9] nên nếu không gỡ immutable/read-only NGAY TỪ ĐÂY,
# `sed -i` (tạo file tạm rồi rename đè) sẽ chết với "Operation not permitted" → script
# thoát exit 4 dưới set -e. Phải gỡ khoá cũ (nếu có) trước khi chạm vào file, bất kể
# lần này có chọn khoá lại hay không — [6/9] sẽ tự khoá lại nếu cần.
if [ -f "$OB_RC" ]; then
    _sudo chattr -i "$OB_RC" 2>/dev/null || true
    chmod u+w "$OB_RC" 2>/dev/null || true
fi

if [ -f "$OB_RC" ]; then
    cp "$OB_RC" "$OB_RC.bak-$(date +%Y%m%d%H%M%S)"
    if [ "$LOCK_SINGLE_DESKTOP" = "1" ]; then
        echo "=== [2/9] Khoá còn 1 desktop tĩnh (Openbox <desktops><number>) ==="
        if grep -q '<desktops>' "$OB_RC"; then
            sed -i '/<desktops>/,/<\/desktops>/ s|<number>[0-9]*</number>|<number>1</number>|' "$OB_RC"
        fi
        echo "  → Đã set number=1 trong <desktops> (hiệu lực sau 'openbox --reconfigure' hoặc đăng nhập lại)."
    else
        echo "=== [2/9] Bật lại nhiều desktop mặc định (number=4) ==="
        if grep -q '<desktops>' "$OB_RC"; then
            sed -i '/<desktops>/,/<\/desktops>/ s|<number>[0-9]*</number>|<number>4</number>|' "$OB_RC"
        fi
    fi
else
    echo "CẢNH BÁO: không tìm thấy $OB_RC hoặc mẫu hệ thống — bỏ qua bước khoá desktop. Đăng nhập vào LXQt 1 lần rồi chạy lại." >&2
fi

# ─────────────────────────────────────────────────────────────
# Dò display manager THẬT đang dùng — Lubuntu 20.04+ mặc định SDDM, bản cũ hơn
# (LXDE-based) thường LightDM. KHÔNG hardcode 1 loại.
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

# ─────────────────────────────────────────────────────────────
if [ "$ENABLE_AUTOLOGIN" = "1" ]; then
    DM_NAME="$(_detect_dm)"
    echo "=== [3/9] Autologin cho user '$KIOSK_USER' (display manager: ${DM_NAME:-không rõ}) ==="
    AUTOLOGIN_OK=0
    case "$DM_NAME" in
        sddm)
            SDDM_DIR="/etc/sddm.conf.d"
            SDDM_CONF="$SDDM_DIR/60-kiosk-autologin.conf"
            _sudo mkdir -p "$SDDM_DIR" || true
            _sudo bash -c "printf '[Autologin]\nUser=%s\nSession=lxqt.desktop\n' '$KIOSK_USER' > '$SDDM_CONF'" || true
            if grep -q "^User=$KIOSK_USER" "$SDDM_CONF" 2>/dev/null; then
                echo "  → AUTOLOGIN-VERIFIED: $SDDM_CONF (User=$KIOSK_USER, Session=lxqt.desktop)"
                AUTOLOGIN_OK=1
            fi
            ;;
        lightdm)
            LDM_DIR="/etc/lightdm/lightdm.conf.d"
            LDM_CONF="$LDM_DIR/60-kiosk-autologin.conf"
            _sudo mkdir -p "$LDM_DIR" || true
            _sudo bash -c "printf '[Seat:*]\nautologin-user=%s\nautologin-user-timeout=0\nautologin-session=Lubuntu\n' '$KIOSK_USER' > '$LDM_CONF'" || true
            if grep -q "^autologin-user=$KIOSK_USER" "$LDM_CONF" 2>/dev/null; then
                echo "  → AUTOLOGIN-VERIFIED: $LDM_CONF (autologin-user=$KIOSK_USER)"
                echo "  Lưu ý: user cần thuộc group autologin/nopasswdlogin trên một số distro; nếu tên session"
                echo "  'Lubuntu' không khớp máy thật, sửa autologin-session= cho đúng (xem /usr/share/xsessions/*.desktop)." >&2
                AUTOLOGIN_OK=1
            fi
            ;;
        *)
            echo "LỖI: không xác định được display manager (không phải sddm/lightdm, hoặc đọc /etc/X11/default-display-manager + display-manager.service đều thất bại)." >&2
            ;;
    esac
    if [ "$AUTOLOGIN_OK" != "1" ]; then
        echo "AUTOLOGIN-FAILED: cấu hình autologin CHƯA được áp dụng. Máy sẽ vẫn hỏi đăng nhập sau khi restart." >&2
        exit 1
    fi
else
    DM_NAME="$(_detect_dm)"
    echo "=== [3/9] Tắt autologin (display manager: ${DM_NAME:-không rõ}) ==="
    case "$DM_NAME" in
        sddm)    _sudo rm -f /etc/sddm.conf.d/60-kiosk-autologin.conf || true; echo "  → Đã gỡ 60-kiosk-autologin.conf (sddm)." ;;
        lightdm) _sudo rm -f /etc/lightdm/lightdm.conf.d/60-kiosk-autologin.conf || true; echo "  → Đã gỡ 60-kiosk-autologin.conf (lightdm)." ;;
        *)       echo "CẢNH BÁO: không xác định được display manager — bỏ qua tắt autologin." >&2 ;;
    esac
fi
fi # [DEPLOY_SCOPE != software]

# ─────────────────────────────────────────────────────────────
if [ "$DEPLOY_SCOPE" != "machine" ]; then
if [ "$DISABLE_SW_UPDATE" = "1" ]; then
    echo "=== [4/9] Tắt popup Software Updater ==="
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
else
    echo "=== [4/9] Bật lại Software Updater (mặc định) ==="
    if [ -f "$HOME/.config/autostart/update-notifier.desktop" ] && \
       grep -qx 'Hidden=true' "$HOME/.config/autostart/update-notifier.desktop"; then
        rm -f "$HOME/.config/autostart/update-notifier.desktop"
        echo "  → Đã xoá override ẩn update-notifier (popup cập nhật hiện lại từ lần đăng nhập kế tiếp)."
    else
        echo "  → Không có override ẩn update-notifier — giữ nguyên."
    fi
fi


echo "=== [5/9] Autostart app iPGS khi vào desktop (Autostart=$ENABLE_AUTOSTART, Watchdog=$ENABLE_WATCHDOG) ==="
mkdir -p "$HOME/.config/autostart"

if [ "$ENABLE_WATCHDOG" = "1" ]; then
    rm -f "$HOME/.config/autostart/ipgs-kiosk.desktop"
    echo "  → Watchdog BẬT: app do service ipgs-kiosk-app quản lý; đã xóa autostart .desktop của app (tránh chạy 2 lần)."
elif [ "$ENABLE_AUTOSTART" = "1" ]; then
    cat > "$HOME/.config/autostart/ipgs-kiosk.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=iPGS Kiosk
Exec=$APP_EXEC
EOF
    echo "  → Đã tạo $HOME/.config/autostart/ipgs-kiosk.desktop (Exec=$APP_EXEC)"
else
    rm -f "$HOME/.config/autostart/ipgs-kiosk.desktop"
    echo "  → Autostart + Watchdog đều TẮT: đã xóa autostart .desktop của app (nếu có)."
fi

# unclutter.desktop KHÔNG còn do ô Autostart quản lý — thuộc ô "Cài unclutter" (tab Config
# máy tính, 1-install-software.sh), tránh 2 ô cùng tạo/xoá 1 file và đá nhau.

# Desktop icon LXQt: pcmanfm-qt chạy .desktop trên ~/Desktop chỉ cần chmod +x (không
# cần "gio set trusted" — đó là cơ chế riêng của GNOME Files/Nautilus).
for _kz in "$HOME/Desktop/"*.desktop; do
    [ -f "$_kz" ] || continue
    chmod +x "$_kz"
done
echo "  → Đã chmod +x mọi icon .desktop trên $HOME/Desktop (pcmanfm-qt không cần cờ trusted riêng)."

# ─────────────────────────────────────────────────────────────
fi # [DEPLOY_SCOPE != machine]
# [6/9] F09-tương-đương — Khoá phím tắt thoát kiosk trong Openbox rc.xml.
#
# KHÁC BIỆT QUAN TRỌNG SO VỚI BẢN GNOME: dconf có cơ chế LOCK cấp hệ thống (user
# không ghi đè được dù có gsettings). Openbox KHÔNG có cơ chế tương đương — rc.xml là
# 1 file cấu hình thường, user (hoặc app nào đó) vẫn có thể sửa lại nếu có quyền ghi.
# Giảm nhẹ bằng cách set file thành 444 + chattr +i (immutable, cần root để gỡ) sau khi
# sửa — không tuyệt đối bằng dconf lock nhưng đủ chặn thao tác vô tình/qua GUI.
# FIX: trước đây khối này lồng if sai (nhánh "không tìm thấy rc.xml" thực ra chạy khi deploy
# tab phần mềm, phần [6b] chạy cả khi deploy tab phần mềm và chỉ chạy khi có rc.xml), và
# bỏ tick "Khoá lối thoát" KHÔNG trả lại các keybind Openbox đã xoá. Giờ: keybind bị xoá
# được cất vào $OB_KEYBIND_STORE, bỏ tick → chèn lại đúng các keybind đó.
OB_KEYBIND_STORE="$OB_DIR/kiosk-removed-keybinds.xml"
if [ "$DEPLOY_SCOPE" != "software" ]; then
if [ -f "$OB_RC" ]; then
    _sudo chattr -i "$OB_RC" 2>/dev/null || true
    chmod u+w "$OB_RC" 2>/dev/null || true
    if [ "$LOCKDOWN_SHELL" = "1" ]; then
        echo "=== [6/9] Khoá phím tắt thoát kiosk (Openbox rc.xml) ==="
        # Xoá các <keybind key="..."> nguy hiểm (Alt+F4, Alt+Tab, Ctrl+Alt+T, Alt+F2,
        # Alt+Space, phím Super...) — parse XML bằng python3 thay vì sed đa dòng. Keybind
        # gốc được cất vào $OB_KEYBIND_STORE để bỏ tick là trả lại được.
        if command -v python3 >/dev/null 2>&1; then
            python3 - "$OB_RC" "$OB_KEYBIND_STORE" <<'PYEOF'
import os, sys, xml.etree.ElementTree as ET

path, store = sys.argv[1], sys.argv[2]
NS = 'http://openbox.org/3.4/rc'
DANGEROUS_KEYS = {
    "A-F4", "A-Tab", "A-S-Tab", "C-A-T", "A-F2", "A-space",
    "W", "A-grave", "C-A-Escape", "C-A-Delete",
}
try:
    ET.register_namespace('', NS)
    tree = ET.parse(path)
    root = tree.getroot()
    saved = {}
    if os.path.isfile(store):
        try:
            for kb in ET.parse(store).getroot():
                saved.setdefault(kb.get('key', ''), kb)
        except Exception:
            pass
    removed = 0
    for keyboard in root.iter('{%s}keyboard' % NS):
        for kb in list(keyboard.findall('{%s}keybind' % NS)):
            key = kb.get('key', '')
            if key in DANGEROUS_KEYS:
                saved.setdefault(key, kb)
                keyboard.remove(kb)
                removed += 1
    tree.write(path, xml_declaration=True, encoding='UTF-8')
    if saved:
        holder = ET.Element('{%s}removed' % NS)
        for kb in saved.values():
            holder.append(kb)
        ET.ElementTree(holder).write(store, xml_declaration=True, encoding='UTF-8')
    print(f"  -> Da xoa {removed} keybind nguy hiem (cat ban goc vao {store}).")
except Exception as e:
    print(f"CANH BAO: khong parse duoc {path} bang python3 ({e}) - bo qua buoc khoa phim tat.", file=sys.stderr)
PYEOF
        else
            echo "CẢNH BÁO: không có python3 — bỏ qua khoá phím tắt Openbox (cần python3 để parse XML an toàn)." >&2
        fi

        # Chạy qua SSH không có DISPLAY, openbox in "Failed to open the display" ra STDOUT
        # → nuốt cả stdout lẫn stderr.
        openbox --reconfigure >/dev/null 2>&1 || true
        chmod 444 "$OB_RC" 2>/dev/null || true
        _sudo chattr +i "$OB_RC" 2>/dev/null || echo "  CẢNH BÁO: không set immutable (chattr +i) — filesystem có thể không hỗ trợ (VD overlayfs)." >&2
        echo "  → Đã áp dụng + đặt $OB_RC read-only (444 + chattr +i nếu hỗ trợ)."
    else
        echo "=== [6/9] GỠ khoá phím tắt Openbox (trả lại keybind đã xoá) ==="
        if [ -f "$OB_KEYBIND_STORE" ] && command -v python3 >/dev/null 2>&1; then
            python3 - "$OB_RC" "$OB_KEYBIND_STORE" <<'PYEOF'
import os, sys, xml.etree.ElementTree as ET

path, store = sys.argv[1], sys.argv[2]
NS = 'http://openbox.org/3.4/rc'
try:
    ET.register_namespace('', NS)
    tree = ET.parse(path)
    root = tree.getroot()
    keyboard = root.find('{%s}keyboard' % NS)
    if keyboard is None:
        keyboard = ET.SubElement(root, '{%s}keyboard' % NS)
    existing = {kb.get('key', '') for kb in keyboard.findall('{%s}keybind' % NS)}
    restored = 0
    for kb in ET.parse(store).getroot():
        if kb.get('key', '') not in existing:
            keyboard.append(kb)
            restored += 1
    tree.write(path, xml_declaration=True, encoding='UTF-8')
    os.remove(store)
    print(f"  -> Da tra lai {restored} keybind (Openbox rc.xml).")
except Exception as e:
    print(f"CANH BAO: khong khoi phuc duoc keybind ({e}) - xem {store} va {path}.bak-*.", file=sys.stderr)
PYEOF
        else
            echo "  → Không có keybind nào bị kiosk xoá trước đó — chỉ gỡ read-only."
        fi
        openbox --reconfigure >/dev/null 2>&1 || true
        echo "  → Đã gỡ read-only khỏi $OB_RC."
    fi
else
    echo "CẢNH BÁO: không tìm thấy $OB_RC — bỏ qua khoá/gỡ khoá phím tắt Openbox." >&2
fi

# ─────────────────────────────────────────────────────────────
# [6b/9] Khoá lối thoát THẬT SỰ của LXQt — daemon lxqt-globalkeyshortcutsd.
#
# XÁC MINH THẬT trên Lubuntu 22.04 (192.168.21.39): Openbox rc.xml ở trên chỉ khoá
# được phím tắt cấp WINDOW MANAGER (Alt+F4, Alt+Tab...). Các phím tắt NGUY HIỂM NHẤT
# để thoát kiosk lại nằm ở NƠI KHÁC — daemon riêng của LXQt, cấu hình tại
# ~/.config/lxqt/globalkeyshortcuts.conf (sinh ra ở lần đăng nhập desktop đầu tiên từ
# mẫu /etc/xdg/lxqt/globalkeyshortcuts.conf). Trên máy test thật, các entry mặc định
# nguy hiểm gồm:
#   Control+Alt+T   → mở qterminal (Terminal!)
#   Control+Alt+Delete → mở qps (Task Manager)
#   Meta+E          → mở pcmanfm-qt (File Manager)
#   Meta+R          → show/hide Runner (app launcher gõ lệnh chạy bất kỳ app nào)
#   Super_L         → show/hide main menu (mở được mọi app cài trên máy)
# Đây MỚI LÀ bề mặt thoát kiosk thực sự trên LXQt — quan trọng hơn Openbox rc.xml.
GKS_CONF="$HOME/.config/lxqt/globalkeyshortcuts.conf"
if [ ! -f "$GKS_CONF" ]; then
    mkdir -p "$HOME/.config/lxqt"
    for _tmpl in /etc/xdg/lxqt/globalkeyshortcuts.conf/globalkeyshortcuts.conf /etc/xdg/lxqt/globalkeyshortcuts.conf; do
        if [ -f "$_tmpl" ]; then
            cp "$_tmpl" "$GKS_CONF"
            echo "  → Đã khởi tạo $GKS_CONF từ mẫu hệ thống $_tmpl."
            break
        fi
    done
fi

if [ -f "$GKS_CONF" ]; then
    if command -v python3 >/dev/null 2>&1; then
        cp "$GKS_CONF" "$GKS_CONF.bak-$(date +%Y%m%d%H%M%S)"
        if [ "$LOCKDOWN_SHELL" = "1" ]; then
            echo "=== [6b/9] Khoá phím tắt thoát kiosk thật (lxqt-globalkeyshortcuts) ==="
        else
            echo "=== [6b/9] GỠ khoá phím tắt thật (chế độ bảo trì) ==="
        fi
        # 1 = khoá (Enabled=false), 0 = bật lại (Enabled=true) — cùng 1 đoạn code 2 chiều.
        python3 - "$GKS_CONF" "$LOCKDOWN_SHELL" <<'PYEOF'
import sys, configparser

path, hide = sys.argv[1], sys.argv[2] == "1"
cp = configparser.ConfigParser(interpolation=None)
cp.optionxform = str  # giữ nguyên hoa/thường của key
cp.read(path, encoding="utf-8")
# Section dạng "<phím>.<số thứ tự>" — so khớp theo phần phím (trước dấu chấm cuối).
DANGEROUS_PREFIXES = {
    "Control%2BAlt%2BT",        # Ctrl+Alt+T - mo Terminal
    "Control%2BAlt%2BDelete",   # Ctrl+Alt+Delete - Task Manager
    "Meta%2BE",                 # Meta+E - mo File Manager
    "Meta%2BR",                 # Meta+R - Runner (go lenh chay app bat ky)
    "Super_L",                  # phim Super - mo main menu
}
want = "false" if hide else "true"
changed = 0
for section in cp.sections():
    prefix = section.rsplit(".", 1)[0]
    if prefix in DANGEROUS_PREFIXES and cp.has_option(section, "Enabled"):
        if cp.get(section, "Enabled") != want:
            cp.set(section, "Enabled", want)
            changed += 1
with open(path, "w", encoding="utf-8") as f:
    cp.write(f, space_around_delimiters=False)
print(f"  -> Da doi Enabled={want} cho {changed} phim tat (lxqt-globalkeyshortcuts).")
PYEOF
        # lxqt-globalkeyshortcutsd chỉ đọc file lúc khởi động — kill để lxqt-session respawn
        # (best-effort; chắc chắn nhất là đăng nhập lại).
        pkill -x lxqt-globalkeyshortcutsd 2>/dev/null || true
        echo "  → Đã ghi $GKS_CONF (hiệu lực ngay nếu daemon tự respawn, chắc chắn nhất là đăng nhập lại)."
        echo "  Backup: $GKS_CONF.bak-*"
    else
        echo "CẢNH BÁO: không có python3 — bỏ qua khoá/gỡ khoá phím tắt lxqt-globalkeyshortcuts." >&2
    fi
else
    echo "CẢNH BÁO: không tìm thấy $GKS_CONF hoặc mẫu hệ thống — bỏ qua bước khoá phím tắt thật. Đăng nhập vào LXQt 1 lần rồi chạy lại." >&2
fi
fi # [DEPLOY_SCOPE != software]

# ─────────────────────────────────────────────────────────────
# [7/9] Watchdog systemd USER service — CƠ CHẾ Y HỆT bản GNOME (systemd --user không
# phụ thuộc desktop environment, tái sử dụng nguyên logic).
WATCHDOG_UNIT_NAME="ipgs-kiosk-app.service"
if [ "$DEPLOY_SCOPE" != "machine" ]; then
WATCHDOG_UNIT="$HOME/.config/systemd/user/$WATCHDOG_UNIT_NAME"

if [ "$ENABLE_WATCHDOG" = "1" ]; then
    echo "=== [7/9] Watchdog systemd: tự khởi động lại app kiosk khi đóng/crash ==="
    mkdir -p "$HOME/.config/systemd/user"

    REAL_EXEC="$(_get_real_exec "$APP_EXEC")"
    REAL_EXEC_DIR="$(dirname "$REAL_EXEC")"
    echo "  → Kiểm tra binary: $REAL_EXEC"
    if [ ! -f "$REAL_EXEC" ] || [ ! -x "$REAL_EXEC" ]; then
        echo "LỖI: không tìm thấy binary '$REAL_EXEC' (tồn tại + có quyền thực thi)." >&2
        exit 1
    fi

    cat > "$WATCHDOG_UNIT" <<EOF
[Unit]
Description=iPGS Kiosk App Watchdog (tu khoi dong lai khi dong/crash)
After=graphical-session.target
PartOf=graphical-session.target
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
    echo "  → Đã ghi $WATCHDOG_UNIT"

    systemctl --user daemon-reload 2>/dev/null || true
    systemctl --user enable "$WATCHDOG_UNIT_NAME" 2>/dev/null || true

    if systemctl --user cat "$WATCHDOG_UNIT_NAME" >/dev/null 2>&1; then
        WD_STATE="$(systemctl --user is-enabled "$WATCHDOG_UNIT_NAME" 2>/dev/null || echo 'unknown')"
        echo "  → WATCHDOG-VERIFIED: unit hợp lệ, is-enabled=$WD_STATE."
    else
        echo "CẢNH BÁO: không đọc lại được unit watchdog qua systemctl --user." >&2
    fi
else
    echo "=== [7/9] GỠ watchdog systemd app kiosk (không chọn) ==="
    if [ -f "$WATCHDOG_UNIT" ]; then
        systemctl --user disable --now "$WATCHDOG_UNIT_NAME" 2>/dev/null || true
        rm -f "$WATCHDOG_UNIT"
        systemctl --user daemon-reload 2>/dev/null || true
        echo "  → Đã gỡ watchdog service."
    else
        echo "  → Chưa từng cài watchdog — bỏ qua."
    fi
fi

# ─────────────────────────────────────────────────────────────
fi # [DEPLOY_SCOPE != machine]
# [8/9] Tường lửa ufw. LUÔN allow OpenSSH TRƯỚC khi enable — nếu quên bước này, bật
# ufw trên máy chỉ có SSH sẽ tự khoá luôn quyền truy cập từ xa (không có GUI để mở lại
# nếu kỹ thuật viên đang thao tác qua SSH).
# FIX: trước đây 2 dòng if bị đảo thứ tự → bỏ tick Firewall thì KHÔNG ufw disable, còn
# tick Firewall + "Deploy Config phần mềm" lại chạy nhánh else → ufw disable nhầm.
if [ "$DEPLOY_SCOPE" != "software" ]; then
if [ "$ENABLE_FIREWALL" = "1" ]; then
    echo "=== [8/9] Bật tường lửa ufw ==="
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
    echo "=== [8/9] Tắt tường lửa ufw (giữ nguyên rule, chỉ ufw disable) ==="
    command -v ufw >/dev/null 2>&1 && _sudo ufw disable 2>&1 || echo "  → Chưa cài ufw — bỏ qua."
fi

# ─────────────────────────────────────────────────────────────
fi # [DEPLOY_SCOPE != software]
echo ""
echo "✓ HOÀN THÀNH. Cần LOG OUT / RESTART để áp dụng đầy đủ (đặc biệt autologin SDDM/LightDM + autostart)."
echo "  Kiểm tra sau khi restart:"
echo "    - Máy tự vào thẳng user '$KIOSK_USER' không cần đăng nhập"
echo "    - App '$APP_EXEC' tự chạy fullscreen"
