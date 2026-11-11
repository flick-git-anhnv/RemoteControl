#!/bin/bash
# 1-install-software.sh — Cài phần mềm/extension cần thiết + áp dụng lựa chọn
# ẩn/hiện UI GNOME cho kiosk iPGS.
#
# Phần "phần mềm" trong bộ setup kiosk iPGS (tách từ setup-kiosk.sh):
#   - Cài curl/unzip (offline qua .deb nhúng sẵn nếu có, F17)
#   - Cài + bật extension "Just Perfection" (offline qua zip nhúng sẵn nếu có, F16)
#   - Compile schema + set các key ẩn/HIỆN UI (panel/top bar, activities button,
#     dash, workspace switcher) — 2 CHIỀU: 1=ẩn, 0=hiện lại
#   - Tắt/BẬT LẠI bàn phím ảo GNOME (on-screen keyboard) — 2 chiều
#   - Unclutter (ẩn con trỏ chuột) — 2 CHIỀU theo hiệu ứng: 1=cài + bật tự chạy,
#     0=tắt tự chạy + dừng tiến trình (con trỏ hiện lại). KHÔNG gỡ package khi = 0
#     (gỡ package là thao tác phá hoại/cần mạng để cài lại).
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
# GHI CHÚ (đã khắc phục — F13 2026-07-27): trước đây bước "gext install <uuid>"
# gọi D-Bus InstallRemoteExtension → GNOME Shell hiện popup xác nhận trên màn
# hình vật lý và timeout ~24s nếu không ai bấm → set -e abort toàn script khi
# chạy qua SSH. Đã thay bằng _install_ext_offline (curl + unzip) — tải thẳng
# zip từ extensions.gnome.org và giải nén vào ~/.local/share/gnome-shell/extensions/
# mà không cần D-Bus/GUI. Không cần ai đứng trước màn hình nữa.
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
EXT_DIR_KEYBOARD="$HOME/.local/share/gnome-shell/extensions/$EXT_UUID_KEYBOARD"

echo "=== [1] Cài phần mềm cho Kiosk iPGS — Ubuntu 22.04 ==="
echo "  Home hiện tại: $HOME"
echo "  Ẩn Top Bar=$HIDE_TOPBAR  Activities=$HIDE_ACTIVITIES  Workspace=$HIDE_WORKSPACE  Dash=$HIDE_DASH  Unclutter=$INSTALL_UNCLUTTER  BànPhímẢo=$HIDE_KEYBOARD"
echo ""

if [ "$EUID" -eq 0 ]; then
    echo "LỖI: đừng chạy script bằng 'sudo bash ...' hay user root." >&2
    echo "     Chạy trực tiếp: bash scripts/linux-kiosk/1-install-software.sh" >&2
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

# Cài extension GNOME Shell từ extensions.gnome.org bằng curl+unzip — không cần
# D-Bus/GUI dialog (F13: thay thế `gext install` gây popup + timeout SSH ~24s).
# Idempotent: unzip -o ghi đè nếu dir đã có. Thất bại thật (lỗi mạng, zip hỏng)
# vẫn exit != 0 rõ ràng thay vì âm thầm thành công giả.
_install_ext_offline() {
    local uuid="$1"
    local ext_dir="$HOME/.local/share/gnome-shell/extensions/$uuid"

    # F16: ưu tiên zip offline đã upload sẵn (nhúng trong CcuUI/Resources/gnome-extensions,
    # KioskDeployService upload lên $GEXT_OFFLINE_DIR/<uuid>.zip) — không cần mạng.
    # Chỉ tải từ extensions.gnome.org khi không thấy file local (chạy tay script này
    # trực tiếp trên ZCU, hoặc build CcuUI cũ chưa nhúng resource).
    local offline_zip="${GEXT_OFFLINE_DIR:-}/${uuid}.zip"
    if [ -n "${GEXT_OFFLINE_DIR:-}" ] && [ -f "$offline_zip" ]; then
        echo "  → Cài extension '$uuid' từ zip offline ($offline_zip)..."
        mkdir -p "$ext_dir"
        if ! unzip -o -q "$offline_zip" -d "$ext_dir"; then
            echo "LỖI: không giải nén được extension zip offline '$uuid'." >&2
            return 1
        fi
        echo "  → Đã cài extension '$uuid' vào $ext_dir (offline, không cần bấm popup)."
        return 0
    fi

    # Major version GNOME Shell (42, 43, ...)
    local shell_ver
    shell_ver="$(gnome-shell --version 2>/dev/null | grep -oE '[0-9]+' | head -1)"
    if [ -z "$shell_ver" ]; then
        echo "LỖI (_install_ext_offline): không xác định được phiên bản GNOME Shell." >&2
        return 1
    fi

    local zip_url="https://extensions.gnome.org/download-extension/${uuid}.shell-extension.zip?shell_version=${shell_ver}"
    local tmp_zip; tmp_zip="$(mktemp --suffix=.zip)"

    echo "  → Tải extension '$uuid' (GNOME Shell $shell_ver) từ extensions.gnome.org..."
    if ! curl -fsSL --max-time 30 --retry 2 --retry-delay 3 "$zip_url" -o "$tmp_zip"; then
        rm -f "$tmp_zip"
        echo "LỖI: không tải được extension '$uuid'." >&2
        echo "     URL: $zip_url" >&2
        echo "     Kiểm tra kết nối mạng hoặc shell_version=$shell_ver có bản tương thích không." >&2
        return 1
    fi

    mkdir -p "$ext_dir"
    if ! unzip -o -q "$tmp_zip" -d "$ext_dir"; then
        rm -f "$tmp_zip"
        echo "LỖI: không giải nén được extension zip '$uuid'." >&2
        return 1
    fi
    rm -f "$tmp_zip"
    echo "  → Đã cài extension '$uuid' vào $ext_dir (không cần bấm popup)."
}

