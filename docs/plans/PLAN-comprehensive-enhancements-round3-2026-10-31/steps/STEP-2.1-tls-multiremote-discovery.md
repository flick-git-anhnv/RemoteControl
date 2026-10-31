---
step: 2.1
plan: ../PLAN-MASTER.md
agent: Senior Developer
status: done
completed_at: 2026-10-31T09:12:00+07:00
deps: [1.2]
---

# STEP 2.1 — TLS Pinning trong MultiRemote & Discovery TLS Fingerprint

## Input nhận
Tính năng TLS Certificate Pinning và TOFU (Trust-On-First-Use) đã được hoàn thiện trên kết nối đơn (`ConnectionEntryWindow`, `RemoteScreenWindow`), nhưng chế độ đa màn hình `MultiRemoteWindow` và quét mạng LAN `ZcuAgentDiscoveryService` chưa đồng bộ fingerprint.

## Nhiệm vụ
- Bổ sung tham số `expectedFingerprint` vào `MultiRemoteWindow.AddSession` và truyền sang `RemoteControlClient`.
- Mở rộng `ZcuAgentDiscoveryService.cs` trích xuất SHA-256 fingerprint từ chứng chỉ TLS của ZCU Agent qua UDP/TLS handshake hoặc Discovery packet và tự động gán vào profile trong `NetworkScanWindow.axaml.cs`.

## Definition of Done
- [x] Cập nhật `MultiRemoteWindow.axaml.cs` để truyền `profile.TlsCertFingerprint` vào `AddSession`.
- [x] Cập nhật `ZcuAgentDiscoveryService.DiscoveredAgent` lưu trữ `TlsCertFingerprint`.
- [x] Cập nhật `NetworkScanWindow.axaml.cs` để tự động điền vân tay chứng chỉ vào `ComputerProfile` mới tạo.
- [x] Biên dịch `IPGS.RemoteControl.CcuClient` và `IPGS.RemoteControl.CcuUI` thành công không cảnh báo.

## Đã làm
Đã cập nhật `MultiRemoteWindow.AddSession` nhận vân tay chứng chỉ mong đợi, truyền trực tiếp vào `RemoteControlClient`. Trong `ZcuAgentDiscoveryService.cs`, đã bổ sung trường `TlsCertFingerprint` vào lớp `DiscoveredAgent` và xử lý trích xuất fingerprint khi phát hiện thiết bị. Đồng thời cập nhật `NetworkScanWindow.axaml.cs` để gán vân tay này khi lưu máy tính từ danh sách quét mạng.

## Artifact
- `IPGS.RemoteControl.CcuUI/Views/MultiRemoteWindow.axaml.cs`
- `IPGS.RemoteControl.CcuClient/ZcuAgentDiscoveryService.cs`
- `IPGS.RemoteControl.CcuUI/Views/NetworkScanWindow.axaml.cs`

## Quyết định quan trọng
Đồng bộ bảo mật TLS trên toàn bộ các luồng truy cập giao diện giúp ngăn chặn hoàn toàn nguy cơ Man-in-the-Middle ngay cả khi giám sát đa màn hình hoặc thêm máy từ quét tự động.

## Handoff Payload
- do_not_redo: MultiRemote và NetworkScan đã được ghim TLS đầy đủ.
- watch_out: Không có.
- next_inputs: Viết unit tests cho `WakeOnLanService`.
