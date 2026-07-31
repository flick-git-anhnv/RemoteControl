#!/bin/bash
# 3-toggle-topbar.sh ΓÇö ß║¿n HOß║╢C hiß╗çn lß║íi Top Bar/Dock GNOME bß║Ñt kß╗│ l├║c n├áo,
# kh├┤ng cß║ºn c├ái lß║íi phß║ºn mß╗üm (d├╣ng khi cß║ºn bß║¡t lß║íi giao diß╗çn ─æß║ºy ─æß╗º ─æß╗â debug,
# bß║úo tr├¼, hoß║╖c b├án giao m├íy).
#
# Chß║íy:
#   bash scripts/linux-kiosk/3-toggle-topbar.sh hide   # ß║⌐n top bar + dock + icon desktop
#   bash scripts/linux-kiosk/3-toggle-topbar.sh show   # hiß╗çn lß║íi nh╞░ mß║╖c ─æß╗ïnh Ubuntu

set -e

MODE="$1"
if [ "$MODE" != "hide" ] && [ "$MODE" != "show" ]; then
    echo "C├ích d├╣ng: bash $0 {hide|show}" >&2
    exit 1
fi

EXT_UUID="just-perfection-desktop@just-perfection"
EXT_DIR="$HOME/.local/share/gnome-shell/extensions/$EXT_UUID"
SCHEMA_DIR="$EXT_DIR/schemas"

if [ "$MODE" = "hide" ]; then
    VALUE="false"
    DOCK_ACTION="disable"
    echo "=== ß║¿n Top Bar + Dock + Desktop Icons ==="
else
    VALUE="true"
    DOCK_ACTION="enable"
    echo "=== Hiß╗çn lß║íi Top Bar + Dock + Desktop Icons ==="
fi

if [ -d "$SCHEMA_DIR" ]; then
    gsettings --schemadir "$SCHEMA_DIR" set org.gnome.shell.extensions.just-perfection panel $VALUE
    gsettings --schemadir "$SCHEMA_DIR" set org.gnome.shell.extensions.just-perfection activities-button $VALUE || true
    gsettings --schemadir "$SCHEMA_DIR" set org.gnome.shell.extensions.just-perfection workspace-switcher-should-show $VALUE || true
    gsettings --schemadir "$SCHEMA_DIR" set org.gnome.shell.extensions.just-perfection dash $VALUE || true
    echo "  ΓåÆ ─É├ú ─æß║╖t panel/activities-button/workspace-switcher/dash = $VALUE"
else
    echo "Cß║óNH B├üO: ch╞░a c├ái extension Just Perfection (chß║íy 1-install-software.sh tr╞░ß╗¢c)." >&2
fi

gnome-extensions $DOCK_ACTION ubuntu-dock@ubuntu.com 2>/dev/null || echo "  ΓåÆ ubuntu-dock@ubuntu.com kh├┤ng c├│, bß╗Å qua."
gnome-extensions $DOCK_ACTION ding@rastersoft.com 2>/dev/null || echo "  ΓåÆ ding@rastersoft.com kh├┤ng c├│, bß╗Å qua."

echo "Γ£ô Xong. C├│ thß╗â ├íp dß╗Ñng ngay, kh├┤ng cß║ºn restart."
