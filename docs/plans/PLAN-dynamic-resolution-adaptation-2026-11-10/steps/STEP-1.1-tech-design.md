# STEP 1.1 — Thiết kế kỹ thuật Auto-fit Aspect Ratio (CCU) & Headless Resolution Adapter (ZCU)

**Agent:** Tech Lead (L3)
**Trạng thái:** ✅ Hoàn thành
**Thời gian:** 2026-11-10 14:58

---

## 1. Phân tích hiện trạng & Nguyên nhân gốc rễ

1. **Phía ZCU (Linux X11 Target — `192.168.21.230`):**
   - Trên các máy ZCU đã cấu hình chống màn hình ảo theo G019 (`video=eDP-1:d video=HDMI-A-1:e`), cổng `HDMI-1` luôn báo trạng thái `connected` trong DRM/Xorg kể cả khi rút cáp HDMI vật lý.
   - Tuy nhiên, khi **không cắm màn hình vật lý** (Headless), kernel không đọc được EDID nên `xrandr --current` báo kích thước vật lý `0mm x 0mm`, không có mode nào mang cờ `+` (preferred EDID mode), và chỉ có 4 mode VESA an toàn (`1024x768`, `800x600`, `848x480`, `640x480`), khiến máy bị kẹt ở `1024x768` (tỷ lệ 4:3).
   - Khi **có cắm màn hình vật lý**, `xrandr --current` báo kích thước vật lý thực (ví dụ `376mm x 301mm`) và mode chuẩn theo EDID có dấu `+` (ví dụ `1280x1024 60.02*+`).
2. **Phía CCU (`IPGS.RemoteControl.CcuUI`):**
   - `RemoteScreenWindow.axaml` đang khởi tạo kích thước cố định `Width="1280" Height="800"` (tỷ lệ 16:10), trong khi `RemoteScreenControl.axaml` đặt cứng `Stretch="Uniform"`.
   - Khi máy đích có tỷ lệ 5:4 (`1280x1024`), 4:3 (`1024x768`) hoặc 16:9 (`1920x1080`), khung cửa sổ 16:10 không khớp tỷ lệ dẫn đến xuất hiện hai dải viền đen (letterboxing/pillarboxing) hoặc nếu đổi sang `Stretch="Fill"` thì bị méo hình.

---

## 2. Thiết kế kiến trúc giải pháp

### 2.1. Phía ZCU Agent: `HeadlessResolutionAdapter` (`IPGS.RemoteControl.ZcuAgent/Capture/HeadlessResolutionAdapter.cs`)

- **Parser không phụ thuộc thư viện ngoài (`ParseXrandrOutput`):**
  - Đọc kết quả `xrandr --current` (chỉ đọc cache của Xorg server, tốn < 2ms, không gây nháy hình như `xrandr --query`).
  - Trích xuất cho từng cổng output (`HDMI-*`, `DP-*`, `VGA-*`):
    - `Name`, `IsConnected`, `IsPrimary`
    - `PhysicalWidthMm`, `PhysicalHeightMm` (từ mẫu `(\d+)mm x (\d+)mm`)
    - `ActiveMode` (mode có dấu `*`), `PreferredEdidMode` (mode có dấu `+`)
    - `AvailableModes` (danh sách tên mode của cổng)
- **Logic nhận diện trạng thái thực tế (Auto-Detection):**
  - **Có màn hình vật lý thật (`HasPhysicalMonitor`):** `IsConnected == true` VÀ (`(PhysicalWidthMm > 0 && PhysicalHeightMm > 0)` HOẶC `PreferredEdidMode != null`).
    - Nếu cổng đang chạy trên mode ảo `"1920x1080_60.00"` (do phiên headless trước đó tạo ra) mà màn hình thật vừa cắm vào có `PreferredEdidMode` khác: tự động gọi `xrandr --output <Name> --auto` để trả về đúng độ phân giải gốc EDID của màn hình vật lý.
    - Ngược lại: giữ nguyên 100%, không can thiệp.
  - **Không cắm màn hình vật lý (`IsHeadless`):** Không có cổng nào thỏa mãn `HasPhysicalMonitor` (hoặc cổng `connected` nhưng `0mm x 0mm` và không có `PreferredEdidMode`, hoặc tất cả `disconnected`).
    - Nếu cổng hiện tại chưa ở độ phân giải mục tiêu `1920x1080`:
      1. Tạo mode chuẩn CVT 1080p60 nếu chưa tồn tại:
         `xrandr --newmode "1920x1080_60.00" 173.00 1920 2048 2248 2576 1080 1083 1088 1120 -hsync +vsync`
      2. Gắn mode vào cổng:
         `xrandr --addmode <Name> "1920x1080_60.00"`
      3. Kích hoạt mode:
         `xrandr --output <Name> --mode "1920x1080_60.00"`