# Bật/tắt extension GNOME Shell bằng cách sửa TRỰC TIẾP 2 key gsettings
# org.gnome.shell enabled-extensions / disabled-extensions — KHÔNG cần gnome-shell đang
# chạy (chỉ ghi dconf), nên vẫn đúng khi shell đang bị nạp lại (killall gnome-shell ở
# cuối 1-install-software.sh) — trước đây `gnome-extensions enable/disable` gọi D-Bus tới
# shell, shell đang khởi động lại → lệnh fail âm thầm ("bỏ qua") → bỏ tick không có tác
# dụng. GNOME 40+: uuid nằm trong disabled-extensions THẮNG enabled-extensions, và
# extension mặc định của session Ubuntu (ubuntu-dock, ding) CHỈ tắt được qua
# disabled-extensions → phải sửa cả 2 key theo cặp.
_ext_gsettings_set() {
    local uuid="$1" want="$2"   # want: on | off
    python3 - "$uuid" "$want" <<'PYEOF' || true
import ast, subprocess, sys
uuid, want = sys.argv[1], sys.argv[2]
def get(key):
    try:
        out = subprocess.run(["gsettings", "get", "org.gnome.shell", key],
                             capture_output=True, text=True).stdout.strip()
    except Exception:
        return None
    if out.startswith("@as "):
        out = out[4:]
    try:
        v = ast.literal_eval(out)
        return list(v) if isinstance(v, (list, tuple)) else None
    except Exception:
        return None
def put(key, lst):
    subprocess.run(["gsettings", "set", "org.gnome.shell", key, str(lst)], capture_output=True)
en, dis = get("enabled-extensions"), get("disabled-extensions")
if en is None or dis is None:
    sys.exit(0)
if want == "on":
    if uuid not in en:
        put("enabled-extensions", en + [uuid])
    if uuid in dis:
        put("disabled-extensions", [u for u in dis if u != uuid])
else:
    if uuid in en:
        put("enabled-extensions", [u for u in en if u != uuid])
    if uuid not in dis:
        put("disabled-extensions", dis + [uuid])
PYEOF
}

_force_enable_ext() {
    local uuid="$1"
    gnome-extensions enable "$uuid" 2>/dev/null || true
    _ext_gsettings_set "$uuid" on
    echo "  → Extension '$uuid' đã ENABLE (có trong enabled-extensions, đã gỡ khỏi disabled-extensions)."
}

_force_disable_ext() {
    local uuid="$1"
    gnome-extensions disable "$uuid" 2>/dev/null || true
    _ext_gsettings_set "$uuid" off
    echo "  → Extension '$uuid' đã DISABLE (gỡ khỏi enabled-extensions, thêm vào disabled-extensions)."
}

