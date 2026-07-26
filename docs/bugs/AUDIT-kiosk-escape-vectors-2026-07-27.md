# AUDIT — Lối thoát / phá kiosk trên ZCU thật (192.168.0.101)

**Người thực hiện:** QA Engineer (audit bảo mật kiosk, theo yêu cầu user)
**Ngày:** 2026-07-27 00:04–00:15 (+07)
**Môi trường:** ZCU thật `192.168.0.101` — Ubuntu 22.04, GNOME Shell 42.9, X11 (Xorg), user kiosk `kztek`, display manager GDM3, phiên `:0`. Agent `ipgs-remote-agent.service` = **active/running** (không bị gián đoạn trong suốt audit).
**Cách kiểm chứng:** SSH (`temp/user-manual-ccu-zcu/zcu-ssh.ps1`) → chạy `gsettings`/`dconf`/`xdotool`/`gnome-screenshot` trong đúng context phiên GNOME (`DISPLAY=:0`, `DBUS_SESSION_BUS_ADDRESS=/run/user/1000/bus`, `XAUTHORITY=/run/user/1000/gdm/Xauthority`). Mọi cửa sổ test mở ra đã được đóng lại (khôi phục baseline). **KHÔNG** sửa code/cấu hình máy, **KHÔNG** reboot.
**Phạm vi đã cover:** ~30 hạng mục thuộc 5 nhóm (bàn phím, chuột/cảm ứng, thoát app, truy cập hệ thống, chống tự chỉnh).

> ⚠️ **PHÁT HIỆN NGHIÊM TRỌNG NHẤT:** Phím `Super+1..9` (`switch-to-application-N`) **KHÔNG bị F09 khoá**. Trên máy này favorite-apps = `[Nautilus, parkingv8, Extension Manager, GNOME Terminal]` → **Super+4 mở thẳng GNOME Terminal (shell tương tác), Super+1 mở Nautilus (toàn bộ filesystem)**. Đã kiểm chứng bằng xdotool + screenshot thật. Đây là lỗ hổng phá vỡ TOÀN BỘ lockdown F09.

---

## 0. Tình trạng hiện tại của máy (quan trọng — ngữ cảnh cho mọi phát hiện)

| Quan sát | Bằng chứng | Ý nghĩa |
|---|---|---|
| **App kiosk KHÔNG chạy** | `pgrep` chỉ thấy `IPGS.RemoteControl.ZcuAgent` (PID 1750); cửa sổ X duy nhất = `mutter guard window` + `gnome-shell`. Screenshot `audit-baseline-desktop.png` = desktop trống (wallpaper Ubuntu, không top bar). | Người dùng cuối đang nhìn thấy **desktop GNOME trống** — không có app che. Mọi phím tắt/chuột thao tác thẳng lên shell. |
| **Binary `ipgskioskavalonia` KHÔNG tồn tại** | `command -v ipgskioskavalonia` → rỗng. Autostart `~/.config/autostart/ipgs-kiosk.desktop` trỏ `Exec=ipgskioskavalonia` (lệnh không có trong PATH). | Autostart trỏ tới lệnh không tồn tại → app không bao giờ tự khởi động ở máy này (đây là ZCU chạy agent, không phải máy chạy app kiosk iPGS). |
| **Không có watchdog / systemd service cho app kiosk** | `systemctl --user` + `/etc/systemd/system` không có unit kiosk/ipgs/parking nào ngoài `ipgs-remote-agent.service`. | App kiosk khởi động 1 lần qua autostart `.desktop`, **không có cơ chế tự restart** khi crash/đóng → rơi về desktop trống. |

---

## 1. Bảng tổng hợp

Trạng thái: ✅ Chặn được · 🔴 CÒN HỞ · ⚠️ Cần thử tay tại máy · ❔ Không kiểm chứng được

### Nhóm 1 — Bàn phím / phím tắt GNOME

