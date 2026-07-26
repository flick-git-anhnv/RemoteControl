#!/bin/bash
# 1-install-software.sh — Cài phần mềm/extension cần thiết + áp dụng lựa chọn
# ẩn/hiện UI GNOME cho kiosk iPGS.
#
# Phần "phần mềm" trong bộ setup kiosk iPGS (tách từ setup-kiosk.sh):
#   - Cài python3-pip + gnome-extensions-cli (gext)
#   - Cài + bật extension "Just Perfection"
#   - Compile schema + set các key ẩn/HIỆN UI (panel/top bar, activities button,
#     dash, workspace switcher) — 2 CHIỀU: 1=ẩn, 0=hiện lại
#   - Tắt/BẬT LẠI bàn phím ảo GNOME (on-screen keyboard) — 2 chiều
#   - Cài package "unclutter" (ẩn con trỏ chuột) — CHỈ 1 CHIỀU: 1=cài, 0=bỏ qua
#     (không tự gỡ khi = 0, vì gỡ package là thao tác phá hoại/khó hoàn tác)
#
# GHI CHÚ QUAN TRỌNG (rút ra ngày 2026-07-25, test thật trên 192.168.21.230):
# `gsettings set org.gnome.desktop.a11y.applications screen-keyboard-enabled false`
# KHÔNG đủ để chặn bàn phím ảo GNOME tự bật khi chạm tay vào textbox trên màn cảm
# ứng thật (eGalax touchscreen) — đã verify: set đúng false (kiểm tra lại bằng cả
# gsettings lẫn dconf, gắn đúng DBUS_SESSION_BUS_ADDRESS của phiên gnome-shell thật),
# thậm chí reboot cả máy — bàn phím ảo hệ thống (không phải KzKeyboard của app, đây
# là 2 control khác nhau — bàn phím ảo hệ thống nằm ĐÈ lên/tại cùng vị trí do GNOME
# Shell tự vẽ, không phải X11 window nên xwininfo không thấy) vẫn hiện. Setting đó
# chỉ điều khiển toggle thủ công trong Settings > Accessibility, KHÔNG chặn được cơ
# chế tự động theo cảm ứng thật của GNOME Shell 42. Giải pháp xác nhận hoạt động:
# extension "Block Caribou 36" (UUID block-caribou-36@lxylxy123456.ercli.dev,
# https://extensions.gnome.org/extension/3222/block-caribou-36/), tested PASS trên
# đúng GNOME Shell 42.2 + Xorg (X11) theo changelog gốc — cùng stack với máy này.
#
# Đã test thực tế trên máy kiosk 192.168.21.230 (Ubuntu 22.04, GNOME Shell 42).
#
# BẮT BUỘC: chạy trong phiên desktop (GUI) thật của user kiosk — không SSH thuần,
# không "sudo bash ..." cả script (script tự sudo khi cần).
#
# GHI CHÚ QUAN TRỌNG rút ra từ lần test đầu: bước "gext install <uuid>" gọi
# D-Bus method InstallRemoteExtension của GNOME Shell — Shell sẽ hiện 1 popup
# xác nhận NGAY TRÊN MÀN HÌNH VẬT LÝ của máy (không thấy được qua SSH), và có
# timeout ngắn (~24s). Nếu không có người đứng trước màn hình bấm "Install"
# kịp, bước này sẽ báo lỗi "Timeout was reached (24)" — không phải lỗi script,
# chỉ cần chạy lại (script idempotent, đã cài rồi sẽ tự bỏ qua) sau khi đã bấm
# xác nhận, hoặc đứng trước màn hình kiosk khi chạy lần đầu để bấm popup đó.
#
# Chạy (mặc định ẩn tất cả, không cần đối số):
#   bash scripts/linux-kiosk/1-install-software.sh
#
# Chạy có chọn lọc từng mục (mỗi tham số 1=ẩn/bật, 0=hiện lại/bỏ qua, mặc định 1):
#   bash scripts/linux-kiosk/1-install-software.sh <hide_topbar> <hide_activities> <hide_workspace> <hide_dash> <install_unclutter> <hide_keyboard>
# Ví dụ: ẩn Top Bar, HIỆN LẠI Activities/Workspace/Dash, cài unclutter, HIỆN LẠI bàn phím ảo:
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

