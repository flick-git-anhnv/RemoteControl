---
title: "DEPLOY — CCU Remote Control ZCU"
feature: ccu-zcu-remote-control
author: DevOps Engineer
created: 2026-07-23
updated: 2026-07-23
version: "1.3"
tdd: docs/tech-design/TDD-remote-control.md
---

# Hướng dẫn Triển khai — CCU Remote Control ZCU

Tài liệu này mô tả các bước kỹ thuật để cài đặt và triển khai tính năng Remote Control từ CCU đến ZCU. Đây là **hướng dẫn dành cho người vận hành/dev**, không phải hướng dẫn thao tác cuối người dùng.

Chi tiết giao thức TCP, message format, và kiến trúc nội bộ: xem `docs/tech-design/TDD-remote-control.md`.

---

## 🔌 Cài đặt OFFLINE (không cần Internet) — F15/F16

Kể từ bản này, `IPGS.RemoteControl.CcuUI` **nhúng sẵn** mọi thứ cần thiết để cài ZCU/kiosk mà KHÔNG cần Internet:

| Resource nhúng (`IPGS.RemoteControl.CcuUI/Resources/`) | Dùng cho | Kích thước |
|---|---|---|
| `zcu-agent/linux-x64/` | Binary ZcuAgent đã publish (framework-dependent) | ~11 MB |
| `dotnet-runtime/dotnet-runtime-8.0-linux-x64.tar.gz` | .NET 8 Runtime — giải nén thẳng vào `$HOME/.dotnet` trên ZCU | ~30 MB |
| `x11-deb/*.deb` | `libx11-6`, `libxext6`, `libxtst6` (Ubuntu 22.04 amd64) — cài bằng `dpkg -i` | ~700 KB |
| `scripts/setup-zcu-agent.sh` | Bản tham khảo của script cài tay (không bắt buộc dùng qua wizard) | 8 KB |
| `scripts/linux-kiosk/*.sh` | 4 script Kiosk Deploy (1-install-software, 2-configure-system, 3-toggle-topbar, kiosk-setup-gui) | 84 KB |
| `gnome-extensions/*.zip` | Extension GNOME Shell 42: Just Perfection + Block Caribou 36 | ~164 KB |

**Cách hoạt động:** `ZcuRemoteInstallerService` và `KioskDeployService` (trong `IPGS.RemoteControl.CcuClient`) tự tìm các resource này tại `<thư mục chứa CcuUI.exe>/Resources/...` (qua `ResolveResourceDir`). Nếu tìm thấy → upload qua SFTP và cài bằng `dpkg -i`/giải nén tarball/giải nén zip cục bộ, không gọi `apt-get`/`wget`/`curl` ra mạng cho các phần này. Nếu KHÔNG thấy (ví dụ chạy bản CcuUI cũ chưa có resource) → tự động fallback về đường mạng như trước (không phá luồng cũ).

> ⚠️ Vẫn cần mạng cho: `apt install curl unzip python3-pip unclutter` + `pip3 install gnome-extensions-cli` trong `1-install-software.sh` (Kiosk Deploy) — các gói hệ thống nhẹ này chưa được offline-hóa.

**Khi nào cần refresh resource:** mỗi khi sửa code ZcuAgent hoặc script kiosk, phải build/copy lại rồi rebuild CcuUI:

```bash
# 1. Publish lại ZcuAgent
dotnet publish IPGS.RemoteControl.ZcuAgent/IPGS.RemoteControl.ZcuAgent.csproj -c Release -r linux-x64 --self-contained false -o IPGS.RemoteControl.ZcuAgent/publish/linux-x64

# 2. Đồng bộ vào Resources (Windows PowerShell hoặc bash)
cp -r IPGS.RemoteControl.ZcuAgent/publish/linux-x64/. IPGS.RemoteControl.CcuUI/Resources/zcu-agent/linux-x64/
cp scripts/linux-kiosk/*.sh IPGS.RemoteControl.CcuUI/Resources/scripts/linux-kiosk/

# 3. Build lại CcuUI — Content Include="Resources\**" trong .csproj tự copy vào output
dotnet build IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj -c Release
```

`dotnet-runtime-8.0-linux-x64.tar.gz` và 2 file `.zip` GNOME extension chỉ cần tải lại khi nâng version .NET/extension — không đổi theo mỗi lần sửa code ZcuAgent.

---

## ⚡ PHƯƠNG ÁN CÀI ĐẶT NHANH (KHUYẾN NGHỊ)

Để đơn giản hóa và loại bỏ các bước cài đặt thủ công phức tạp, hệ thống hỗ trợ 2 công cụ cài đặt **1-Click tự động hóa 100%**:

