#!/bin/bash
# kiosk-setup-gui.sh ΓÇö GUI (zenity) chß║íy Tß║áI CHß╗û tr├¬n m├íy kiosk ─æß╗â setup ß║⌐n Top Bar
# + cß║Ñu h├¼nh kiosk iPGS, kh├┤ng cß║ºn g├╡ lß╗çnh terminal thß╗º c├┤ng.
#
# D├╣ng khi kß╗╣ thuß║¡t vi├¬n ─æß╗⌐ng trß╗▒c tiß║┐p tr╞░ß╗¢c m├án h├¼nh m├íy kiosk.
# (Muß╗æn deploy tß╗½ xa cho nhiß╗üu m├íy c├╣ng l├║c ΓÇö xem
#  scripts/windows-tools/KioskDeployTool.ps1 chß║íy tr├¬n m├íy Windows cß╗ºa IT.)
#
# Y├¬u cß║ºu: g├│i `zenity` (th╞░ß╗¥ng c├│ sß║╡n tr├¬n Ubuntu Desktop). Nß║┐u ch╞░a c├│:
#   sudo apt install zenity
#
# Chß║íy (double-click file .desktop tß║ío b├¬n d╞░ß╗¢i, hoß║╖c tß╗½ terminal):
#   bash scripts/linux-kiosk/kiosk-setup-gui.sh

set -e

# ├ëp locale UTF-8 t╞░ß╗¥ng minh cho zenity ΓÇö ph├▓ng tr╞░ß╗¥ng hß╗úp script ─æ╞░ß╗úc gß╗ìi tß╗½
# ngß╗» cß║únh thiß║┐u LANG/LC_ALL (autostart, cron, .desktop bß╗ï strip env...) khiß║┐n
# GTK hiß╗ân thß╗ï tiß║┐ng Viß╗çt sai (mojibake). Kh├┤ng sß╗¡a ─æ╞░ß╗úc nß║┐u bß║ún th├ón FILE ─æ├ú
# bß╗ï hß╗Ång encoding l├║c copy ΓÇö xem ghi ch├║ "QUAN TRß╗îNG" b├¬n d╞░ß╗¢i.
export LANG="${LANG:-en_US.UTF-8}"
export LC_ALL="en_US.UTF-8"

# QUAN TRß╗îNG: nß║┐u chß╗» tiß║┐ng Viß╗çt trong dialog zenity hiß╗çn ra k├╜ tß╗▒ lß╗ùi
# (VD: "Kh├â┬┤ng c├â┬│ g├â┬¼") d├╣ ─æ├ú export locale ß╗ƒ tr├¬n ΓÇö ─æ├óy L├Ç Dß║ñU HIß╗åU file
# .sh ─æ├ú bß╗ï hß╗Ång encoding trong l├║c copy sang m├íy (th╞░ß╗¥ng gß║╖p khi copy qua
# USB/Files app cß╗ºa tr├¼nh quß║ún l├╜ file, hoß║╖c mß╗ƒ bß║▒ng nano/gedit rß╗ôi paste).
# C├ích fix: x├│a file tr├¬n m├íy kiosk, copy lß║íi bß║▒ng scp/pscp (giß╗» nguy├¬n byte,
# kh├┤ng qua GUI file manager):
#   pscp scripts\linux-kiosk\kiosk-setup-gui.sh <user>@<ip>:~/
# Kiß╗âm tra nhanh file tr├¬n m├íy kiosk c├│ ─æ├║ng UTF-8 kh├┤ng:
#   file kiosk-setup-gui.sh   # phß║úi b├ío "UTF-8 Unicode text", kh├┤ng phß║úi "ISO-8859" / "ASCII text, with..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT1="$SCRIPT_DIR/1-install-software.sh"
SCRIPT2="$SCRIPT_DIR/2-configure-system.sh"
SCRIPT3="$SCRIPT_DIR/3-toggle-topbar.sh"

if ! command -v zenity >/dev/null 2>&1; then
    echo "Lß╗ûI: ch╞░a c├ái zenity. Chß║íy: sudo apt install zenity" >&2
    exit 1
fi

# ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
# B╞░ß╗¢c 1: Chß╗ìn c├íc b╞░ß╗¢c muß╗æn chß║íy
CHOICE=$(zenity --list --checklist \
    --title="Setup Kiosk iPGS" \
    --text="Chß╗ìn c├íc b╞░ß╗¢c muß╗æn thß╗▒c hiß╗çn:" \
    --width=520 --height=280 \
    --column="Chß╗ìn" --column="B╞░ß╗¢c" \
    TRUE  "1. C├ái phß║ºn mß╗üm (extension ß║⌐n top bar + unclutter)" \
    TRUE  "2. Cß║Ñu h├¼nh hß╗ç thß╗æng (autologin, autostart, tß║»t dock/sleep/update popup...)" \
    --separator=";")

