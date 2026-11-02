---
step: 2.2
plan: ../PLAN-MASTER.md
agent: junior-developer
status: done
completed_at: 2026-10-31T09:01:00+07:00
deps: ["2.1"]
---

# STEP 2.2 — Mở Rộng Unit Test Suite Cho Secret & Profile

## Nhiệm vụ
1. Viết `SecretProtectorTests.cs` (7 test cases): kiểm tra mã hóa/giải mã DPAPI `enc:v1:`, tính bất biến (idempotent), migration plaintext cũ, fallback an toàn và xử lý chuỗi hỏng.
2. Viết `ComputerProfileStoreTests.cs` (6 test cases): kiểm tra ghi đĩa nguyên tử, cập nhật profile, kiểm tra tính bền vững của `TlsCertFingerprint`, cơ chế TOFU và xóa profile.

## Kết quả
- Nâng tổng số test cases từ 28 lên **41 unit tests**.
- Phát hiện và vá ngay lỗ hổng thiếu `TlsCertFingerprint` trong `CloneForStorage`.
- Toàn bộ 41 bài test đỗ 100%.
