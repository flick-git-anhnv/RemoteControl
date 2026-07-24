#!/bin/bash
# 2-configure-system.sh â€” CÃ¡c tinh chá»‰nh há»‡ thá»‘ng cÃ²n láº¡i cho kiosk iPGS
# (KHÃ”NG cÃ i thÃªm pháº§n má»m nÃ o â€” pháº§n cÃ i Ä‘áº·t náº±m á»Ÿ 1-install-software.sh).
#
# Bao gá»“m:
#   - Táº¯t hot corner
#   - Táº¯t notification banner
#   - Táº¯t khÃ³a mÃ n hÃ¬nh / screensaver
#   - Táº¯t Ubuntu Dock + desktop icons (dock trÃ¡i, icon Trash/Home) â€” quan trá»ng
#     vá»›i mÃ¡y má»›i cÃ i Ubuntu Desktop vÃ¬ 2 extension nÃ y báº­t máº·c Ä‘á»‹nh
#   - Cháº·n suspend/sleep khi cáº¯m Ä‘iá»‡n (trÃ¡nh mÃ n hÃ¬nh táº¯t giá»¯a chá»«ng)
#   - Táº¯t popup Software Updater (trÃ¡nh giÃ¡n Ä‘oáº¡n kiosk khi cÃ³ báº£n vÃ¡ má»›i)
#   - Bá» qua mÃ n hÃ¬nh gnome-initial-setup (há»¯u Ã­ch khi user kiosk vá»«a táº¡o má»›i)
#   - Autologin GDM cho user kiosk
#   - Autostart app iPGS fullscreen + unclutter khi vÃ o desktop
#
# Báº®T BUá»˜C cháº¡y SAU khi Ä‘Ã£ cháº¡y xong 1-install-software.sh (cáº§n unclutter Ä‘Ã£
# cÃ i Ä‘á»ƒ autostart unclutter.desktop hoáº¡t Ä‘á»™ng).
#
# Cháº¡y (máº·c Ä‘á»‹nh báº­t táº¥t cáº£):
#   bash scripts/linux-kiosk/2-configure-system.sh [kiosk_user] [app_exec]
#
# Cháº¡y cÃ³ chá»n lá»c tá»«ng má»¥c (tham sá»‘ 3-9: 1=báº­t, 0=bá» qua, máº·c Ä‘á»‹nh 1 náº¿u khÃ´ng truyá»n):
#   bash scripts/linux-kiosk/2-configure-system.sh <kiosk_user> <app_exec> \
#        <disable_hotcorner> <disable_dock_icons> <block_sleep> \
#        <skip_initial_setup> <enable_autologin> <disable_sw_update> <enable_autostart>
#
# Tham sá»‘ (Ä‘á»u cÃ³ default):
#   kiosk_user          Máº·c Ä‘á»‹nh: kztek â€” user dÃ¹ng Ä‘á»ƒ autologin GDM.
#   app_exec            Máº·c Ä‘á»‹nh: ipgskioskavalonia (lá»‡nh cÃ³ sáºµn trong PATH sau khi
#                       cÃ i .deb tá»« scripts/linux-deb/build-deb.sh)
#   disable_hotcorner   Táº¯t hot corner/notification banner/khÃ³a mÃ n hÃ¬nh/idle-delay
#   disable_dock_icons  Táº¯t Ubuntu Dock + Desktop Icons
#   block_sleep         Cháº·n suspend/sleep khi cáº¯m Ä‘iá»‡n
#   skip_initial_setup  Bá» qua mÃ n hÃ¬nh gnome-initial-setup
#   enable_autologin    Autologin GDM cho kiosk_user
#   disable_sw_update   Táº¯t popup + auto-download Software Updater
#   enable_autostart    Autostart app iPGS + unclutter khi vÃ o desktop
#   lock_single_workspace  KhÃ³a cÃ²n 1 workspace tÄ©nh (tham sá»‘ 10) â€” cháº·n triá»‡t Ä‘á»ƒ lá»—i
#                       cá»­ chá»‰ 2/3 ngÃ³n trÃªn mÃ n cáº£m á»©ng bá»‹ Mutter hiá»ƒu thÃ nh gesture
#                       chuyá»ƒn workspace, lÃ m app fullscreen "biáº¿n máº¥t" sang workspace
#                       khÃ¡c. Set dynamic-workspaces=false + num-workspaces=1 thÃ¬
#                       gesture váº«n kÃ­ch hoáº¡t nhÆ°ng khÃ´ng cÃ²n workspace nÃ o Ä‘á»ƒ chuyá»ƒn tá»›i.
#
# VÃ­ dá»¥:
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

