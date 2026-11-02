---
task: TASK-industrial-polish-round4-2026-10-31
created: 2026-10-31
updated: 2026-10-31
status: done
workflow: WF-REFACTOR
priority: P1
---

# PLAN MASTER: Thực Thi Cải Tiến Đẳng Cấp Công Nghiệp Vòng 4 (Industrial-Grade Telemetry & Socket Tuning)

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md`.

## Mô tả
Tiếp tục chu trình lặp vô hạn theo tôn chỉ: "Cải tiến tới khi hết tất cả tính năng". Vòng 4 tập trung vào nâng tầm trải nghiệm và tối ưu hóa vi kiến trúc mạng đẳng cấp công nghiệp (RustDesk / AnyDesk standard):
1. **F-ROUND4-1:** Real-Time Network Performance Telemetry (FPS, RTT ms, Bitrate MB/s) & Live HUD Badge trên Toolbar CCU.
2. **F-ROUND4-2:** TCP Socket High-Throughput Tuning (`NoDelay = true`, `SendBufferSize = 256KB`, `ReceiveBufferSize = 256KB`) triệt tiêu độ trễ Nagle 40ms trên ZCU Agent.
3. **F-ROUND4-3:** Smooth Auto-Reconnect Countdown & Resilience UX trong `RemoteScreenViewModel`.
4. **F-ROUND4-4:** Mở rộng Unit Test Suite với `ComputerStatusCheckerTests` và `ZcuAgentDiscoveryServiceTests` (đạt 65/65 tests passed 100%).

## Nguồn yêu cầu
- Yêu cầu người dùng: "tiep tc" — tiếp tục tối ưu hóa tới khi không còn điểm nào có thể cải tiến thêm
- Workflow: WF-REFACTOR
- Agent chain: Senior Developer → Junior Developer → QA Engineer → Documentation Writer → Tech Lead

## Phases & Steps

### Phase 1: Đo Kiểm Mạng Thời Gian Thực & Socket Tuning (P1)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 1.1 | Telemetry HUD (RTT, FPS, Bitrate) trong `RemoteControlClient` & `RemoteScreenViewModel` | Senior Developer | ✅ | `steps/STEP-1.1-telemetry-hud-rtt-fps.md` | 2026-10-31T09:22:00+07:00 |
| 1.2 | TCP Socket Tuning (`NoDelay`, Buffer 256KB) trên ZCU `TcpServer` & CCU `RemoteControlClient` | Senior Developer | ✅ | `steps/STEP-1.2-tcp-socket-tuning.md` | 2026-10-31T09:23:00+07:00 |

### Phase 2: Nâng cấp Giao diện & Mở rộng Test Suite (P2)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 2.1 | Tích hợp HUD Badge & Auto-Reconnect UX trên `RemoteScreenWindow.axaml` | Senior Developer | ✅ | `steps/STEP-2.1-ui-hud-badge-reconnect.md` | 2026-10-31T09:23:30+07:00 |
| 2.2 | Mở rộng Unit Test Suite (`ComputerStatusChecker` & `DiscoveryService`) | Junior Developer | ✅ | `steps/STEP-2.2-unit-tests-status-discovery.md` | 2026-10-31T09:24:15+07:00 |

### Phase 3: Kiểm thử Toàn diện & Đồng bộ Tài liệu
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 3.1 | Test Suite Run & Solution Clean Build Verification | QA Engineer | ✅ | `steps/STEP-3.1-qa-test-run.md` | 2026-10-31T09:24:30+07:00 |
| 3.2 | Cập nhật Code Graph v3.3 & Gotchas G014 | Documentation Writer | ✅ | `steps/STEP-3.2-sync-code-graph-docs.md` | 2026-10-31T09:25:00+07:00 |

### Phase 4: Re-Review Vòng 5 & Nghiệm Thu Chung Cuộc Đỉnh Cao
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|:------:|-----------|-----------------|
| 4.1 | Re-Review Phản biện Vòng 5 & Phê duyệt Nghiệm thu | Tech Lead | ✅ | `steps/STEP-4.1-tech-lead-re-review-round5.md` | 2026-10-31T09:25:30+07:00 |

## Artifacts đạt được
- Mã nguồn cập nhật tại `RemoteControlClient.cs`, `IRemoteControlClient.cs`, `TcpServer.cs`, `RemoteScreenViewModel.cs`, `RemoteScreenWindow.axaml`.
- Thêm file test `ComputerStatusCheckerTests.cs` và `ZcuAgentDiscoveryServiceTests.cs` (tổng 65/65 tests passed 100%).
- Báo cáo Re-Review Vòng 5 `RE-REVIEW-verification-round5-2026-10-31.md` (+ DOCX, PDF).
- Cập nhật `code-graph/CODE-GRAPH.md` (+ DOCX, PDF) lên v3.3.
- Thêm entry G014 vào `.gemini/shared/GOTCHAS.md`.