# ─────────────────────────────────────────────────────────────
# Top Bar/Activities/Workspace/Dash giờ là toggle 2 CHIỀU thật sự (không còn kiểu
# "bỏ qua nếu = 0") nên LUÔN cần extension Just Perfection cài & bật, dù đang ẩn
# hay hiện lại — vì cả 2 chiều đều đi qua gsettings của chính extension đó.
echo "=== [1/5] Cài công cụ cần thiết (curl, unzip) ==="
# curl + unzip: dùng cho _install_ext_offline — giải nén zip local (đường chính,
# đã nhúng sẵn trong CcuUI/Resources/gnome-extensions) hoặc curl mạng (fallback khi
# không có GEXT_OFFLINE_DIR). F17: đã bỏ pip3/gnome-extensions-cli (gext) — dead
# code từ F13, không còn nơi nào gọi lệnh `gext`; cài extension đi qua
# _install_ext_offline (unzip) + _force_enable_ext (lệnh `gnome-extensions` có sẵn
# của hệ thống), không cần package pip3 này nữa.
#
# F17: cài OFFLINE bằng dpkg -i từ $KIOSK_DEB_OFFLINE_DIR (.deb nhúng sẵn trong
# CcuUI/Resources/kiosk-deb, KioskDeployService upload lên) nếu có; fallback về
# apt install khi không có (chạy tay script này trực tiếp, hoặc build CcuUI cũ).
#
# F26: tham số 2+ là các gói .deb DEPENDENCY đi kèm (VD libcurl4 cho curl) — dpkg -i
# không tự resolve version lệch như apt, nên phải nhúng sẵn + cài CÙNG LÚC để khớp
# version nội bộ, không phụ thuộc bản dependency có sẵn trên máy đích (verify thật:
# curl .deb offline yêu cầu libcurl4 đúng version, máy đích có bản libcurl4 cũ hơn
# → dpkg -i unpack xong nhưng "chưa cấu hình" (iU) → LỖI exit 1, dù log trông như
# đã cài xong). Nếu sau dpkg -i package vẫn chưa "ii" (installed ok) → fallback
# sửa qua apt-get install -f (cần mạng) thay vì fail cứng toàn bộ deploy.
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
echo "=== [2/5] Cài + bật extension Just Perfection ==="
# F13: dùng _install_ext_offline thay gext install — không cần GUI dialog/bấm popup.
# Idempotent: nếu dir đã có và shell đã nhận diện uuid → bỏ qua tải lại;
#             nếu dir bị xoá → tải và cài lại từ extensions.gnome.org.
if ! gnome-extensions list 2>/dev/null | grep -q "^$EXT_UUID$" || [ ! -d "$EXT_DIR" ]; then
    _install_ext_offline "$EXT_UUID"
else
    echo "  → Extension đã cài, bỏ qua bước install."
fi

# Sửa bug GNOME 42 trong Just Perfection: Looking Glass gọi layoutManager.primaryMonitor khi monitor chưa init gây 'TypeError: primary is null' -> State: ERROR
if [ -f "$EXT_DIR/lib/API.js" ]; then
    python3 -c "
path = '$EXT_DIR/lib/API.js'
try:
    with open(path, 'r', encoding='utf-8') as f:
        c = f.read()
    target = 'let lookingGlass = this._main.createLookingGlass();'
    replacement = 'if (!this._main.layoutManager || !this._main.layoutManager.primaryMonitor) return; let lookingGlass; try { lookingGlass = this._main.createLookingGlass(); } catch(e) { return; }'
    if target in c:
        c = c.replace(target, replacement, 1)
        with open(path, 'w', encoding='utf-8') as f:
            f.write(c)
except Exception:
    pass
" 2>/dev/null || true
fi

_force_enable_ext "$EXT_UUID"

STATE="$(gnome-extensions info "$EXT_UUID" 2>/dev/null | grep 'State:' | awk '{print $2}')"
if [ "$STATE" != "ENABLED" ]; then
    echo "CẢNH BÁO: extension chưa ở trạng thái ENABLED (State: $STATE) — hiệu lực ở lần login kế tiếp." >&2
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
    _force_enable_ext "$EXT_UUID_GESTURE"
    echo "  → Đã cài + đăng ký enable '$EXT_UUID_GESTURE' (hiệu lực sau khi RESTART/đăng nhập lại)."
    echo "    Overview bị vô hiệu hoàn toàn — cử chỉ cảm ứng/hot corner không mở được overview nữa."
else
    echo "=== [3b/5] Bỏ tick Ẩn Activities → gỡ extension chặn cử chỉ overview ==="
    _force_disable_ext "$EXT_UUID_GESTURE"
    rm -rf "$EXT_DIR_GESTURE" 2>/dev/null || true
    echo "  → Đã tắt/gỡ extension chặn cử chỉ overview (trả lại hành vi mặc định GNOME)."
fi

# ─────────────────────────────────────────────────────────────
echo "=== [4/5] Bàn phím ảo GNOME (2 chiều) ==="
# gsettings screen-keyboard-enabled=false KHÔNG đủ (xem GHI CHÚ đầu file) — cần
# thêm extension Block Caribou 36 để chặn thật cơ chế tự bật theo cảm ứng.
if [ "$HIDE_KEYBOARD" = "1" ]; then
    gsettings set org.gnome.desktop.a11y.applications screen-keyboard-enabled false 2>/dev/null || true

    # F13: dùng _install_ext_offline thay gext install — không cần GUI dialog.
    if ! gnome-extensions list 2>/dev/null | grep -q "^$EXT_UUID_KEYBOARD$" || [ ! -d "$EXT_DIR_KEYBOARD" ]; then
        _install_ext_offline "$EXT_UUID_KEYBOARD"
    else
        echo "  → Extension Block Caribou 36 đã cài, bỏ qua bước install."
    fi
    _force_enable_ext "$EXT_UUID_KEYBOARD"

    KB_STATE="$(gnome-extensions info "$EXT_UUID_KEYBOARD" 2>/dev/null | grep 'State:' | awk '{print $2}')"
    if [ "$KB_STATE" != "ENABLED" ]; then
        echo "CẢNH BÁO: Block Caribou 36 chưa ở trạng thái ENABLED (State: $KB_STATE) — hiệu lực ở lần login kế tiếp." >&2
    fi
    echo "  → Đã tắt bàn phím ảo (gsettings + extension Block Caribou 36)."
