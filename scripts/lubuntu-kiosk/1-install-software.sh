#!/bin/bash
# 1-install-software.sh (Lubuntu/LXQt) — Cài phần mềm/tinh chỉnh cần thiết + áp dụng
# lựa chọn ẩn/hiện UI cho kiosk iPGS trên Lubuntu (LXQt + Openbox), KHÔNG phải GNOME.
#
# ĐÂY LÀ BẢN SONG SONG của scripts/linux-kiosk/1-install-software.sh (bản gốc chỉ chạy
# đúng trên Ubuntu Desktop/GNOME Shell). LXQt KHÔNG có gnome-shell/gnome-extensions/
# gsettings schema của GNOME nên toàn bộ cơ chế "Just Perfection"/"Block Caribou" không
# áp dụng được — phải thay bằng cơ chế riêng của LXQt/Openbox bên dưới.
#
# CHƯA TEST TRÊN MÁY LUBUNTU THẬT (khác các script scripts/linux-kiosk/ đã verify nhiều
# lần trên ZCU thật) — trước khi dùng cho sản xuất, PHẢI chạy thử trên 1 máy Lubuntu
# thật rồi cập nhật lesson theo đúng quy trình (C:\Users\nguye\.claude\lessons\).
#
# Khác biệt quan trọng so với GNOME:
#   - Panel LXQt (lxqt-panel) ẩn/hiện bằng cách bật/tắt AUTOSTART của chính panel,
#     KHÔNG có khái niệm "extension ẩn panel" như GNOME. Ẩn hẳn panel = kill + không
#     autostart lxqt-panel nữa (khác với chỉ ẩn 1 phần top bar như Just Perfection).
#   - Desktop icons do pcmanfm-qt (--desktop) vẽ, tắt bằng cách gỡ autostart của nó
#     (không cần "trusted" flag như Nautilus/GNOME Files).
#   - LXQt mặc định KHÔNG có bàn phím ảo tự bật theo cảm ứng như GNOME Shell 42 (đây là
#     hành vi hard-code riêng của GNOME Shell, không tồn tại ở Openbox/LXQt) — nên KHÔNG
#     cần bước "chặn bàn phím ảo hệ thống" như bản GNOME. Nếu máy có cài sẵn "onboard"/
#     "florence" (virtual keyboard rời) và bị tự bật, xử lý riêng bằng cách gỡ autostart
#     của gói đó — không có ở đây vì Lubuntu mặc định không cài các gói này.
#
# Chạy (mặc định ẩn tất cả, không cần đối số):
#   bash scripts/lubuntu-kiosk/1-install-software.sh
#
# Chạy có chọn lọc từng mục (mỗi tham số 1=ẩn/bật, 0=hiện lại/bỏ qua, mặc định 1):
#   bash scripts/lubuntu-kiosk/1-install-software.sh <hide_panel> <hide_desktop_icons> <install_unclutter>
# Ví dụ: ẩn panel, HIỆN LẠI desktop icons, cài unclutter:
#   bash scripts/lubuntu-kiosk/1-install-software.sh 1 0 1

set -e

HIDE_PANEL="${1:-1}"
HIDE_DESKTOP_ICONS="${2:-1}"
INSTALL_UNCLUTTER="${3:-1}"

echo "=== [1] Cài phần mềm cho Kiosk iPGS — Lubuntu (LXQt/Openbox) ==="
echo "  Home hiện tại: $HOME"
echo "  Ẩn Panel=$HIDE_PANEL  ẨnDesktopIcons=$HIDE_DESKTOP_ICONS  Unclutter=$INSTALL_UNCLUTTER"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "LỖI: đừng chạy script bằng 'sudo bash ...' hay user root." >&2
    echo "     Chạy trực tiếp: bash scripts/lubuntu-kiosk/1-install-software.sh" >&2
    exit 1
fi

# Helper sudo cho SSH session không có TTY.
_sudo() {
    if [ -n "${KIOSK_SUDO_PASS:-}" ]; then
        echo "$KIOSK_SUDO_PASS" | sudo -S "$@" 2>/dev/null
    else
        sudo "$@"
    fi
}

# F17-tương-đương: cài OFFLINE bằng dpkg -i từ $KIOSK_DEB_OFFLINE_DIR nếu có (giữ đúng
# cơ chế nhúng .deb như bản Ubuntu/GNOME), fallback apt install khi không có.
_install_deb_offline_or_apt() {
    local pkg="$1"; shift
    local deb_glob="${KIOSK_DEB_OFFLINE_DIR:-}/${pkg}_amd64.deb"
    if [ -n "${KIOSK_DEB_OFFLINE_DIR:-}" ] && ls $deb_glob >/dev/null 2>&1; then
        local all_debs=("$deb_glob")
        local dep
        for dep in "$@"; do
            local dep_glob="${KIOSK_DEB_OFFLINE_DIR}/${dep}_amd64.deb"
            ls $dep_glob >/dev/null 2>&1 && all_debs+=("$dep_glob")
        done
        _sudo dpkg -i "${all_debs[@]}"
        if ! dpkg -s "$pkg" 2>/dev/null | grep -q "^Status: install ok installed"; then
            echo "  ⚠️ $pkg chưa cấu hình xong sau dpkg -i offline (lệch version dependency?) — thử sửa qua apt (cần mạng)..."
            _sudo apt-get install -f -y 2>&1 || echo "  ⚠️ Không sửa được offline — $pkg có thể chưa hoạt động đúng, cần kiểm tra tay."
        fi
    else
        _sudo apt install -y "$pkg"
    fi
}

