---
step: 3.1
task: fix-dashboard-cpu-metric
agent: qa-engineer
status: done
deps: [2.1]
created: 2026-11-04
completed_at: 2026-11-04 13:51:00
---

# STEP 3.1 — QA Kiểm Thử & Nghiệm Thu Thực Địa

## 1. Kết quả Unit Tests Toàn Bộ Hệ Thống
- Lệnh chạy: `dotnet test IPGS.RemoteControl.sln`
- Kết quả: **Passed! - Failed: 0, Passed: 140, Skipped: 0, Total: 140, Duration: 1 s**.

## 2. Đo kiểm thực tế trực tiếp với thiết bị 192.168.21.56
- Lệnh chạy qua SSH: `LC_ALL=C top -b -n 2 -d 0.5`
- Dữ liệu thu được từ mẫu thứ 2:
  `%Cpu(s): 0.9 us, 0.9 sy, 0.0 ni, 98.1 id, 0.0 wa, 0.0 hi, 0.0 si, 0.0 st`
- Kết quả tính toán của TopParser:
  - **Total CPU:** **1.9%** (thay vì hiển thị sai 93% - 99% như bản cũ).
  - **CPU Idle:** **98.1%**.
  - **Top processes:** `top` chỉ chiếm 5.7% (thay vì 11%), các tiến trình khác ở trạng thái ngủ.
- Kết luận: Số liệu tải CPU hiển thị cực kỳ chính xác, trung thực, phản ánh đúng hiện trạng máy đích. Đạt 100% tiêu chí nghiệm thu.
