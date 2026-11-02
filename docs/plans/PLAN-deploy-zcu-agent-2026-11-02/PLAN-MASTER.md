---
task: deploy-zcu-agent
created: 2026-11-02
updated: 2026-11-02
status: completed
workflow: WF-DEVOPS
priority: P1
---

# PLAN MASTER: Triển khai Cài đặt ZcuAgent Mới Nhất lên Máy đích ZCU 192.168.21.230

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md` tương ứng.

## Mô tả
Biên dịch bản publish mới nhất của `IPGS.RemoteControl.ZcuAgent` (đầy đủ các tối ưu Zero-Allocation frame streaming, X11DisplayResolver, DPMS anti-blanking, socket tuning) và thực hiện kết nối SSH/SFTP triển khai, cấu hình systemd user service, mở port firewall 17600/tcp lên máy đích ZCU Linux `192.168.21.230` (tài khoản `kztek`/`123456`).

## Nguồn yêu cầu
- Yêu cầu gốc: "192.168.21.230 kztek/123456 hãy remote và cài zcu client mới nhất cho tôi"
- Workflow: WF-DEVOPS — CI/CD / Deployment
- Agent chain: DevOps Engineer (DOE) → DevOps Lead (DOL)

## Phases & Steps

### Phase 1: Biên dịch ZcuAgent Bản Mới Nhất
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Biên dịch Release linux-x64 cho IPGS.RemoteControl.ZcuAgent | devops-engineer | ✅ | `steps/STEP-1.1-publish-zcu-agent.md` | 2026-11-02 08:43:12 |

### Phase 2: Thăm dò Môi trường Máy đích ZCU
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Kết nối SSH thăm dò OS, X11, Display, .NET runtime và Token cũ | devops-engineer | ✅ | `steps/STEP-2.1-probe-zcu-target.md` | 2026-11-02 08:42:43 |

### Phase 3: Triển khai, Cấu hình Dịch vụ & Tường lửa
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | Upload binary, cấu hình appsettings, systemd service & firewall | devops-engineer | ✅ | `steps/STEP-3.1-deploy-configure-service.md` | 2026-11-02 08:43:54 |

### Phase 4: Xác minh Hoạt động & Ký duyệt Triển khai
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 4.1 | Kiểm tra service active, port 17600 listen, kết nối và sign-off | devops-lead | ✅ | `steps/STEP-4.1-verify-and-report.md` | 2026-11-02 08:44:43 |

## Artifacts dự kiến (tổng)
- [x] `bin/publish/zcu-agent-linux-x64/` (Binary build hoàn tất)
- [x] `/home/kztek/ipgs/remote-agent/` (Thư mục cài đặt trên ZCU)
- [x] `/home/kztek/.config/systemd/user/ipgs-remote-agent.service` (Systemd service)
- [x] Báo cáo thông số kết nối (IP: 192.168.21.230, Port: 17600, Token: 123456)

## Blockers
Không có

## Quyết định / Ghi chú tổng
- Host đích: `192.168.21.230`, SSH user: `kztek`, password: `123456`, port SSH: `22`.
- Bảo toàn Token xác thực `123456` và Port `17600` đã cấu hình trên ZCU.
- Cài đặt dưới dạng systemd user service với `loginctl enable-linger kztek` để service tự động khởi động và chạy ngầm độc lập với phiên SSH.
- Đã kiểm chứng luồng streaming trực tiếp từ client: Nhận frame JPEG 768x1024 hoàn hảo.

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-11-02 | Tạo plan triển khai ZcuAgent lên 192.168.21.230 | task-planner |
| 2026-11-02 | Hoàn thành toàn bộ các bước triển khai và kiểm thử nghiệm thu 100% | devops-lead |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