### 🎯 Cách 1: Sử dụng Giao diện Setup Wizard UI (1-Click từ máy CCU)

1. Mở ứng dụng **`IPGS.RemoteControl.CcuUI`** trên máy CCU (Windows hoặc Linux).
2. Tại thanh tiêu đề trên cùng, nhấn nút **`⚡ Cài đặt ZCU từ xa...`**.
3. Nhập thông tin kết nối SSH tới máy ZCU:
   - **Địa chỉ IP ZCU:** Ví dụ `192.168.1.50`
   - **Cổng SSH:** `22`
   - **Tài khoản SSH:** Username & Password (hoặc Key)
4. Tùy chỉnh tham số ZcuAgent (Cổng TCP `17600`, Nhấn **🎲 Sinh Token** ngẫu nhiên).
5. Nhấn **🚀 Bắt đầu Cài đặt**:
   - Wizard sẽ **tự động hoàn toàn**: Cài native X11, cài .NET 8 Runtime, upload ZcuAgent binary, cấu hình `appsettings.json`, đăng ký `systemd user service`, enable lingering, mở cổng firewall `ufw 17600`, và tắt chế độ khoá màn hình tự động GNOME.
6. Sau khi xong, ZCU sẽ tự động được thêm vào danh sách máy tính để bạn kết nối ngay lập tức!

---

### 📜 Cách 2: Sử dụng Script Cài đặt Tự động 1 Dòng (Chạy trực tiếp trên ZCU)

Nếu bạn đang ngồi trực tiếp tại máy ZCU (hoặc SSH bằng Terminal), chỉ cần chạy kịch bản tự động hoá:

```bash
# 1. Tải hoặc chạy script setup-zcu-agent.sh
bash scripts/setup-zcu-agent.sh 17600 "CHUOITOKEN_BAOMAT_32KYTU" "0.0.0.0/0"
```

Script sẽ thực hiện toàn bộ 7 bước cấu hình hệ thống và khởi chạy `systemd` service cho ZcuAgent.

---

## 1. Tổng quan kiến trúc thủ công (Dành cho Dev / Sysadmin)

```
[CCU — Windows hoặc Linux]              [ZCU — Ubuntu 22.04 X11]
┌────────────────────────────┐           ┌──────────────────────────────┐
│  IPGS.RemoteControl.CcuUI  │           │  IPGS.RemoteControl.ZcuAgent  │
│  (Avalonia, cross-platform) │ TCP 17600 │  - X11 screen capture (XShm)  │
│  └─ ConnectionEntryWindow  │◄─────────►│  - JPEG encode (SkiaSharp)    │
│  └─ RemoteScreenWindow      │           │  - TCP server (Generic Host)  │
│  └─ IPGS.RemoteControl.     │           │  - Mouse inject (XTest)       │
│     CcuClient               │           │  - Keyboard inject (XTest)   │
└────────────────────────────┘           └──────────────────────────────┘
```

- **CCU** (Windows hoặc Linux): ứng dụng **`IPGS.RemoteControl.CcuUI`** (Avalonia, cross-platform) đóng vai **TCP client**, hiển thị màn hình ZCU và gửi lệnh chuột/bàn phím.
- **ZCU** (Ubuntu 22.04, X11): service .NET 8 đóng vai **TCP server**, chụp màn hình liên tục và inject input nhận từ CCU.
- **Giao thức:** TCP cổng 17600, binary length-prefix, xác thực shared secret, stream JPEG 15 FPS.

---

## 2. Yêu cầu tiên quyết — ZCU-side

### 2.1 Hệ điều hành

- **Ubuntu 22.04 LTS**, session **X11 hoặc GNOME Wayland** — ZcuAgent tự phát hiện lúc start
  và chọn đúng backend (xem branch `wayland`, TDD §14b). Bất kỳ session type nào khác
  (headless, không xác định) sẽ bị từ chối khởi động.

Kiểm tra loại session đang dùng:

```bash
echo $XDG_SESSION_TYPE
```

Kết quả `x11` → dùng đường XTest/XShm (mục 2.2). Kết quả `wayland` → dùng đường Mutter
D-Bus + PipeWire (mục 2.3) — cần thêm gói `gstreamer1.0-pipewire`, ZcuSetupWizard/
`ZcuRemoteInstallerService` tự cài khi phát hiện Wayland.

> ⚠️ Đường Wayland dùng API D-Bus riêng của Mutter (`org.gnome.Mutter.ScreenCast`/
> `RemoteDesktop`), KHÔNG phải xdg-desktop-portal chuẩn — phù hợp kiosk không người trực
> (không cần bấm "Allow" trên dialog chia sẻ màn hình) nhưng CHƯA được kiểm chứng trên
> phần cứng GNOME Shell 42 thật (xem mục "⚠️ CẦN VERIFY" trong TDD §14b). Khuyến nghị test
> kỹ trên 1 máy trước khi rollout diện rộng.