| Hạng mục | Trạng thái | Rủi ro | Bằng chứng | Khắc phục đề xuất |
|---|---|---|---|---|
| `switch-to-application-1..9` (**Super+1..9**) | 🔴 CÒN HỞ | **P1** | **Super+4 → GNOME Terminal mở** (`audit-super4-terminal.png`, prompt `kztek@ubuntu-22:~$`); **Super+1 → Nautilus mở** (`audit-super1-nautilus.png`). `gsettings writable ... switch-to-application-4 = true` (không bị khoá). | Set `org.gnome.shell.keybindings switch-to-application-1..9 = @as []` + LOCK trong dconf system-db. Đồng thời **xoá favorite-apps nhạy cảm** (Terminal, Nautilus, Extension Manager) khỏi `org.gnome.shell favorite-apps`. |
| `minimize` (**Super+h**) | 🔴 CÒN HỞ | P2 | Còn giá trị `['<Super>h']`, `writable=true`. (Không test trực quan được vì hiện không có app; nhưng key active → minimize app kiosk → lộ desktop.) | Khoá `wm.keybindings/minimize = @as []` + lock. |
| `activate-window-menu` (**Alt+Space**) | 🔴 CÒN HỞ | P2 | Còn `['<Alt>space']`, `writable=true`. Menu tiêu đề cửa sổ có Close/Minimize/Move/Resize → thoát/thu nhỏ app. | Khoá `wm.keybindings/activate-window-menu = @as []` + lock. |
| `begin-move`/`begin-resize` (**Alt+F7/F8**) | 🔴 CÒN HỞ | P2 | Còn `['<Alt>F7']`/`['<Alt>F8']`, `writable=true`. Cho kéo/di app fullscreen ra ngoài bằng bàn phím → lộ desktop. | Khoá cả 2 = `@as []` + lock. |
| `switch-group`/`-backward` (**Alt+`**) | 🔴 CÒN HỞ | P2 | Còn `['<Super>Above_Tab','<Alt>Above_Tab']`. F09 khoá Alt+Tab (`switch-applications`) nhưng **bỏ sót** Alt+\` (switch-group). | Khoá `switch-group` + `switch-group-backward` = `@as []` + lock. |
| `cycle-panels`/`switch-panels` (**Ctrl+Alt+Esc / Ctrl+Alt+Tab**) | 🔴 CÒN HỞ | P2 | Còn `['<Control><Alt>Escape']` / `['<Control><Alt>Tab']`. Chuyển focus sang top bar/panel hệ thống — lối thoát kiosk kinh điển. | Khoá `cycle-panels`, `cycle-panels-backward`, `switch-panels`, `switch-panels-backward` = `@as []` + lock. |
| Screenshot UI (**Print** / Shift+Print / Alt+Print / Ctrl+Shift+Alt+R) | 🔴 CÒN HỞ | P3 | **Đã kiểm chứng:** nhấn `Print` → overlay screenshot GNOME hiện (`audit-print-screenshot-ui.png` khác baseline). Còn `show-screenshot-ui=['Print']`, `screenshot`, `screenshot-window`, `show-screen-recording-ui`. | Khoá nhóm `shell.keybindings/screenshot*`, `show-screenshot-ui`, `show-screen-recording-ui` = `@as []` + lock. |
| `help` (**Super+F1**) | 🔴 CÒN HỞ | P3 | `media-keys/help = ['', '<Super>F1']` (không bị khoá; F09 chỉ khoá `home/email/www/...`). Mở trình xem trợ giúp Yelp → có thể điều hướng/mở link. | Khoá `media-keys/help = @as []` + lock. |
| `magnifier` / `magnifier-zoom-in/out` / `screenreader` (Alt+Super+8/s...) | 🔴 CÒN HỞ | P3 | Còn giá trị mặc định. Bật phóng đại/Orca → làm rối màn hình (annoyance, không phải escape). | Khoá nhóm a11y media-keys = `@as []` + lock (hoặc chấp nhận rủi ro thấp). |
| Phím media dạng `*-static` (**XF86Tools/WWW/Explorer/Mail/Calculator/Search**) | 🔴 CÒN HỞ | P2–P3 | `control-center-static=['XF86Tools']`, `www-static=['XF86WWW']`, `home-static=['XF86Explorer']`, `email-static`, `calculator-static`, `search-static` — **KHÔNG bị khoá** (F09 chỉ khoá bản không `-static`). Bàn phím đa phương tiện có các phím này → mở Settings/trình duyệt/file manager. | Khoá thêm toàn bộ biến thể `*-static` tương ứng, hoặc set `= @as []` + lock. Rủi ro thực tế phụ thuộc có bàn phím đa phương tiện gắn vào không. |
| `shift-overview-up/down`, `open-application-menu` (Super+Alt+↑↓, Super+F10) | 🔴 CÒN HỞ | P3 | Còn giá trị mặc định, writable. | Khoá = `@as []` + lock nếu muốn triệt để. |
| **VT switch** (**Ctrl+Alt+F1..F6** → console text) | 🔴 CÒN HỞ | P2 | `/etc/X11` **KHÔNG có** `DontVTSwitch`; `loginctl show-seat seat0` → `CanTTY=yes`. → Ctrl+Alt+F3.. chuyển sang VT text (rời kiosk; nếu biết mật khẩu kztek → shell). *Không trigger qua SSH vì không chvt lại được — xác nhận bằng config.* | Thêm `Section "ServerFlags" Option "DontVTSwitch" "true" EndSection` vào `/etc/X11/xorg.conf.d/`; cân nhắc `logind.conf ReserveVT`. **Cần thử tay** để xác nhận hành vi phím. |
| **Alt+SysRq** (magic SysRq: B=reboot, O=poweroff...) | 🔴 CÒN HỞ | P2–P3 | `/proc/sys/kernel/sysrq = 176` (=16 sync +32 remount-ro +128 reboot/poweroff **đang bật**). Alt+SysRq+B/O buộc reboot/tắt máy (DoS kiosk). Cần bàn phím vật lý có phím SysRq. | Set `kernel.sysrq=0` (hoặc bitmask an toàn) trong `/etc/sysctl.d/`. **Cần thử tay** để xác nhận keypress. |
| **Alt+Tab** (`switch-applications`) | ✅ Chặn | — | `= @as []`, `writable=false` (F09 lock enforced). | — |
| **Alt+F2** (`panel-run-dialog`) | ✅ Chặn | — | **Đã kiểm chứng:** nhấn Alt+F2 → không có run dialog (screenshot y hệt baseline). Key locked. | — |
| **Alt+F4** (`close`) | ✅ Chặn | — | `wm.keybindings/close = @as []` (nằm trong F09 lock list, đã `dconf update`). | — |
| **Ctrl+Alt+Backspace** (zap X) | ⚠️ Cần thử tay | P3 | Không thấy `terminate:ctrl_alt_bksp` trong `setxkbmap -query`; Ubuntu default DontZap bật (ctrl+alt+bksp tắt mặc định từ X.Org 1.6). Nhiều khả năng đã vô hiệu, nhưng chưa hardening tường minh. | Xác nhận tại máy; nếu muốn chắc: thêm `XKBOPTIONS` bỏ `terminate`. |
| **Ctrl+Alt+Del** | ⚠️ Cần thử tay | P3 | `disable-log-out=true` (locked) — hộp thoại logout mặc định bị vô hiệu, nhưng chưa test trực tiếp keypress. | Thử tay; đã có lockdown log-out che phần lớn. |

### Nhóm 2 — Chuột / cảm ứng

| Hạng mục | Trạng thái | Rủi ro | Bằng chứng | Khắc phục |
|---|---|---|---|---|
| Chuột phải trên desktop (context menu) | ✅ Chặn | — | **Đã kiểm chứng:** `xdotool mousemove 960 500 click 3` → không có cửa sổ menu nào (ding/desktop-icons đã tắt → root window không có menu). | — |
| Hot corner (góc trên trái mở overview) | ✅ Chặn | — | `enable-hot-corners=false`, `writable=false` (locked). | — |
| **Cử chỉ cảm ứng vuốt 3 ngón → Activities overview** | ⚠️ Cần thử tay | P2 | Đã biết từ F09: GNOME 40+ hard-code, không có key dconf. Giảm nhẹ: đã ẩn Activities + search (Just Perfection). **Không giả lập qua SSH được.** | Xem hướng dẫn thử tay §3. Chặn triệt để cần session `gnome-kiosk`/`cage`. |
| **Nhấn giữ lâu trên cảm ứng = right-click** | ⚠️ Cần thử tay | P2 | User báo chính đường này bung overview trước đây. Không giả lập touch thật qua SSH. | Thử tay §3. |
| Chuột giữa (paste/middle-click emulation) | ✅ Chặn (touchpad) | P3 | `touchpad middle-click-emulation=false`. Chuột USB vẫn có nút giữa vật lý (không dconf hoá được). | Rủi ro thấp. |

### Nhóm 3 — Thoát / đóng ứng dụng kiosk

| Hạng mục | Trạng thái | Rủi ro | Bằng chứng | Khắc phục |
|---|---|---|---|---|
| App crash/đóng → không tự khởi động lại | 🔴 CÒN HỞ | **P1** | Không có systemd service/watchdog cho app (chỉ autostart `.desktop` chạy 1 lần). Hiện tại app **không chạy** → desktop trống. | Chạy app qua systemd `--user` service `Restart=always` (hoặc `gnome-kiosk` session), không dựa vào autostart `.desktop`. Sửa `Exec=` trỏ đúng binary. |
| Alt+F4 đóng app | ✅ Chặn | — | `close` khoá (xem Nhóm 1). | — |
| App fullscreen/always-on-top | ❔ Không kiểm chứng được | — | App không chạy → không đánh giá được fullscreen/topmost. Cần chạy app thật để xác nhận. | Khi deploy app thật: xác nhận fullscreen + không minimize được (kết hợp khoá `minimize`). |

### Nhóm 4 — Truy cập hệ thống khác

| Hạng mục | Trạng thái | Rủi ro | Bằng chứng | Khắc phục |
|---|---|---|---|---|
| **Nautilus / file manager reachable** | 🔴 CÒN HỞ | **P1** | Super+1 mở Nautilus (`audit-super1-nautilus.png`): thấy Home, "Other Locations", VBox shared folder, toàn bộ cây thư mục. Từ đây đổi tên/chạy file/duyệt filesystem. | Xoá Nautilus khỏi favorites + khoá Super+N; cân nhắc gỡ/che nautilus. |
| **GNOME Terminal reachable** | 🔴 CÒN HỞ | **P1** | Super+4 mở Terminal (`audit-super4-terminal.png`) — shell đầy đủ, **bỏ qua `disable-command-line=true`** (setting đó chỉ chặn run-dialog, không chặn khởi động app terminal). | Xoá Terminal khỏi favorites + khoá Super. Cân nhắc gỡ `gnome-terminal` khỏi máy kiosk. |
| **USB cắm vào → tự mở file manager** | 🔴 CÒN HỞ | P2 | `media-handling automount=true`, `automount-open=true`, `autorun-never=false`, `autorun-x-content-start-app` chứa `x-content/unix-software`. → cắm USB tự mount + mở Nautilus + có thể tự chạy phần mềm từ media. **Cần thử tay** để xác nhận vật lý. | Set `automount=false`, `automount-open=false`, `autorun-never=true` + lock. |
| gnome-control-center (Settings) hiện diện | 🔴 CÒN HỞ | P2 | `command -v gnome-control-center` → present. `control-center` media-key bị khoá nhưng `control-center-static=['XF86Tools']` **không** khoá; Settings cũng mở được gián tiếp qua Nautilus/Terminal. | Khoá `-static` + cân nhắc gỡ control-center trên kiosk. |
| firefox hiện diện | 🔴 CÒN HỞ | P2 | `command -v firefox` → present; `www-static=['XF86WWW']` không khoá; mở được qua Terminal/Nautilus. | Gỡ hoặc chặn khởi động trên kiosk. |
| Màn hình khoá (lock screen) | ✅ Chặn | — | `disable-lock-screen=true` (locked); `screensaver lock-enabled=false` (locked); Super+L (`screensaver`) không khoá nhưng lock đã bị vô hiệu → không gây lockout. | — |
| GDM greeter: nút Restart/Power/"Not listed?" | ⚠️ Cần thử tay | P3 | `/etc/gdm3/greeter.dconf-defaults`: `disable-restart-buttons=true` và `disable-user-list=true` đều **đang comment (`#`)** → greeter mặc định hiện nút restart/power. Autologin che phần lớn (greeter hiếm hiện). | Bỏ comment 2 dòng trên trong greeter.dconf-defaults + `dconf update`. Thử tay khi greeter hiện. |

### Nhóm 5 — Chống tự chỉnh lại

| Hạng mục | Trạng thái | Rủi ro | Bằng chứng | Khắc phục |
|---|---|---|---|---|
| **User kiosk `kztek` thuộc group `sudo`** | 🔴 CÒN HỞ | P2 | `id kztek` → `groups=...,27(sudo),...`. `sudo -n -l` → "a password is required" (**KHÔNG NOPASSWD** — điểm tốt). Nhưng nếu biết mật khẩu kztek → `sudo` → root → gỡ mọi dconf lock. | Loại kztek khỏi group `sudo`; tạo user admin riêng để bảo trì. Đây là điều kiện cần để lockdown thực sự bất khả xâm phạm. |
| dconf lock enforcement | ✅ Chặn | — | **Đã kiểm chứng 5 key:** `disable-command-line`, `overlay-key`, `switch-applications`, `media-keys/terminal`, `screensaver/lock-enabled` → tất cả `writable=false` + `gsettings set` báo **"The key is not writable"** + giá trị không đổi. F09 lock hoạt động đúng. | — (giữ nguyên) |
| **`~/.config/autostart` + `~/.bashrc` GHI ĐƯỢC** | 🔴 CÒN HỞ | P2 | `~/.config` writable, `~/.bashrc` writable (user sở hữu home). Kết hợp Super+4 (terminal) → thả file `.config/autostart/*.desktop` hoặc dòng `.bashrc` = cửa hậu bền vững qua reboot. | Không thể tước quyền ghi home của chính user; **giảm rủi ro gốc = chặn đường vào terminal (Super+4) + bỏ sudo**. |
| Chỉ 1 extension bật (Just Perfection); **Block Caribou 36 chưa cài** | 🔴 CÒN HỞ | P3 | `gnome-extensions list --enabled` → chỉ `just-perfection-desktop`. `gnome-extensions info block-caribou-36...` → **NOT INSTALLED**. `screen-keyboard-enabled=false` nhưng (theo ghi chú 1-install-software.sh) setting này KHÔNG đủ chặn OSK tự bật khi chạm. | Cài + enable Block Caribou 36 nếu là màn cảm ứng (chặn bàn phím ảo tự bung). **Cần thử tay** trên touch để xác nhận OSK. |
| Extension Manager trong favorites (Super+3) | 🔴 CÒN HỞ | P2 | favorite-apps chứa `com.mattjakeman.ExtensionManager.desktop` → Super+3 mở Extension Manager → **tắt Just Perfection** = hiện lại top bar/activities/dash, mở toang UI. | Xoá khỏi favorites + gỡ Extension Manager trên kiosk. |

---

## 2. 🔴 Ưu tiên xử lý ngay (giảm dần theo rủi ro)

1. **[P1] Super+1..9 mở Terminal/Nautilus/Extension Manager** — nguy hiểm nhất, đã kiểm chứng bằng ảnh. Một phím vật lý = shell/file manager, phá vỡ toàn bộ F09.
   → Khoá `switch-to-application-1..9 = @as []` + lock **VÀ** dọn `favorite-apps` (bỏ Terminal, Nautilus, Extension Manager).
2. **[P1] Không có watchdog + binary app sai** — app không chạy, rơi về desktop trống; nếu app thật crash cũng không tự dậy.
   → systemd `--user` service `Restart=always`, sửa `Exec=` đúng binary (hoặc dùng `gnome-kiosk`).
3. **[P1/P2] Nautilus + GNOME Terminal + firefox + control-center hiện diện & reachable** — mọi công cụ thoát đều có sẵn.
   → Gỡ/chặn trên máy kiosk; tối thiểu bỏ khỏi favorites + khoá phím.
4. **[P2] kztek trong group `sudo`** — biết mật khẩu là thành root, gỡ sạch lock.
   → Loại khỏi sudo, tách user admin bảo trì.
5. **[P2] Bộ phím tắt WM còn hở:** Super+h (minimize), Alt+Space (window menu), Alt+F7/F8 (move/resize), Alt+\` (switch-group), Ctrl+Alt+Tab/Esc (switch/cycle-panels) — đều lộ desktop/thoát app.
   → Bổ sung vào danh sách khoá dconf F09 + lock.
6. **[P2] VT switch (Ctrl+Alt+F1..F6)** — không có `DontVTSwitch`.
   → Thêm `DontVTSwitch=true` vào xorg.conf.d.
7. **[P2] USB automount-open=true / autorun** — cắm USB tự mở Nautilus + tự chạy phần mềm.
   → `automount=false`, `automount-open=false`, `autorun-never=true` + lock.
8. **[P2] Alt+SysRq bật (sysrq=176)** — Alt+SysRq+B/O buộc reboot/tắt.
   → `kernel.sysrq=0`.
9. **[P2] ~/.config/autostart + ~/.bashrc ghi được** — cửa hậu bền vững (chỉ khai thác được sau khi có terminal → xử lý #1 là chặn gốc).
10. **[P3] Screenshot UI (Print), help (Super+F1), a11y magnifier/screenreader, phím `*-static`, greeter restart-buttons, Block Caribou chưa cài** — annoyance/edge, xử lý sau khi xong P1/P2.

---

## 3. ⚠️ Cần user thử tay tại máy (không giả lập được qua SSH)

Chuẩn bị: đứng trước màn hình ZCU thật; nếu có thể, mở app kiosk trước để test "thoát khỏi app".

1. **Cử chỉ 3 ngón trên màn cảm ứng:** Vuốt 3 ngón từ dưới lên giữa màn hình → quan sát Activities overview có bung không. (Dự kiến VẪN bung — GNOME hard-code; đã ẩn search nên không gõ được tên app, nhưng overview mở ra là đã lộ.)
2. **Nhấn giữ lâu trên cảm ứng (long-press):** Chạm và giữ ~1s lên vùng trống/nút → xem có ra menu chuột phải hoặc bung overview không (user từng báo đường này).
3. **Ctrl+Alt+F3 (VT switch):** Nhấn → xem có nhảy ra màn hình console text (login prompt) không. Nhấn Ctrl+Alt+F1 (hoặc F2) để quay lại kiosk. Nếu ra được console = xác nhận gap #6.
4. **Alt+SysRq+S rồi Alt+SysRq+U (an toàn, không reboot):** nếu đèn/hành vi phản hồi = SysRq đang bật (KHÔNG thử +B/+O vì sẽ reboot/tắt).
5. **Cắm USB thật:** Cắm 1 USB có file → xem Nautilus có tự bật + tự mở thư mục USB không (xác nhận gap #7).
6. **Bàn phím ảo (OSK):** Chạm vào 1 ô nhập liệu (nếu app có) → xem bàn phím ảo GNOME tự bung không (Block Caribou chưa cài).
7. **Ctrl+Alt+Backspace:** Nhấn → xem X có bị kill/logout không (dự kiến không, nhưng cần xác nhận).
8. **GDM greeter:** Nếu từng thấy màn đăng nhập (VD sau khi tắt autologin để bảo trì) → xem có nút Restart/Power/"Not listed?" không.

---

## 4. Kết luận — đánh giá thẳng mức an toàn

**Kiosk hiện tại KHÔNG an toàn để đưa ra môi trường công cộng.**

F09 (dconf system-db + lock) **có hoạt động đúng** cho tập key nó bao phủ (đã kiểm chứng `writable=false`), nhưng **danh sách khoá bị thiếu nhiều key nguy hiểm** — nghiêm trọng nhất là `switch-to-application-1..9` (Super+1..9), cho phép **mở thẳng GNOME Terminal (Super+4) và Nautilus (Super+1)** chỉ bằng một tổ hợp phím vật lý, đã chứng minh bằng ảnh chụp thật. Khi đã có terminal + user `kztek` nằm trong group `sudo` + home ghi được, kẻ tấn công tại chỗ có thể: chạy lệnh tùy ý, gỡ toàn bộ dconf lock (nếu biết mật khẩu → root), cài cửa hậu bền vững. Bên cạnh đó còn hàng loạt đường phụ chưa chặn: VT switch (Ctrl+Alt+Fx), USB automount-open, Alt+SysRq, và bộ phím WM (Super+h/Alt+Space/Alt+F7/Ctrl+Alt+Tab...).

Ngoài ra app kiosk **không chạy và không có watchdog** — bản thân màn hình đang là desktop GNOME trống, phơi bày mọi vector trên ngay lập tức.

**Khuyến nghị chiến lược:** vá gấp nhóm P1 (khoá Super+1..9 + dọn favorites + watchdog app + gỡ/chặn Terminal/Nautilus/firefox) là bắt buộc trước bất kỳ release nào. Về lâu dài, cách bền vững nhất là chuyển sang **session kiosk chuyên dụng (`gnome-kiosk` hoặc compositor `cage`)** thay vì khoá từng phím trên GNOME Shell đầy đủ — vì cách khoá-từng-key luôn có nguy cơ bỏ sót (đúng như audit này phát hiện), và mọi thứ đổ vỡ nếu user có được một terminal + sudo.

---

## Phụ lục — Bằng chứng ảnh (docs/bugs/screenshots/)

| File | Nội dung |
|---|---|
| `audit-baseline-desktop.png` | Desktop GNOME trống (app kiosk không chạy) — trạng thái người dùng đang thấy |
| `audit-super4-terminal.png` | **Super+4 → GNOME Terminal mở** (shell `kztek@ubuntu-22:~$`) |
| `audit-super1-nautilus.png` | **Super+1 → Nautilus mở** (Home + Other Locations + filesystem) |
| `audit-print-screenshot-ui.png` | Print → overlay screenshot UI GNOME hiện |

*Các phép đo `gsettings`/`dconf`/`loginctl`/`pgrep`/`/proc/sys/kernel/sysrq` trích dẫn trực tiếp trong bảng, chạy trong context phiên GNOME `:0` của kztek qua SSH ngày 2026-07-27.*

---
*QA Engineer — audit only (không sửa code/cấu hình máy). Bàn giao QA Lead / Senior Developer để lên bản vá theo thứ tự ưu tiên §2.*
