#!/bin/bash
# 1-install-software.sh â€” CÃ i pháº§n má»m/extension cáº§n thiáº¿t + Ã¡p dá»¥ng lá»±a chá»n
# áº©n/hiá»‡n UI GNOME cho kiosk iPGS.
#
# Pháº§n "pháº§n má»m" trong bá»™ setup kiosk iPGS (tÃ¡ch tá»« setup-kiosk.sh):
#   - CÃ i python3-pip + gnome-extensions-cli (gext)
#   - CÃ i + báº­t extension "Just Perfection"
#   - Compile schema + set cÃ¡c key áº©n/HIá»†N UI (panel/top bar, activities button,
#     dash, workspace switcher) â€” 2 CHIá»€U: 1=áº©n, 0=hiá»‡n láº¡i
#   - Táº¯t/Báº¬T Láº I bÃ n phÃ­m áº£o GNOME (on-screen keyboard) â€” 2 chiá»u, Ä‘á»™c láº­p extension
#   - CÃ i package "unclutter" (áº©n con trá» chuá»™t) â€” CHá»ˆ 1 CHIá»€U: 1=cÃ i, 0=bá» qua
#     (khÃ´ng tá»± gá»¡ khi = 0, vÃ¬ gá»¡ package lÃ  thao tÃ¡c phÃ¡ hoáº¡i/khÃ³ hoÃ n tÃ¡c)
#
# ÄÃ£ test thá»±c táº¿ trÃªn mÃ¡y kiosk 192.168.21.230 (Ubuntu 22.04, GNOME Shell 42).
#
# Báº®T BUá»˜C: cháº¡y trong phiÃªn desktop (GUI) tháº­t cá»§a user kiosk â€” khÃ´ng SSH thuáº§n,
# khÃ´ng "sudo bash ...â€ cáº£ script (script tá»± sudo khi cáº§n).
#
# GHI CHÃš QUAN TRá»ŒNG rÃºt ra tá»« láº§n test Ä‘áº§u: bÆ°á»›c "gext install <uuid>" gá»i
# D-Bus method InstallRemoteExtension cá»§a GNOME Shell â€” Shell sáº½ hiá»‡n 1 popup
# xÃ¡c nháº­n NGAY TRÃŠN MÃ€N HÃŒNH Váº¬T LÃ cá»§a mÃ¡y (khÃ´ng tháº¥y Ä‘Æ°á»£c qua SSH), vÃ  cÃ³
# timeout ngáº¯n (~24s). Náº¿u khÃ´ng cÃ³ ngÆ°á»i Ä‘á»©ng trÆ°á»›c mÃ n hÃ¬nh báº¥m "Install"
# ká»‹p, bÆ°á»›c nÃ y sáº½ bÃ¡o lá»—i "Timeout was reached (24)" â€” khÃ´ng pháº£i lá»—i script,
# chá»‰ cáº§n cháº¡y láº¡i (script idempotent, Ä‘Ã£ cÃ i rá»“i sáº½ tá»± bá» qua) sau khi Ä‘Ã£ báº¥m
# xÃ¡c nháº­n, hoáº·c Ä‘á»©ng trÆ°á»›c mÃ n hÃ¬nh kiosk khi cháº¡y láº§n Ä‘áº§u Ä‘á»ƒ báº¥m popup Ä‘Ã³.
#
# Cháº¡y (máº·c Ä‘á»‹nh áº©n táº¥t cáº£, khÃ´ng cáº§n Ä‘á»‘i sá»‘):
#   bash scripts/linux-kiosk/1-install-software.sh
#
# Cháº¡y cÃ³ chá»n lá»c tá»«ng má»¥c (má»—i tham sá»‘ 1=áº©n/báº­t, 0=hiá»‡n láº¡i/bá» qua, máº·c Ä‘á»‹nh 1):
#   bash scripts/linux-kiosk/1-install-software.sh <hide_topbar> <hide_activities> <hide_workspace> <hide_dash> <install_unclutter> <hide_keyboard>
# VÃ­ dá»¥: áº©n Top Bar, HIá»†N Láº I Activities/Workspace/Dash, cÃ i unclutter, HIá»†N Láº I bÃ n phÃ­m áº£o:
#   bash scripts/linux-kiosk/1-install-software.sh 1 0 0 0 1 0

set -e

HIDE_TOPBAR="${1:-1}"
HIDE_ACTIVITIES="${2:-1}"
HIDE_WORKSPACE="${3:-1}"
HIDE_DASH="${4:-1}"
INSTALL_UNCLUTTER="${5:-1}"
HIDE_KEYBOARD="${6:-1}"

EXT_UUID="just-perfection-desktop@just-perfection"
EXT_DIR="$HOME/.local/share/gnome-shell/extensions/$EXT_UUID"

echo "=== [1] CÃ i pháº§n má»m cho Kiosk iPGS â€” Ubuntu 22.04 ==="
echo "  Home hiá»‡n táº¡i: $HOME"
echo "  áº¨n Top Bar=$HIDE_TOPBAR  Activities=$HIDE_ACTIVITIES  Workspace=$HIDE_WORKSPACE  Dash=$HIDE_DASH  Unclutter=$INSTALL_UNCLUTTER  BÃ nPhÃ­máº¢o=$HIDE_KEYBOARD"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "Lá»–I: Ä‘á»«ng cháº¡y script báº±ng 'sudo bash ...' hay user root." >&2
    echo "     Cháº¡y trá»±c tiáº¿p: bash scripts/linux-kiosk/1-install-software.sh" >&2
    exit 1
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# Top Bar/Activities/Workspace/Dash giá» lÃ  toggle 2 CHIá»€U tháº­t sá»± (khÃ´ng cÃ²n kiá»ƒu
# "bá» qua náº¿u = 0") nÃªn LUÃ”N cáº§n extension Just Perfection cÃ i & báº­t, dÃ¹ Ä‘ang áº©n
# hay hiá»‡n láº¡i â€” vÃ¬ cáº£ 2 chiá»u Ä‘á»u Ä‘i qua gsettings cá»§a chÃ­nh extension Ä‘Ã³.
echo "=== [1/4] CÃ i python3-pip + gnome-extensions-cli ==="
if ! command -v pip3 >/dev/null 2>&1; then
    sudo apt install -y python3-pip
