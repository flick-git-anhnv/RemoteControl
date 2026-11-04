#!/bin/bash
# ==============================================================================
# build-deb.sh — Đóng gói IPGSUseCam (Avalonia) thành file .deb cho Linux
# ==============================================================================
# Cách dùng:
#   bash scripts/linux-deb/build-deb.sh [version]
#   (chạy trong WSL/Linux có sẵn dotnet SDK + dpkg-deb)
#
# Các gotcha đã fix (đúc kết từ ParkingV8 — KHÔNG được bỏ khi sửa script này):
#   1. BUILD_DIR nằm ở /tmp — KHÔNG build trực tiếp trên /mnt/<ổ Windows> khi
#      chạy qua WSL, vì DrvFs (9p) không hỗ trợ đầy đủ permission bits/symlink
#      mà dpkg-deb cần → build sẽ lỗi hoặc file quyền sai âm thầm.
#   2. postinst chown cả THƯ MỤC CHA của INSTALL_DIR (không chỉ INSTALL_DIR) —
#      vì INSTALL_DIR nằm dưới /opt/kztek, nếu chỉ chown riêng thư mục app,
#      user thường vẫn không ghi được vào /opt/kztek (thư mục cha do root tạo)
#      → lỗi ẩn khi app cần ghi log/cache cạnh binary.
#   3. Icon dùng .png (không phải .ico) trong .desktop — icon theme spec của
#      Linux desktop chỉ chắc chắn render png/svg/xpm, .ico thường bị bỏ qua
#      khiến icon không hiện trên desktop/taskbar.
#   4. dpkg-deb build ra .deb trong BUILD_DIR (/tmp) rồi COPY file .deb hoàn
#      chỉnh ra dist/ — không build thẳng .deb lên /mnt/* (cùng lý do mục 1).
# ==============================================================================

set -euo pipefail

# ------------------------------------------------------------------------------
# Cấu hình
# ------------------------------------------------------------------------------
VERSION="${1:-1.0.0}"
PKG_NAME="kztek-ipgsusecam"
INSTALL_DIR="/opt/kztek/ipgsusecam"
INSTALL_PARENT_DIR="$(dirname "$INSTALL_DIR")"   # /opt/kztek
BIN_NAME="IPGSUseCam"
CMD_NAME="ipgsusecam"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
APP_PROJECT="$REPO_ROOT/IPGSUseCam/IPGSUseCam.csproj"

# GOTCHA #1: build ở /tmp, KHÔNG build trực tiếp trên /mnt/<ổ Windows> (DrvFs)
BUILD_DIR="/tmp/kztek-deb-build/$PKG_NAME"
DIST_DIR="$REPO_ROOT/dist"
ARCH="amd64"
DEB_FILE="${PKG_NAME}_${VERSION}_${ARCH}.deb"

echo "=== [1/7] Dọn build dir cũ ==="
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR/DEBIAN"
mkdir -p "$BUILD_DIR$INSTALL_DIR"
mkdir -p "$BUILD_DIR/usr/bin"
mkdir -p "$DIST_DIR"

echo "=== [2/7] dotnet publish (linux-x64, self-contained) ==="
dotnet publish "$APP_PROJECT" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -o "$BUILD_DIR$INSTALL_DIR"

echo "=== [3/7] Copy file hỗ trợ Linux (run.sh/install.sh/.desktop/icon) ==="
# Các file này đã được publish sẵn qua target PublishLinuxLauncher trong .csproj.
# Nếu vì lý do gì đó chưa có (VD: publish không set RuntimeIdentifier đúng cách),
# copy trực tiếp từ deploy-linux/ để đảm bảo .deb luôn đầy đủ.
for f in run.sh install.sh kztek-ipgsusecam.desktop appIcon.png; do
    if [ ! -f "$BUILD_DIR$INSTALL_DIR/$f" ]; then
        cp "$REPO_ROOT/IPGSUseCam/deploy-linux/$f" "$BUILD_DIR$INSTALL_DIR/$f"
    fi
done
chmod +x "$BUILD_DIR$INSTALL_DIR/run.sh" "$BUILD_DIR$INSTALL_DIR/install.sh" "$BUILD_DIR$INSTALL_DIR/$BIN_NAME"

echo "=== [4/7] Tạo wrapper /usr/bin/$CMD_NAME ==="
cat > "$BUILD_DIR/usr/bin/$CMD_NAME" <<EOF
#!/bin/bash
exec "$INSTALL_DIR/run.sh" "\$@"
EOF
chmod +x "$BUILD_DIR/usr/bin/$CMD_NAME"

echo "=== [5/7] Sinh DEBIAN/control ==="
cat > "$BUILD_DIR/DEBIAN/control" <<EOF
Package: $PKG_NAME
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Depends: libc6, libgcc-s1, libstdc++6, zlib1g, libicu70 | libicu72 | libicu74 | libicu76
Maintainer: KZTEK <sales@kztek.net>
Description: KZTEK iPGS Use Camera
 Ung dung quan ly va cau hinh camera cho he thong iPGS (Avalonia UI).
EOF

echo "=== [6/7] Sinh DEBIAN/postinst + postrm ==="
cat > "$BUILD_DIR/DEBIAN/postinst" <<EOF
#!/bin/bash
set -e

# GOTCHA #2: chown cả thư mục cha ($INSTALL_PARENT_DIR), không chỉ $INSTALL_DIR —
# nếu chỉ chown $INSTALL_DIR, user vẫn không ghi được vào $INSTALL_PARENT_DIR
# (thư mục do root/dpkg tạo ra khi cài lần đầu) → app không ghi được log/cache
# nằm cạnh (nhưng ngoài) thư mục cài đặt.
TARGET_USER="\${SUDO_USER:-\$(logname 2>/dev/null || echo root)}"

chmod -R 777 "$INSTALL_DIR"
chown -R "\$TARGET_USER":"\$TARGET_USER" "$INSTALL_PARENT_DIR"

# Cài desktop shortcut cho đúng user (không phải root) nếu có thể
if [ "\$TARGET_USER" != "root" ] && command -v runuser >/dev/null 2>&1; then
    runuser -l "\$TARGET_USER" -c "bash '$INSTALL_DIR/install.sh'" || true
fi

echo "Cai dat thanh cong. Chay bang lenh: $CMD_NAME"
exit 0
EOF
chmod +x "$BUILD_DIR/DEBIAN/postinst"

cat > "$BUILD_DIR/DEBIAN/postrm" <<EOF
#!/bin/bash
set -e
if [ "\$1" = "purge" ] || [ "\$1" = "remove" ]; then
    rm -f "\$HOME/.local/share/applications/kztek-ipgsusecam.desktop" 2>/dev/null || true
fi
exit 0
EOF
chmod +x "$BUILD_DIR/DEBIAN/postrm"

echo "=== [7/7] Đóng gói .deb ==="
# GOTCHA #4: build .deb trong BUILD_DIR (/tmp), copy file .deb hoàn chỉnh ra dist/
dpkg-deb --build --root-owner-group "$BUILD_DIR" "/tmp/kztek-deb-build/$DEB_FILE"
cp "/tmp/kztek-deb-build/$DEB_FILE" "$DIST_DIR/$DEB_FILE"

echo ""
echo "✓ Hoàn thành: $DIST_DIR/$DEB_FILE"
echo "  Cài đặt bằng: sudo dpkg -i $DIST_DIR/$DEB_FILE"