### 2.2 Thư viện native X11

Kiểm tra và cài nếu chưa có:

```bash
dpkg -l libx11-6 libxext6 libxtst6 2>/dev/null | grep ^ii
# Nếu thiếu dòng nào → cài:
sudo apt install -y libx11-6 libxext6 libxtst6
```

| Thư viện | Vai trò |
|----------|---------|
| `libx11-6` | P/Invoke libX11 — mở display, capture màn hình |
| `libxext6` | P/Invoke libXext — MIT-SHM (XShmGetImage, bộ nhớ chia sẻ) |
| `libxtst6` | P/Invoke libXtst — XTest (inject mouse/keyboard) |

### 2.3 Gói cho session Wayland (chỉ cần khi `XDG_SESSION_TYPE=wayland`)

```bash
sudo apt install -y gstreamer1.0-tools gstreamer1.0-plugins-base gstreamer1.0-pipewire
```

| Gói | Vai trò |
|-----|---------|
| `gstreamer1.0-tools` | cung cấp `gst-launch-1.0` — WaylandScreenCapturer chạy nó làm subprocess |
| `gstreamer1.0-plugins-base` | element `videoconvert`/`videorate` dùng trong pipeline capture |
| `gstreamer1.0-pipewire` | element `pipewiresrc` — đọc frame từ PipeWire node do Mutter ScreenCast tạo |

Không cần cài `libx11-6`/`libxext6`/`libxtst6` khi chạy thuần Wayland (nhánh Wayland không
gọi Xlib/XTest), nhưng cài thêm cũng không hại gì nếu máy có thể đổi qua lại X11/Wayland.

### 2.3 .NET 8 Runtime

**Trên production (chỉ cần Runtime, không cần SDK):**

```bash
# Kiểm tra đã có chưa
dotnet --version 2>/dev/null || echo "NOT_FOUND"

# Nếu chưa có — cài qua script Microsoft (không cần sudo/apt):
wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir $HOME/.dotnet

# Thêm vào PATH (thêm vào ~/.bashrc hoặc ~/.profile để áp dụng vĩnh viễn):
echo 'export PATH=$HOME/.dotnet:$PATH' >> ~/.bashrc
source ~/.bashrc

# Xác nhận:
dotnet --version   # phải hiển thị 8.x.x
```

> **Lưu ý:** `--runtime dotnet` chỉ cài Runtime (~90 MB). SDK (~600 MB) chỉ cần nếu build trực tiếp trên ZCU (Cách B — xem mục 3.2).

> ⚠️ **GOTCHA — cài đặt bị gián đoạn giữa chừng (mất mạng, đóng terminal sớm...) có thể tạo ra thư mục `~/.dotnet` KHÔNG ĐẦY ĐỦ** — script vẫn in `Installation finished successfully` ở dòng cuối nhưng thư mục `sdk/` hoặc `shared/` bên trong có thể rỗng/thiếu, khiến `dotnet --version` báo `No .NET SDKs were found` dù rõ ràng vừa cài xong. Luôn xác nhận bằng cách gọi TRỰC TIẾP đường dẫn đầy đủ (bỏ qua PATH) trước khi tin tưởng:
> ```bash
> ls ~/.dotnet/sdk/            # phải thấy 1 thư mục version, VD: 8.0.423
> ~/.dotnet/dotnet --version   # phải in ra đúng version, không lỗi
> ```
> Nếu thiếu `sdk/` hoặc lệnh trên vẫn báo lỗi → xoá và cài lại từ đầu (không cố sửa cài đặt dở dang):
> ```bash
> rm -rf ~/.dotnet
> wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
> chmod +x /tmp/dotnet-install.sh
> /tmp/dotnet-install.sh --channel 8.0 --install-dir $HOME/.dotnet
> ```

---

## 3. Build và Deploy ZcuAgent

### 3.1 Cách A — Khuyến nghị Production

Build trên máy dev Windows/Linux, copy publish output sang ZCU. ZcuAgent chạy như **systemd user service**.

#### Bước 1: Build và Publish trên máy dev

```bash
# Từ thư mục gốc solution (iPGSv4/)
cd IPGS.RemoteControl.ZcuAgent

dotnet publish -c Release -r linux-x64 --self-contained false \
  -o ./publish/linux-x64

# Sau khi thành công, publish output nằm tại:
# IPGS.RemoteControl.ZcuAgent/publish/linux-x64/
```