EXT_UUID_KEYBOARD="block-caribou-36@lxylxy123456.ercli.dev"

echo "=== [1] Cài phần mềm cho Kiosk iPGS — Ubuntu 22.04 ==="
echo "  Home hiện tại: $HOME"
echo "  Ẩn Top Bar=$HIDE_TOPBAR  Activities=$HIDE_ACTIVITIES  Workspace=$HIDE_WORKSPACE  Dash=$HIDE_DASH  Unclutter=$INSTALL_UNCLUTTER  BànPhímẢo=$HIDE_KEYBOARD"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "LỖI: đừng chạy script bằng 'sudo bash ...' hay user root." >&2
    echo "     Chạy trực tiếp: bash scripts/linux-kiosk/1-install-software.sh" >&2
    exit 1
fi

# ─────────────────────────────────────────────────────────────
# Top Bar/Activities/Workspace/Dash giờ là toggle 2 CHIỀU thật sự (không còn kiểu
# "bỏ qua nếu = 0") nên LUÔN cần extension Just Perfection cài & bật, dù đang ẩn
# hay hiện lại — vì cả 2 chiều đều đi qua gsettings của chính extension đó.
echo "=== [1/5] Cài python3-pip + gnome-extensions-cli ==="
if ! command -v pip3 >/dev/null 2>&1; then
    sudo apt install -y python3-pip
else
    echo "  → pip3 đã có, bỏ qua."
fi

export PATH="$HOME/.local/bin:$PATH"
if ! command -v gext >/dev/null 2>&1; then
    pip3 install --user gnome-extensions-cli
    export PATH="$HOME/.local/bin:$PATH"
else
    echo "  → gext đã có, bỏ qua."
fi

if ! grep -q '.local/bin' "$HOME/.bashrc" 2>/dev/null; then
    echo 'export PATH="$HOME/.local/bin:$PATH"' >> "$HOME/.bashrc"
fi

# ─────────────────────────────────────────────────────────────
echo "=== [2/5] Cài + bật extension Just Perfection ==="
if ! gnome-extensions list 2>/dev/null | grep -q "^$EXT_UUID$"; then
    echo "  → LƯU Ý: GNOME Shell sẽ hiện popup xác nhận trên màn hình — hãy đứng"
    echo "    trước màn hình kiosk và bấm 'Install' trong vài giây tới."
    gext install "$EXT_UUID"
else
    echo "  → Extension đã cài, bỏ qua bước install."
fi
gnome-extensions enable "$EXT_UUID" || true

STATE="$(gnome-extensions info "$EXT_UUID" 2>/dev/null | grep 'State:' | awk '{print $2}')"
if [ "$STATE" != "ENABLED" ]; then
    echo "CẢNH BÁO: extension chưa ở trạng thái ENABLED (State: $STATE)." >&2
    echo "          Có thể cần log out/log in lại rồi chạy lại script." >&2
fi

# ─────────────────────────────────────────────────────────────
echo "=== [3/5] Compile schema + áp dụng ẩn/hiện UI (2 chiều) ==="
if [ ! -d "$EXT_DIR/schemas" ]; then
    echo "LỖI: không tìm thấy $EXT_DIR/schemas — extension có cài đúng không?" >&2
    exit 1
fi
glib-compile-schemas "$EXT_DIR/schemas/"

# Helper: đổi "1"/"0" thành "false"/"true" (1 = ẩn = false, 0 = hiện = true)
to_visible_value() { [ "$1" = "1" ] && echo "false" || echo "true"; }

gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection panel "$(to_visible_value "$HIDE_TOPBAR")"
gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection activities-button "$(to_visible_value "$HIDE_ACTIVITIES")" || true
gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection workspace-switcher-should-show "$(to_visible_value "$HIDE_WORKSPACE")" || true
gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection dash "$(to_visible_value "$HIDE_DASH")" || true

# F09 — chống lối thoát qua Activities overview:
# - startup-status=0: GNOME 40+ mặc định KHỞI ĐỘNG VÀO overview (hiện "Type to
#   search") khi login chưa có cửa sổ nào → vào thẳng desktop thay vì overview.
# - search=false + type-to-search=false: ẩn ô "Type to search" trong overview —
#   kể cả khi overview vẫn mở được bằng cử chỉ cảm ứng vuốt 3 ngón (GNOME 40+,
#   KHÔNG chặn được bằng dconf), user cũng không còn ô search để gõ tên app
#   (terminal/settings...) nữa. Verify thật trên ZCU 192.168.0.101 (Shell 42.9).
if [ "$HIDE_ACTIVITIES" = "1" ]; then
    gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection startup-status 0 || true
    gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection search false || true
    gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection type-to-search false || true
else
    gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection startup-status 1 || true
    gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection search true || true
    gsettings --schemadir "$EXT_DIR/schemas/" set org.gnome.shell.extensions.just-perfection type-to-search true || true
fi
echo "  → panel=$(to_visible_value "$HIDE_TOPBAR") activities-button=$(to_visible_value "$HIDE_ACTIVITIES") workspace-switcher=$(to_visible_value "$HIDE_WORKSPACE") dash=$(to_visible_value "$HIDE_DASH") startup-status/search theo Activities=$HIDE_ACTIVITIES"
# Bản v26 không còn key "overview" riêng — đã bỏ (xem GHI CHÚ ở đầu file gốc
# setup-kiosk.sh / docs/devops/KIOSK-SETUP-hide-topbar-ubuntu2204.md).

# ─────────────────────────────────────────────────────────────
# F10 — Chặn CỬ CHỈ CẢM ỨNG mở Activities overview.
#
# BỐI CẢNH: F09 (dconf lock) chỉ vô hiệu được PHÍM TẮT (Super/Alt+F2/...). Cử chỉ
# cảm ứng (vuốt nhiều ngón lên, edge-swipe) và hot corner mở overview là hard-code
# trong GNOME Shell 42, KHÔNG có dconf key nào tắt được (Just Perfection v26 — bản
# duy nhất tương thích Shell 42 — cũng KHÔNG có key `gesture`, đã kiểm chứng schema
# trên ZCU 192.168.0.101). Máy kiosk cảm ứng KHÔNG có bàn phím nên cử chỉ chạm là
# lối thoát khả dĩ duy nhất → phải chặn.
#
# GIẢI PHÁP: cài 1 extension GNOME Shell CỤC BỘ (self-contained, không tải từ store
# nên KHÔNG cần bấm popup xác nhận trên màn hình như `gext install`) vô hiệu HOÀN
# TOÀN overview — bất kể trigger nào (cử chỉ đa chạm, edge-swipe, hot corner, double-
# super, gọi lập trình). Kiosk = 1 app fullscreen duy nhất nên KHÔNG cần overview.
# Chạm 1 ngón để dùng app vẫn hoạt động bình thường (chỉ overview bị chặn, không
# đụng tới sự kiện chạm của app).
#
# Chỉ áp dụng khi HIDE_ACTIVITIES=1 (cùng ý nghĩa "chặn truy cập overview"); bỏ tick
# Ẩn Activities = gỡ/tắt extension để trả lại hành vi mặc định.
EXT_UUID_GESTURE="disable-overview-gestures@kztek"
EXT_DIR_GESTURE="$HOME/.local/share/gnome-shell/extensions/$EXT_UUID_GESTURE"

