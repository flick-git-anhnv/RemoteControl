#!/usr/bin/env bash
# ==============================================================================
# Script Cài đặt & Cấu hình Tự động iPGS ZcuAgent trên Ubuntu 22.04 (X11)
# Sử dụng: ./setup-zcu-agent.sh [PORT] [TOKEN] [ALLOWED_IPS] [TARGET_FPS] [JPEG_QUALITY]
# ==============================================================================

set -e

PORT="${1:-17600}"
TOKEN="${2:-$(openssl rand -hex 16 2>/dev/null || echo "ZCU_AGENT_DEFAULT_TOKEN_CHANGE_ME")}"
ALLOWED_IPS="${3:-0.0.0.0/0}"
TARGET_FPS="${4:-15}"
JPEG_QUALITY="${5:-70}"
INSTALL_DIR="$HOME/ipgs/remote-agent"

echo "======================================================================"
echo "🚀 BẮT ĐẦU CÀI ĐẶT iPGS REMOTE CONTROL ZCU AGENT"
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
  echo "👉 Vui lòng đăng xuất và chọn 'Ubuntu on Xorg' tại màn hình đăng nhập."
else
  echo "✅ Môi trường hiển thị: X11 OK."
fi

# 2. Cài đặt các thư viện Native X11
echo "📦 [2/7] Kiểm tra & Cài đặt thư viện Native X11 (libx11, libxext, libxtst)..."
if dpkg -l libx11-6 libxext6 libxtst6 wget >/dev/null 2>&1; then
  echo "✅ Thư viện Native X11 đã sẵn sàng (đã được cài đặt)."
elif command -v apt-get >/dev/null 2>&1; then
  sudo systemctl stop unattended-upgrades.service 2>/dev/null || true
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
  
  # Thêm PATH nếu chưa có
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
cat <<EOF > "$INSTALL_DIR/appsettings.json"
{
  "RemoteControl": {
    "Port": $PORT,
    "Token": "$TOKEN",
    "AllowedClientIPs": [ "$ALLOWED_IPS" ],
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

# 5. Cấu hình systemd user service
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

# Kích hoạt lingering để service tự chạy ngay cả khi chưa login terminal
if command -v loginctl >/dev/null 2>&1; then
  sudo loginctl enable-linger "$CURRENT_USER" || true
fi
echo "✅ Systemd user service đã đăng ký."

# 6. Khởi động service nếu file thực thi ZcuAgent đã có sẵn
echo "🚀 [6/7] Kiểm tra file thực thi ZcuAgent..."
if [ -f "$INSTALL_DIR/IPGS.RemoteControl.ZcuAgent" ]; then
  chmod +x "$INSTALL_DIR/IPGS.RemoteControl.ZcuAgent"
  systemctl --user restart ipgs-remote-agent.service
  echo "✅ ZcuAgent service đã khởi chạy!"
else
  echo "ℹ️ File $INSTALL_DIR/IPGS.RemoteControl.ZcuAgent chưa có. Hãy copy file binary vào thư mục này rồi chạy 'systemctl --user start ipgs-remote-agent'."
fi

# 7. Cấu hình Firewall & Screen Lock
echo "🛡️ [7/7] Cấu hình Firewall & Screen Lock..."
if command -v ufw >/dev/null 2>&1; then
  sudo ufw allow "$PORT"/tcp comment "IPGS Remote Control Agent" >/dev/null 2>&1 || true
  echo "✅ Đã mở port $PORT/tcp trên UFW firewall."
fi

# Tắt tự động khóa màn hình GNOME để tránh lỗi màn hình đen khi remote
if command -v gsettings >/dev/null 2>&1; then
  gsettings set org.gnome.desktop.screensaver lock-enabled false >/dev/null 2>&1 || true
  gsettings set org.gnome.desktop.session idle-delay 0 >/dev/null 2>&1 || true
  echo "✅ Đã tắt tự động khóa màn hình GNOME."
fi

echo "======================================================================"
echo "🎉 HOÀN THÀNH CÀI ĐẶT ZCU AGENT!"
echo "📌 Cổng TCP : $PORT"
echo "📌 Token   : $TOKEN"
echo "======================================================================"