echo "=== [1/3] Cài công cụ cần thiết (curl, unzip) ==="
if ! command -v curl >/dev/null 2>&1; then
    _install_deb_offline_or_apt curl libcurl4
else
    echo "  → curl đã có, bỏ qua."
fi
if ! command -v unzip >/dev/null 2>&1; then
    _install_deb_offline_or_apt unzip
else
    echo "  → unzip đã có, bỏ qua."
fi

# ─────────────────────────────────────────────────────────────
# [2/3] Ẩn/hiện Panel LXQt + Desktop Icons (pcmanfm-qt --desktop).
#
# LXQt KHÔNG có schema gsettings/dconf như GNOME — cấu hình autostart panel/desktop
# nằm trong file .desktop chuẩn XDG tại /etc/xdg/autostart (mặc định hệ thống) và
# $HOME/.config/autostart (override theo user, KHÔNG cần sudo, đúng scope "user hiện
# tại" như bản GNOME). Ghi "Hidden=true" đè lên bản hệ thống để tắt autostart, xoá file
# override để trả lại mặc định.
mkdir -p "$HOME/.config/autostart"

_override_autostart() {
    local desktop_id="$1"   # tên file .desktop, VD lxqt-panel.desktop
    local hide="$2"         # 1 = ẩn (Hidden=true), 0 = trả lại mặc định (xoá override)
    local override="$HOME/.config/autostart/$desktop_id"
    if [ "$hide" = "1" ]; then
        cat > "$override" <<EOF
[Desktop Entry]
Hidden=true
EOF
        echo "  → Đã ẩn autostart '$desktop_id' (override: $override)."
    else
        rm -f "$override"
        echo "  → Đã bỏ override autostart '$desktop_id' (trả lại mặc định hệ thống)."
    fi
}

echo "=== [2/3] Ẩn/hiện Panel LXQt + Desktop Icons ==="
# Panel LXQt: file autostart hệ thống thường là lxqt-panel.desktop (kiểm tra cả 2 tên
# phổ biến tuỳ bản đóng gói distro).
for _panel_id in lxqt-panel.desktop lxqtpanel.desktop; do
    if [ -f "/etc/xdg/autostart/$_panel_id" ]; then
        _override_autostart "$_panel_id" "$HIDE_PANEL"
    fi
done

# Desktop icons LXQt do pcmanfm-qt vẽ qua module "desktop", autostart bằng file
# lxqt-desktop.desktop (Exec=pcmanfm-qt --desktop --profile=lxqt — xác nhận thật trên
# Lubuntu 22.04/lxqt-session, KHÔNG phải "pcmanfm-qt-desktop-pref.desktop" như đoán ban
# đầu). Không cần "gio set trusted" như GNOME Files — pcmanfm-qt chạy icon .desktop
# trên Desktop được ngay sau khi chmod +x.
for _pcmanfm_id in lxqt-desktop.desktop pcmanfm-qt-desktop-pref.desktop; do
    if [ -f "/etc/xdg/autostart/$_pcmanfm_id" ]; then
        _override_autostart "$_pcmanfm_id" "$HIDE_DESKTOP_ICONS"
    fi
done

# ─────────────────────────────────────────────────────────────
if [ "$INSTALL_UNCLUTTER" = "1" ]; then
    echo "=== [3/3] Cài unclutter (ẩn con trỏ chuột) ==="
    if ! dpkg -s unclutter >/dev/null 2>&1; then
        _install_deb_offline_or_apt unclutter
    else
        echo "  → unclutter đã cài, bỏ qua."
    fi
else
    echo "=== [3/3] Bỏ qua cài unclutter (không được chọn — KHÔNG tự gỡ nếu đã cài trước đó) ==="
fi

echo ""
echo "✓ Xong phần cài phần mềm. Chạy tiếp: bash scripts/lubuntu-kiosk/2-configure-system.sh [kiosk_user] [app_exec]"
echo "  LƯU Ý: ẩn/hiện panel + desktop icon chỉ có hiệu lực ở lần đăng nhập kế tiếp"
echo "  (autostart chỉ được LXQt session đọc lúc khởi động phiên desktop)."
