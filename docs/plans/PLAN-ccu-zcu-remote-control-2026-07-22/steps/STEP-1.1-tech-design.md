---
step: 1.1
plan: ../PLAN-MASTER.md
agent: tech-lead
status: done
completed_at: 2026-07-22 (thời điểm commit)
---

# STEP 1.1 — Tech Lead thiết kế giao thức TCP + TDD

## Input nhận

Từ user (yêu cầu đã xác nhận):
- ZCU: Ubuntu 22.04, X11, .NET Linux service; dùng XShm/XGetImage capture, XTest inject mouse
- CCU: Windows, Avalonia; dùng SkiaSharp hiển thị frame
- Giao tiếp: TCP socket giữa CCU (client) và ZCU (server)
- Auth: whitelist IP + shared secret (v1, không cần TLS)
- Frame encoding: JPEG (chất lượng ~70%, có thể điều chỉnh)
- Ràng buộc: TUYỆT ĐỐI không sửa project hiện có (IPGS.Object, IPGSUseCam, ...)

## Nhiệm vụ

Thiết kế đầy đủ giao thức truyền thông TCP giữa ZcuAgent và CcuClient, bao gồm: định nghĩa message format (header/payload), frame encoding pipeline, connection lifecycle (handshake, keepalive, disconnect), cơ chế authentication v1, và cấu trúc project mới. Ghi thành TDD.

## Definition of Done

- [x] `docs/tech-design/TDD-remote-control.md` tạo xong, đủ 5 mục chính + phần mở rộng
- [x] TDD ghi rõ hằng số, P/Invoke signatures (libX11 / libXext-XShm / libXtst), interface C# public
- [x] TDD ghi điểm mở rộng cho TLS và H.264 (§13, §16)
- [x] `.docx` xuất thành công; `.pdf` thất bại do Word COM (docx2pdf) — theo CLAUDE.md §19.4 KHÔNG block; sẽ retry sau
- [ ] Commit + push lên nhánh `zcu-avalonia` (do parent Dispatcher thực hiện — subagent này không tự push để tránh chồng lấn với các bước khác)

## Đã làm

1. Đọc PLAN-MASTER + STEP-1.1 để nắm scope.
2. Khảo sát cấu trúc solution: các project hiện có nằm ngang hàng ở root repo (`IPGS.Object/`, `IPGSUseCam/`, `KztekComponentAvalonia/`, ...). Xác định vị trí đặt 3 project mới cùng cấp.
3. Tạo `docs/tech-design/` (chưa tồn tại) và viết `TDD-remote-control.md` gồm 16 mục: bối cảnh, goals/non-goals, mermaid sequence diagram, cấu trúc solution, message format binary length-prefix, frame pipeline, state machine, authentication v1, P/Invoke signatures (libX11/libXext/libXtst + SysV shm), interface C# public (`IRemoteControlClient`, MouseButton enum, event args), constants, appsettings.json mẫu, security v1 vs v2, rủi ro & mitigation, task breakdown khớp PLAN-MASTER, điểm mở rộng.
4. Chạy `scripts/md_to_docx_kztek.py` → `.docx` thành công, `.pdf` thất bại do Word COM crash (retry 2 lần đều lỗi `docx2pdf`). Ghi chú theo CLAUDE.md §19.4 — không block workflow.

## Artifact

- `docs/tech-design/TDD-remote-control.md` (~450 dòng)
- `docs/tech-design/TDD-remote-control.docx` (xuất bởi md_to_docx_kztek.py, brand KZTEK)
- `docs/tech-design/TDD-remote-control.pdf` — CHƯA CÓ (docx2pdf lỗi, để bước sau retry hoặc dùng LibreOffice)

## Quyết định quan trọng

1. **Message format:** binary length-prefix (1 byte type + 4 byte BE length + payload), KHÔNG dùng JSON để tối ưu băng thông cho FRAME_JPEG.
2. **Default TCP port:** `17600` (chưa dùng bởi service KZTEK nào khác đã biết).
3. **Encoding lib:** `SkiaSharp` cả 2 phía (loại `System.Drawing`, `ImageSharp`) — dùng NuGet `SkiaSharp.NativeAssets.Linux` trên ZcuAgent.
4. **Auth v1:** shared token plaintext + IP whitelist + rate-limit 3 fail/60s → ban 5min. `CryptographicOperations.FixedTimeEquals` để so sánh.
5. **Wayland guard:** ZcuAgent PHẢI check `$XDG_SESSION_TYPE=x11` khi start, refuse nếu Wayland (XTest không hoạt động trên Wayland).
6. **X11 threading:** `XInitThreads()` gọi TRƯỚC `XOpenDisplay` — cần thiết vì capture thread + inject thread cùng dùng display.
7. **Shared protocol code:** đặt trong `CcuClient/Protocol/` và `<Compile Link="…"/>` vào ZcuAgent để tránh copy-paste.
8. **Ràng buộc cứng:** `IPGSUseCam` CHỈ thêm 1 `<ProjectReference>` sang `CcuUI` + 1 menu item mở `RemoteScreenWindow` — KHÔNG đụng business logic.

## Handoff Log — bước sau cần biết

- **Đã làm:** TDD đầy đủ 16 mục tại `docs/tech-design/TDD-remote-control.md`. Đã chốt: 3 tên project chính xác (`IPGS.RemoteControl.ZcuAgent`, `IPGS.RemoteControl.CcuClient`, `IPGS.RemoteControl.CcuUI`), port 17600, giao thức binary length-prefix, JPEG q=70 fps=15, SkiaSharp cross-platform, shared secret auth.
- **File/module đã đọc hoặc đổi:** đọc `PLAN-MASTER.md`, `STEP-1.1-tech-design.md`; tạo mới `docs/tech-design/TDD-remote-control.md` + `.docx`.
- **Quyết định quan trọng:** xem mục "Quyết định quan trọng" ở trên — Senior Dev bước 2.1 & 3.x **PHẢI đọc TDD trước khi code**, đặc biệt §5 (message format), §7 (state machine), §9 (P/Invoke signatures).
- **Bước sau cần biết:**
  - Bước 2.1 (ZcuAgent): tạo project net8.0 `linux-x64`, cần `SkiaSharp.NativeAssets.Linux` NuGet; postinst script phải `apt install libxtst6 libxext6 libx11-6`. Nhớ `XInitThreads()` gọi TRƯỚC `XOpenDisplay`. `XShmGetImage` trả về Bool int, phải check.
  - Bước 3.1 (CcuClient): giữ project **cross-platform** (`net8.0` không RID cụ thể) để test trên cả Win và Linux.
  - Bước 3.3 (integration): CHỈ được add `<ProjectReference>` từ `IPGSUseCam.csproj` sang `IPGS.RemoteControl.CcuUI.csproj` + thêm menu item — TUYỆT ĐỐI không đụng file logic cũ.
  - PDF của TDD hiện thiếu do Word COM lỗi — không block, sau này có thể chạy lại `md_to_docx_kztek.py` hoặc dùng LibreOffice fallback.
  - Không có gotcha P/Invoke bất ngờ nào phát sinh khi soạn TDD (mọi thứ theo docs X11).

## Commit

- Hash: [Dispatcher/parent commit]
- Đã push: [Dispatcher/parent quyết định]

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
