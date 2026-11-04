---
step: 5.5
name: fix-tls-downgrade
phase: 5
status: done
agent: senior-developer
created: 2026-11-04
updated: 2026-11-04
completed_at: 2026-11-04 10:44:30
---

# BƯỚC 5.5: Khắc Phục Lỗ Hổng Hạ Cấp TLS & Coalescing Hàng Đợi Chuột Trong RemoteControlClient.cs

## 1. Mục tiêu
- Ngăn chặn triệt để lỗ hổng tự động hạ cấp TLS về TCP plaintext khi có chứng chỉ ghim (`ExpectedCertFingerprint`) hoặc lỗi xác thực TLS.
- Triệt tiêu hiện tượng nghẽn hàng đợi TCP do di chuột dày đặc (mouse flooding): áp dụng Latest-Wins Coalescing cho `SendMouseMoveAsync`.

## 2. Kết quả thực tế
- Đã bổ sung rào chắn bảo mật trong `NegotiateStreamAsync`: nếu `ExpectedCertFingerprint` được thiết lập, mọi lỗi bắt tay TLS hoặc sai lệch vân tay chứng chỉ sẽ lập tức ném ngoại lệ `AuthenticationException`, tuyệt đối không bao giờ âm thầm hạ cấp về TCP plaintext.
- Đã triển khai Latest-Wins Coalescing với packed atomic 64-bit toạ độ `_pendingMouseMove`:
  - Khi người dùng di chuột 60 Hz, các toạ độ trung gian bị nghẽn trên socket sẽ được ghi đè tự động.
  - Luồng drain chỉ gửi toạ độ mới nhất, ngăn ngừa nghẽn hàng đợi và đảm bảo lệnh click chuột hoặc gõ phím không bị trễ.
