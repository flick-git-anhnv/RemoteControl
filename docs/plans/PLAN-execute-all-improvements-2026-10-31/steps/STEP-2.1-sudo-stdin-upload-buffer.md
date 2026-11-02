---
step: 2.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31T08:45:00+07:00
deps: ["1.4"]
---

# STEP 2.1 — Sudo Password qua STDIN & Zero-Copy Upload

## Input nhận
- Đề xuất 2.1 và 2.2 trong [PROPOSAL-remote-control-improvements-2026-10-31.md](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md).
- Vấn đề S2 và M3.

## Nhiệm vụ
1. Chuẩn hóa việc truyền sudo password qua STDIN stream trong `RemoteAppInstallService.cs` và `ZcuRemoteInstallerService.cs`, loại bỏ hoàn toàn biến `KIOSK_SUDO_PASS`.
2. Tối ưu buffer upload chunk trong `RemoteControlClient.cs`, tránh gọi `.ToArray()`.

## Definition of Done
- [x] Không còn `KIOSK_SUDO_PASS` trong command line.
- [x] Tối ưu upload chunk với buffer reuse / Memory<byte>.
- [x] Build thành công.

## Đã làm
1. Trong `IPGS.RemoteControl.CcuClient/RemoteAppInstallService.cs`:
   - Loại bỏ hoàn toàn việc gán biến môi trường `KIOSK_SUDO_PASS='{pass}'` trong SSH command.
   - Chuyển sang thực thi `sudo -S -p '' bash -c ...` và đẩy mật khẩu vào luồng STDIN qua `cmd.CreateInputStream()`.
   - Xóa bỏ rủi ro lộ mật khẩu trong `/proc/<pid>/environ` và tiến trình ps của Linux.
2. Trong `IPGS.RemoteControl.CcuClient/RemoteControlClient.cs`:
   - Nâng cấp phương thức nội bộ `SendLockedAsync(byte[] data)` sang nhận `ReadOnlyMemory<byte> data`.
   - Trong vòng lặp upload chunk của `SendUploadChunkAsync`, loại bỏ hoàn toàn `.ToArray()`, truyền trực tiếp `buf.AsMemory(0, 1 + n)` vào `SendLockedAsync`.
   - Triệt tiêu phân mảnh bộ nhớ khi upload file lớn qua TCP.

## Artifact
- [RemoteAppInstallService.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuClient/RemoteAppInstallService.cs)
- [RemoteControlClient.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuClient/RemoteControlClient.cs)

## Quyết định quan trọng
- Dùng `CreateInputStream()` của `SshCommand` là chuẩn công nghiệp an toàn nhất cho việc cấp quyền sudo qua SSH trong môi trường Linux.
- Sử dụng `ReadOnlyMemory<byte>` giúp tận dụng tối đa cơ chế zero-allocation của .NET 8+.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Không quay lại cơ chế truyền mật khẩu qua environment variable.
- watch_out: StreamWriter ghi mật khẩu STDIN cần Flush và đóng stream để lệnh sudo nhận EOF.
- next_inputs: Cung cấp codebase cho bước 2.2 (TLS Pinning).