> `--self-contained false`: publish framework-dependent — nhẹ hơn (~5 MB), yêu cầu .NET 8 Runtime đã cài trên ZCU (mục 2.3). Nếu muốn binary tự đủ (không cần Runtime trên ZCU), đổi thành `--self-contained true` (~80 MB).

#### Bước 2: Copy sang ZCU

```bash
# Từ máy dev — thay <zcu-ip> và <zcu-user> cho phù hợp:
scp -r IPGS.RemoteControl.ZcuAgent/publish/linux-x64/ \
  <zcu-user>@<zcu-ip>:/home/<zcu-user>/ipgs/remote-agent/

# Hoặc dùng rsync (đồng bộ thay vì copy lại toàn bộ):
rsync -avz --delete IPGS.RemoteControl.ZcuAgent/publish/linux-x64/ \
  <zcu-user>@<zcu-ip>:/home/<zcu-user>/ipgs/remote-agent/
```

#### Bước 3: Cấu hình appsettings.json trên ZCU

Chỉnh sửa file cấu hình tại thư mục vừa copy:

```bash
nano /home/<zcu-user>/ipgs/remote-agent/appsettings.json
```

Nội dung mẫu (đổi các giá trị cần thiết):

```json
{
  "RemoteControl": {
    "Port": 17600,
    "Token": "THAY_BANG_CHUOI_NGAU_NHIEN_DAI_IT_NHAT_32_KY_TU",
    "AllowedClientIPs": [ "192.168.1.0/24" ],
    "TargetFps": 15,
    "JpegQuality": 70,
    "MaxFrameBytes": 8388608
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**Lưu ý cấu hình quan trọng:**

| Field | Mô tả | Khuyến nghị |
|-------|-------|-------------|
| `Token` | Shared secret dùng để xác thực CCU. PHẢI đổi khỏi giá trị placeholder. | Chuỗi random ≥ 32 ký tự (chữ + số + ký tự đặc biệt). Dùng: `openssl rand -hex 32` |
| `AllowedClientIPs` | Danh sách IP/CIDR của CCU được phép kết nối. | Giới hạn subnet LAN nội bộ. KHÔNG để `0.0.0.0/0` trừ môi trường test cô lập. |
| `Port` | Cổng TCP ZcuAgent lắng nghe. | 17600 (mặc định). Đổi nếu bị xung đột. |
| `TargetFps` | Tốc độ khung hình stream. | 15 cho mạng LAN ổn định; giảm xuống 10 nếu ZCU CPU cao. |
| `JpegQuality` | Chất lượng nén JPEG (1–100). | 70 là mức cân bằng tốt. Tăng lên 80–85 nếu cần hình ảnh sắc nét hơn; giảm nếu băng thông hạn chế. |

> **Bảo mật:** TUYỆT ĐỐI KHÔNG log giá trị Token. ZcuAgent đã được implement để chỉ log `AUTH_OK for <ip>` — không log nội dung token.

#### Bước 4: Phân quyền thực thi

```bash
chmod +x /home/<zcu-user>/ipgs/remote-agent/IPGS.RemoteControl.ZcuAgent
```

#### Bước 5: Tạo systemd user service

ZcuAgent cần quyền truy cập display server (X11 hoặc Wayland) của session desktop đang đăng
nhập. Khuyến nghị chạy như **systemd user service** (không phải system service) để kế thừa
đúng biến môi trường (`DISPLAY`/`XAUTHORITY` cho X11, `DBUS_SESSION_BUS_ADDRESS` cho Wayland)
của user.

Tạo file unit:

```bash
mkdir -p ~/.config/systemd/user/
nano ~/.config/systemd/user/ipgs-remote-agent.service
```

Nội dung file unit:

```ini
[Unit]
Description=IPGS Remote Control ZCU Agent
After=graphical-session.target
Wants=graphical-session.target

