#!/bin/bash
# 3-toggle-panel.sh (Lubuntu/LXQt) — Ẩn HOẶC hiện lại Panel LXQt + Desktop Icons bất kỳ
# lúc nào, không cần cài lại phần mềm (tương đương 3-toggle-topbar.sh bản GNOME).
#
# Chạy:
#   bash scripts/lubuntu-kiosk/3-toggle-panel.sh hide   # ẩn panel + desktop icons
#   bash scripts/lubuntu-kiosk/3-toggle-panel.sh show   # hiện lại như mặc định Lubuntu

set -e

MODE="$1"
if [ "$MODE" != "hide" ] && [ "$MODE" != "show" ]; then
    echo "Cách dùng: bash $0 {hide|show}" >&2
    exit 1
fi

mkdir -p "$HOME/.config/autostart"

_override_autostart() {
    local desktop_id="$1"
    local hide="$2"
    local override="$HOME/.config/autostart/$desktop_id"
    if [ "$hide" = "1" ]; then
        cat > "$override" <<EOF
[Desktop Entry]
Hidden=true
EOF
    else
        rm -f "$override"
    fi
}

if [ "$MODE" = "hide" ]; then
    HIDE_VAL="1"
    echo "=== Ẩn Panel LXQt + Desktop Icons ==="
else
    HIDE_VAL="0"
    echo "=== Hiện lại Panel LXQt + Desktop Icons (mặc định Lubuntu) ==="
fi

for _panel_id in lxqt-panel.desktop lxqtpanel.desktop; do
    [ -f "/etc/xdg/autostart/$_panel_id" ] && _override_autostart "$_panel_id" "$HIDE_VAL"
done
for _pcmanfm_id in lxqt-desktop.desktop pcmanfm-qt-desktop-pref.desktop; do
    [ -f "/etc/xdg/autostart/$_pcmanfm_id" ] && _override_autostart "$_pcmanfm_id" "$HIDE_VAL"
done

# Nếu panel đang chạy trong phiên hiện tại, thử kill/relaunch ngay để không cần đăng
# nhập lại (best-effort — LXQt session sẽ tự respawn theo autostart ở lần login sau
# dù bước này thất bại).
if [ "$MODE" = "hide" ]; then
    pkill -x lxqt-panel 2>/dev/null || true
    pkill -x pcmanfm-qt 2>/dev/null || true
else
    if command -v lxqt-panel >/dev/null 2>&1 && ! pgrep -x lxqt-panel >/dev/null 2>&1; then
        nohup lxqt-panel >/dev/null 2>&1 &
    fi
    if command -v pcmanfm-qt >/dev/null 2>&1 && ! pgrep -x pcmanfm-qt >/dev/null 2>&1; then
        nohup pcmanfm-qt --desktop >/dev/null 2>&1 &
    fi
fi

echo "✓ Xong. Nếu panel/icon chưa đổi ngay trong phiên hiện tại, đăng nhập lại để chắc chắn áp dụng."