if [ -z "$CHOICE" ]; then
    zenity --info --title="─É├ú hß╗ºy" --text="Kh├┤ng chß╗ìn b╞░ß╗¢c n├áo ΓÇö tho├ít." 2>/dev/null || true
    exit 0
fi

RUN_STEP1=false
RUN_STEP2=false
[[ "$CHOICE" == *"1. C├ái phß║ºn mß╗üm"* ]] && RUN_STEP1=true
[[ "$CHOICE" == *"2. Cß║Ñu h├¼nh hß╗ç thß╗æng"* ]] && RUN_STEP2=true

# ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
# B╞░ß╗¢c 1b: ß║¿n / hiß╗çn lß║íi Top Bar-Dock (─æß╗Öc lß║¡p, kh├┤ng cß║ºn c├ái lß║íi phß║ºn mß╗üm)
TOGGLE_CHOICE=$(zenity --list --radiolist \
    --title="Top Bar / Dock" \
    --text="Bß║ín muß╗æn ß║⌐n hay hiß╗çn lß║íi Top Bar/Dock/Desktop Icons?" \
    --width=480 --height=220 \
    --column="Chß╗ìn" --column="T├╣y chß╗ìn" \
    TRUE  "Kh├┤ng ─æß╗òi (giß╗» nguy├¬n trß║íng th├íi hiß╗çn tß║íi)" \
    FALSE "ß║¿n Top Bar/Dock/Desktop Icons" \
    FALSE "Hiß╗çn lß║íi Top Bar/Dock/Desktop Icons (undo)")

TOGGLE_MODE=""
case "$TOGGLE_CHOICE" in
    "ß║¿n Top Bar"*) TOGGLE_MODE="hide" ;;
    "Hiß╗çn lß║íi"*)   TOGGLE_MODE="show" ;;
esac

if ! $RUN_STEP1 && ! $RUN_STEP2 && [ -z "$TOGGLE_MODE" ]; then
    zenity --info --title="Kh├┤ng c├│ g├¼ ─æß╗â l├ám" --text="Bß║ín ch╞░a chß╗ìn h├ánh ─æß╗Öng n├áo ΓÇö tho├ít." 2>/dev/null || true
    exit 0
fi

# ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
# B╞░ß╗¢c 2: Nß║┐u chß╗ìn b╞░ß╗¢c 2, hß╗Åi kiosk_user + app_exec
KIOSK_USER="$USER"
APP_EXEC="ipgskioskavalonia"

if $RUN_STEP2; then
    FORM_RESULT=$(zenity --forms \
        --title="Cß║Ñu h├¼nh kiosk" \
        --text="Nhß║¡p th├┤ng tin cho b╞░ß╗¢c cß║Ñu h├¼nh hß╗ç thß╗æng:" \
        --add-entry="User d├╣ng ─æß╗â autologin (mß║╖c ─æß╗ïnh: $USER)" \
        --add-entry="Lß╗çnh chß║íy app iPGS (mß║╖c ─æß╗ïnh: ipgskioskavalonia)" \
        --separator="|")

    if [ -n "$FORM_RESULT" ]; then
        F_USER="$(echo "$FORM_RESULT" | cut -d'|' -f1)"
        F_EXEC="$(echo "$FORM_RESULT" | cut -d'|' -f2)"
        [ -n "$F_USER" ] && KIOSK_USER="$F_USER"
        [ -n "$F_EXEC" ] && APP_EXEC="$F_EXEC"
    fi
fi

# ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
# B╞░ß╗¢c 3: X├íc nhß║¡n
SUMMARY="Sß║╜ thß╗▒c hiß╗çn:\n"
$RUN_STEP1 && SUMMARY="${SUMMARY}\nΓ£ô C├ái phß║ºn mß╗üm (1-install-software.sh)"
$RUN_STEP2 && SUMMARY="${SUMMARY}\nΓ£ô Cß║Ñu h├¼nh hß╗ç thß╗æng (kiosk_user=$KIOSK_USER, app_exec=$APP_EXEC)"
[ "$TOGGLE_MODE" = "hide" ] && SUMMARY="${SUMMARY}\nΓ£ô ß║¿n Top Bar/Dock/Desktop Icons"
[ "$TOGGLE_MODE" = "show" ] && SUMMARY="${SUMMARY}\nΓ£ô Hiß╗çn lß║íi Top Bar/Dock/Desktop Icons"
SUMMARY="${SUMMARY}\n\nL╞»U ├¥: nß║┐u chß║íy lß║ºn ─æß║ºu b╞░ß╗¢c 'C├ái phß║ºn mß╗üm', GNOME Shell sß║╜ hiß╗çn\npopup x├íc nhß║¡n c├ái extension tr├¬n m├án h├¼nh ΓÇö h├úy bß║Ñm 'Install' khi thß║Ñy."