[Service]
Type=simple
ExecStart=/home/%u/ipgs/remote-agent/IPGS.RemoteControl.ZcuAgent
WorkingDirectory=/home/%u/ipgs/remote-agent
Environment=DOTNET_ROOT=%h/.dotnet
Environment=PATH=%h/.dotnet:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
# QUAN TRỌNG: XDG_SESSION_TYPE KHÔNG tự inherit vào systemd --user unit — set thủ công
# khớp với session type thật của desktop (kiểm tra trước: `loginctl show-session
# $XDG_SESSION_ID -p Type --value` khi đang đăng nhập desktop đó). Sai giá trị ở đây
# khiến ZcuAgent chọn nhầm backend (Xlib vs D-Bus) và fail ngay khi start.
# X11:     Environment=DISPLAY=:0  (thêm dòng này)
#          Environment=XDG_SESSION_TYPE=x11
# Wayland: KHÔNG cần DISPLAY
#          Environment=XDG_SESSION_TYPE=wayland
Environment=XDG_SESSION_TYPE=x11
Environment=DISPLAY=:0
Restart=on-failure
RestartSec=5
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=default.target
```

**Lưu ý về `DISPLAY=:0`:**

- `:0` là display X11 mặc định khi chỉ có 1 màn hình. Xác nhận display đúng bằng:

  ```bash
  who        # xem session nào đang dùng display nào
  echo $DISPLAY   # khi đang đăng nhập desktop
  ```

- Nếu `DISPLAY` không phải `:0` (ví dụ `:1`), chỉnh lại trong file unit.
- `XAUTHORITY` thường tự được kế thừa từ session user khi dùng user service — không cần set thêm. Nếu gặp lỗi quyền X11, thêm:

  ```ini
  Environment=XAUTHORITY=%h/.Xauthority
  ```

Kích hoạt và khởi động service:

```bash
# Reload cấu hình systemd user
systemctl --user daemon-reload

# Bật service tự khởi động khi user login
systemctl --user enable ipgs-remote-agent.service

# Khởi động ngay
systemctl --user start ipgs-remote-agent.service

# Kiểm tra trạng thái
systemctl --user status ipgs-remote-agent.service

# Xem log
journalctl --user -u ipgs-remote-agent.service -f
```

> **Quan trọng — User service vs System service:**
> - **User service** (`systemctl --user`): chạy dưới quyền user đang đăng nhập desktop, kế thừa X11 session → **khuyến nghị**.
> - **System service** (`/etc/systemd/system/`): chạy trước khi user login, không có DISPLAY/XAUTHORITY → cần cấu hình thêm phức tạp và dễ lỗi; KHÔNG khuyến nghị cho tính năng này.
>
> Để user service tự chạy khi user login (ngay cả khi chưa mở terminal), bật lingering:
> ```bash
> sudo loginctl enable-linger <zcu-user>
> ```

---

### 3.2 Cách B — Test nhanh / Dev (không dùng cho production)

Cách này đã được dùng trong quá trình phát triển và test tính năng. **Không khuyến nghị cho production** vì cần cài đầy đủ .NET SDK và service chạy foreground (mất khi đóng terminal).

```bash
# 1. Cài .NET SDK (đầy đủ, ~600 MB):
wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 8.0 --install-dir $HOME/.dotnet
export PATH=$HOME/.dotnet:$PATH

# 1b. XÁC NHẬN cài đủ trước khi tiếp tục (xem GOTCHA mục 2.3 — cài có thể báo
#     "thành công" nhưng thiếu thư mục sdk/ nếu bị gián đoạn giữa chừng):
ls ~/.dotnet/sdk/
~/.dotnet/dotnet --version

# 2. SSH vào ZCU, set DISPLAY trỏ về X11 session thật:
export DISPLAY=:0

# 3. Kiểm tra quyền truy cập X server (trước khi chạy ZcuAgent):
xdpyinfo | head -5    # phải hiện thông tin màn hình, không báo lỗi "cannot open display"

# 4. Clone hoặc copy source, rồi chạy trực tiếp:
cd /path/to/iPGSv4/IPGS.RemoteControl.ZcuAgent
dotnet run --configuration Release
```

> **Vì sao cần `DISPLAY=:0` khi SSH?**
> SSH session không có DISPLAY X11 mặc định. Khi đăng nhập desktop thật, user có DISPLAY (ví dụ `:0`). ZcuAgent cần DISPLAY để gọi `XOpenDisplay` và capture màn hình. Nếu không set DISPLAY, ZcuAgent sẽ fail khi gọi `XOpenDisplay(null)`.
>
> Lệnh `who` cho biết display nào đang được dùng bởi session desktop hiện tại. Lệnh `xdpyinfo` xác nhận ZcuAgent có quyền truy cập X server.

---

## 4. Cấu hình CCU-side (IPGS.RemoteControl.CcuUI)

> **Thay đổi kiến trúc v1.2:** `IPGS.RemoteControl.CcuUI` đã được tách thành **ứng dụng Avalonia độc lập** (`.exe` riêng), không còn nhúng trong `IPGSUseCam`.
>
> **Thay đổi v1.3:** `CcuUI` giờ hỗ trợ **cross-platform — Windows và Linux**. Người vận hành CCU có thể chạy viewer từ máy Windows hoặc máy Linux (ví dụ: máy admin Linux truy cập vào ZCU từ xa).

### 4.1 Build ứng dụng CcuUI

#### Windows

```powershell
# Build nhanh (dev/test, không publish — chạy framework-dependent):
dotnet build IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj -c Release

