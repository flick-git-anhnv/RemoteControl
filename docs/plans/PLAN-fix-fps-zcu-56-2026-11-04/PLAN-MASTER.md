---
task: fix-fps-zcu-56
created: 2026-11-04
updated: 2026-11-04
status: completed
workflow: WF-BUGFIX
priority: P1
---

# PLAN MASTER: Vá Lỗi XShm G020, Khắc Phục Ngắt Kết Nối 30s & Triệt Tiêu 5 Điểm Nghẽn Hiệu Năng Máy Đích 192.168.21.56

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước (mô tả đầy đủ, Handoff Log, artifact chi tiết) nằm ở `steps/STEP-[N.M]-[tên].md` tương ứng.

## Mô tả
1. Biên dịch bản `IPGS.RemoteControl.ZcuAgent` mới nhất (vá lỗi ngầm G020 XShm MIT-SHM) và triển khai lên `192.168.21.56`. (Đã hoàn thành Phase 1-3).
2. Khắc phục triệt để lỗi ngắt kết nối định kỳ 30 giây một lần (Broken pipe / PING timeout): cập nhật heartbeat on frames và `Task.Yield`. (Đã hoàn thành Phase 4).
3. Triệt tiêu 5 điểm nghẽn hiệu năng gây lag điều khiển thực tế (Phase 5):
   - Diệt tiến trình trùng IPGSv4 PID 3658 và chống mở đè từ Desktop launcher.
   - Nâng cấp AdaptiveQuality/FPS trong `ClientSession.cs` theo chu kỳ CPU (`cycleMs = capture + encode + write`).
   - Tối ưu bộ đệm `WriteableBitmap` pool tái sử dụng trong `RemoteScreenViewModel.cs`.
   - Bỏ lệnh chặn `XSync` trong `MouseInjector.Button` và `KeyboardInjector.SendKey`.
   - Khắc phục lỗ hổng tự động hạ cấp TLS về TCP plaintext trong `RemoteControlClient.cs`.
   - Tích hợp Latest-Wins Coalescing chống dồn hàng đợi di chuột TCP.

## Nguồn yêu cầu
- Yêu cầu gốc: "check lại đi, tôi cài bản remote mới r mà fps nó vẫn thấp" -> "oke, tiến hành" -> "vẫn rất chậm, ktra lại log xem" -> "kiểm tra chi tiết lại toàn bộ..." -> "oke, tiến hành đi"
- Workflow: WF-BUGFIX — Sửa lỗi hiệu năng & Triển khai
- Agent chain: Senior Developer (fix) → Tech Lead (review) → QA Engineer (verify & sign-off) → DevOps Engineer (deploy & sync)

## Phases & Steps

### Phase 1: Biên dịch & Đồng bộ Resource ZcuAgent Bản Mới Nhất
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Publish Release linux-x64 và copy vào Resources/zcu-agent/linux-x64 | senior-developer | ✅ | `steps/STEP-1.1-build-and-sync-resources.md` | 2026-11-04 09:23:00 |

### Phase 2: Triển khai lên Thiết bị 192.168.21.56
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Upload binary mới lên /home/ccu/ipgs/remote-agent/, cấu hình TargetFps 30, restart service | devops-engineer | ✅ | `steps/STEP-2.1-deploy-target-56.md` | 2026-11-04 09:25:35 |

### Phase 3: Kiểm thử, Đo đạc & Nghiệm thu ZcuAgent XShm
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | Xác minh XShm thành công trong log, đo tải CPU, FPS live và nghiệm thu | qa-engineer | ✅ | `steps/STEP-3.1-verify-and-signoff.md` | 2026-11-04 09:26:00 |

### Phase 4: Sửa Lỗi Ngắt Kết Nối Định Kỳ 30s & Cập Nhật CCU UI Client
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 4.1 | Vá Keepalive Ping/Pong trên CcuClient & ZcuAgent, biên dịch và chạy CCU UI mới | senior-developer | ✅ | `steps/STEP-4.1-fix-ping-pong-and-client-render.md` | 2026-11-04 09:47:00 |
| 4.2 | Xác minh luồng stream duy trì liên tục > 60s không disconnect, FPS đạt 25-30 live | qa-engineer | ✅ | `steps/STEP-4.2-verify-stream-stability.md` | 2026-11-04 09:50:50 |

### Phase 5: Triệt Tiêu 5 Điểm Nghẽn Hiệu Năng & Lag Điều Khiển
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 5.1 | Diệt PID 3658 trên máy đích và chặn chạy trùng từ Desktop shortcut | devops-engineer | ✅ | `steps/STEP-5.1-kill-duplicate-app-and-guard.md` | 2026-11-04 10:35:00 |
| 5.2 | Nâng cấp CPU-Aware Adaptive FPS trong ClientSession.cs | senior-developer | ✅ | `steps/STEP-5.2-cpu-aware-adaptive-fps.md` | 2026-11-04 10:43:00 |
| 5.3 | Tái sử dụng WriteableBitmap pool trong RemoteScreenViewModel.cs | senior-developer | ✅ | `steps/STEP-5.3-writeablebitmap-pool.md` | 2026-11-04 10:43:30 |
| 5.4 | Loại bỏ blocking XSync trong MouseInjector & KeyboardInjector | senior-developer | ✅ | `steps/STEP-5.4-remove-blocking-xsync.md` | 2026-11-04 10:44:00 |
| 5.5 | Khắc phục lỗ hổng hạ cấp TLS & Coalescing chuột trong RemoteControlClient.cs | senior-developer | ✅ | `steps/STEP-5.5-fix-tls-downgrade.md` | 2026-11-04 10:44:30 |
| 5.6 | Đo kiểm tổng thể: CPU máy đích, độ mượt chuột, FPS, 117+ tests pass | qa-engineer | ✅ | `steps/STEP-5.6-verify-and-signoff.md` | 2026-11-04 10:49:30 |

## Artifacts dự kiến (tổng)
- [x] `IPGS.RemoteControl.CcuUI/Resources/zcu-agent/linux-x64/IPGS.RemoteControl.ZcuAgent.dll` (cập nhật)
- [x] `/home/ccu/ipgs/remote-agent/` trên 192.168.21.56 (binary mới)
- [x] `/home/ccu/ipgs/remote-agent/appsettings.json` (TargetFps 20, JpegQuality 65, AdaptiveQuality true)
- [x] Log journalctl xác nhận `Using MIT-SHM for screen capture` và `tự động điều tiết theo tải CPU/mạng` thành công.
- [x] Toàn bộ 117 unit tests đạt 100% PASS.

## Blockers
Không có

## Quyết định / Ghi chú tổng
- Bản build trong `Resources/zcu-agent/linux-x64/` trước đó là từ ngày 02/11/2026, chưa có fix G020 ngày 03/11.
- Sửa triệt để bằng cách publish mã nguồn hiện tại, đồng bộ đè vào cả thư mục Resources dự án và thư mục bin output của CCU UI.
- Deploy trực tiếp qua SFTP lên `192.168.21.56` để máy đích nhận ngay bản vá mà không cần thao tác thêm trên GUI.

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|---|---|---|
| 2026-11-04 | Khởi tạo plan sửa lỗi XShm G020 và nâng cấp FPS cho 192.168.21.56 | task-planner |
