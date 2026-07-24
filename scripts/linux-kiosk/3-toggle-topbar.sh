#!/bin/bash
# 3-toggle-topbar.sh â€” áº¨n HOáº¶C hiá»‡n láº¡i Top Bar/Dock GNOME báº¥t ká»³ lÃºc nÃ o,
# khÃ´ng cáº§n cÃ i láº¡i pháº§n má»m (dÃ¹ng khi cáº§n báº­t láº¡i giao diá»‡n Ä‘áº§y Ä‘á»§ Ä‘á»ƒ debug,
# báº£o trÃ¬, hoáº·c bÃ n giao mÃ¡y).
#
# Cháº¡y:
#   bash scripts/linux-kiosk/3-toggle-topbar.sh hide   # áº©n top bar + dock + icon desktop
#   bash scripts/linux-kiosk/3-toggle-topbar.sh show   # hiá»‡n láº¡i nhÆ° máº·c Ä‘á»‹nh Ubuntu

set -e

MODE="$1"
if [ "$MODE" != "hide" ] && [ "$MODE" != "show" ]; then
    echo "CÃ¡ch dÃ¹ng: bash $0 {hide|show}" >&2
    exit 1
fi

EXT_UUID="just-perfection-desktop@just-perfection"
EXT_DIR="$HOME/.local/share/gnome-shell/extensions/$EXT_UUID"
SCHEMA_DIR="$EXT_DIR/schemas"

if [ "$MODE" = "hide" ]; then
    VALUE="false"
    DOCK_ACTION="disable"
    echo "=== áº¨n Top Bar + Dock + Desktop Icons ==="
else
    VALUE="true"
    DOCK_ACTION="enable"
    echo "=== Hiá»‡n láº¡i Top Bar + Dock + Desktop Icons ==="
fi

if [ -d "$SCHEMA_DIR" ]; then
    gsettings --schemadir "$SCHEMA_DIR" set org.gnome.shell.extensions.just-perfection panel $VALUE
    gsettings --schemadir "$SCHEMA_DIR" set org.gnome.shell.extensions.just-perfection activities-button $VALUE || true
    gsettings --schemadir "$SCHEMA_DIR" set org.gnome.shell.extensions.just-perfection workspace-switcher-should-show $VALUE || true
    gsettings --schemadir "$SCHEMA_DIR" set org.gnome.shell.extensions.just-perfection dash $VALUE || true
    echo "  â†’ ÄÃ£ Ä‘áº·t panel/activities-button/workspace-switcher/dash = $VALUE"
else
    echo "Cáº¢NH BÃO: chÆ°a cÃ i extension Just Perfection (cháº¡y 1-install-software.sh trÆ°á»›c)." >&2
fi

gnome-extensions $DOCK_ACTION ubuntu-dock@ubuntu.com 2>/dev/null || echo "  â†’ ubuntu-dock@ubuntu.com khÃ´ng cÃ³, bá» qua."
gnome-extensions $DOCK_ACTION ding@rastersoft.com 2>/dev/null || echo "  â†’ ding@rastersoft.com khÃ´ng cÃ³, bá» qua."

echo "âœ“ Xong. CÃ³ thá»ƒ Ã¡p dá»¥ng ngay, khÃ´ng cáº§n restart."
