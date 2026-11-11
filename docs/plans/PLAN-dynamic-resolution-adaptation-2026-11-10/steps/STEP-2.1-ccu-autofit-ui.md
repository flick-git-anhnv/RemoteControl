# STEP 2.1 — Hiện thực Auto-fit Aspect Ratio & Display Mode switcher trên CCU UI

**Agent:** Senior Developer (L4)
**Trạng thái:** ✅ Hoàn thành
**Thời gian:** 2026-11-10 15:05

---

## 1. Các thay đổi đã thực hiện
1. **`IPGS.RemoteControl.CcuUI/ViewModels/RemoteScreenViewModel.cs`:**
   - Thêm enum `RemoteDisplayMode` (`AutoFit`, `Uniform`, `Fill`).
   - Thêm observable properties `DisplayMode`, `ImageStretch`, `DisplayModeText`, `DisplayModeTooltip`.
   - Bổ sung phương thức `CycleDisplayMode()` chuyển đổi luân phiên giữa 3 chế độ.
   - Bổ sung thuật toán tĩnh `CalculateAutoFitWindowSize(int remoteW, int remoteH, double maxW, double maxH, double minW, double minH)` tính toán kích thước cửa sổ vừa vặn với tỷ lệ màn hình máy đích và vùng làm việc của màn hình CCU.
   - Nâng cấp `MapToZcuCoords(...)` hỗ trợ đầy đủ cả chế độ `Fill` (tuyến tính toàn khung) và `AutoFit`/`Uniform` (trừ lề letterbox khi cửa sổ phóng to).
   - Phát sự kiện `RemoteResolutionChanged` ngay khi nhận frame đầu tiên hoặc khi máy đích thay đổi độ phân giải.
2. **`IPGS.RemoteControl.CcuUI/Views/RemoteScreenControl.axaml`:**
   - Thay `Stretch="Uniform"` cứng bằng binding động `Stretch="{Binding ImageStretch}"`.
3. **`IPGS.RemoteControl.CcuUI/Views/RemoteScreenWindow.axaml` & `.axaml.cs`:**
   - Bổ sung nút `BtnDisplayMode` trên thanh công cụ TopBar hiển thị trạng thái tỷ lệ hiện hành.
   - Bổ sung `OnRemoteResolutionChanged` và `ApplyAutoFitWindow` tự động điều chỉnh kích thước cửa sổ theo tỷ lệ máy đích và cập nhật tiêu đề cửa sổ kèm độ phân giải thực tế `(W×H)`.

---

## 2. Handoff Log (Chuyển giao sang Bước 2.2 — Senior Developer)
- Phần giao diện CCU đã hoàn tất. Tiếp theo triển khai `HeadlessResolutionAdapter` trên `IPGS.RemoteControl.ZcuAgent` để tự động nhận diện trạng thái không cắm màn hình (headless) và kích hoạt độ phân giải 1920×1080.
