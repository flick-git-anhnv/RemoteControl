#!/bin/bash
# kiosk-setup-gui.sh (Lubuntu/LXQt) — GUI (zenity) chạy TẠI CHỖ trên máy kiosk Lubuntu
# để setup ẩn Panel + cấu hình kiosk iPGS, không cần gõ lệnh terminal thủ công.
#
# Dùng khi kỹ thuật viên đứng trực tiếp trước màn hình máy kiosk.
#
# Yêu cầu: gói `zenity` (thường có sẵn trên Lubuntu). Nếu chưa có:
#   sudo apt install zenity

set -e

export LANG="${LANG:-en_US.UTF-8}"
export LC_ALL="en_US.UTF-8"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT1="$SCRIPT_DIR/1-install-software.sh"
SCRIPT2="$SCRIPT_DIR/2-configure-system.sh"
SCRIPT3="$SCRIPT_DIR/3-toggle-panel.sh"

if ! command -v zenity >/dev/null 2>&1; then
    echo "LỖI: chưa cài zenity. Chạy: sudo apt install zenity" >&2
    exit 1
fi

# ─────────────────────────────────────────────────────────────
# Bước 1: Chọn các bước muốn chạy
CHOICE=$(zenity --list --checklist \
    --title="Setup Kiosk iPGS (Lubuntu)" \
    --text="Chọn các bước muốn thực hiện:" \
    --width=520 --height=280 \
    --column="Chọn" --column="Bước" \
    TRUE  "1. Cài phần mềm (ẩn panel/desktop icon + unclutter)" \
    TRUE  "2. Cấu hình hệ thống (autologin, autostart, khoá phím tắt, watchdog...)" \
    --separator=";")

if [ -z "$CHOICE" ]; then
    zenity --info --title="Đã huỷ" --text="Không chọn bước nào — thoát." 2>/dev/null || true
    exit 0
fi

RUN_STEP1=false
RUN_STEP2=false
[[ "$CHOICE" == *"1. Cài phần mềm"* ]] && RUN_STEP1=true
[[ "$CHOICE" == *"2. Cấu hình hệ thống"* ]] && RUN_STEP2=true

# ─────────────────────────────────────────────────────────────
# Bước 1b: ẩn / hiện lại Panel-Desktop Icons (độc lập, không cần cài lại phần mềm)
TOGGLE_CHOICE=$(zenity --list --radiolist \
    --title="Panel / Desktop Icons" \
    --text="Bạn muốn ẩn hay hiện lại Panel/Desktop Icons?" \
    --width=480 --height=220 \
    --column="Chọn" --column="Tùy chọn" \
    TRUE  "Không đổi (giữ nguyên trạng thái hiện tại)" \
    FALSE "Ẩn Panel/Desktop Icons" \
    FALSE "Hiện lại Panel/Desktop Icons (undo)")

TOGGLE_MODE=""
case "$TOGGLE_CHOICE" in
    "Ẩn Panel"*)   TOGGLE_MODE="hide" ;;
    "Hiện lại"*)   TOGGLE_MODE="show" ;;
esac

if ! $RUN_STEP1 && ! $RUN_STEP2 && [ -z "$TOGGLE_MODE" ]; then
    zenity --info --title="Không có gì để làm" --text="Bạn chưa chọn hành động nào — thoát." 2>/dev/null || true
    exit 0
fi

# ─────────────────────────────────────────────────────────────
# Bước 2: Nếu chọn bước 2, hỏi kiosk_user + app_exec
KIOSK_USER="$USER"
APP_EXEC="ipgskioskavalonia"

if $RUN_STEP2; then
    FORM_RESULT=$(zenity --forms \
        --title="Cấu hình kiosk" \
        --text="Nhập thông tin cho bước cấu hình hệ thống:" \
        --add-entry="User dùng để autologin (mặc định: $USER)" \
        --add-entry="Lệnh chạy app iPGS (mặc định: ipgskioskavalonia)" \
        --separator="|")

    if [ -n "$FORM_RESULT" ]; then
        F_USER="$(echo "$FORM_RESULT" | cut -d'|' -f1)"
        F_EXEC="$(echo "$FORM_RESULT" | cut -d'|' -f2)"
        [ -n "$F_USER" ] && KIOSK_USER="$F_USER"
        [ -n "$F_EXEC" ] && APP_EXEC="$F_EXEC"
    fi
fi

# ─────────────────────────────────────────────────────────────
# Bước 3: Xác nhận
SUMMARY="Sẽ thực hiện:\n"
$RUN_STEP1 && SUMMARY="${SUMMARY}\n✔ Cài phần mềm (1-install-software.sh)"
$RUN_STEP2 && SUMMARY="${SUMMARY}\n✔ Cấu hình hệ thống (kiosk_user=$KIOSK_USER, app_exec=$APP_EXEC)"
[ "$TOGGLE_MODE" = "hide" ] && SUMMARY="${SUMMARY}\n✔ Ẩn Panel/Desktop Icons"
[ "$TOGGLE_MODE" = "show" ] && SUMMARY="${SUMMARY}\n✔ Hiện lại Panel/Desktop Icons"

zenity --question --title="Xác nhận" --width=480 --text="$SUMMARY" || exit 0

# ─────────────────────────────────────────────────────────────
# Bước 4: Chạy trong 1 cửa sổ terminal thật (để sudo + mật khẩu hoạt động bình thường).
CMD=""
$RUN_STEP1 && CMD="${CMD}bash '$SCRIPT1'; "
$RUN_STEP2 && CMD="${CMD}bash '$SCRIPT2' '$KIOSK_USER' '$APP_EXEC'; "
[ -n "$TOGGLE_MODE" ] && CMD="${CMD}bash '$SCRIPT3' '$TOGGLE_MODE'; "
CMD="${CMD}echo; echo '=== Đã chạy xong — Enter để đóng cửa sổ này ==='; read"

# LXQt dùng qterminal làm terminal mặc định (không có gnome-terminal D-Bus factory
# như GNOME) — thử qterminal trước, fallback xterm.
TERMINAL_LAUNCHED=false
if command -v qterminal >/dev/null 2>&1; then
    qterminal --title="Kiosk Setup iPGS" -e bash -c "$CMD" &
    QT_PID=$!
    sleep 1
    kill -0 "$QT_PID" 2>/dev/null && TERMINAL_LAUNCHED=true
fi
if ! $TERMINAL_LAUNCHED && command -v xterm >/dev/null 2>&1; then
    xterm -T "Kiosk Setup iPGS" -e bash -c "$CMD" &
    TERMINAL_LAUNCHED=true
fi
if ! $TERMINAL_LAUNCHED; then
    zenity --error --text="Không mở được cửa sổ terminal (thiếu qterminal/xterm).\n\nChạy tay bằng lệnh:\n$CMD" 2>/dev/null || true
    exit 1
fi

zenity --info --title="Đã khởi chạy" \
    --text="Đã mở cửa sổ terminal để chạy setup.\nTheo dõi tiến trình và nhập mật khẩu sudo (nếu được hỏi) ngay trong cửa sổ đó.\n\nSau khi xong, RESTART máy để áp dụng đầy đủ (autologin + autostart)." \
    2>/dev/null || true
