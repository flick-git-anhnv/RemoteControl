# STEP 2.2 — Hiện thực Headless Resolution Adapter tự động cấu hình 1080p trên ZcuAgent

**Agent:** Senior Developer (L4)
**Trạng thái:** ✅ Hoàn thành
**Thời gian:** 2026-11-10 15:08

---

## 1. Các thay đổi đã thực hiện
1. **`IPGS.RemoteControl.ZcuAgent/AgentOptions.cs`:**
   - Thêm thuộc tính `AutoHeadlessResolution` (mặc định `true`) và `HeadlessTargetResolution` (mặc định `"1920x1080"`).
2. **`IPGS.RemoteControl.ZcuAgent/Capture/HeadlessResolutionAdapter.cs`:**
   - Viết parser `ParseXrandrOutput` trích xuất đầy đủ trạng thái các cổng xuất hình X11 (`Name`, `IsConnected`, `IsPrimary`, `PhysicalWidthMm`, `PhysicalHeightMm`, `ActiveMode`, `PreferredEdidMode`, `AvailableModes`).
   - Định nghĩa thuộc tính `HasPhysicalMonitor`: phân biệt chính xác cổng có màn hình vật lý thực (có kích thước vật lý `> 0mm` hoặc có mode `+` từ EDID) với cổng bị kernel ép `connected` (`video=HDMI-A-1:e`) nhưng không cắm cáp (`0mm x 0mm`, không có EDID, rơi về fallback VESA `1024x768`).
   - Viết hàm quyết định `DecideAction`:
     - Khi không cắm màn hình vật lý (Headless): tự động tạo mode CVT `1920x1080_60.00`, gắn vào cổng và kích hoạt `1920x1080`.
     - Khi cắm màn hình vật lý thực vào cổng đang chạy mode ảo `1920x1080_60.00`: tự động gọi `xrandr --output <Name> --auto` để trả về độ phân giải EDID chuẩn của màn hình thật.
3. **`IPGS.RemoteControl.ZcuAgent/RemoteControlHostedService.cs`:**
   - Gọi `HeadlessResolutionAdapter.CheckAndAdaptAsync` ngay sau khi khởi tạo X11 Display và duy trì vòng lặp `RunHeadlessWatchLoopAsync` định kỳ mỗi 15 giây để tự động thích ứng khi rút/cắm cáp HDMI nóng (hotplug).

---

## 2. Handoff Log (Chuyển giao sang Bước 2.3 — Senior Developer)
- Cần liên kết `HeadlessResolutionAdapter.cs` vào `IPGS.RemoteControl.Tests.csproj` và viết bộ Unit Tests `ResolutionAdaptationTests.cs` kiểm thử toàn diện thuật toán tính kích thước cửa sổ Auto-fit, ánh xạ chuột 3 chế độ, và parser/decision của `HeadlessResolutionAdapter`.
