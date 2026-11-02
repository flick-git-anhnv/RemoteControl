---
step: 2.1
plan: ../PLAN-MASTER.md
agent: devops-engineer
status: done
completed_at: 2026-11-02 08:42:43
deps: [1.1]
---

# STEP 2.1 — Kết nối SSH Thăm dò Môi trường Máy đích ZCU 192.168.21.230

## Input nhận
- Thông tin SSH: IP `192.168.21.230`, User `kztek`, Password `123456`, Port `22`.
- Bản binary đã publish từ Step 1.1.

## Nhiệm vụ
Kết nối SSH tới máy đích ZCU để thăm dò thông số hệ điều hành, kiến trúc CPU, phiên bản .NET runtime, trạng thái Display X11, các thư viện đồ họa cần thiết (`libx11-6`, `libxext6`, `libxtst6`, `xclip`), và kiểm tra cấu hình agent cũ để lấy lại Token nếu có.

## Definition of Done
- [x] Kết nối SSH thành công tới `192.168.21.230`.
- [x] Xác định rõ hệ điều hành: Ubuntu 22.04.5 LTS, kiến trúc CPU: `x86_64` (Linux kernel 6.8.0-138-generic).
- [x] Kiểm tra xem .NET 8 runtime đã có trên máy đích: Đã cài tại `/home/kztek/.dotnet`.
- [x] Đọc cấu hình cũ (`appsettings.json`): Token là `"123456"`, Port `17600`.
- [x] Kiểm tra X11 Display: Display `:0` đang hoạt động với socket `/tmp/.X11-unix/X0`.
- [x] Kiểm tra thư viện phụ thuộc: `libx11-6`, `libxext6`, `libxtst6`, `xclip` đều đã được cài đặt đầy đủ.

## Đã làm
- Phát triển và chạy script `scratch/probe_zcu.py` thông qua thư viện Paramiko.
- Thăm dò toàn diện thông số hệ điều hành, cấu hình dịch vụ cũ, và môi trường thực thi.
- Phát hiện dịch vụ cũ `ipgs-remote-agent.service` đang chạy từ ngày 19/10/2026 với PID 1304644.

## Artifact
- `scratch/probe_zcu.py`
- Kết quả thăm dò môi trường ZCU 192.168.21.230

## Quyết định quan trọng
- Bảo toàn Token `"123456"` và Port `17600` để các kết nối từ CCU Client không bị gián đoạn hoặc sai lệch cấu hình.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Đã thăm dò xong môi trường và xác nhận đầy đủ dependencies, không cần probe lại.
- watch_out: Dịch vụ đang chạy dưới quyền user `kztek` với systemd user service (`--user`).
- next_inputs: Cấu hình Token `"123456"`, Port `17600`, thư mục đích `/home/kztek/ipgs/remote-agent`.

## Commit
- Hash: b952e20
- Đã push: Không