if [ "$HIDE_ACTIVITIES" = "1" ]; then
    echo "=== [3b/5] Cài extension cục bộ chặn cử chỉ mở overview ($EXT_UUID_GESTURE) ==="
    mkdir -p "$EXT_DIR_GESTURE/schemas"

    cat > "$EXT_DIR_GESTURE/metadata.json" <<'EOF'
{
  "uuid": "disable-overview-gestures@kztek",
  "name": "Disable Overview Gestures (KZTEK Kiosk)",
  "description": "Vo hieu hoan toan Activities overview cho kiosk iPGS: chan cu chi cam ung (vuot nhieu ngon), edge-swipe, hot corner va moi loi mo overview. Cham 1 ngon de dung app van hoat dong binh thuong.",
  "shell-version": ["42"],
  "version": 1
}
EOF

    # extension.js — 3 lớp phòng thủ:
    #  (1) override Main.overview.show / showApps thành no-op (chặn mọi lời gọi mở).
    #  (2) bắt tín hiệu 'showing' → hide() ngay (belt-and-suspenders nếu path nào lọt).
    #  (3) tắt các SwipeTracker cử chỉ (overview + chuyển workspace) nếu tồn tại.
    # disable() khôi phục nguyên trạng để bỏ tick Ẩn Activities là trả lại mặc định.
    cat > "$EXT_DIR_GESTURE/extension.js" <<'EOF'
const Main = imports.ui.main;

let _showingId = 0;
let _origShow = null;
let _origShowApps = null;

function init() {}

function _setTracker(enabled) {
    try { Main.overview._swipeTracker.enabled = enabled; } catch (e) {}
    try { Main.wm._workspaceAnimation._swipeTracker.enabled = enabled; } catch (e) {}
    try {
        Main.overview._overview._controls._workspacesDisplay._swipeTracker.enabled = enabled;
    } catch (e) {}
    try {
        Main.overview._overview._controls._appDisplay._swipeTracker.enabled = enabled;
    } catch (e) {}
}

function enable() {
    _origShow = Main.overview.show;
    _origShowApps = Main.overview.showApps;
    Main.overview.show = function () {};
    Main.overview.showApps = function () {};

    _showingId = Main.overview.connect('showing', () => {
        Main.overview.hide();
    });

    _setTracker(false);
}

function disable() {
    if (_showingId) {
        Main.overview.disconnect(_showingId);
        _showingId = 0;
    }
    if (_origShow) {
        Main.overview.show = _origShow;
        _origShow = null;
    }
    if (_origShowApps) {
        Main.overview.showApps = _origShowApps;
        _origShowApps = null;
    }
    _setTracker(true);
}
EOF

    # Đăng ký extension vào danh sách enabled-extensions (có hiệu lực ở lần login kế
    # tiếp — deploy vốn đã yêu cầu restart máy). `gnome-extensions enable` có thể báo
    # "not installed" với extension vừa copy khi shell chưa quét lại → thêm trực tiếp
    # vào dconf để chắc chắn, rồi enable best-effort.
    CUR_EXT="$(gsettings get org.gnome.shell enabled-extensions 2>/dev/null || echo '@as []')"
    if ! echo "$CUR_EXT" | grep -q "$EXT_UUID_GESTURE"; then
        if echo "$CUR_EXT" | grep -q "^@as \[\]$" || [ "$CUR_EXT" = "[]" ]; then
            NEW_EXT="['$EXT_UUID_GESTURE']"
        else
            NEW_EXT="$(echo "$CUR_EXT" | sed "s/]$/, '$EXT_UUID_GESTURE']/")"
        fi
        gsettings set org.gnome.shell enabled-extensions "$NEW_EXT" 2>/dev/null || true
    fi
    gnome-extensions enable "$EXT_UUID_GESTURE" 2>/dev/null || true
    echo "  → Đã cài + đăng ký enable '$EXT_UUID_GESTURE' (hiệu lực sau khi RESTART/đăng nhập lại)."
    echo "    Overview bị vô hiệu hoàn toàn — cử chỉ cảm ứng/hot corner không mở được overview nữa."
