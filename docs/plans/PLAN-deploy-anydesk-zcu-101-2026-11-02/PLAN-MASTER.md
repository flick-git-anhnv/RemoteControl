---
task: deploy-anydesk-zcu-101
created: 2026-11-02
updated: 2026-11-02
status: completed
workflow: WF-DEVOPS
priority: P1
---

# PLAN MASTER: Cài đặt AnyDesk & IPGS ZCU Remote Control Agent lên Thiết bị 192.168.21.101

> File này CHỈ chứa tổng quan + trạng thái. Chi tiết từng bước nằm ở `steps/STEP-[N.M]-[tên].md` tương ứng.

## Mô tả
Thực hiện kết nối SSH tới thiết bị ZCU `192.168.21.101` (tài khoản `zcu`/`123456`), cài đặt và cấu hình phần mềm **AnyDesk** (kèm mật khẩu Unattended Access `Kztek@123456` và lấy AnyDesk ID), đồng thời triển khai bản build mới nhất của **IPGS ZCU Remote Control Agent** (Port `17600`, Token `123456`, systemd service, firewall) và kiểm thử kết nối thực tế để bàn giao thông số cho người dùng kiểm tra.

## Nguồn yêu cầu
- Yêu cầu gốc: "cài anydesk trên thiết bị hco tôi (192.168.21.101) cho tôi xong cung cấp các thử để tôi ktra kết nối"
- Scope đã chốt: Cài AnyDesk (Step 3.1 ZCU Agent đã đánh dấu Skipped theo chỉ định tiếp theo của User), SSH `zcu`/`123456`, Unattended Password `Kztek@123456`.
- Workflow: WF-DEVOPS — CI/CD / Deployment
- Agent chain: DevOps Engineer (DOE) → DevOps Lead (DOL)

## Phases & Steps

### Phase 1: Thăm dò Môi trường Thiết bị 192.168.21.101
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 1.1 | Kết nối SSH (`zcu`/`123456`), kiểm tra OS, kiến trúc CPU, Display X11, mạng và trạng thái các gói phụ thuộc | devops-engineer | ✅ | `steps/STEP-1.1-probe-target-101.md` | 2026-11-02 14:42 |

### Phase 2: Cài đặt & Cấu hình AnyDesk
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 2.1 | Cài đặt AnyDesk cho OS đích, cấu hình dịch vụ chạy nền, đặt mật khẩu Unattended Access (`Kztek@123456`) và trích xuất AnyDesk ID | devops-engineer | ✅ | `steps/STEP-2.1-install-configure-anydesk.md` | 2026-11-02 14:48 |

### Phase 3: Triển khai IPGS ZCU Remote Control Agent (Bỏ qua theo yêu cầu User)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 3.1 | [Bỏ qua — User chỉ định chỉ cài AnyDesk] Triển khai ZCU Agent | devops-engineer | ⏭️ | `steps/STEP-3.1-deploy-zcu-agent-101.md` | 2026-11-02 14:41 |

### Phase 4: Kiểm thử Kết nối AnyDesk & Nghiệm thu (Sign-off)
| # | Bước | Agent | Status | Step file | Hoàn thành lúc |
|---|------|-------|--------|-----------|-----------------|
| 4.1 | Kiểm tra trạng thái dịch vụ AnyDesk (Online + ID + Unattended Access) và tổng hợp bảng thông số kiểm tra kết nối cho User | devops-lead | ✅ | `steps/STEP-4.1-verify-and-signoff.md` | 2026-11-02 14:51 |

## Artifacts dự kiến (tổng)
- [x] AnyDesk cài đặt và cấu hình Unattended Access trên `192.168.21.101` (ID: `1457821647`, Version: `8.1.0`, Status: `online`)
- [x] Bảng tổng hợp thông số kiểm tra kết nối (AnyDesk ID & Password + Direct LAN IP/Port + SSH/SFTP)

## Blockers
Không có

## Quyết định / Ghi chú tổng
- Host đích: `192.168.21.101`, SSH user: `zcu`, password: `123456`, port SSH: `22`.
- Mật khẩu Unattended Access của AnyDesk: `Kztek@123456`.
- Đã tắt X11 ScreenSaver & DPMS (`xset s off -dpms s noblank`) trên `DISPLAY=:0` để chống màn hình đen khi kết nối từ xa.

## Lịch sử cập nhật
| Ngày | Cập nhật | Agent |
|------|----------|-------|
| 2026-11-02 | Tạo plan triển khai AnyDesk & ZcuAgent lên 192.168.21.101 | task-planner |
| 2026-11-02 | Hoàn thành Step 1.1: Thăm dò môi trường thiết bị 192.168.21.101 (Ubuntu 22.04.5 amd64, LXQt X11 :0) | devops-engineer |
| 2026-11-02 | Hoàn thành Step 2.1: Cài đặt AnyDesk 8.1.0, kích hoạt service, đặt Unattended Password và lấy ID `1457821647` | devops-engineer |
| 2026-11-02 | Hoàn thành Step 4.1: Nghiệm thu độc lập (Two-Eyes), kiểm thử kết nối LAN TCP 7070, tắt X11 DPMS/ScreenSaver và ký duyệt Sign-off | devops-lead |

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
