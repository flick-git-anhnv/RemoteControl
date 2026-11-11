# KẾ HOẠCH TRIỂN KHAI: BỘ ĐIỀU KHIỂN FPS TỰ THÍCH ỨNG THEO TẢI MÁY (ADAPTIVE FPS)

## Mục tiêu
Nâng cấp cơ chế điều khiển tốc độ khung hình (FPS) của ZcuAgent dựa trên tải thực tế của máy ZCU:
1. **X-Latency Probe (Độ trễ vòng X11):** Gửi `XSync` rỗng định kỳ (~500ms) trên một kết nối X riêng để đo độ trễ hàng đợi Xorg.
2. **CPU Pressure (PSI):** Đọc `/proc/pressure/cpu` (`some avg10`) để nhận diện tình trạng tác vụ phải chờ CPU.
3. **Network Backpressure:** Giữ nguyên tín hiệu ghi socket (`writeMs`).
4. **Thuật toán AIMD (Giảm nhanh, Tăng chậm) + Vùng chết:**
   - Sàn tối thiểu: **`MinFps = 15`** (cố định theo yêu cầu của user, không hạ dưới 15).
   - Trần tối đa: Theo `TargetFps` trong `appsettings.json` (mặc định 30).
   - Chất lượng JPEG: Cố định `q = 70` (không hạ chất lượng JPEG khi tải CPU/X11).
5. **Cấu hình Nice=10:** Nhường quyền ưu tiên CPU cho app `IPGSv4` và `Xorg`.

---

## Danh sách file đã thay đổi / tạo mới (LƯU VẾT ĐỂ ROLLBACK KHI CẦN)

| File | Trạng thái | Chi tiết thay đổi |
| :--- | :--- | :--- |
| `IPGS.RemoteControl.ZcuAgent/Net/AdaptiveFpsController.cs` | **TẠO MỚI** | Pure logic: AIMD, deadband, MinFps=15, MaxFps=TargetFps |
| `IPGS.RemoteControl.ZcuAgent/Net/MachineLoadSampler.cs` | **TẠO MỚI** | Background loop lấy mẫu XSync RTT + đọc PSI /proc/pressure/cpu |
| `IPGS.RemoteControl.Tests/AdaptiveFpsControllerTests.cs` | **TẠO MỚI** | 9 unit tests kiểm thử toàn diện các case (chạm sàn 15, AIMD, deadband, v.v.) |
| `IPGS.RemoteControl.Tests/IPGS.RemoteControl.Tests.csproj` | **SỬA** | Thêm 1 dòng compile link `AdaptiveFpsController.cs` |
| `IPGS.RemoteControl.ZcuAgent/AgentOptions.cs` | **SỬA** | Thêm thuộc tính `CpuAdaptive` (mặc định true) và `MinFps` (mặc định 15) |
| `IPGS.RemoteControl.ZcuAgent/Net/ClientSession.cs` | **SỬA** | Tích hợp Controller + Sampler vào stream loop và log Perf |
| `/home/ccu/.config/systemd/user/ipgs-remote-agent.service` (trên .56) | **CONFIG** | Thêm `Nice=10` vào `[Service]` |
| `/home/ccu/ipgs/remote-agent-backup-pre-adaptive/` (trên .56) | **BACKUP** | Bản backup nguyên vẹn của binary trước khi cập nhật |

---

## Tiến độ thực hiện

- [x] **Bước 1 — Bước 0 cấp OS:** Đã đặt `Nice=10` trong unit `ipgs-remote-agent.service` trên `.56`, tiến trình `IPGS.RemoteControl.ZcuAgent` đang chạy với `NI = 10`.
- [x] **Bước 2 — Core Logic:** Đã viết `AdaptiveFpsController.cs` và 9/9 Unit Tests trong `IPGS.RemoteControl.Tests` đạt 100% PASS.
- [x] **Bước 3 — Hardware Sampler:** Đã viết `MachineLoadSampler.cs` (kết nối X11 riêng đo `XSync` + đọc `/proc/pressure/cpu`).
- [x] **Bước 4 — Tích hợp ClientSession:** Đã hook Sampler và Controller vào vòng lặp stream của `ClientSession.cs`, cập nhật hiển thị `X-RTT` và `PSI` lên log Perf.
- [x] **Bước 5 — Biên dịch & Deploy:** Đã build Release linux-x64, backup phiên bản cũ và deploy lên `.56`. Đang chạy thực tế ổn định.
