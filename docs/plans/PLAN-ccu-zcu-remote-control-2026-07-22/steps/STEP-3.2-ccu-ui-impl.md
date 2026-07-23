---
step: 3.2
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-07-23 09:11
---

# STEP 3.2 — Senior Dev tạo project IPGS.RemoteControl.CcuUI

## Input nhận

Từ STEP-3.1 Handoff Log — CcuClient API: `RemoteControlClient`, events `FrameReceived`/`StateChanged`, methods `ConnectAsync`/`DisconnectAsync`/`SendMouseMoveAsync`/`SendMouseButtonAsync`. ScreenWidth/ScreenHeight đọc từ `((RemoteControlClient)_client).ScreenWidth/ScreenHeight` sau khi state = Streaming. `MouseButton` enum nằm ở namespace `IPGS.RemoteControl.Protocol`.

## Nhiệm vụ

Tạo project mới `IPGS.RemoteControl.CcuUI` — Avalonia class library. Implement `RemoteScreenViewModel`, `RemoteScreenControl` (UserControl hiển thị frame), `RemoteScreenWindow` (Window với toolbar + error banner + remote screen area).

## Definition of Done

- [x] Project `IPGS.RemoteControl.CcuUI/` tồn tại với `.csproj` Avalonia, có `<ProjectReference>` đến `IPGS.RemoteControl.CcuClient`
- [x] `RemoteScreenWindow.axaml` + `.axaml.cs` tồn tại
- [x] `RemoteScreenControl.axaml` + `.axaml.cs` tồn tại (tách riêng theo §20.4)
- [x] Frame JPEG decode bằng SkiaSharp → WriteableBitmap, không bị scale méo (Stretch=Uniform)
- [x] Click chuột lên RemoteScreenControl → toạ độ scale chính xác về ZCU screen space (letterbox-aware)
- [x] Hiển thị trạng thái kết nối (status dot màu + text) + error banner khi Faulted
- [x] Build sạch: `dotnet build IPGS.RemoteControl.CcuUI` — 0 error

## Đã làm

1. **Phát hiện project đã tồn tại** — tất cả 6 file đã được tạo sẵn trong session trước (csproj + ViewModels/RemoteScreenViewModel.cs + Views/RemoteScreenControl.axaml/.cs + Views/RemoteScreenWindow.axaml/.cs)
2. **Phát hiện bug build**: ViewModel thiếu `using IPGS.RemoteControl.Protocol;` khiến `MouseButton` không tìm thấy (4 lỗi CS0246/CS0103)
3. **Fix bug**: thêm `using IPGS.RemoteControl.Protocol;` vào `ViewModels/RemoteScreenViewModel.cs`
4. **Verify build**: `dotnet build IPGS.RemoteControl.CcuUI` → 0 error, 0 error (chỉ có warnings từ KztekComponentAvalonia — không thuộc scope)

## Artifact

- `IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj` — net8.0 class library, Avalonia 12.1.0 + SkiaSharp 2.88.9 + CommunityToolkit.Mvvm 8.4.2
- `IPGS.RemoteControl.CcuUI/ViewModels/RemoteScreenViewModel.cs` — MVVM ViewModel: manages CcuClient lifecycle, JPEG decode background thread, coordinate mapping (letterbox-aware), mouse throttle ≤60Hz
- `IPGS.RemoteControl.CcuUI/Views/RemoteScreenControl.axaml` + `.axaml.cs` — UserControl: `Image` bind `CurrentFrame`, PointerMoved/Pressed/Released → ViewModel.HandleMouseMove/HandleMouseButton
- `IPGS.RemoteControl.CcuUI/Views/RemoteScreenWindow.axaml` + `.axaml.cs` — Window: toolbar (status dot + status text + Disconnect button) + error banner (IsVisible bind IsFaulted) + RemoteScreenControl fills remaining space

## Quyết định quan trọng

- **SkiaSharp decode** (không phải Avalonia Bitmap từ MemoryStream): decode JPEG trên background thread → copy pixels sang `WriteableBitmap` (BGRA8888) để post lên UI thread. Đảm bảo không block UI.
- **Letterbox-aware coordinate mapping** trong `MapToZcuCoords()`: tính offset letterbox của Stretch=Uniform, clamp click ngoài vùng ảnh về biên ảnh — chính xác hơn map đơn giản `x/w * sw`.
- **KztekComponentAvalonia controls tái sử dụng**: `KzButton` dùng cho nút "Ngắt kết nối" trong toolbar. KHÔNG dùng KzTextBox/KzPasswordTextBox cho connection form — vì window này nhận tham số qua constructor từ IPGSUseCam (không có form nhập host/port/token inline); nếu cần sửa kết nối, đóng window và mở lại từ IPGSUseCam.
- **Window pattern**: constructor nhận `(string host, int port, string token)` → gọi `ConnectAsync` tự động khi `OnOpened`, gọi `DisconnectAsync` + `Dispose` khi `OnClosed`.

## Handoff Log — bước sau cần biết

- **Cách mở từ IPGSUseCam (Bước 3.3)**:
  ```csharp
  var win = new IPGS.RemoteControl.CcuUI.Views.RemoteScreenWindow(
      host: "192.168.1.x",
      port: IPGS.RemoteControl.Protocol.RemoteControlConstants.DefaultPort, // 17600
      token: "<shared-secret>"); 
  win.Show(); // hoặc win.ShowDialog(parentWindow)
  ```
- **Namespace quan trọng**: `IPGS.RemoteControl.CcuUI.Views.RemoteScreenWindow` — cần `using IPGS.RemoteControl.CcuUI.Views;`
- **ProjectReference cần thêm vào IPGSUseCam.csproj**:
  ```xml
  <ProjectReference Include="..\IPGS.RemoteControl.CcuUI\IPGS.RemoteControl.CcuUI.csproj" />
  ```
- **Không có thêm appsettings** — host/port/token truyền trực tiếp qua constructor; IPGSUseCam tự đọc config từ nguồn của mình (Settings, DB, appsettings.json...) rồi truyền vào.
- **Bug đã fix (không làm lại)**: `MouseButton` enum ở namespace `IPGS.RemoteControl.Protocol` (không phải `IPGS.RemoteControl.CcuClient`) — cần `using IPGS.RemoteControl.Protocol;` trong ViewModel.
- **Bước sau cần làm**: Bước 3.3 chỉ cần (1) thêm ProjectReference CcuUI vào IPGSUseCam.csproj, (2) đọc host/port/token từ config ZCU hiện có trong IPGSUseCam, (3) thêm 1 menu item / nút mở `new RemoteScreenWindow(host, port, token).Show()`. KHÔNG sửa logic cũ.

## Commit

- Hash: [chưa commit — không tự commit/push theo yêu cầu bước này]
- Đã push: không

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
