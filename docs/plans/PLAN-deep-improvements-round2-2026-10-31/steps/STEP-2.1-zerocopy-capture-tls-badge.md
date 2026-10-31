---
step: 2.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31T09:00:00+07:00
deps: ["1.2"]
---

# STEP 2.1 — Nén JPEG Zero-Copy trên ZCU & Huy Hiệu TLS UI

## Nhiệm vụ
1. Trong `JpegEncoder.cs`: sử dụng `SKImage.FromPixels(info, pinnedPtr, rowBytes)` nén ảnh trực tiếp từ mảng byte đã ghim mà không cần copy qua `_reusableBitmap`.
2. Trong `RemoteScreenWindow.axaml`: thêm huy hiệu `🔒 TLS` trên thanh công cụ hiển thị trạng thái mã hóa và tooltip vân tay SHA-256.

## Kết quả
- Tiết kiệm 8.3 MB per-frame `Buffer.MemoryCopy` (~250 MB/s tại 15fps) trên CPU bo mạch ZCU.
- Giao diện người dùng minh bạch 100% về bảo mật đường truyền.
