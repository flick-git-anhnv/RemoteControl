---
step: 1.1
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 16:44
---

# STEP 1.1 — Build Release CcuUI + khởi động app thật

## Input nhận
- Handoff Log STEP-0.2 (Screen Inventory + điều kiện tiên quyết từng màn hình).
- Project cần build: `IPGS.RemoteControl.CcuUI` (Avalonia 12.1.0, net8.0, WinExe).

## Nhiệm vụ
Build Release solution/project CcuUI (`dotnet build -c Release`), chạy app từ output Release, xác nhận app khởi động và hiển thị màn hình chính bình thường. Ghi lại đường dẫn exe + lệnh chạy để các bước Phase 2 dùng lại. Nếu build hoặc chạy fail → status 🛑 BLOCK, ghi lỗi chính xác, báo user — KHÔNG tự sửa code ngoài scope tài liệu.

## Definition of Done
- [ ] Build Release thành công (0 error) — ghi lại lệnh + đường dẫn output exe
- [ ] App khởi động được, màn hình chính hiển thị (chụp 1 ảnh xác nhận nháp vào `temp/user-manual-ccu-zcu/`)
- [ ] Ghi Handoff Log: đường dẫn exe, cách chạy, cảnh báo môi trường (nếu có)
- [ ] Nếu fail → step đánh 🛑 Blocked + mô tả lỗi, PLAN-MASTER cập nhật Blockers, báo user
- [ ] Cập nhật step file này + PLAN-MASTER.md

## Đã làm
- Build Release: `dotnet build IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj -c Release` → **Build succeeded, 0 Error** (456 warnings — chủ yếu AVLN5001 `Watermark` obsolete, không ảnh hưởng).
- Output exe: `IPGS.RemoteControl.CcuUI/bin/Release/net8.0/IPGS.RemoteControl.CcuUI.exe` — FileVersion 1.0.0.0, ProductVersion `1.0.0+2888754`.
- Khởi động app từ exe Release (`Start-Process`, WorkingDirectory = thư mục net8.0) → app chạy ổn định, PID 17180, cửa sổ đầu tiên "Remote Control — Danh sách máy tính Máy khách" (ConnectionEntryWindow), không popup lỗi/crash.
- Viết + kiểm chứng script chụp screenshot `temp/user-manual-ccu-zcu/capture-window.ps1`: dùng Win32 `PrintWindow` với `PW_RENDERFULLCONTENT` (chụp theo window handle — không dính taskbar/desktop, KHÔNG bị cửa sổ khác che đè), có `SetProcessDPIAware`, hỗ trợ lọc cửa sổ con theo `-WindowTitle` (EnumWindows theo PID).
- Chụp thử thật 1 ảnh `docs/user-manuals/screenshots/connection-entry-default.png` (1216×799, ~93KB) — đã verify bằng Read: đúng giao diện ConnectionEntryWindow đầy đủ nội dung.
- Lần chụp đầu bằng `CopyFromScreen` bị cửa sổ app khác trên desktop ("iPGS CCU — KZTEK") che đè → chuyển sang `PrintWindow` và chụp lại thành công. Đây là lý do script mặc định dùng PrintWindow.

### XÁC NHẬN KHỞI ĐỘNG ỨNG DỤNG (§Bước 0.5B)
```
Loại ứng dụng    : Desktop Avalonia 12.1.0 (net8.0, WinExe) — không phải WinForms
Cách khởi động   : Start-Process 'IPGS.RemoteControl.CcuUI\bin\Release\net8.0\IPGS.RemoteControl.CcuUI.exe' -WorkingDirectory '<thư mục net8.0>'
Trạng thái       : ✅ Đang chạy bình thường (PID 17180 — để nguyên cho Phase 2)
Màn hình đầu tiên: ConnectionEntryWindow — "Remote Control — Danh sách máy tính Máy khách"
Không có lỗi     : ✅ Không popup lỗi, không crash, không warning dialog
```

## Artifact
- `docs/user-manuals/screenshots/connection-entry-default.png` — ảnh kiểm chứng cơ chế chụp (1216×799, PrintWindow) — commit
- `temp/user-manual-ccu-zcu/capture-window.ps1` — script chụp chuẩn cho Phase 2 (temp/ gitignore, không commit)

## Quyết định quan trọng
- **Dùng `PrintWindow(PW_RENDERFULLCONTENT)` thay `CopyFromScreen`:** máy user có app khác cùng lúc trên desktop che vùng cửa sổ → CopyFromScreen chụp nhầm nội dung cửa sổ đè lên. PrintWindow chụp trực tiếp nội dung cửa sổ theo handle, an toàn với Avalonia (composition), kể cả khi bị che.
- App là Avalonia (không phải WinForms) — quy trình build Release + chạy từ exe vẫn áp dụng như WinForms theo documentation-writer.md; không cần `dotnet publish`.
- Ảnh `connection-entry-default.png` hiện chứa dữ liệu profile thật (4 máy: ZCU, Kiosk, VietAnh-VirtualMachine, Kien) — bước 2.1 sẽ backup profile store, chụp lại theo kịch bản 3 máy mẫu P01/P02/P03 và GHI ĐÈ ảnh này. Ảnh hiện tại chỉ để kiểm chứng cơ chế.

## Handoff Log — bước sau cần biết
- Đã làm: Build Release CcuUI thành công (0 error), app chạy thật từ exe Release (PID 17180, để nguyên đang chạy), cơ chế chụp screenshot đã kiểm chứng bằng 1 ảnh thật `connection-entry-default.png`.
- File/module đã đọc hoặc đổi: tạo `temp/user-manual-ccu-zcu/capture-window.ps1` + `docs/user-manuals/screenshots/connection-entry-default.png`; sửa step file này + PLAN-MASTER.md.
- Quyết định quan trọng: script chụp PHẢI dùng như sau — `powershell -NoProfile -ExecutionPolicy Bypass -File temp\user-manual-ccu-zcu\capture-window.ps1 -ProcessName "IPGS.RemoteControl.CcuUI" -OutputPath "docs\user-manuals\screenshots\<ten>.png" [-WindowTitle "<phần tiêu đề cửa sổ>"] [-DelayMs 500]` — luôn truyền `-WindowTitle` khi app có nhiều cửa sổ mở (dialog con) để chụp đúng cửa sổ; PrintWindow không bị cửa sổ khác che nhưng vẫn nên để cửa sổ hiện foreground để chắc chắn đã render xong.
- Bước sau cần biết (2.1): app ĐANG chạy PID 17180, KHÔNG cần khởi động lại (nếu đã tắt: chạy lại exe Release theo lệnh ở khối Xác nhận); trước khi chụp bộ 2.1 PHẢI backup profile store hiện có (4 máy thật) và chụp `connection-entry-empty` TRƯỚC khi thêm 3 máy mẫu P01/P02/P03; ảnh `connection-entry-default.png` sẽ được chụp lại/ghi đè với dữ liệu mẫu; kích thước cửa sổ chính 1216×799 — giữ nguyên để bộ ảnh đồng nhất; DPI đã xử lý trong script (SetProcessDPIAware).

## Commit
- Hash: 6fbaa94 (commit bước 1.1 — hash cuối xem `git log --oneline -1` nhánh main)
- Đã push: không (theo chỉ đạo bước này)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
