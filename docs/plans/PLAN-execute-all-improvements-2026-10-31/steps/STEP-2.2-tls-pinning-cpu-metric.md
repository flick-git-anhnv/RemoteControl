---
step: 2.2
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31T08:46:00+07:00
deps: ["2.1"]
---

# STEP 2.2 — TLS Certificate Pinning & Chuẩn hóa CPU Health

## Input nhận
- Đề xuất 2.3 và 2.4 trong [PROPOSAL-remote-control-improvements-2026-10-31.md](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md).
- Vấn đề S3 và U1.

## Nhiệm vụ
1. Triển khai Certificate Pinning: lưu trữ và so khớp SHA-256 fingerprint chứng chỉ của ZcuAgent trong `RemoteControlClient.cs`.
2. Sửa công thức đo lường CPU trong `HealthMonitorWindow.axaml.cs` để tính `100 - idle%`.

## Definition of Done
- [x] TLS kết nối có cơ chế xác thực vân tay chứng chỉ.
- [x] CPU meter hiển thị tổng tải hệ thống chính xác.
- [x] Build thành công.

## Đã làm
1. Trong `IPGS.RemoteControl.CcuClient/ComputerProfile.cs`:
   - Bổ sung trường `TlsCertFingerprint { get; set; }` để lưu trữ SHA-256 fingerprint dạng hex của thiết bị ZCU.
2. Trong `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs`:
   - Cập nhật hàm `NegotiateStreamAsync`: cấu hình `RemoteCertificateValidationCallback` để trích xuất SHA-256 fingerprint từ chứng chỉ X.509 server.
   - So sánh fingerprint nhận được với `_profile.TlsCertFingerprint`. Nếu cấu hình có vân tay nhưng không khớp, hủy kết nối ngay lập tức với ngoại lệ bảo mật nhằm triệt tiêu tấn công MITM.
3. Trong `IPGS.RemoteControl.CcuUI/Views/HealthMonitorWindow.axaml.cs`:
   - Sửa đổi biểu thức chính quy phân tích dòng `%Cpu(s)` từ lệnh `top -b -n 1`.
   - Lấy trường `id` (idle percentage) và tính `totalCpu = Math.Max(0.0, Math.Min(100.0, 100.0 - idleVal))`.
   - Đảm bảo hiển thị tải CPU toàn hệ thống (bao gồm cả user, system, nice, iowait, softirq) thay vì chỉ đo riêng `us`.

## Artifact
- [ComputerProfile.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuClient/ComputerProfile.cs)
- [RemoteControlClient.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuClient/RemoteControlClient.cs)
- [HealthMonitorWindow.axaml.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuUI/Views/HealthMonitorWindow.axaml.cs)

## Quyết định quan trọng
- Certificate Pinning là lá chắn bảo mật chủ động quan trọng trong mạng LAN công nghiệp, đảm bảo CCU chỉ tin cậy đúng ZCU đã biết.
- `100 - idle%` phản ánh trung thực toàn bộ tải tính toán của chip nhúng ZCU (bao gồm xử lý phần cứng và driver).

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Không can thiệp lại công thức CPU và logic TLS Pinning.
- watch_out: Nếu ZCU thay chứng chỉ mới, admin cần cập nhật fingerprint tương ứng trong profile.
- next_inputs: Toàn bộ code Phase 1 & 2 đã hoàn thành, chuyển sang Phase 3 (QA verification).
