#!/bin/bash
# kiosk-setup-gui.sh â€” GUI (zenity) cháº¡y Táº I CHá»– trÃªn mÃ¡y kiosk Ä‘á»ƒ setup áº©n Top Bar
# + cáº¥u hÃ¬nh kiosk iPGS, khÃ´ng cáº§n gÃµ lá»‡nh terminal thá»§ cÃ´ng.
#
# DÃ¹ng khi ká»¹ thuáº­t viÃªn Ä‘á»©ng trá»±c tiáº¿p trÆ°á»›c mÃ n hÃ¬nh mÃ¡y kiosk.
# (Muá»‘n deploy tá»« xa cho nhiá»u mÃ¡y cÃ¹ng lÃºc â€” xem
#  scripts/windows-tools/KioskDeployTool.ps1 cháº¡y trÃªn mÃ¡y Windows cá»§a IT.)
#
# YÃªu cáº§u: gÃ³i `zenity` (thÆ°á»ng cÃ³ sáºµn trÃªn Ubuntu Desktop). Náº¿u chÆ°a cÃ³:
#   sudo apt install zenity
#
# Cháº¡y (double-click file .desktop táº¡o bÃªn dÆ°á»›i, hoáº·c tá»« terminal):
#   bash scripts/linux-kiosk/kiosk-setup-gui.sh

set -e

# Ã‰p locale UTF-8 tÆ°á»ng minh cho zenity â€” phÃ²ng trÆ°á»ng há»£p script Ä‘Æ°á»£c gá»i tá»«
# ngá»¯ cáº£nh thiáº¿u LANG/LC_ALL (autostart, cron, .desktop bá»‹ strip env...) khiáº¿n
# GTK hiá»ƒn thá»‹ tiáº¿ng Viá»‡t sai (mojibake). KhÃ´ng sá»­a Ä‘Æ°á»£c náº¿u báº£n thÃ¢n FILE Ä‘Ã£
# bá»‹ há»ng encoding lÃºc copy â€” xem ghi chÃº "QUAN TRá»ŒNG" bÃªn dÆ°á»›i.
export LANG="${LANG:-en_US.UTF-8}"
export LC_ALL="en_US.UTF-8"

# QUAN TRá»ŒNG: náº¿u chá»¯ tiáº¿ng Viá»‡t trong dialog zenity hiá»‡n ra kÃ½ tá»± lá»—i
# (VD: "KhÃƒÂ´ng cÃƒÂ³ gÃƒÂ¬") dÃ¹ Ä‘Ã£ export locale á»Ÿ trÃªn â€” Ä‘Ã¢y LÃ€ Dáº¤U HIá»†U file
# .sh Ä‘Ã£ bá»‹ há»ng encoding trong lÃºc copy sang mÃ¡y (thÆ°á»ng gáº·p khi copy qua
# USB/Files app cá»§a trÃ¬nh quáº£n lÃ½ file, hoáº·c má»Ÿ báº±ng nano/gedit rá»“i paste).
# CÃ¡ch fix: xÃ³a file trÃªn mÃ¡y kiosk, copy láº¡i báº±ng scp/pscp (giá»¯ nguyÃªn byte,
# khÃ´ng qua GUI file manager):
#   pscp scripts\linux-kiosk\kiosk-setup-gui.sh <user>@<ip>:~/
# Kiá»ƒm tra nhanh file trÃªn mÃ¡y kiosk cÃ³ Ä‘Ãºng UTF-8 khÃ´ng:
#   file kiosk-setup-gui.sh   # pháº£i bÃ¡o "UTF-8 Unicode text", khÃ´ng pháº£i "ISO-8859" / "ASCII text, with..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT1="$SCRIPT_DIR/1-install-software.sh"
SCRIPT2="$SCRIPT_DIR/2-configure-system.sh"
SCRIPT3="$SCRIPT_DIR/3-toggle-topbar.sh"

if ! command -v zenity >/dev/null 2>&1; then
    echo "Lá»–I: chÆ°a cÃ i zenity. Cháº¡y: sudo apt install zenity" >&2
    exit 1
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# BÆ°á»›c 1: Chá»n cÃ¡c bÆ°á»›c muá»‘n cháº¡y
CHOICE=$(zenity --list --checklist \
    --title="Setup Kiosk iPGS" \
    --text="Chá»n cÃ¡c bÆ°á»›c muá»‘n thá»±c hiá»‡n:" \
    --width=520 --height=280 \
    --column="Chá»n" --column="BÆ°á»›c" \
    TRUE  "1. CÃ i pháº§n má»m (extension áº©n top bar + unclutter)" \
    TRUE  "2. Cáº¥u hÃ¬nh há»‡ thá»‘ng (autologin, autostart, táº¯t dock/sleep/update popup...)" \
    --separator=";")

