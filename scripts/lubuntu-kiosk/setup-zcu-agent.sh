#!/usr/bin/env bash
# ==============================================================================
# Script Cài đặt & Cấu hình Tự động iPGS ZcuAgent trên Lubuntu (X11, LXQt)
# Sử dụng: ./setup-zcu-agent.sh [PORT] [TOKEN] [ALLOWED_IPS] [TARGET_FPS] [JPEG_QUALITY]
#
# ĐÂY LÀ BẢN SONG SONG của scripts/setup-zcu-agent.sh (bản gốc ghi cho Ubuntu Desktop/
# GNOME). Logic cài đặt + service giống hệt bản gốc (KHÔNG có gì GNOME-specific ở đó
# ngoại trừ 1 đoạn tắt screensaver GNOME ở cuối — đã bỏ, thay bằng xset DE-agnostic).
#
# Xác nhận: máy Lubuntu mục tiêu dùng mặc định X11 (không phải Wayland) — ZcuAgent
# capture màn hình qua X11 nên bắt buộc phải là phiên X11, không cần bước cảnh báo
# Wayland như bản gốc (Lubuntu/LXQt hiện chưa hỗ trợ Wayland nên luôn là X11).
#
# Service vẫn dùng systemd --user (KHÔNG phải system-level): ZcuAgent cần DISPLAY=:0
# của phiên đồ hoạ đang chạy để capture màn hình, nên không thể chạy như 1 service hệ
# thống thật khởi động TRƯỚC khi có phiên X (lúc đó chưa có X server nào để bind vào).
# `loginctl enable-linger` là phần làm cho service này khởi động CÙNG LÚC máy boot +
# autologin vào desktop, mà không cần kỹ thuật viên bấm đăng nhập thủ công — đây chính
# là cơ chế "khởi động cùng hệ thống" khả thi nhất với ràng buộc cần DISPLAY thật.
# ==============================================================================

set -e

PORT="${1:-17600}"
TOKEN="${2:-$(openssl rand -hex 16 2>/dev/null || echo "ZCU_AGENT_DEFAULT_TOKEN_CHANGE_ME")}"
ALLOWED_IPS="${3:-192.168.0.0/16,10.0.0.0/8,172.16.0.0/12}"
TARGET_FPS="${4:-15}"
JPEG_QUALITY="${5:-70}"
INSTALL_DIR="$HOME/ipgs/remote-agent"

echo "======================================================================"
echo "🚀 BẮT ĐẦU CÀI ĐẶT iPGS REMOTE CONTROL ZCU AGENT (Lubuntu)"
echo "======================================================================"
echo "📌 Cổng lắng nghe  : $PORT"
echo "📌 Allowed IPs     : $ALLOWED_IPS"
echo "📌 Target FPS      : $TARGET_FPS"
echo "📌 Jpeg Quality    : $JPEG_QUALITY%"
echo "📌 Thư mục cài đặt : $INSTALL_DIR"
echo "----------------------------------------------------------------------"

# 1. Kiểm tra môi trường Session X11
echo "🔍 [1/7] Kiểm tra XDG_SESSION_TYPE..."
SESSION_TYPE="${XDG_SESSION_TYPE:-x11}"
if [ "$SESSION_TYPE" = "wayland" ]; then
  echo "⚠️ CẢNH BÁO: Môi trường hiện tại là Wayland. ZcuAgent yêu cầu session X11."
  echo "👉 Lubuntu/LXQt mặc định chạy X11 — kiểm tra lại session đang chọn ở màn hình đăng nhập."
else
  echo "✅ Môi trường hiển thị: X11 OK."
fi

# 2. Cài đặt các thư viện Native X11
echo "📦 [2/7] Kiểm tra & Cài đặt thư viện Native X11 (libx11, libxext, libxtst)..."
if dpkg -l libx11-6 libxext6 libxtst6 wget >/dev/null 2>&1; then
  echo "✅ Thư viện Native X11 đã sẵn sàng (đã được cài đặt)."
elif command -v apt-get >/dev/null 2>&1; then
  sudo apt-get update -qq || true
  sudo apt-get install -y -qq libx11-6 libxext6 libxtst6 wget || true
  echo "✅ Thư viện Native X11 đã hoàn tất cài đặt."
else
  echo "⚠️ Hệ thống không sử dụng apt-get. Vui lòng đảm bảo libx11, libxext, libxtst đã được cài."
fi

# 3. Kiểm tra và Cài đặt .NET 8 Runtime nếu chưa có
echo "💻 [3/7] Kiểm tra .NET 8 Runtime..."
if ! command -v dotnet >/dev/null 2>&1 && [ ! -f "$HOME/.dotnet/dotnet" ]; then
  echo "⬇️ Đang tải và cài đặt .NET 8 Runtime..."
  wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir "$HOME/.dotnet"
  rm -f /tmp/dotnet-install.sh

  if ! grep -q '\.dotnet' "$HOME/.bashrc" 2>/dev/null; then
    echo 'export PATH=$HOME/.dotnet:$PATH' >> "$HOME/.bashrc"
  fi
  export PATH="$HOME/.dotnet:$PATH"
