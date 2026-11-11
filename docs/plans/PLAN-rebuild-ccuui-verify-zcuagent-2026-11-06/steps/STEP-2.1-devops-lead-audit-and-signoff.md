---
step: 2.1
plan: ../PLAN-MASTER.md
agent: DEVOPS LEAD
status: done
completed_at: 2026-11-06 11:18
deps: ["1.2"]
---

# STEP 2.1 — Thẩm định kết quả build và phê duyệt bàn giao

## Input nhận
- Báo cáo kết quả build từ DevOps Engineer cho cả hai bản Debug và Release.
- Hash SHA256 và kích thước các file ZcuAgent.dll trong `Resources`, `bin\Debug` và `bin\Release`.

## Nhiệm vụ
1. Đối soát kích thước binary `IPGS.RemoteControl.ZcuAgent.dll` đạt đúng 152.064 byte ở cả hai output Debug và Release.
2. Thẩm định không còn tồn tại file ZcuAgent 165.376 byte (có guard ngày 05/11) hoặc 133.632 byte (cũ ngày 04/11).
3. Cấp quyết định Approve và hướng dẫn người dùng khởi chạy.

## Definition of Done
- [x] Xác nhận nhị phân Debug và Release hoàn toàn đồng nhất 152.064 byte.
- [x] Báo cáo tổng kết rõ ràng và hướng dẫn người dùng.

## Đã làm
- Tiến hành audit đối chiếu checksum SHA256:
  + Nguồn (`Resources/.../IPGS.RemoteControl.ZcuAgent.dll`): `5C923C4455723A82D3FD51767AB65DEF6F9E322BB29EF4FC73F7A0A10007260D` (152.064 byte)
  + Output Debug (`bin/Debug/.../IPGS.RemoteControl.ZcuAgent.dll`): `5C923C4455723A82D3FD51767AB65DEF6F9E322BB29EF4FC73F7A0A10007260D` (152.064 byte)
  + Output Release (`bin/Release/.../IPGS.RemoteControl.ZcuAgent.dll`): `5C923C4455723A82D3FD51767AB65DEF6F9E322BB29EF4FC73F7A0A10007260D` (152.064 byte)
- Bản có guard (165.376 byte) và bản cũ (133.632 byte) đã bị triệt tiêu hoàn toàn khỏi môi trường chạy.
- Phê duyệt SIGN-OFF bàn giao bản build cho người dùng khởi chạy.

## Artifact
- Bộ nhị phân chuẩn tại `IPGS.RemoteControl.CcuUI\bin\Debug\net8.0\` và `IPGS.RemoteControl.CcuUI\bin\Release\net8.0\`

## Quyết định quan trọng
- Bản build hợp lệ, sẵn sàng để user chạy thử nghiệm hoặc deploy agent lên ZCU.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Đã hoàn tất toàn bộ quy trình build và verify.
- watch_out: Khi khởi động app, người dùng có thể chạy trực tiếp từ IDE hoặc chạy lệnh `dotnet run --project IPGS.RemoteControl.CcuUI`.
- next_inputs: Thông báo tới người dùng kết quả chi tiết.

## Commit
- Hash: N/A
- Đã push: N/A
