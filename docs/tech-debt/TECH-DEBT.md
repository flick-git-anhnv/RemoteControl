---
title: Sổ Tech-Debt — Remote Control Tool
created: 2026-07-26
updated: 2026-07-26
owner: Tech Lead
---

# TECH-DEBT — Remote Control Tool (CCU↔ZCU)

> Ghi nhận nợ kỹ thuật đã được Tech Lead quyết định HOÃN có chủ đích (không phải bỏ quên).
> Mỗi mục xử lý qua WF-REFACTOR riêng khi đến lượt.

## Danh sách đang mở

### TD-1 — Nâng `ShellQuote` thành `public` + helper sudo-stdin dùng chung

- **Nguồn:** Tech Lead review Bước 4.1, plan `PLAN-remote-control-audit-fix-2026-07-26` (2026-07-26)
- **Hiện trạng:** `IPGS.RemoteControl.CcuClient/ShellQuote.cs` là `internal`; logic pipe password sudo qua STDIN (`sudo -S -p ''` + `SudoPattern`) bị lặp ~45 dòng ở 2 window CcuUI (`RemoteCommandWindow.axaml.cs`, `BulkActionWindow.axaml.cs`) — đã có comment tham chiếu chéo.
- **Việc cần làm:** Nâng `ShellQuote` → `public`; tách helper sudo-stdin (build lệnh + feed password 1 dòng/1 sudo) vào CcuClient; 2 window CcuUI gọi helper chung.
- **Lý do hoãn:** Refactor đụng 3 file ngay trước push làm tăng rủi ro regression cho code vừa review PASS.
- **Effort ước tính:** ~2h (WF-REFACTOR nhỏ). **Ưu tiên:** P3.
- **Trạng thái:** ⬜ Mở

### TD-2 — `MessageCodec.EncodeFrameJpeg` alloc `new byte[24+jpeg.Length]` mỗi frame

- **Nguồn:** Q3 tồn đọng — Tech Lead review Bước 4.1 (2026-07-26)
- **Hiện trạng:** Phía capture/encode ZcuAgent đã buffer-reuse (fix Q3), nhưng `EncodeFrameJpeg` (CcuClient/Protocol/MessageCodec.cs) vẫn alloc 1 mảng mỗi frame (1080p/15fps ≈ 15 alloc/s cỡ frame).
- **Việc cần làm:** Đổi API codec + `WriteMessageAsync` sang ownership ArrayPool (rent/return xuyên async boundary) — thay đổi signature protocol codec, cần round-trip test đi kèm.
- **Lý do hoãn:** Vượt scope review; mức alloc hiện tại không phải hot-spot nghiêm trọng (đã có buffer reuse ở capture+encode).
- **Effort ước tính:** ~4h (đổi API + test). **Ưu tiên:** P3.
- **Trạng thái:** ⬜ Mở

## Ghi chú liên quan (không phải tech-debt, cần user quyết)

1. **`ZcuAgent/appsettings.json`** vẫn còn `"AllowedClientIPs": ["0.0.0.0/0"]` + token placeholder (hook `config-protection` chặn sửa). Code đã phòng thủ đủ (fail-fast token, deny-all khi rỗng, warning khi `0.0.0.0/0`). Chờ user quyết: sửa thành `[]`/CIDR nội bộ, hoặc chấp nhận vì là file mẫu (installer ghi đè khi deploy).
2. **Caveat `KIOSK_SUDO_PASS`:** CcuUI đã bỏ env này (S3); user chạy TAY script kiosk qua RemoteCommandWindow → `_sudo()` fallback sudo thường (có thể hỏi TTY). Luồng chính qua `KioskDeployService` không ảnh hưởng.
3. **Thiếu test:** cả 3 project không có unit/integration test — khuyến nghị bổ sung round-trip codec test + reconnect leak test trước các thay đổi protocol tiếp theo (đặc biệt TD-2).

## Lịch sử cập nhật

| Ngày | Cập nhật | Người |
|------|----------|-------|
| 2026-07-26 | Tạo file — ghi TD-1, TD-2 từ quyết định Tech Lead Bước 4.1 | Senior Developer (Bước 5.1) |
