---
step: 2.2
plan: ../PLAN-MASTER.md
agent: Junior Developer
status: done
completed_at: 2026-10-31T09:12:30+07:00
deps: [2.1]
---

# STEP 2.2 — Mở rộng Unit Test Suite cho Wake-on-LAN Service

## Input nhận
Dự án kiểm thử tự động `IPGS.RemoteControl.Tests` đã có 41 tests (bao gồm Protocol, SecretProtector, ComputerProfileStore). Cần bổ sung kiểm thử cho `WakeOnLanService` để đảm bảo định dạng Magic Packet và xử lý lỗi địa chỉ MAC.

## Nhiệm vụ
Tạo file kiểm thử mới `WakeOnLanServiceTests.cs` kiểm thử toàn diện:
- Tạo Magic Packet hợp lệ với địa chỉ MAC chuẩn phân cách bằng dấu hai chấm `:`.
- Hỗ trợ phân cách dấu gạch ngang `-` và dấu cách hoặc viết liền.
- Phân biệt chữ hoa/chữ thường.
- Kiểm tra quăng ngoại lệ `ArgumentException` khi MAC rỗng, null, sai số lượng byte, hoặc chứa ký tự không phải hex.

## Definition of Done
- [x] Tạo `IPGS.RemoteControl.Tests/WakeOnLanServiceTests.cs` với tối thiểu 10 test cases.
- [x] Đạt 100% tỷ lệ pass cho toàn bộ các ca kiểm thử.
- [x] Nâng tổng số unit tests trong solution lên 55 tests.

## Đã làm
Đã tạo `WakeOnLanServiceTests.cs` chứa 14 ca kiểm thử chi tiết bao phủ cả trường hợp hợp lệ, trường hợp định dạng khác biệt và các trường hợp lỗi dữ liệu đầu vào. Toàn bộ 14 bài kiểm tra đều vượt qua thành công.

## Artifact
- `IPGS.RemoteControl.Tests/WakeOnLanServiceTests.cs`

## Quyết định quan trọng
Bảo vệ tầng logic mạng bằng kiểm thử tự động ngăn ngừa việc gửi các gói tin UDP rác lên mạng nội bộ khi người dùng nhập sai cấu hình MAC.

## Handoff Payload
- do_not_redo: Không cần viết thêm test cho MAC validation.
- watch_out: Không có.
- next_inputs: Chạy xác nhận QA test toàn bộ giải pháp.