# Publish framework-dependent (yêu cầu .NET 8 Runtime đã cài trên máy đích):
dotnet publish IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj -c Release -r win-x64 --self-contained false -o IPGS.RemoteControl.CcuUI/publish/win-x64/
```

Output: `IPGS.RemoteControl.CcuUI.exe` trong thư mục publish.

#### Linux (máy CCU hoặc admin chạy trên Linux)

```bash
# Publish framework-dependent (yêu cầu .NET 8 Runtime đã cài):
dotnet publish IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj -c Release -r linux-x64 --self-contained false -o IPGS.RemoteControl.CcuUI/publish/linux-x64/

# Chạy trực tiếp trên máy Linux (dev/test — không cần publish, cần .NET 8 SDK):
dotnet run --project IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj
```

Output: `IPGS.RemoteControl.CcuUI` (binary Linux ELF) trong thư mục publish.

> **Yêu cầu môi trường Linux để chạy CcuUI:**
> - `.NET 8 Runtime` đã cài (`dotnet --version` phải hiển thị `8.x.x`).
> - Đang trong session **X11** hoặc **Wayland** với môi trường desktop (GNOME, KDE, XFCE...) — CcuUI là Avalonia GUI app, cần display server.
> - Biến môi trường `DISPLAY` (X11) hoặc `WAYLAND_DISPLAY` (Wayland) phải được set đúng. Khi chạy trong terminal của desktop thì thường tự động có sẵn.
>
> **Lưu ý:** `libSkiaSharp.so` và `libHarfBuzzSharp.so` (native Skia renderer) đã được bao gồm tự động trong publish output Linux bởi `Avalonia.Desktop 12.1.0` — không cần cài thêm gói nào. Không cần thêm `SkiaSharp.NativeAssets.Linux` riêng.

### 4.2 Sử dụng tính năng Remote

1. Chạy trực tiếp file **`IPGS.RemoteControl.CcuUI.exe`** (Windows) hoặc **`IPGS.RemoteControl.CcuUI`** (Linux) trên máy CCU/admin.

   ```powershell
   # Windows — chạy thẳng qua dotnet (dev/test):
   dotnet run --project IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj
   ```

   ```bash
   # Linux — chạy thẳng qua dotnet (dev/test):
   dotnet run --project IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj
   # Hoặc chạy binary đã publish:
   ./IPGS.RemoteControl.CcuUI/publish/linux-x64/IPGS.RemoteControl.CcuUI
   ```

2. Cửa sổ **"Kết nối điều khiển ZCU từ xa"** xuất hiện — nhập:
   - **Địa chỉ IP ZCU:** địa chỉ IP của máy ZCU trên LAN (ví dụ: `192.168.1.50`)
   - **Cổng TCP:** `17600` (hoặc giá trị đã cấu hình trong `appsettings.json` bên ZCU)
   - **Token:** chuỗi token khớp chính xác với `Token` trong `appsettings.json` bên ZCU
3. Nhấn **Kết nối** — cửa sổ Remote Screen xuất hiện, hiển thị màn hình ZCU.
4. Khi đóng cửa sổ Remote Screen, cửa sổ nhập liệu hiện lại — có thể kết nối lại mà không cần khởi động lại app.
5. Nhấn **Thoát** hoặc đóng cửa sổ nhập liệu để kết thúc app.

> **Lưu ý:** Token phải khớp chính xác (phân biệt hoa/thường). Nếu token sai, kết nối bị từ chối và **không tự kết nối lại** (bảo mật chống brute force). Cần sửa lại token và nhấn Kết nối thủ công.

> **Lưu ý quan hệ với IPGSUseCam:** `IPGS.RemoteControl.CcuUI` giờ là app độc lập — không còn tích hợp vào menu IPGSUseCam. Cả hai app chạy độc lập trên CCU, không có phụ thuộc lẫn nhau.

---

## 5. Cấu hình Network / Firewall

### 5.1 Mở port TCP 17600 trên ZCU

```bash
# Nếu ZCU dùng ufw (Ubuntu mặc định):
sudo ufw allow 17600/tcp comment "IPGS Remote Control ZCuAgent"
sudo ufw reload

