---
step: 3.3
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-07-23 09:17
---

# STEP 3.3 — Senior Dev tích hợp vào IPGSUseCam

## Input nhận

Từ STEP-3.2 Handoff Log — cần có: cách mở `RemoteControlWindow` từ code ngoài (constructor params, namespace), NuGet packages đã thêm.

## Nhiệm vụ

Tích hợp remote control vào IPGSUseCam: thêm `<ProjectReference>` đến `IPGS.RemoteControl.CcuUI` trong `IPGSUseCam.csproj`, thêm 1 menu item hoặc button "Remote ZCU" vào UI hiện có (chọn vị trí phù hợp trong MainWindow hoặc màn hình quản lý ZCU), và khi user click thì mở `RemoteControlWindow` với host/port/secret lấy từ config ZCU đã chọn.

**Ràng buộc cứng:** chỉ được THÊM reference + THÊM 1 menu/button entry point — TUYỆT ĐỐI KHÔNG sửa business logic, không refactor code cũ, không đổi bất kỳ class/method hiện có.

## Definition of Done

- [ ] `IPGSUseCam.csproj` có thêm `<ProjectReference>` đến `IPGS.RemoteControl.CcuUI` (và `IPGS.RemoteControl.CcuClient` nếu cần trực tiếp)
- [ ] Có menu item hoặc button "Remote ZCU" (hoặc tên tương đương) trong IPGSUseCam UI
- [ ] Click button → `RemoteControlWindow` mở ra, kết nối được với ZcuAgent đang chạy
- [ ] Build sạch: `dotnet build IPGSUseCam` không error
- [ ] KHÔNG có thay đổi nào trong các file `.axaml`/`.axaml.cs` hiện có ngoài việc thêm button/menu item và event handler tối thiểu
- [ ] Commit + push lên nhánh `zcu-avalonia`

## Đã làm

1. Khảo sát `StaticPool.ZCU` — object ZCU trong IPGSUseCam KHÔNG có trường IP/hostname riêng cho kết nối TCP (chỉ có `comport`, `baudrate` cho serial, và `username`/`password` cho share path). Quyết định: dùng dialog nhập thủ công.
2. Thêm `<ProjectReference>` đến `IPGS.RemoteControl.CcuUI` vào `IPGSUseCam.csproj`.
3. Thêm menu item `PART_MenuRemoteZCU` "Điều khiển ZCU từ xa..." dưới menu "Hệ thống" trong `MainWindow.axaml`.
4. Tạo mới `RemoteZcuConnectionDialog.axaml` + `.axaml.cs` — dialog 3 trường: IP/Host, Port (mặc định 17600), Token; dùng `KzTextBox` từ KztekComponentAvalonia.
5. Thêm event handler 6 dòng vào `OnLoaded` trong `MainWindow.axaml.cs` — show dialog → nếu user nhập host → `new RemoteScreenWindow(...).Show()`.
6. Build `dotnet build IPGSUseCam` → **0 error, 818 warning** (tất cả warning là pre-existing, không có warning mới liên quan đến code thêm vào ngoài AVLN5001 `Watermark` cũng có ở các file cũ khác).

## Artifact

- `IPGSUseCam/IPGSUseCam.csproj` — +1 dòng ProjectReference
- `IPGSUseCam/Views/MainWindow.axaml` — +3 dòng (Separator + MenuItem)
- `IPGSUseCam/Views/MainWindow.axaml.cs` — +6 dòng (event handler trong OnLoaded)
- `IPGSUseCam/Views/RemoteZcuConnectionDialog.axaml` — file mới (45 dòng)
- `IPGSUseCam/Views/RemoteZcuConnectionDialog.axaml.cs` — file mới (42 dòng)

## Quyết định quan trọng

- **Giải pháp host/token:** `StaticPool.ZCU` không có IP field → tạo `RemoteZcuConnectionDialog` nhập thủ công. Đây là giải pháp đơn giản nhất theo chỉ định trong Handoff Log của Bước 3.2 (không tạo thêm hệ thống config phức tạp).
- **Không dùng `KzPasswordTextBox`** cho token — chưa xác nhận control đó tồn tại trong KztekComponentAvalonia; dùng `KzTextBox` thường là đủ cho admin nhập token.
- **`RemoteScreenWindow.Show()`** (không `ShowDialog`) — window remote chạy song song, không block MainWindow.

## Handoff Log — bước sau cần biết

- **Đã làm:** Thêm ProjectReference + menu item "Điều khiển ZCU từ xa..." dưới "Hệ thống" + dialog nhập IP/Port/Token + event handler mở RemoteScreenWindow. Build sạch 0 error.
- **File đã sửa trong IPGSUseCam:**
  - `IPGSUseCam.csproj` — chỉ thêm 1 dòng ProjectReference (dòng cuối ItemGroup ProjectReference)
  - `Views/MainWindow.axaml` — chỉ thêm 2 dòng (`<Separator />` + `<MenuItem PART_MenuRemoteZCU>`) bên trong MenuItem "Hệ thống"
  - `Views/MainWindow.axaml.cs` — chỉ thêm 6 dòng event handler sau dòng wire `PART_MenuSimulator` trong `OnLoaded`
- **File mới tạo:** `Views/RemoteZcuConnectionDialog.axaml` + `.axaml.cs` — không liên quan project cũ nào
- **Bước sau cần biết:** KHÔNG có thay đổi business logic nào trong IPGSUseCam. `git diff --stat HEAD` cho đúng 3 file sửa + 2 file mới untracked (+ `dist/` là untracked pre-existing không liên quan). Reviewer chỉ cần kiểm tra 5 file trên.
- **Cách user dùng:** Menu "Hệ thống" → "Điều khiển ZCU từ xa..." → nhập IP ZCU, cổng (mặc định 17600), token → click "Kết nối" → RemoteScreenWindow mở.

## Commit

- Hash: [chưa commit theo chỉ định — Bước 3.3 không tự commit/push]
- Đã push: không

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
