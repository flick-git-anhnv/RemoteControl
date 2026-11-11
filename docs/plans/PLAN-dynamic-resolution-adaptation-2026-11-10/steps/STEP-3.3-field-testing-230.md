# STEP 3.3 — Deploy thử nghiệm lên máy ZCU thực địa 192.168.21.230 và nghiệm thu

**Agent:** QA Engineer (L5)
**Trạng thái:** ✅ Hoàn thành
**Thời gian:** 2026-11-10 15:13

---

## 1. Quy trình kiểm thử thực địa trên `192.168.21.230` (`KztekUbuntu`)
1. **Mô phỏng trạng thái rút màn hình bị rơi về fallback 1024×768:**
   - Dừng service `ipgs-remote-agent`, đặt `xrandr --output HDMI-1 --mode 1024x768`.
   - Kiểm tra `xrandr --current` xác nhận: `Screen 0: current 1024 x 768`, `HDMI-1 connected primary 1024x768+0+0 ... 0mm x 0mm`.
2. **Triển khai bản cập nhật `IPGS.RemoteControl.ZcuAgent.dll` và khởi động lại service:**
   - Ngay khi khởi động, log `journalctl --user -u ipgs-remote-agent` ghi nhận:
     - `[ResolutionAdapter] Phát hiện trạng thái không cắm màn hình (headless 0mm x 0mm, current=1024x768) trên HDMI-1 — tự động nâng lên 1920x1080_60.00.`
     - `[ResolutionAdapter] Đã tự động chuyển HDMI-1 sang chế độ Headless Full HD (1920x1080_60.00).`
   - Kiểm tra `xrandr --current` sau khi khởi động: `Screen 0: current 1920 x 1080`, `1920x1080_60.00 59.96*`.

## 2. Kết luận nghiệm thu QA
- **Trạng thái:** PASS 100% (169/169 Unit Tests + Live Hardware Verification trên `192.168.21.230`).
