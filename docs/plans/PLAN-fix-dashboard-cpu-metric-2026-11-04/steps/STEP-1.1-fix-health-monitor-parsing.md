---
step: 1.1
task: fix-dashboard-cpu-metric
agent: senior-developer
status: done
deps: []
created: 2026-11-04
completed_at: 2026-11-04 13:49:00
---

# STEP 1.1 — Sửa Regex & Parser trong HealthMonitorWindow.axaml.cs

## Đã thực hiện
1. Tạo lớp `TopParser.cs` trong `IPGS.RemoteControl.CcuClient`:
   - Định nghĩa `DefaultTopCommand = "LC_ALL=C top -b -n 2 -d 0.5"`.
   - Phân tích lấy mẫu cuối cùng (mẫu 2) bằng `LastOrDefault(l => l.StartsWith("%Cpu"))` và `Array.FindLastIndex(...)`.
   - Xử lý regex `([\d.,]+)\s*id`, hỗ trợ cả số thập phân dấu chấm `.` và dấu phẩy `,` của locale tiếng Việt (`vi_VN`).
   - Chuẩn hoá `.Replace(',', '.')` trước khi parse số thực với `CultureInfo.InvariantCulture`.
   - Chuẩn hoá các trường Cpu/Mem của từng tiến trình về dạng dấu chấm.
2. Cập nhật `HealthMonitorWindow.axaml.cs`:
   - Dùng lệnh `TopParser.DefaultTopCommand` qua SSH.
   - Gọi `TopParser.Parse(topStr)` và cập nhật lên UI (`PART_CpuText.Text = $"{parsedTop.TotalCpuPercent.Value:F1}%"`).

## Artifacts
- `IPGS.RemoteControl.CcuClient/TopParser.cs`
- `IPGS.RemoteControl.CcuUI/Views/HealthMonitorWindow.axaml.cs`