zenity --question --title="X├íc nhß║¡n" --width=480 --text="$SUMMARY" || exit 0

# ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
# B╞░ß╗¢c 4: Chß║íy trong 1 cß╗¡a sß╗ò terminal thß║¡t (─æß╗â sudo + popup x├íc nhß║¡n hoß║ít ─æß╗Öng
# b├¼nh th╞░ß╗¥ng ΓÇö kh├┤ng cß╗æ gß║»ng relay password qua zenity cho phß╗⌐c tß║íp/k├⌐m an to├án).
CMD=""
$RUN_STEP1 && CMD="${CMD}bash '$SCRIPT1'; "
$RUN_STEP2 && CMD="${CMD}bash '$SCRIPT2' '$KIOSK_USER' '$APP_EXEC'; "
[ -n "$TOGGLE_MODE" ] && CMD="${CMD}bash '$SCRIPT3' '$TOGGLE_MODE'; "
CMD="${CMD}echo; echo '=== ─É├ú chß║íy xong ΓÇö Enter ─æß╗â ─æ├│ng cß╗¡a sß╗ò n├áy ==='; read"

# GHI CH├Ü (gotcha ─æ├ú gß║╖p thß╗▒c tß║┐): `gnome-terminal` mß║╖c ─æß╗ïnh KH├öNG tß╗▒ mß╗ƒ cß╗¡a
# sß╗ò ΓÇö n├│ gß╗ìi qua D-Bus tß╗¢i service "org.gnome.Terminal" (factory) ─æß╗â 1 tiß║┐n
# tr├¼nh gnome-terminal-server c├│ sß║╡n mß╗ƒ window gi├║p. Nß║┐u server ─æ├│ ch╞░a chß║íy/
# bß╗ï treo, D-Bus service activation timeout ΓåÆ lß╗ùi
# "Error calling StartServiceByName for org.gnome.Terminal: Timeout was reached"
# v├á KH├öNG c├│ cß╗¡a sß╗ò n├áo mß╗ƒ ra. D├╣ng `--disable-factory` ─æß╗â gnome-terminal tß╗▒
# chß║íy nh╞░ 1 tiß║┐n tr├¼nh standalone, kh├┤ng phß╗Ñ thuß╗Öc D-Bus factory ΓÇö n├⌐ lß╗ùi n├áy
# ho├án to├án. Nß║┐u m├íy n├áo ─æ├│ gnome-terminal vß║½n lß╗ùi kiß╗âu kh├íc ΓåÆ fallback xterm.
# `--disable-factory` l├ám gnome-terminal chß║íy nh╞░ tiß║┐n tr├¼nh standalone
# (kh├┤ng hß╗Åi D-Bus factory nß╗»a) NH╞»NG c┼⌐ng c├│ ngh─⌐a lß╗çnh sß║╜ BLOCK ─æß║┐n khi cß╗¡a
# sß╗ò ─æ├│ng ΓÇö phß║úi tß╗▒ `&` ─æß╗â background, rß╗ôi kiß╗âm tra sau ~1s xem tiß║┐n tr├¼nh c├▓n
# sß╗æng kh├┤ng (proxy cho "mß╗ƒ th├ánh c├┤ng") tr╞░ß╗¢c khi fallback sang xterm.
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
    zenity --error --text="Kh├┤ng mß╗ƒ ─æ╞░ß╗úc cß╗¡a sß╗ò terminal (gnome-terminal lß╗ùi D-Bus factory, kh├┤ng c├│ xterm dß╗▒ ph├▓ng).\n\nChß║íy tay bß║▒ng lß╗çnh:\n$CMD" 2>/dev/null || true
    rm -f "$ERR_LOG"
    exit 1
fi
rm -f "$ERR_LOG"

zenity --info --title="─É├ú khß╗ƒi chß║íy" \
    --text="─É├ú mß╗ƒ cß╗¡a sß╗ò terminal ─æß╗â chß║íy setup.\nTheo d├╡i tiß║┐n tr├¼nh v├á nhß║¡p mß║¡t khß║⌐u sudo (nß║┐u ─æ╞░ß╗úc hß╗Åi) ngay trong cß╗¡a sß╗ò ─æ├│.\n\nSau khi xong, RESTART m├íy ─æß╗â ├íp dß╗Ñng ─æß║ºy ─æß╗º (autologin + autostart)." \
    2>/dev/null || true