# Kiểm tra:
sudo ufw status | grep 17600
```

### 5.2 Khuyến nghị bảo mật mạng

- **Giới hạn trong LAN nội bộ:** Tính năng v1 dùng plaintext TCP (không TLS). TUYỆT ĐỐI KHÔNG expose cổng 17600 ra Internet.
- **Firewall giới hạn theo source IP:** Nếu có thể, giới hạn rule firewall chỉ cho phép IP subnet của CCU:

  ```bash
  # Ví dụ chỉ cho phép subnet 192.168.1.0/24:
  sudo ufw allow from 192.168.1.0/24 to any port 17600 proto tcp comment "IPGS Remote"
  # Xóa rule mở cho tất cả nếu đã tạo trước đó:
  sudo ufw delete allow 17600/tcp
  ```

- **Cấu hình `AllowedClientIPs` trong appsettings.json:** Đây là lớp bảo vệ thứ 2 ở application layer — ZcuAgent tự check IP trước khi đọc HELLO, đóng kết nối nếu không match.

---

## 6. Xác minh sau khi Deploy (Verification Steps)

### 6.1 Xác nhận ZcuAgent đang chạy và lắng nghe port

SSH vào ZCU và chạy:

```bash
# Kiểm tra service đang active:
systemctl --user status ipgs-remote-agent.service
# → phải thấy "Active: active (running)"

# Kiểm tra port đang lắng nghe:
ss -tlnp | grep 17600
# → phải thấy dòng: LISTEN ... 0.0.0.0:17600 ... "ZcuAgent"

# Kiểm tra X11 display hoạt động (quan trọng):
export DISPLAY=:0
xdpyinfo | grep "dimensions"
# → phải hiện kích thước màn hình, ví dụ: dimensions: 1920x1080 pixels
```

### 6.2 Test kết nối từ CCU

1. Chạy `IPGS.RemoteControl.CcuUI.exe` trên CCU (hoặc `dotnet run --project IPGS.RemoteControl.CcuUI`).
2. Cửa sổ nhập liệu xuất hiện — nhập IP/Port/Token đúng → nhấn **Kết nối**.
4. Dấu hiệu thành công:
   - Cửa sổ Remote Screen xuất hiện và hiển thị hình ảnh màn hình ZCU (có thể delay 1-2 giây đầu khi handshake).
   - Di chuột trong cửa sổ → con trỏ trên màn hình ZCU di chuyển tương ứng.
   - Status bar hoặc tiêu đề cửa sổ hiển thị trạng thái "Đang kết nối" / "Streaming".

### 6.3 Xem log ZcuAgent

```bash
# Xem log real-time:
journalctl --user -u ipgs-remote-agent.service -f

# Xem log gần nhất (100 dòng):
journalctl --user -u ipgs-remote-agent.service -n 100
```

### 6.4 Dấu hiệu lỗi thường gặp và cách xử lý

| Triệu chứng | Nguyên nhân | Cách xử lý |
|-------------|-------------|------------|
| CCU báo lỗi "Connection refused" | ZcuAgent chưa chạy hoặc sai port/IP | Kiểm tra `ss -tlnp \| grep 17600`; kiểm tra IP ZCU |
| CCU kết nối được nhưng màn hình trắng/đen | ZcuAgent không có quyền truy cập X11 display | Kiểm tra `DISPLAY` trong unit file; chạy `xdpyinfo` trên ZCU để xác nhận |
| ZcuAgent khởi động fail với `XDG_SESSION_TYPE=...` không xác định | Unit file set sai `XDG_SESSION_TYPE` so với session thật | Kiểm tra `loginctl show-session $XDG_SESSION_ID -p Type --value` khi đăng nhập desktop, sửa lại unit file khớp giá trị đó (mục 2.1/Bước 5) |
| Session Wayland: ZcuAgent fail lúc start với lỗi timeout gst-launch-1.0 / D-Bus | Thiếu gói `gstreamer1.0-pipewire`, hoặc Mutter D-Bus API không khớp bản GNOME (xem TDD §14b) | Cài gói mục 2.3; chạy `busctl --user introspect org.gnome.Shell /org/gnome/Mutter/ScreenCast` để so khớp signature |
| CCU báo "AUTH_FAIL" | Token sai hoặc IP CCU không có trong `AllowedClientIPs` | Kiểm tra lại token (phân biệt hoa/thường); kiểm tra `AllowedClientIPs` trong `appsettings.json` |
| Sau 3 lần AUTH_FAIL, IP bị ban 5 phút | Rate limit được kích hoạt | Chờ 5 phút, hoặc restart ZcuAgent service để xóa ban; sau đó sửa token cho đúng |
| Kết nối bị ngắt sau ~15 giây không hoạt động | PING timeout | Bình thường nếu không có traffic — client tự reconnect sau 3s. Nếu reconnect liên tục: kiểm tra mạng giữa CCU và ZCU |
| Màn hình ZCU hiển thị được nhưng chuột không điều khiển được | `libxtst6` thiếu hoặc XTest bị disable | Cài `libxtst6`; kiểm tra log ZcuAgent xem có lỗi `XTestFakeMotionEvent` không |
| Log hiện `XShmAttach rejected by X server (async BadAccess)` | Bình thường — KHÔNG phải lỗi chặn hoạt động. Xảy ra khi ZcuAgent capture màn hình không thuộc session desktop gốc (ví dụ chạy qua SSH set `DISPLAY=:0` thủ công thay vì chạy trong session thật/systemd user service). | Agent tự fallback sang `XGetImage` (chậm hơn XShm nhưng vẫn hoạt động đúng) — không cần xử lý gì thêm. Nếu deploy đúng theo Cách A (systemd user service, mục 3.1) thì thường KHÔNG gặp cảnh báo này vì service chạy trong đúng session. |
| Video/màn hình hiển thị được 1 lúc rồi chuyển sang **đen hoàn toàn** dù ZcuAgent vẫn chạy, không có lỗi trong log | Session desktop trên ZCU bị **tự động khoá màn hình / kích hoạt screensaver** sau thời gian idle (mặc định GNOME: khoá sau 300s không thao tác) — ZcuAgent capture đúng những gì màn hình đang hiển thị, mà màn hình lock/blank thì capture ra sẽ đen. | Tắt khoá màn hình + screensaver trên ZCU (khuyến nghị cho máy dùng riêng cho remote-control, không dùng làm máy làm việc trực tiếp): `gsettings set org.gnome.desktop.screensaver lock-enabled false` và `gsettings set org.gnome.desktop.session idle-delay 0`. Nếu màn hình đang bị khoá sẵn, mở khoá bằng `loginctl unlock-session <session-id>` (tìm session bằng `loginctl list-sessions`). |
| CPU ZCU cao (>50%) | FPS hoặc JPEG quality quá cao | Giảm `TargetFps` xuống 10; giảm `JpegQuality` xuống 60 trong `appsettings.json` rồi restart service |

---

## 7. Rollback / Gỡ cài đặt

### 7.1 Dừng service tạm thời

```bash
systemctl --user stop ipgs-remote-agent.service
```

### 7.2 Gỡ cài đặt hoàn toàn (ZCU-side)

```bash
# Dừng và tắt auto-start:
systemctl --user stop ipgs-remote-agent.service
systemctl --user disable ipgs-remote-agent.service