if [ -z "$CHOICE" ]; then
    zenity --info --title="ÄÃ£ há»§y" --text="KhÃ´ng chá»n bÆ°á»›c nÃ o â€” thoÃ¡t." 2>/dev/null || true
    exit 0
fi

RUN_STEP1=false
RUN_STEP2=false
[[ "$CHOICE" == *"1. CÃ i pháº§n má»m"* ]] && RUN_STEP1=true
[[ "$CHOICE" == *"2. Cáº¥u hÃ¬nh há»‡ thá»‘ng"* ]] && RUN_STEP2=true

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# BÆ°á»›c 1b: áº¨n / hiá»‡n láº¡i Top Bar-Dock (Ä‘á»™c láº­p, khÃ´ng cáº§n cÃ i láº¡i pháº§n má»m)
TOGGLE_CHOICE=$(zenity --list --radiolist \
    --title="Top Bar / Dock" \
    --text="Báº¡n muá»‘n áº©n hay hiá»‡n láº¡i Top Bar/Dock/Desktop Icons?" \
    --width=480 --height=220 \
    --column="Chá»n" --column="TÃ¹y chá»n" \
    TRUE  "KhÃ´ng Ä‘á»•i (giá»¯ nguyÃªn tráº¡ng thÃ¡i hiá»‡n táº¡i)" \
    FALSE "áº¨n Top Bar/Dock/Desktop Icons" \
    FALSE "Hiá»‡n láº¡i Top Bar/Dock/Desktop Icons (undo)")

TOGGLE_MODE=""
case "$TOGGLE_CHOICE" in
    "áº¨n Top Bar"*) TOGGLE_MODE="hide" ;;
    "Hiá»‡n láº¡i"*)   TOGGLE_MODE="show" ;;
esac

if ! $RUN_STEP1 && ! $RUN_STEP2 && [ -z "$TOGGLE_MODE" ]; then
    zenity --info --title="KhÃ´ng cÃ³ gÃ¬ Ä‘á»ƒ lÃ m" --text="Báº¡n chÆ°a chá»n hÃ nh Ä‘á»™ng nÃ o â€” thoÃ¡t." 2>/dev/null || true
    exit 0
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# BÆ°á»›c 2: Náº¿u chá»n bÆ°á»›c 2, há»i kiosk_user + app_exec
KIOSK_USER="$USER"
APP_EXEC="ipgskioskavalonia"

if $RUN_STEP2; then
    FORM_RESULT=$(zenity --forms \
        --title="Cáº¥u hÃ¬nh kiosk" \
        --text="Nháº­p thÃ´ng tin cho bÆ°á»›c cáº¥u hÃ¬nh há»‡ thá»‘ng:" \
        --add-entry="User dÃ¹ng Ä‘á»ƒ autologin (máº·c Ä‘á»‹nh: $USER)" \
        --add-entry="Lá»‡nh cháº¡y app iPGS (máº·c Ä‘á»‹nh: ipgskioskavalonia)" \
        --separator="|")

    if [ -n "$FORM_RESULT" ]; then
        F_USER="$(echo "$FORM_RESULT" | cut -d'|' -f1)"
        F_EXEC="$(echo "$FORM_RESULT" | cut -d'|' -f2)"
        [ -n "$F_USER" ] && KIOSK_USER="$F_USER"
        [ -n "$F_EXEC" ] && APP_EXEC="$F_EXEC"
    fi
fi

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# BÆ°á»›c 3: XÃ¡c nháº­n
SUMMARY="Sáº½ thá»±c hiá»‡n:\n"
$RUN_STEP1 && SUMMARY="${SUMMARY}\nâœ“ CÃ i pháº§n má»m (1-install-software.sh)"
$RUN_STEP2 && SUMMARY="${SUMMARY}\nâœ“ Cáº¥u hÃ¬nh há»‡ thá»‘ng (kiosk_user=$KIOSK_USER, app_exec=$APP_EXEC)"
[ "$TOGGLE_MODE" = "hide" ] && SUMMARY="${SUMMARY}\nâœ“ áº¨n Top Bar/Dock/Desktop Icons"
[ "$TOGGLE_MODE" = "show" ] && SUMMARY="${SUMMARY}\nâœ“ Hiá»‡n láº¡i Top Bar/Dock/Desktop Icons"
SUMMARY="${SUMMARY}\n\nLÆ¯U Ã: náº¿u cháº¡y láº§n Ä‘áº§u bÆ°á»›c 'CÃ i pháº§n má»m', GNOME Shell sáº½ hiá»‡n\npopup xÃ¡c nháº­n cÃ i extension trÃªn mÃ n hÃ¬nh â€” hÃ£y báº¥m 'Install' khi tháº¥y."

zenity --question --title="XÃ¡c nháº­n" --width=480 --text="$SUMMARY" || exit 0

