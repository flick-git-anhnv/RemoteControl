---
step: 1.3
plan: ../PLAN-MASTER.md
agent: junior-developer
status: done
completed_at: 2026-10-31T08:40:00+07:00
deps: ["1.2"]
---

# STEP 1.3 — Nâng cấp SSH.NET & Xóa nợ XAML

## Input nhận
- Đề xuất 1.3 và 3.3.
- Vấn đề S1 (NU1903 High Severity) và U2 (32 cảnh báo XAML Watermark obsolete).

## Nhiệm vụ
1. Nâng cấp package `SSH.NET` trong `IPGS.RemoteControl.CcuClient.csproj` lên phiên bản mới nhất sạch cảnh báo bảo mật.
2. Chuẩn hóa toàn bộ thuộc tính `TextBox.Watermark` sang `PlaceholderText` trong các file AXAML của `CcuUI`.

## Definition of Done
- [x] Không còn cảnh báo NU1903 về lỗ hổng SSH.NET khi build.
- [x] Không còn cảnh báo AVLN5001 về TextBox.Watermark.

## Đã làm
1. Trong `IPGS.RemoteControl.CcuClient/IPGS.RemoteControl.CcuClient.csproj`:
   - Nâng cấp `SSH.NET` từ `2025.1.0` lên `2026.0.0`.
   - Biên dịch kiểm tra: xóa sạch hoàn toàn 2 lỗ hổng bảo mật NU1903 (GHSA-mggc-4xg6-vcxf và GHSA-q939-rpr3-3284).
2. Trong `IPGS.RemoteControl.CcuUI`:
   - Thay thế toàn bộ thuộc tính `Watermark="..."` thành `PlaceholderText="..."` trên 10 file AXAML:
     - `AppUpdateWindow.axaml`
     - `BulkActionWindow.axaml`
     - `ComputerEditWindow.axaml`
     - `ConnectionEntryWindow.axaml`
     - `FileManagerWindow.axaml`
     - `LicenseWindow.axaml`
     - `MountFolderWindow.axaml`
     - `NetworkScanWindow.axaml`
     - `RemoteAppInstallWindow.axaml`
     - `ZcuSetupWizardWindow.axaml`
   - Cập nhật cả `MountFolderWindow.axaml.cs` (`txtRemotePath.PlaceholderText`).
   - Xóa sạch toàn bộ 32 cảnh báo AVLN5001/CS0618.

## Artifact
- [IPGS.RemoteControl.CcuClient.csproj](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuClient/IPGS.RemoteControl.CcuClient.csproj)
- 10 file AXAML trong `IPGS.RemoteControl.CcuUI/Views/`
- [MountFolderWindow.axaml.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuUI/Views/MountFolderWindow.axaml.cs)

## Quyết định quan trọng
- Sử dụng phiên bản ổn định `SSH.NET 2026.0.0` để bảo đảm tính tương thích ngược với API hiện tại mà không phải sửa đổi code SSH.
- Triệt để xóa bỏ `Watermark` chuẩn bị tương thích hoàn toàn với các phiên bản Avalonia kế tiếp.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Không quay lại dùng cú pháp Watermark.
- watch_out: Khi viết XAML mới cho các view sau, luôn dùng `PlaceholderText`.
- next_inputs: Cung cấp codebase sạch cảnh báo cho bước khởi tạo Unit Test Suite.
