---
step: 1.2
plan: ../PLAN-MASTER.md
agent: DEVOPS ENGINEER
status: done
completed_at: 2026-11-06 11:17
deps: ["1.1"]
---

# STEP 1.2 — Rebuild CcuUI Release & Kiểm tra kích thước ZcuAgent.dll

## Input nhận
- Handoff từ STEP 1.1: Debug đã hoàn tất chuẩn 152.064 byte.
- Cần cập nhật bản Release từ 133.632 byte lên 152.064 byte.

## Nhiệm vụ
1. Thực hiện clean/rebuild cấu hình `Release` cho project `IPGS.RemoteControl.CcuUI`.
2. Kiểm tra file `bin\Release\net8.0\Resources\zcu-agent\linux-x64\IPGS.RemoteControl.ZcuAgent.dll` đạt đúng 152.064 byte.
3. Đối soát cả 2 môi trường Debug và Release.

## Definition of Done
- [x] Build Release thành công (0 errors).
- [x] File `bin\Release\net8.0\Resources\zcu-agent\linux-x64\IPGS.RemoteControl.ZcuAgent.dll` có kích thước đúng 152.064 byte.

## Đã làm
- Chạy `dotnet build IPGS.RemoteControl.CcuUI -c Release` thành công (0 errors, 5 warnings về obsolete / nullable).
- Kiểm tra file `IPGS.RemoteControl.CcuUI\bin\Release\net8.0\Resources\zcu-agent\linux-x64\IPGS.RemoteControl.ZcuAgent.dll`: kích thước đạt đúng 152.064 byte, SHA256 `5C923C4455723A82D3FD51767AB65DEF6F9E322BB29EF4FC73F7A0A10007260D` khớp 100% với file nguồn và file Debug.

## Artifact
- `IPGS.RemoteControl.CcuUI/bin/Release/net8.0/Resources/zcu-agent/linux-x64/IPGS.RemoteControl.ZcuAgent.dll` (152.064 bytes)

## Quyết định quan trọng
- Cả Debug và Release hiện tại đã đồng nhất hoàn toàn về binary ZcuAgent.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Không cần build lại Release hay Debug.
- watch_out: Chuyển giao sang DevOps Lead để đối soát và phê duyệt nghiệm thu bàn giao.
- next_inputs: Báo cáo kết quả hash và kích thước nhị phân.

## Commit
- Hash: N/A
- Đã push: N/A