# Xóa file unit:
rm ~/.config/systemd/user/ipgs-remote-agent.service
systemctl --user daemon-reload

# Xóa thư mục deploy:
rm -rf /home/<zcu-user>/ipgs/remote-agent/

# Xóa rule firewall (nếu đã mở):
sudo ufw delete allow 17600/tcp
# Hoặc nếu đã tạo rule giới hạn subnet:
sudo ufw delete allow from 192.168.1.0/24 to any port 17600 proto tcp
```

### 7.3 Rollback CCU-side (IPGSUseCam)

Tính năng Remote ZCU là project độc lập được tích hợp chỉ qua `<ProjectReference>` và 1 menu item. Gỡ bỏ không ảnh hưởng đến toàn bộ logic nghiệp vụ hiện có của IPGSUseCam.

Để gỡ bỏ khỏi IPGSUseCam (nếu cần):
1. Xóa dòng `<ProjectReference>` trỏ đến `IPGS.RemoteControl.CcuUI` trong `IPGSUseCam.csproj`.
2. Xóa menu item "Điều khiển ZCU từ xa..." và handler tương ứng trong MainWindow.
3. Build lại — không còn dependency, không cần xóa project thư viện.

> Giữ các project `IPGS.RemoteControl.ZcuAgent/`, `IPGS.RemoteControl.CcuClient/`, `IPGS.RemoteControl.CcuUI/` trong solution nếu có kế hoạch dùng lại sau — chúng hoàn toàn độc lập, không gây lỗi build khi không có reference từ IPGSUseCam.

---

## Phụ lục — Tham chiếu nhanh

| Thông tin | Giá trị mặc định |
|-----------|-----------------|
| TCP Port | 17600 |
| Config file (ZCU) | `/home/<user>/ipgs/remote-agent/appsettings.json` |
| Systemd unit file | `~/.config/systemd/user/ipgs-remote-agent.service` |
| Log | `journalctl --user -u ipgs-remote-agent.service` |
| TDD chi tiết | `docs/tech-design/TDD-remote-control.md` |
| Plan | `.claude/plans/PLAN-ccu-zcu-remote-control-2026-07-22/PLAN-MASTER.md` |

---

*Tài liệu này là hướng dẫn kỹ thuật — phiên bản 1.0, tương ứng với ZcuAgent v1 (JPEG, shared-secret, TCP plaintext). Khi nâng cấp lên v2 (TLS, HMAC, H.264), cập nhật tài liệu này tương ứng.*