else
    echo "=== [3b/5] Bỏ tick Ẩn Activities → gỡ extension chặn cử chỉ overview ==="
    gnome-extensions disable "$EXT_UUID_GESTURE" 2>/dev/null || true
    CUR_EXT="$(gsettings get org.gnome.shell enabled-extensions 2>/dev/null || echo '@as []')"
    if echo "$CUR_EXT" | grep -q "$EXT_UUID_GESTURE"; then
        NEW_EXT="$(echo "$CUR_EXT" | sed "s/'$EXT_UUID_GESTURE', //g; s/, '$EXT_UUID_GESTURE'//g; s/'$EXT_UUID_GESTURE'//g")"
        gsettings set org.gnome.shell enabled-extensions "$NEW_EXT" 2>/dev/null || true
    fi
    rm -rf "$EXT_DIR_GESTURE" 2>/dev/null || true
    echo "  → Đã tắt/gỡ extension chặn cử chỉ overview (trả lại hành vi mặc định GNOME)."
fi

# ─────────────────────────────────────────────────────────────
echo "=== [4/5] Bàn phím ảo GNOME (2 chiều) ==="
# gsettings screen-keyboard-enabled=false KHÔNG đủ (xem GHI CHÚ đầu file) — cần
# thêm extension Block Caribou 36 để chặn thật cơ chế tự bật theo cảm ứng.
if [ "$HIDE_KEYBOARD" = "1" ]; then
    gsettings set org.gnome.desktop.a11y.applications screen-keyboard-enabled false 2>/dev/null || true

    if ! gnome-extensions list 2>/dev/null | grep -q "^$EXT_UUID_KEYBOARD$"; then
        echo "  → LƯU Ý: GNOME Shell sẽ hiện popup xác nhận trên màn hình — hãy đứng"
        echo "    trước màn hình kiosk và bấm 'Install' trong vài giây tới."
        gext install "$EXT_UUID_KEYBOARD"
    else
        echo "  → Extension Block Caribou 36 đã cài, bỏ qua bước install."
    fi
    gnome-extensions enable "$EXT_UUID_KEYBOARD" || true

    KB_STATE="$(gnome-extensions info "$EXT_UUID_KEYBOARD" 2>/dev/null | grep 'State:' | awk '{print $2}')"
    if [ "$KB_STATE" != "ENABLED" ]; then
        echo "CẢNH BÁO: Block Caribou 36 chưa ở trạng thái ENABLED (State: $KB_STATE)." >&2
        echo "          Có thể cần log out/log in lại rồi chạy lại script." >&2
    fi
    echo "  → Đã tắt bàn phím ảo (gsettings + extension Block Caribou 36)."
else
    gsettings set org.gnome.desktop.a11y.applications screen-keyboard-enabled true 2>/dev/null || true
    gnome-extensions disable "$EXT_UUID_KEYBOARD" 2>/dev/null || true
    echo "  → Đã bật lại bàn phím ảo (gsettings + tắt extension Block Caribou 36)."
fi

# ─────────────────────────────────────────────────────────────
if [ "$INSTALL_UNCLUTTER" = "1" ]; then
    echo "=== [5/5] Cài unclutter (ẩn con trỏ chuột) ==="
    if ! dpkg -s unclutter >/dev/null 2>&1; then
        sudo apt install -y unclutter
    else
        echo "  → unclutter đã cài, bỏ qua."
    fi
else
    echo "=== [5/5] Bỏ qua cài unclutter (không được chọn — KHÔNG tự gỡ nếu đã cài trước đó) ==="
fi

echo ""
echo "✓ Xong phần cài phần mềm. Chạy tiếp: bash scripts/linux-kiosk/2-configure-system.sh [kiosk_user] [app_exec]"