else
    echo "  â†’ pip3 Ä‘Ã£ cÃ³, bá» qua."
fi

export PATH="$HOME/.local/bin:$PATH"
if ! command -v gext >/dev/null 2>&1; then
    pip3 install --user gnome-extensions-cli
    export PATH="$HOME/.local/bin:$PATH"
else
    echo "  â†’ gext Ä‘Ã£ cÃ³, bá» qua."
fi

if ! grep -q '.local/bin' "$HOME/.bashrc" 2>/dev/null; then
    echo 'export PATH="$HOME/.local/bin:$PATH"' >> "$HOME/.bashrc"
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
echo "=== [2/4] CÃ i + báº­t extension Just Perfection ==="
if ! gnome-extensions list 2>/dev/null | grep -q "^$EXT_UUID$"; then
    echo "  â†’ LÆ¯U Ã: GNOME Shell sáº½ hiá»‡n popup xÃ¡c nháº­n trÃªn mÃ n hÃ¬nh â€” hÃ£y Ä‘á»©ng"
    echo "    trÆ°á»›c mÃ n hÃ¬nh kiosk vÃ  báº¥m 'Install' trong vÃ i giÃ¢y tá»›i."
    gext install "$EXT_UUID"
else
    echo "  â†’ Extension Ä‘Ã£ cÃ i, bá» qua bÆ°á»›c install."
fi
gnome-extensions enable "$EXT_UUID" || true

STATE="$(gnome-extensions info "$EXT_UUID" 2>/dev/null | grep 'State:' | awk '{print $2}')"
if [ "$STATE" != "ENABLED" ]; then
    echo "Cáº¢NH BÃO: extension chÆ°a á»Ÿ tráº¡ng thÃ¡i ENABLED (State: $STATE)." >&2
    echo "          CÃ³ thá»ƒ cáº§n log out/log in láº¡i rá»“i cháº¡y láº¡i script." >&2
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
echo "=== [3/4] Compile schema + Ã¡p dá»¥ng áº©n/hiá»‡n UI (2 chiá»u) ==="
if [ ! -d "$EXT_DIR/schemas" ]; then
    echo "Lá»–I: khÃ´ng tÃ¬m tháº¥y $EXT_DIR/schemas â€” extension cÃ³ cÃ i Ä‘Ãºng khÃ´ng?" >&2
    exit 1
fi
glib-compile-schemas "$EXT_DIR/schemas/"

# Helper: Ä‘á»•i "1"/"0" thÃ nh "false"/"true" (1 = áº©n = false, 0 = hiá»‡n = true)
to_visible_value() { [ "$1" = "1" ] && echo "false" || echo "true"; }

gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection panel "$(to_visible_value "$HIDE_TOPBAR")"
gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection activities-button "$(to_visible_value "$HIDE_ACTIVITIES")" || true
gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection workspace-switcher-should-show "$(to_visible_value "$HIDE_WORKSPACE")" || true
gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection dash "$(to_visible_value "$HIDE_DASH")" || true
echo "  â†’ panel=$(to_visible_value "$HIDE_TOPBAR") activities-button=$(to_visible_value "$HIDE_ACTIVITIES") workspace-switcher=$(to_visible_value "$HIDE_WORKSPACE") dash=$(to_visible_value "$HIDE_DASH")"
# Báº£n v26 khÃ´ng cÃ²n key "overview" riÃªng â€” Ä‘Ã£ bá» (xem GHI CHÃš á»Ÿ Ä‘áº§u file gá»‘c
# setup-kiosk.sh / docs/devops/KIOSK-SETUP-hide-topbar-ubuntu2204.md).

echo "=== BÃ n phÃ­m áº£o GNOME (2 chiá»u, Ä‘á»™c láº­p extension) ==="
if [ "$HIDE_KEYBOARD" = "1" ]; then
    gsettings set org.gnome.desktop.a11y.applications screen-keyboard-enabled false 2>/dev/null || true
    echo "  â†’ ÄÃ£ táº¯t bÃ n phÃ­m áº£o."
else
    gsettings set org.gnome.desktop.a11y.applications screen-keyboard-enabled true 2>/dev/null || true
    echo "  â†’ ÄÃ£ báº­t láº¡i bÃ n phÃ­m áº£o."
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if [ "$INSTALL_UNCLUTTER" = "1" ]; then
    echo "=== [4/4] CÃ i unclutter (áº©n con trá» chuá»™t) ==="
    if ! dpkg -s unclutter >/dev/null 2>&1; then
        sudo apt install -y unclutter
    else
        echo "  â†’ unclutter Ä‘Ã£ cÃ i, bá» qua."
    fi
else
    echo "=== [4/4] Bá» qua cÃ i unclutter (khÃ´ng Ä‘Æ°á»£c chá»n â€” KHÃ”NG tá»± gá»¡ náº¿u Ä‘Ã£ cÃ i trÆ°á»›c Ä‘Ã³) ==="
fi

echo ""
echo "âœ“ Xong pháº§n cÃ i pháº§n má»m. Cháº¡y tiáº¿p: bash scripts/linux-kiosk/2-configure-system.sh [kiosk_user] [app_exec]"