- **Chu kỳ thực thi:**
  - Chạy ngay khi `RemoteControlHostedService` khởi tạo (trước khi mở capture loop) và kiểm tra định kỳ mỗi 15 giây trong background để tự động thích ứng khi người dùng rút hoặc cắm cáp HDMI nóng (hotplug).

### 2.2. Phía CCU Client: Auto-fit Window Aspect Ratio & Display Mode Switcher

- **Enum `RemoteDisplayMode` (`RemoteScreenViewModel.cs`):**
  - `AutoFit` (Mặc định): Tự động điều chỉnh kích thước cửa sổ `RemoteScreenWindow` khớp chính xác với tỷ lệ khung hình `screenW / screenH` của máy đích; `Image.Stretch = Stretch.Uniform`. Khi tỷ lệ cửa sổ bằng đúng tỷ lệ hình, hình ảnh lấp đầy 100% cửa sổ, không viền đen và không méo hình.
  - `Uniform`: Giữ nguyên tỷ lệ gốc của máy đích (`Stretch.Uniform`), không tự động đổi kích thước cửa sổ khi người dùng tự kéo giãn.
  - `Fill`: Kéo giãn lấp đầy 100% khung cửa sổ (`Stretch.Fill`), hữu ích khi người dùng phóng to toàn màn hình (Maximized) trên màn hình lệch tỷ lệ nhưng muốn xem kín khung.
- **Thuật toán `CalculateAutoFitWindowSize(int remoteW, int remoteH, double maxW, double maxH, double minW, double minH)`:**
  - Tính tỷ lệ `aspect = (double)remoteW / remoteH`.
  - Giới hạn vùng hiển thị tối đa bằng 88% chiều rộng và 85% chiều cao `WorkingArea` của màn hình CCU hiện tại.
  - Chọn kích thước tối ưu sao cho khớp tuyệt đối tỷ lệ `aspect`, đạt độ lớn dễ nhìn (~82%–85% chiều cao hoặc chiều rộng khả dụng) và tôn trọng `MinWidth`/`MinHeight`.
- **Ánh xạ toạ độ chuột (`MapToZcuCoords`):**
  - Hỗ trợ chính xác cả `Stretch.Fill` (tỷ lệ tuyến tính trực tiếp theo `controlW`, `controlH`) và `Stretch.Uniform` / `AutoFit` (tính bù phần đệm `offsetX`, `offsetY` nếu cửa sổ đang ở trạng thái Maximized).

---

## 3. Handoff Log (Chuyển giao cho Senior Developer — Bước 2.1, 2.2, 2.3)
- **Files cần sửa/tạo mới:**
  1. `IPGS.RemoteControl.CcuUI/ViewModels/RemoteScreenViewModel.cs`: Thêm `RemoteDisplayMode`, `DisplayMode`, `ImageStretch`, `DisplayModeText`, `CycleDisplayMode()`, event `RemoteResolutionChanged`, hàm `CalculateAutoFitWindowSize`, và cập nhật `MapToZcuCoords`.
  2. `IPGS.RemoteControl.CcuUI/Views/RemoteScreenControl.axaml`: Đổi `Stretch="Uniform"` thành `Stretch="{Binding ImageStretch}"`.
  3. `IPGS.RemoteControl.CcuUI/Views/RemoteScreenWindow.axaml` & `.axaml.cs`: Thêm nút `BtnDisplayMode` trên TopBar, lắng nghe `RemoteResolutionChanged` để tự động co giãn cửa sổ theo tỷ lệ máy đích.
  4. `IPGS.RemoteControl.ZcuAgent/Capture/HeadlessResolutionAdapter.cs`, `AgentOptions.cs`, `Program.cs`, `RemoteControlHostedService.cs`: Hiện thực bộ tự động nhận diện và điều chỉnh độ phân giải trên ZCU.
  5. `IPGS.RemoteControl.Tests/ResolutionAdaptationTests.cs`: Unit test bao phủ `CalculateAutoFitWindowSize`, `MapToZcuCoords`, và `HeadlessResolutionAdapter.ParseXrandrOutput`.