echo "=== [2] Cáº¥u hÃ¬nh há»‡ thá»‘ng cho Kiosk iPGS â€” Ubuntu 22.04 ==="
echo "  Kiosk user : $KIOSK_USER"
echo "  App exec   : $APP_EXEC"
echo "  HotCorner=$DISABLE_HOTCORNER DockIcons=$DISABLE_DOCK_ICONS Sleep=$BLOCK_SLEEP InitialSetup=$SKIP_INITIAL_SETUP Autologin=$ENABLE_AUTOLOGIN SwUpdate=$DISABLE_SW_UPDATE Autostart=$ENABLE_AUTOSTART LockWorkspace=$LOCK_SINGLE_WORKSPACE"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "Lá»–I: Ä‘á»«ng cháº¡y script báº±ng 'sudo bash ...' hay user root." >&2
    echo "     Cháº¡y trá»±c tiáº¿p: bash scripts/linux-kiosk/2-configure-system.sh" >&2
    exit 1
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# 2 chiá»u: 1 = táº¯t/áº©n, 0 = báº­t/hiá»‡n láº¡i nhÆ° máº·c Ä‘á»‹nh GNOME
if [ "$DISABLE_HOTCORNER" = "1" ]; then
    echo "=== [1/8] Táº¯t hot corner, notification banner, screensaver/lock ==="
    gsettings set org.gnome.desktop.interface enable-hot-corners false
    gsettings set org.gnome.desktop.notifications show-banners false
    gsettings set org.gnome.desktop.screensaver lock-enabled false
    gsettings set org.gnome.desktop.session idle-delay 0
else
    echo "=== [1/8] Báº­t láº¡i hot corner, notification banner, screensaver/lock (máº·c Ä‘á»‹nh) ==="
    gsettings set org.gnome.desktop.interface enable-hot-corners true
    gsettings set org.gnome.desktop.notifications show-banners true
    gsettings set org.gnome.desktop.screensaver lock-enabled true
    gsettings set org.gnome.desktop.session idle-delay 300
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if [ "$LOCK_SINGLE_WORKSPACE" = "1" ]; then
    echo "=== [2/8] KhÃ³a cÃ²n 1 workspace tÄ©nh (cháº·n gesture 2/3 ngÃ³n chuyá»ƒn workspace) ==="
    gsettings set org.gnome.mutter dynamic-workspaces false
    gsettings set org.gnome.desktop.wm.preferences num-workspaces 1 2>/dev/null || true
    gsettings set org.gnome.shell.overrides workspaces-only-on-primary true 2>/dev/null || true
else
    echo "=== [2/8] Báº­t láº¡i workspace Ä‘á»™ng (máº·c Ä‘á»‹nh GNOME) ==="
    gsettings set org.gnome.mutter dynamic-workspaces true
    gsettings reset org.gnome.desktop.wm.preferences num-workspaces 2>/dev/null || true
    gsettings reset org.gnome.shell.overrides workspaces-only-on-primary 2>/dev/null || true
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if [ "$DISABLE_DOCK_ICONS" = "1" ]; then
    echo "=== [3/8] Táº¯t Ubuntu Dock + Desktop Icons (máº·c Ä‘á»‹nh báº­t trÃªn mÃ¡y má»›i) ==="
    gnome-extensions disable ubuntu-dock@ubuntu.com 2>/dev/null || echo "  â†’ ubuntu-dock@ubuntu.com khÃ´ng cÃ³/Ä‘Ã£ táº¯t, bá» qua."
    gnome-extensions disable ding@rastersoft.com 2>/dev/null || echo "  â†’ ding@rastersoft.com khÃ´ng cÃ³/Ä‘Ã£ táº¯t, bá» qua."
else
    echo "=== [3/8] Báº­t láº¡i Ubuntu Dock + Desktop Icons ==="
    gnome-extensions enable ubuntu-dock@ubuntu.com 2>/dev/null || echo "  â†’ ubuntu-dock@ubuntu.com khÃ´ng cÃ³, bá» qua."
    gnome-extensions enable ding@rastersoft.com 2>/dev/null || echo "  â†’ ding@rastersoft.com khÃ´ng cÃ³, bá» qua."
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if [ "$BLOCK_SLEEP" = "1" ]; then
    echo "=== [4/8] Cháº·n suspend/sleep khi cáº¯m Ä‘iá»‡n (trÃ¡nh mÃ n hÃ¬nh táº¯t giá»¯a chá»«ng) ==="
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'nothing'
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'nothing' 2>/dev/null || true
else
    echo "=== [4/8] Báº­t láº¡i suspend/sleep máº·c Ä‘á»‹nh ==="
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'suspend'
    gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'suspend' 2>/dev/null || true
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if [ "$SKIP_INITIAL_SETUP" = "1" ]; then
    echo "=== [5/8] Bá» qua mÃ n hÃ¬nh gnome-initial-setup (náº¿u user vá»«a táº¡o má»›i) ==="
    mkdir -p "$HOME/.config"
    touch "$HOME/.config/gnome-initial-setup-done"
    echo "  â†’ ÄÃ£ Ä‘Ã¡nh dáº¥u gnome-initial-setup-done cho '$KIOSK_USER'."