else
    gsettings set org.gnome.desktop.a11y.applications screen-keyboard-enabled true 2>/dev/null || true
    _force_disable_ext "$EXT_UUID_KEYBOARD"
    echo "  → Đã bật lại bàn phím ảo (gsettings + tắt extension Block Caribou 36)."
fi

# ─────────────────────────────────────────────────────────────
# Unclutter 2 CHIỀU theo hiệu ứng "ẩn con trỏ chuột": 1 = cài + bật tự chạy, 0 = tắt
# tự chạy + dừng tiến trình đang chạy (con trỏ hiện lại). KHÔNG gỡ package khi = 0.
# Ô này là NƠI DUY NHẤT quản lý unclutter (trước đây unclutter.desktop do ô "Autostart"
# ở tab phần mềm tạo/xoá → bỏ tick ô này không có tác dụng gì).
# Gói unclutter của Ubuntu còn tự chạy qua /etc/X11/Xsession.d/90unclutter theo cờ
# START_UNCLUTTER trong /etc/default/unclutter (mặc định "true") → phải đổi cả cờ này,
# không chỉ file autostart của user.
UNCLUTTER_AUTOSTART="$HOME/.config/autostart/unclutter.desktop"
_set_unclutter_default() {  # $1 = true | false
    [ -f /etc/default/unclutter ] || return 0
    if grep -q '^START_UNCLUTTER=' /etc/default/unclutter; then
        _sudo sed -i "s/^START_UNCLUTTER=.*/START_UNCLUTTER=\"$1\"/" /etc/default/unclutter || true
    else
        _sudo bash -c "echo 'START_UNCLUTTER=\"$1\"' >> /etc/default/unclutter" || true
    fi
}
if [ "$INSTALL_UNCLUTTER" = "1" ]; then
    echo "=== [5/5] Cài + bật unclutter (ẩn con trỏ chuột) ==="
    if ! dpkg -s unclutter >/dev/null 2>&1; then
        _install_deb_offline_or_apt unclutter
    else
        echo "  → unclutter đã cài, bỏ qua bước cài."
    fi
    _set_unclutter_default true
    mkdir -p "$HOME/.config/autostart"
    cat > "$UNCLUTTER_AUTOSTART" <<EOF
[Desktop Entry]
Type=Application
Name=Unclutter
Exec=unclutter -idle 1
X-GNOME-Autostart-enabled=true
EOF
    # Chạy ngay (tách hẳn khỏi phiên SSH — setsid + đóng mọi fd để kênh SSH không bị treo)
    if command -v unclutter >/dev/null 2>&1 && ! pgrep -u "$USER" -x unclutter >/dev/null 2>&1; then
        DISPLAY="${DISPLAY:-:0}" setsid nohup unclutter -idle 1 >/dev/null 2>&1 < /dev/null &
    fi
    echo "  → Đã bật unclutter (autostart: $UNCLUTTER_AUTOSTART + START_UNCLUTTER=true)."
else
    echo "=== [5/5] Tắt unclutter (hiện lại con trỏ chuột — giữ package, không gỡ cài) ==="
    rm -f "$UNCLUTTER_AUTOSTART"
    _set_unclutter_default false
    pkill -u "$USER" -x unclutter 2>/dev/null || true
    echo "  → Đã xoá autostart unclutter + START_UNCLUTTER=false + dừng tiến trình unclutter."
fi

# Nạp lại GNOME Shell trên X11 để quét và kích hoạt extension mới / cập nhật Top Bar ngay lập tức
if pgrep -u "$USER" -f '^/usr/bin/gnome-shell' >/dev/null 2>&1; then
    echo "  → Đang nạp lại GNOME Shell để áp dụng các thay đổi giao diện/extension ngay..."
    killall -9 gnome-shell 2>/dev/null || true
    sleep 1
fi

echo ""
echo "✓ Xong phần cài phần mềm. Chạy tiếp: bash scripts/linux-kiosk/2-configure-system.sh [kiosk_user] [app_exec]"
