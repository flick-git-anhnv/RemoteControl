# GOTCHAS.md — Ràng buộc ngầm & Lỗi đã gặp

> **Mục đích:** Ghi lại các lỗi "ngầm" — không có trong docs chính thức, nhưng thực tế đã gặp và mất thời gian debug. Học từ pattern `PLUGIN_SCHEMA_NOTES.md` của affaan-m/ecc.
>
> **Quy tắc:** Agent fix xong 1 lỗi ngầm (không có trong GEMINI.md hay README) PHẢI thêm 1 entry vào file này trước khi đánh dấu task hoàn thành.
>
> **Đọc file này khi:** bắt đầu session mới, hoặc gặp lỗi lạ chưa rõ nguyên nhân — tra ở đây trước khi debug từ đầu.

---

## Mục lục nhanh

| # | Vấn đề | Ngày |
|---|--------|------|
| G001 | `scripts/md_to_docx_kztek.py` — thiếu `python-docx`/`Pillow`; PDF không cần trên cloud/sandbox | 2026-07-12 |
| G002 | Tràn và phình RAM (200MB lên >600MB) trên Linux ARM64 do ép DOTNET_GCgen0size=64MB và glibc malloc arena fragmentation | 2026-10-28 |
| G003 | Thứ tự nạp LoadingViewModel làm mất Thang máy/Thang cuốn và IsVisible=false khiến Billboard 3D dạt về góc (0,0) | 2026-10-28 |

---

## G001 — `scripts/md_to_docx_kztek.py`: thiếu `python-docx`/`Pillow`; PDF là optional trên cloud/sandbox

**Ngày phát hiện:** 2026-07-12

**Môi trường:** Linux sandbox (claude.ai / cloud agent)

**Vấn đề ban đầu:**
Chạy `python scripts/md_to_docx_kztek.py <file.md>` báo `ModuleNotFoundError: No module named 'docx'` vì thiếu package `python-docx` và `Pillow`.

**Khắc phục (ĐÃ XÁC NHẬN HOẠT ĐỘNG):**
```bash
pip install python-docx Pillow
```
Sau khi cài, DOCX tạo thành công. Đây là fix dứt điểm cho lỗi ModuleNotFoundError.

**Về PDF export trên cloud/sandbox:**
LibreOffice đã cài tại `/usr/bin/soffice`, nhưng `soffice --headless --convert-to pdf` báo lỗi "source file could not be loaded" trong môi trường sandbox — đây là hiện tượng đã biết, KHÔNG cần debug thêm.

Theo chỉ đạo: **trên cloud/sandbox, PDF không cần thiết**. Dùng `--no-pdf` làm mặc định:
```bash
python scripts/md_to_docx_kztek.py <file.md> --no-pdf
```

PDF chỉ cần khi chạy trên máy local có LibreOffice GUI đầy đủ — không phải môi trường sandbox.

**Không cần làm lại:**
- Không cần điều tra tại sao soffice lỗi trên sandbox — không blocking, không cần fix
- Không cần thử `pip install docx2pdf` — phụ thuộc vào Word/LibreOffice GUI, không hoạt động trên Linux sandbox
- DOCX là artifact chính; PDF là optional và chỉ cần ở môi trường local

**Lần đầu gặp:** Bước 1.1-1.2 — WF-REFACTOR optimize-framework (2026-07-12)

---


---

## G002 — Tràn và phình RAM (200MB lên >600MB) trên Linux ARM64 do ép DOTNET_GCgen0size=64MB và glibc malloc arena fragmentation

**Ngày phát hiện:** 2026-10-28

**Môi trường:** Linux ARM64 Rockchip RV1126, .NET 8 / Avalonia 11, SkiaSharp

**Vấn đề ban đầu:**
App mới khởi động chiếm ~200MB RAM, nhưng sau vài thao tác chuyển màn, bật camera, mở dialog thì VmRSS phình liên tục lên 565MB - 608MB và không bao giờ giảm xuống.

**Nguyên nhân gốc rễ:**
1. Cấu hình trong `/etc/systemd/system/ilocker.service` đặt `DOTNET_GCgen0size=0x4000000` (ép Gen 0 = 64MB) và `DOTNET_GCLOHThreshold=0x400000` (4MB). Khiến đối tượng tạm không được GC dọn kịp ở Gen 0, bị dồn sang Gen 1 và Gen 2. Vì bo mạch có 2GB RAM nên Gen 2 hầu như không bao giờ kích hoạt thu gom toàn phần (Managed Heap tăng từ 10MB lên 165MB).
2. Linux `glibc malloc` khi giải phóng bộ nhớ unmanaged của SkiaSharp (`libSkiaSharp.so`), FreeType, Wayland DRM buffers mặc định giữ lại các trang nhớ trong thread arena free-lists mà KHÔNG trả về cho Linux kernel nếu không gọi `malloc_trim(0)`. Dẫn đến `VmData` tăng lên 757MB và `RssAnon` chiếm hơn 400MB.