else
    echo "=== [5/8] Bá» Ä‘Ã¡nh dáº¥u gnome-initial-setup-done (mÃ n hÃ¬nh initial-setup sáº½ hiá»‡n láº¡i) ==="
    rm -f "$HOME/.config/gnome-initial-setup-done"
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if [ "$ENABLE_AUTOLOGIN" = "1" ]; then
    echo "=== [6/8] Autologin GDM cho user '$KIOSK_USER' ==="
    GDM_CONF="/etc/gdm3/custom.conf"
    if [ -f "$GDM_CONF" ]; then
        sudo cp "$GDM_CONF" "$GDM_CONF.bak-$(date +%Y%m%d%H%M%S)" 2>/dev/null || true
        if sudo grep -q "^AutomaticLoginEnable" "$GDM_CONF"; then
            sudo sed -i "s/^AutomaticLoginEnable.*/AutomaticLoginEnable = true/" "$GDM_CONF"
        else
            sudo sed -i "/^\[daemon\]/a AutomaticLoginEnable = true" "$GDM_CONF"
        fi
        if sudo grep -q "^AutomaticLogin " "$GDM_CONF"; then
            sudo sed -i "s/^AutomaticLogin .*/AutomaticLogin = $KIOSK_USER/" "$GDM_CONF"
        else
            sudo sed -i "/^AutomaticLoginEnable/a AutomaticLogin = $KIOSK_USER" "$GDM_CONF"
        fi
        echo "  â†’ ÄÃ£ cáº­p nháº­t $GDM_CONF (backup: $GDM_CONF.bak-*)"
    else
        echo "Cáº¢NH BÃO: khÃ´ng tÃ¬m tháº¥y $GDM_CONF â€” bá» qua bÆ°á»›c autologin, cáº¥u hÃ¬nh thá»§ cÃ´ng sau." >&2
    fi
else
    echo "=== [6/8] Táº¯t autologin GDM ==="
    GDM_CONF="/etc/gdm3/custom.conf"
    if [ -f "$GDM_CONF" ]; then
        sudo cp "$GDM_CONF" "$GDM_CONF.bak-$(date +%Y%m%d%H%M%S)" 2>/dev/null || true
        sudo sed -i "s/^AutomaticLoginEnable.*/AutomaticLoginEnable = false/" "$GDM_CONF" 2>/dev/null || true
        echo "  â†’ ÄÃ£ táº¯t autologin trong $GDM_CONF (backup: $GDM_CONF.bak-*)"
    else
        echo "Cáº¢NH BÃO: khÃ´ng tÃ¬m tháº¥y $GDM_CONF â€” bá» qua." >&2
    fi
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if [ "$DISABLE_SW_UPDATE" = "1" ]; then
    echo "=== [7/8] Táº¯t popup Software Updater ==="
    mkdir -p "$HOME/.config/autostart"
    if [ -f /etc/xdg/autostart/update-notifier.desktop ]; then
        cat > "$HOME/.config/autostart/update-notifier.desktop" <<EOF
[Desktop Entry]
Hidden=true
EOF
        echo "  â†’ ÄÃ£ áº©n autostart update-notifier cho user hiá»‡n táº¡i."
    else
        echo "  â†’ KhÃ´ng tháº¥y update-notifier.desktop, bá» qua."
    fi
    gsettings set org.gnome.software download-updates false 2>/dev/null || true
else
    echo "=== [7/8] Bá» qua (khÃ´ng chá»n) ==="
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if [ "$ENABLE_AUTOSTART" = "1" ]; then
    echo "=== [8/8] Autostart app iPGS fullscreen + unclutter khi vÃ o desktop ==="
    mkdir -p "$HOME/.config/autostart"

    cat > "$HOME/.config/autostart/ipgs-kiosk.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=iPGS Kiosk
Exec=$APP_EXEC
X-GNOME-Autostart-enabled=true
EOF
    echo "  â†’ ÄÃ£ táº¡o $HOME/.config/autostart/ipgs-kiosk.desktop (Exec=$APP_EXEC)"
    echo "    Náº¿u \$APP_EXEC chÆ°a Ä‘Ãºng, sá»­a láº¡i field Exec= trong file trÃªn."

    if command -v unclutter >/dev/null 2>&1; then
        cat > "$HOME/.config/autostart/unclutter.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Unclutter
Exec=unclutter -idle 1
X-GNOME-Autostart-enabled=true
EOF
        echo "  â†’ ÄÃ£ táº¡o $HOME/.config/autostart/unclutter.desktop"
    else
        echo "Cáº¢NH BÃO: chÆ°a tháº¥y lá»‡nh 'unclutter' â€” cháº¡y 1-install-software.sh trÆ°á»›c khi cháº¡y script nÃ y." >&2
    fi
else
    echo "=== [8/8] Bá» qua (khÃ´ng chá»n) ==="
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
echo ""
echo "âœ“ HOÃ€N THÃ€NH. Cáº§n LOG OUT / RESTART Ä‘á»ƒ Ã¡p dá»¥ng Ä‘áº§y Ä‘á»§ (Ä‘áº·c biá»‡t autologin GDM + autostart)."
echo "  Kiá»ƒm tra sau khi restart:"
echo "    - MÃ¡y tá»± vÃ o tháº³ng user '$KIOSK_USER' khÃ´ng cáº§n Ä‘Äƒng nháº­p"
echo "    - App '$APP_EXEC' tá»± cháº¡y fullscreen"
