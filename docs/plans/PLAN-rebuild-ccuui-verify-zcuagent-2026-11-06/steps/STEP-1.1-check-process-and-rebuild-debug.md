---
step: 1.1
plan: ../PLAN-MASTER.md
agent: DEVOPS ENGINEER
status: done
completed_at: 2026-11-06 11:16
deps: []
---

# STEP 1.1 — Kiểm tra process CcuUI & Clean/Rebuild CcuUI Debug

## Input nhận
- CcuUI có thể đang chạy làm khóa file binary trong `bin\Debug\net8.0`.
- File nguồn `Resources\zcu-agent\linux-x64\IPGS.RemoteControl.ZcuAgent.dll` có dung lượng chuẩn 152.064 byte.

## Nhiệm vụ
1. Kiểm tra toàn bộ tiến trình hệ thống để đảm bảo không còn tiến trình CcuUI / IPGS.RemoteControl.CcuUI nào đang chạy khóa file.
2. Thực hiện clean/rebuild cấu hình `Debug` cho project `IPGS.RemoteControl.CcuUI`.
3. Kiểm tra file `bin\Debug\net8.0\Resources\zcu-agent\linux-x64\IPGS.RemoteControl.ZcuAgent.dll` đạt đúng 152.064 byte.

## Definition of Done
- [x] Không có tiến trình CcuUI nào đang chạy.
- [x] Build Debug thành công (0 errors).
- [x] File `bin\Debug\net8.0\Resources\zcu-agent\linux-x64\IPGS.RemoteControl.ZcuAgent.dll` có kích thước đúng 152.064 byte.

## Đã làm
- Đã quét toàn bộ tiến trình hệ thống, xác nhận không có `CcuUI` hay `dotnet` nào đang chạy, file binary `bin\Debug\net8.0\IPGS.RemoteControl.CcuUI.dll` mở đọc/ghi hoàn toàn tự do (không bị khóa).
- Chạy `dotnet build IPGS.RemoteControl.CcuUI -c Debug` thành công (0 errors, 0 warnings).
- Kiểm tra file `IPGS.RemoteControl.CcuUI\bin\Debug\net8.0\Resources\zcu-agent\linux-x64\IPGS.RemoteControl.ZcuAgent.dll`: kích thước đạt đúng 152.064 byte, SHA256 `5C923C4455723A82D3FD51767AB65DEF6F9E322BB29EF4FC73F7A0A10007260D` khớp 100% với file nguồn.

## Artifact
- `IPGS.RemoteControl.CcuUI/bin/Debug/net8.0/Resources/zcu-agent/linux-x64/IPGS.RemoteControl.ZcuAgent.dll` (152.064 bytes)

## Quyết định quan trọng
- Cơ chế `PreserveNewest` của MSBuild đã tự động chép đè bản mới 152.064 byte thay thế bản cũ 165.376 byte.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Không cần kiểm tra tiến trình hay build lại Debug, Debug đã hoàn tất 152.064 byte.
- watch_out: Tiếp tục build bản Release vì bản Release trước đó đang ở trạng thái rất cũ (133.632 byte).
- next_inputs: Tiến hành build Release và verify SHA256/kích thước.

## Commit
- Hash: N/A
- Đã push: N/A