# â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# BÆ°á»›c 4: Cháº¡y trong 1 cá»­a sá»• terminal tháº­t (Ä‘á»ƒ sudo + popup xÃ¡c nháº­n hoáº¡t Ä‘á»™ng
# bÃ¬nh thÆ°á»ng â€” khÃ´ng cá»‘ gáº¯ng relay password qua zenity cho phá»©c táº¡p/kÃ©m an toÃ n).
CMD=""
$RUN_STEP1 && CMD="${CMD}bash '$SCRIPT1'; "
$RUN_STEP2 && CMD="${CMD}bash '$SCRIPT2' '$KIOSK_USER' '$APP_EXEC'; "
[ -n "$TOGGLE_MODE" ] && CMD="${CMD}bash '$SCRIPT3' '$TOGGLE_MODE'; "
CMD="${CMD}echo; echo '=== ÄÃ£ cháº¡y xong â€” Enter Ä‘á»ƒ Ä‘Ã³ng cá»­a sá»• nÃ y ==='; read"

# GHI CHÃš (gotcha Ä‘Ã£ gáº·p thá»±c táº¿): `gnome-terminal` máº·c Ä‘á»‹nh KHÃ”NG tá»± má»Ÿ cá»­a
# sá»• â€” nÃ³ gá»i qua D-Bus tá»›i service "org.gnome.Terminal" (factory) Ä‘á»ƒ 1 tiáº¿n
# trÃ¬nh gnome-terminal-server cÃ³ sáºµn má»Ÿ window giÃºp. Náº¿u server Ä‘Ã³ chÆ°a cháº¡y/
# bá»‹ treo, D-Bus service activation timeout â†’ lá»—i
# "Error calling StartServiceByName for org.gnome.Terminal: Timeout was reached"
# vÃ  KHÃ”NG cÃ³ cá»­a sá»• nÃ o má»Ÿ ra. DÃ¹ng `--disable-factory` Ä‘á»ƒ gnome-terminal tá»±
# cháº¡y nhÆ° 1 tiáº¿n trÃ¬nh standalone, khÃ´ng phá»¥ thuá»™c D-Bus factory â€” nÃ© lá»—i nÃ y
# hoÃ n toÃ n. Náº¿u mÃ¡y nÃ o Ä‘Ã³ gnome-terminal váº«n lá»—i kiá»ƒu khÃ¡c â†’ fallback xterm.
# `--disable-factory` lÃ m gnome-terminal cháº¡y nhÆ° tiáº¿n trÃ¬nh standalone
# (khÃ´ng há»i D-Bus factory ná»¯a) NHÆ¯NG cÅ©ng cÃ³ nghÄ©a lá»‡nh sáº½ BLOCK Ä‘áº¿n khi cá»­a
# sá»• Ä‘Ã³ng â€” pháº£i tá»± `&` Ä‘á»ƒ background, rá»“i kiá»ƒm tra sau ~1s xem tiáº¿n trÃ¬nh cÃ²n
# sá»‘ng khÃ´ng (proxy cho "má»Ÿ thÃ nh cÃ´ng") trÆ°á»›c khi fallback sang xterm.
ERR_LOG="$(mktemp)"
TERMINAL_LAUNCHED=false
if command -v gnome-terminal >/dev/null 2>&1; then
    gnome-terminal --disable-factory --title="Kiosk Setup iPGS" -- bash -c "$CMD" 2>"$ERR_LOG" &
    GT_PID=$!
    sleep 1
    if kill -0 "$GT_PID" 2>/dev/null; then
        TERMINAL_LAUNCHED=true
    else
        wait "$GT_PID" 2>/dev/null
        cat "$ERR_LOG" >&2 2>/dev/null || true
    fi
fi
if ! $TERMINAL_LAUNCHED && command -v xterm >/dev/null 2>&1; then
    xterm -T "Kiosk Setup iPGS" -e bash -c "$CMD" &
    TERMINAL_LAUNCHED=true
fi
if ! $TERMINAL_LAUNCHED; then
    zenity --error --text="KhÃ´ng má»Ÿ Ä‘Æ°á»£c cá»­a sá»• terminal (gnome-terminal lá»—i D-Bus factory, khÃ´ng cÃ³ xterm dá»± phÃ²ng).\n\nCháº¡y tay báº±ng lá»‡nh:\n$CMD" 2>/dev/null || true
    rm -f "$ERR_LOG"
    exit 1
fi
rm -f "$ERR_LOG"

zenity --info --title="ÄÃ£ khá»Ÿi cháº¡y" \
    --text="ÄÃ£ má»Ÿ cá»­a sá»• terminal Ä‘á»ƒ cháº¡y setup.\nTheo dÃµi tiáº¿n trÃ¬nh vÃ  nháº­p máº­t kháº©u sudo (náº¿u Ä‘Æ°á»£c há»i) ngay trong cá»­a sá»• Ä‘Ã³.\n\nSau khi xong, RESTART mÃ¡y Ä‘á»ƒ Ã¡p dá»¥ng Ä‘áº§y Ä‘á»§ (autologin + autostart)." \
    2>/dev/null || true
