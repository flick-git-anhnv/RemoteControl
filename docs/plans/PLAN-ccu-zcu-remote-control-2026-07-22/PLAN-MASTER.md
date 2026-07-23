---
task: ccu-zcu-remote-control
created: 2026-07-22
updated: 2026-07-23 10:30
status: blocked
workflow: WF-FEATURE (rút gọn — xem ghi chú)
priority: P1
---

# PLAN MASTER: CCU → ZCU Remote Control (Remote Desktop tự xây dựng)

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md` tương ứng.

## Mô tả

Xây dựng tính năng remote control từ phần mềm CCU (Windows, Avalonia) đến một ZCU cụ thể (Ubuntu 22.04, X11) — tương tự remote desktop nhưng tự viết, không dùng VNC/TeamViewer để tránh chi phí bản quyền.

**Kiến trúc hai phía:**
- **ZCU-side (server):** .NET Linux service — dùng X11 `XShm`/`XGetImage` capture màn hình liên tục, nén JPEG, gửi frame qua TCP; nhận lệnh chuột từ CCU, inject qua `XTest` (`XTestFakeMotionEvent`, `XTestFakeButtonEvent`).
- **CCU-side (client):** thư viện .NET + Avalonia UserControl — kết nối TCP tới ZCU, nhận frame, hiển thị; khi user click vào vùng remote, gửi toạ độ + loại click ngược lại ZCU.

**Ràng buộc cứng:** TUYỆT ĐỐI KHÔNG chèn code vào project ZCU/CCU hiện có. Chỉ tạo project mới + thêm reference từ CCU hiện có.

## Nguồn yêu cầu

- Yêu cầu gốc: User yêu cầu remote control CCU → ZCU tự xây, không dùng tool bên thứ 3
- ZCU: Ubuntu 22.04, X11 (echo $XDG_SESSION_TYPE = x11)
- CCU: Windows, Avalonia UI (IPGSUseCam.sln)
- Workflow: WF-FEATURE (rút gọn — bỏ PM/BA/UX/EM/CTO/PJM vì đây là tính năng hạ tầng kỹ thuật với yêu cầu đã xác định đầy đủ; giữ Tech Lead + Senior Dev + UXR + QA theo CLAUDE.md §3.5)
- Agent chain: Tech Lead → Senior Dev (ZcuAgent) → Tech Lead (review) → Senior Dev (CcuClient + CcuUI + integration) → Tech Lead (review) → UX/UI Reviewer → QA Engineer

## Phases & Steps

> **Session isolation (CLAUDE.md §16.5):** Mỗi bước ⬜/🔄 PHẢI chạy tách session — LOCAL dùng `Agent` subagent, WEB dùng `RemoteTrigger`. Agent/trigger tự tạo/cập nhật step file riêng, commit+push, rồi cập nhật đúng 1 dòng status ở bảng dưới đây.

### Phase 1: Thiết kế kỹ thuật

| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Tech Lead thiết kế giao thức TCP + TDD (message format, frame encoding, auth, connection lifecycle) | Tech Lead | ✅ | `steps/STEP-1.1-tech-design.md` | 2026-07-22 |

### Phase 2: ZCU Agent — Server-side Linux Service

| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Senior Dev tạo project `IPGS.RemoteControl.ZcuAgent` (X11 capture + XTest inject + TCP server + auth) | Senior Developer | ✅ | `steps/STEP-2.1-zcu-agent-impl.md` | 2026-07-23 08:25 |
| 2.2 | Tech Lead review ZcuAgent (build sạch, X11 P/Invoke đúng, không chèn vào project cũ) | Tech Lead | ✅ | `steps/STEP-2.2-zcu-agent-review.md` | 2026-07-23 10:15 |

### Phase 3: CCU Client — Client-side Avalonia

| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | Senior Dev tạo project `IPGS.RemoteControl.CcuClient` (TCP client library, decode frame, gửi mouse event) | Senior Developer | ✅ | `steps/STEP-3.1-ccu-client-impl.md` | 2026-07-23 08:36 |
| 3.2 | Senior Dev tạo project `IPGS.RemoteControl.CcuUI` (Avalonia UserControl/Window hiển thị remote, handle click) | Senior Developer | ✅ | `steps/STEP-3.2-ccu-ui-impl.md` | 2026-07-23 09:11 |
| 3.3 | Senior Dev tích hợp vào IPGSUseCam — chỉ add reference + tạo entry point (menu/button mở remote window) | Senior Developer | ✅ | `steps/STEP-3.3-integration.md` | 2026-07-23 09:17 |
| 3.4 | Tech Lead review CCU client + UI + integration (build sạch, không sửa business logic cũ) | Tech Lead | ✅ | `steps/STEP-3.4-ccu-review.md` | 2026-07-23 09:24 |

### Phase 4: QA & UI Review

| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 4.1 | UX/UI Reviewer kiểm tra UI remote screen (hiển thị frame, layout, feedback trạng thái kết nối) | UX/UI Reviewer | 🛑 | `steps/STEP-4.1-ux-review.md` | - |
| 4.2 | QA Engineer verify chức năng (kết nối TCP, hiển thị màn hình ZCU, click điều khiển được, auth) | QA Engineer | 🛑 | `steps/STEP-4.2-qa-verify.md` | - |

## Artifacts dự kiến (tổng)

- [ ] `docs/tech-design/TDD-remote-control.md` — Thiết kế giao thức + kiến trúc
- [ ] `docs/tech-design/TDD-remote-control.docx` + `.pdf`
- [ ] `IPGS.RemoteControl.ZcuAgent/` — project Linux service (cấp solution root)
- [ ] `IPGS.RemoteControl.CcuClient/` — project thư viện client (cấp solution root)
- [ ] `IPGS.RemoteControl.CcuUI/` — project Avalonia UserControl/Window (cấp solution root)
- [ ] `docs/ux-review/UX-REVIEW-remote-control.md` + `.docx` + `.pdf`
- [ ] `docs/test-cases/TC-remote-control.md`
- [x] `docs/devops/DEPLOY-remote-control.md` + `.docx` + `.pdf` — hướng dẫn triển khai kỹ thuật (deploy guide)

## Blockers

Không có (đã unblock — xem Lịch sử cập nhật 2026-07-23 cho quá trình test thật + mở rộng scope).

## Quyết định / Ghi chú tổng

1. **Rút gọn WF-FEATURE:** bỏ bước PM/BA/UX Designer/EM/CTO/PJM vì yêu cầu kỹ thuật đã được user xác nhận đầy đủ (X11, XTest, TCP, SkiaSharp). Vẫn giữ UXR theo CLAUDE.md §3.5 vì có UI mới (remote screen window).
2. **Vị trí project mới:** đặt ngang hàng với các project hiện có ở solution root (cùng cấp `IPGS.Object/`, `IPGSUseCam/`, ...).
3. **[CẬP NHẬT 2026-07-23 chiều] `IPGS.RemoteControl.CcuUI` không còn tích hợp vào IPGSUseCam** — user yêu cầu tách hẳn thành app Avalonia độc lập (`.exe` riêng, cross-platform Windows + Linux), giống 1 VNC viewer. Đã gỡ hoàn toàn `<ProjectReference>`, menu, dialog khỏi IPGSUseCam (grep xác nhận sạch). Ràng buộc cứng "IPGSUseCam bất khả xâm phạm" vẫn giữ nguyên và nay càng chặt hơn — không còn bất kỳ liên kết nào giữa 2 project.
4. **Encoding frame:** JPEG chất lượng có thể điều chỉnh (mặc định ~70%) là đủ cho remote desktop hành chính; H.264 là tùy chọn nâng cấp sau, không bắt buộc trong sprint này.
5. **Authentication:** whitelist IP + shared secret (token đơn giản) là đủ cho v1; không cần TLS/cert trong sprint này nhưng Tech Lead ghi rõ điểm mở rộng trong TDD.
6. **[MỞ RỘNG v1.1] Keyboard Support:** đã implement đầy đủ (KEY_EVENT 0x40), review PASS. Giới hạn đã biết: gõ tiếng Việt có dấu cần IME chạy trên ZCU (Unikey/ibus), CCU chỉ gửi phím Latin gốc.

## Lịch sử cập nhật

| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-07-22 | Plan tạo mới | task-planner |
| 2026-07-22 | Bước 1.1 Done — TDD-remote-control.md + .docx đã tạo (PDF fail docx2pdf, không block) | Tech Lead |
| 2026-07-23 | Bước 2.1 Done — CcuClient (Protocol + Interface) + ZcuAgent (đầy đủ) build sạch 0 error | Senior Developer |
| 2026-07-23 10:15 | Bước 2.2 Done — Review PASS; tự fix PONG timeout gap (TDD §7) trong ClientSession.cs; build 0 warning/error | Tech Lead |
| 2026-07-23 08:36 | Bước 3.1 Done — RemoteControlClient.cs implement đầy đủ; build 0 error 0 warning | Senior Developer |
| 2026-07-23 09:11 | Bước 3.2 Done — CcuUI project build 0 error; fix bug using IPGS.RemoteControl.Protocol cho MouseButton | Senior Developer |
| 2026-07-23 09:17 | Bước 3.3 Done — IPGSUseCam tích hợp: ProjectReference + RemoteZcuConnectionDialog mới + menu item + handler; build 0 error | Senior Developer |
| 2026-07-23 09:24 | Bước 3.4 Done — Review CCU-side PASS; tự fix leak WriteableBitmap trong RemoteScreenViewModel (dispose bản cũ khi gán mới trên UI thread + trong Dispose); build cả 3 project PASS 0 error; ràng buộc IPGS.Object/KztekComponentAvalonia/IPGSUseCam logic cũ tôn trọng | Tech Lead |
| 2026-07-23 10:30 | Phase 4 BLOCKED — chờ user cung cấp máy Ubuntu 22.04 X11 thật (SSH) để UXR/QA test end-to-end (WSL2 không đủ điều kiện) | Dispatcher |
| 2026-07-23 14:00 | User cung cấp VM Ubuntu 22.04 X11 thật (192.168.21.37) qua SSH key. Cài .NET 8 SDK, build+run ZcuAgent thật. Phát hiện + fix bug: XShmAttach async BadAccess gây crash process (thiếu XSetErrorHandler) — fix + refactor thành X11ErrorTracker dùng chung, có logging | Dispatcher + Senior Developer + Tech Lead |
| 2026-07-23 14:30 | Test thật end-to-end: video streaming + mouse control hoạt động tốt (user xác nhận qua screenshot). Màn hình đen do session tự khoá (screensaver) — đã tắt lock, không phải bug. Cập nhật DEPLOY guide với các bài học này | Dispatcher (user thao tác thủ công) |
| 2026-07-23 15:45 | Bổ sung scope mới theo yêu cầu user: (1) Keyboard Support v1.1 — thiết kế (TDD §17) + implement 2 phía (ZCU: KeyboardInjector + ReleaseAllKeys chống stuck-key; CCU: KeyboardMapper 3-tier + ReleaseAllDownKeys) — Tech Lead review PASS | Tech Lead + Senior Developer |
| 2026-07-23 16:15 | (2) Tách `IPGS.RemoteControl.CcuUI` thành app Avalonia độc lập, cross-platform (win-x64;linux-x64), gỡ hoàn toàn khỏi IPGSUseCam (xóa ProjectReference/menu/dialog) — Tech Lead review PASS, IPGSUseCam xác nhận sạch (grep rỗng) | Tech Lead + Senior Developer |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
**Cách đọc nhanh:** đọc MASTER trước → nếu cần chi tiết bước cụ thể mới mở step file tương ứng.
