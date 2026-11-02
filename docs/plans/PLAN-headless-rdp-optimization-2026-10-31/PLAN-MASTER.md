---
task: TASK-headless-rdp-optimization-2026-10-31
created: 2026-10-31
updated: 2026-10-31
status: done
workflow: WF-REFACTOR
priority: P1
---

# PLAN MASTER: Tối Ưu Remote Không Màn Hình Vật Lý (Headless), RDP & Virtual Display

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả
Tối ưu hóa toàn diện khả năng điều khiển từ xa khi máy ZCU chạy trong môi trường **không có màn hình vật lý (Headless / Kiosk không cắm cáp HDMI)** hoặc chạy trong môi trường **máy ảo / phiên RDP (xrdp)**:
1. **F-HEADLESS-1:** Bộ phân giải X11 Display thông minh (`X11DisplayResolver`): tự động dò tìm Unix domain sockets tại `/tmp/.X11-unix/`, fallback linh hoạt giữa `:0`, `:1` (Xvfb/dummy), và `:10.0` / `:11.0` (xrdp RDP virtual session).
2. **F-HEADLESS-2:** Nới lỏng rào chắn `XDG_SESSION_TYPE` trong `Program.cs`: chỉ chặn `wayland`, cho phép chạy mượt mà trong daemon systemd headless và phiên RDP.
3. **F-HEADLESS-3:** Chống màn hình đen (Blanking) & DPMS Sleep khi remote không màn hình bằng `XSetScreenSaver(_display, 0, 0, 0, 0)` và `XResetScreenSaver`.
4. **F-HEADLESS-4:** Cấu hình tùy chọn `Display` trong `AgentOptions.cs` cho phép người dùng chỉ định Display cụ thể nếu cần.
5. **F-HEADLESS-5:** Mở rộng Unit Test Suite tự động kiểm thử `X11DisplayResolverTests` (nâng tổng số lên 94 tests passed 100%).

## Nguồn yêu cầu
- Yêu cầu người dùng: "tối ưu remote không màn hình RDP"
- Workflow: WF-REFACTOR
- Agent chain: Senior Developer → Junior Developer → QA Engineer → Documentation Writer → Tech Lead

## Phases & Steps

### Phase 1: Hiện thực hóa Bộ Dò Display X11 Thông Minh & DPMS Anti-Blanking (P1)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 1.1 | Tạo `X11DisplayResolver` tự động dò tìm Display `:0`, `:1`, `:10.0` (xrdp) | Senior Developer | done | `steps/STEP-1.1-x11-display-resolver.md` | 2026-10-31 09:44 |
| 1.2 | Nới lỏng `XDG_SESSION_TYPE` guard & Tích hợp DPMS Anti-Blanking trong `X11ScreenCapturer` | Senior Developer | done | `steps/STEP-1.2-headless-guard-dpms-antiblank.md` | 2026-10-31 09:44 |

### Phase 2: Mở rộng Cấu hình & Unit Test Suite (P2)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 2.1 | Thêm cấu hình `Display` trong `AgentOptions.cs` và cập nhật Injectors | Senior Developer | done | `steps/STEP-2.1-display-options-injectors.md` | 2026-10-31 09:44 |
| 2.2 | Xây dựng bộ Unit Test tự động cho `X11DisplayResolverTests` | Junior Developer | done | `steps/STEP-2.2-unit-tests-display-resolver.md` | 2026-10-31 09:45 |

### Phase 3: Kiểm thử Toàn diện & Đồng bộ Tài liệu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 3.1 | Test Suite Run & Solution Clean Build Verification | QA Engineer | done | `steps/STEP-3.1-qa-test-run.md` | 2026-10-31 09:45 |
| 3.2 | Cập nhật Code Graph v3.5 & Gotchas G016 | Documentation Writer | done | `steps/STEP-3.2-sync-code-graph-docs.md` | 2026-10-31 09:46 |

### Phase 4: Re-Review Vòng 7 & Nghiệm Thu Headless RDP
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 4.1 | Re-Review Phản biện Vòng 7 & Nghiệm thu Tính năng Headless RDP | Tech Lead | done | `steps/STEP-4.1-tech-lead-re-review-round7.md` | 2026-10-31 09:47 |

## Artifacts hoàn thành
- `IPGS.RemoteControl.ZcuAgent/Interop/X11DisplayResolver.cs`
- `IPGS.RemoteControl.ZcuAgent/Capture/X11ScreenCapturer.cs`, `Program.cs`, `AgentOptions.cs`
- `IPGS.RemoteControl.ZcuAgent/Input/MouseInjector.cs`, `KeyboardInjector.cs`
- File test `IPGS.RemoteControl.Tests/X11DisplayResolverTests.cs` (tổng 94 tests passed 100%).
- Cập nhật bài học `G016` trong `.gemini/shared/GOTCHAS.md`.
- Báo cáo Re-Review Vòng 7 `RE-REVIEW-headless-rdp-2026-10-31.md` (+ DOCX, PDF).
- Cập nhật `code-graph/CODE-GRAPH.md` (+ DOCX, PDF).