**Khắc phục (ĐÃ XÁC NHẬN HOẠT ĐỘNG):**
1. Loại bỏ `DOTNET_GCgen0size` và `DOTNET_GCLOHThreshold` khỏi service file. Bổ sung `DOTNET_GCConserveMemory=5` để runtime .NET chủ động trả uncommitted memory cho OS.
2. Thêm P/Invoke `[DllImport("libc", EntryPoint = "malloc_trim")] private static extern int MallocTrim(nuint pad);` trong `AppMemoryLogger.TrimProcessMemory()`.
3. Kích hoạt `TrimProcessMemory()` tự động (ThreadPool) khi chuyển màn hình, khi Kiosk vào Standby hoặc khi đóng Dialog:
   - Xả cache Skia: `SkiaSharp.SKGraphics.PurgeResourceCache();`
   - Nén LOH và thu gom Gen 2: `GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);`
   - Gọi `MallocTrim(0);` để ép glibc giải phóng unmanaged heap về kernel.
4. Kết quả: VmRSS giảm và duy trì ổn định tuyệt đối ở mức **~215MB - 232MB** sau mọi thao tác giao diện và camera.

**Lần đầu gặp:** WF-BUGFIX / Khắc phục phình RAM trên Linux Rockchip RV1126 (2026-10-28)

<!-- Thêm entry mới theo format:

## G00N — [Tên vấn đề ngắn gọn]

**Ngày phát hiện:** YYYY-MM-DD
**Môi trường:** [OS / platform / version]
**Vấn đề:** [Mô tả triệu chứng cụ thể]
**Nguyên nhân:** [Root cause đã xác định]
**Cách xử lý:** [Giải pháp, workaround, hoặc cách tránh]
**Lần đầu gặp:** [Context task / session]
**Không cần làm lại:** [Những gì đã thử mà KHÔNG hoạt động — để tránh lặp lại]

-->


---

## G003 — Thứ tự nạp LoadingViewModel làm mất Thang máy/Thang cuốn và IsVisible=false khiến Billboard 3D dạt về góc (0,0)

**Ngày phát hiện:** 2026-10-28

**Môi trường:** .NET 8, Avalonia 11/12, Skia backend (iPGS CCU v4 Desktop)

**Vấn đề:**
1. Khi tắt app mở lại, toàn bộ thang máy và thang cuốn biến mất khỏi bản đồ MapStatusView.
2. Đôi khi thang cuốn/thang máy hoặc đối tượng 3D bị vẽ dạt hết lên góc trên cùng bên trái (0, 0).

**Nguyên nhân:**
1. Trong LoadingViewModel.cs, LoadElevatorMapDetail (bước 39) và LoadEscalatorMapDetail (bước 40) được nạp TRƯỚC LoadElevator (bước 43) và LoadEscalator (bước 44). Khi GetElevatorMaps() lọc theo StaticPool.elevators.GetObjectById(d.Id), tập dữ liệu elevators rỗng dẫn đến lọc ra 0 bản ghi.
2. Trong MapStatusView.axaml.cs, khi chuyển chế độ 3D, code cũ gán .IsVisible = !has3D;. Khi IsVisible = false, Avalonia ngắt control khỏi Visual Tree layout; dẫn tới TransformToVisual(layer) trả về Matrix.Identity (gốc toạ độ 0, 0), khiến các toạ độ 3D bị chiếu sai lệch dạt về góc trên cùng bên trái màn hình.

**Cách xử lý:**
1. Di chuyển LoadElevator và LoadEscalator lên trước các hàm nạp MapDetail trong LoadingViewModel.cs.
2. Thêm self-healing guard trong PgsLayoutRepository.GetElevatorMaps() / GetEscalatorMaps() và fallback tên mặc định trong MapStatusViewModel.cs / MapDetailViewModel.cs.
3. Giữ .IsVisible = true và chỉ dùng .Opacity = has3D ? 0.0 : 0.88; để control 2D ẩn về mặt quang học nhưng vẫn được Avalonia tính toán ma trận layout chuẩn xác; đồng thời thêm điều kiện 	v.Value == Matrix.Identity để schedule render lại ở DispatcherPriority.Loaded thay vì vẽ tại (0, 0).

**Lần đầu gặp:** WF-BUGFIX / Sửa lỗi hiển thị bản đồ 2D/3D (2026-10-28)