fi
DOTNET_VER="$($HOME/.dotnet/dotnet --version 2>/dev/null || dotnet --version 2>/dev/null || echo 'OK')"
echo "✅ .NET Runtime: $DOTNET_VER"

# 4. Tạo cấu hình appsettings.json
echo "⚙️ [4/7] Cấu hình file appsettings.json..."
mkdir -p "$INSTALL_DIR"
ALLOWED_IPS_JSON=""
IFS=',' read -ra _CIDRS <<< "$ALLOWED_IPS"
for _c in "${_CIDRS[@]}"; do
  _c="$(echo "$_c" | xargs)"
  [ -n "$_c" ] && ALLOWED_IPS_JSON="$ALLOWED_IPS_JSON\"$_c\", "
done
ALLOWED_IPS_JSON="${ALLOWED_IPS_JSON%, }"
cat <<EOF > "$INSTALL_DIR/appsettings.json"
{
  "RemoteControl": {
    "Port": $PORT,
    "Token": "$TOKEN",
    "AllowedClientIPs": [ $ALLOWED_IPS_JSON ],
    "TargetFps": $TARGET_FPS,
    "JpegQuality": $JPEG_QUALITY,
    "MaxFrameBytes": 8388608
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
EOF
echo "✅ File appsettings.json đã được tạo tại $INSTALL_DIR/appsettings.json"

# 5. Cấu hình systemd user service (giữ nguyên cơ chế bản Ubuntu — xem GHI CHÚ đầu file
#    vì sao vẫn là --user chứ không phải system-level).
echo "🛠️ [5/7] Thiết lập systemd user service (ipgs-remote-agent.service)..."
mkdir -p "$HOME/.config/systemd/user"
SERVICE_FILE="$HOME/.config/systemd/user/ipgs-remote-agent.service"

CURRENT_USER=$(whoami)
cat <<EOF > "$SERVICE_FILE"
[Unit]
Description=IPGS Remote Control ZCU Agent
After=graphical-session.target
Wants=graphical-session.target

[Service]
Type=simple
ExecStart=$INSTALL_DIR/IPGS.RemoteControl.ZcuAgent
WorkingDirectory=$INSTALL_DIR
Environment=DOTNET_ROOT=$HOME/.dotnet
Environment=PATH=$HOME/.dotnet:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
Environment=DISPLAY=:0
Environment=XDG_SESSION_TYPE=x11
Restart=on-failure
RestartSec=5
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=default.target
EOF

systemctl --user daemon-reload
systemctl --user enable ipgs-remote-agent.service || true

# Kích hoạt lingering để service tự chạy ngay cả khi chưa login terminal — đây là phần
# thật sự làm service "khởi động cùng hệ thống" (boot xong + autologin là chạy, không
# cần kỹ thuật viên can thiệp).
if command -v loginctl >/dev/null 2>&1; then
  sudo loginctl enable-linger "$CURRENT_USER" || true
fi
echo "✅ Systemd user service đã đăng ký (enable + linger)."

# 6. Khởi động service nếu file thực thi ZcuAgent đã có sẵn
echo "🚀 [6/7] Kiểm tra file thực thi ZcuAgent..."
if [ -f "$INSTALL_DIR/IPGS.RemoteControl.ZcuAgent" ]; then
  chmod +x "$INSTALL_DIR/IPGS.RemoteControl.ZcuAgent"
  systemctl --user restart ipgs-remote-agent.service
  echo "✅ ZcuAgent service đã khởi chạy!"
else
  echo "ℹ️ File $INSTALL_DIR/IPGS.RemoteControl.ZcuAgent chưa có. Hãy copy file binary vào thư mục này rồi chạy 'systemctl --user start ipgs-remote-agent'."
fi

# 7. Cấu hình Firewall & Screen Lock (DE-agnostic — KHÔNG dùng gsettings GNOME).
echo "🛡️ [7/7] Cấu hình Firewall & Screen Lock..."
if command -v ufw >/dev/null 2>&1; then
  sudo ufw allow "$PORT"/tcp comment "IPGS Remote Control Agent" >/dev/null 2>&1 || true
  echo "✅ Đã mở port $PORT/tcp trên UFW firewall."
fi

# Tắt tự động khóa/blank màn hình bằng lệnh X11 core (xset) — dùng được trên mọi DE
# (thay cho gsettings org.gnome.desktop.screensaver chỉ có trên GNOME).
if command -v xset >/dev/null 2>&1; then
  xset s off 2>/dev/null || true
  xset -dpms 2>/dev/null || true
  xset s noblank 2>/dev/null || true
  echo "✅ Đã tắt tự động khóa/blank màn hình (xset)."
fi

echo "======================================================================"
echo "🎉 HOÀN THÀNH CÀI ĐẶT ZCU AGENT (Lubuntu)!"
echo "📌 Cổng TCP : $PORT"
echo "📌 Token   : $TOKEN"
echo "======================================================================"
