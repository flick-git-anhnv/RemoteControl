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
| G003 | Rò rỉ DRM DMABUFs và treo đơ VPU khi mở/đóng camera liên tục trên Rockchip RV1126 | 2026-10-28 |
| G004 | Triệt tiêu viền lưới tam giác (seam artifact) và sai lệch sắc độ khi dựng hình 3D glTF trong Avalonia Skia | 2026-10-28 |
| G005 | Treo đơ TCP/SFTP và lỗi WinError 10054 khi deploy file lớn từ Windows Wi-Fi sang board RV1126 do PMTUD Black Hole | 2026-10-29 |
| G006 | Publish Linux ARM64 cho Kiosk nhúng (RV1126B) thiếu .NET runtime hệ thống bắt buộc dùng `--self-contained true` | 2026-10-31 |
| G007 | Avalonia DRM/KMS direct rendering trên RV1126B: `/dev/fb0` là dummy buffer màu đen, chụp màn hình thật bắt buộc dùng ffmpeg `kmsgrab` | 2026-10-31 |
| G008 | NumericUpDown bị xén bo góc (anti-aliasing corner clipping) trong FluentTheme Avalonia do inner `#PART_TextBox` có viền & nền đục | 2026-10-31 |
| G009 | Triệt tiêu rác LOH 124MB/s và giật lag GC Gen 2 bằng Ping-Pong Double-Buffering zero-copy cùng malloc_trim(0) | 2026-10-31 |
| G010 | X11 XSync blocking round-trip trên motion events làm trễ chuột và nghẽn message queue; XFlush là đủ | 2026-10-31 |
| G011 | Avalonia WriteableBitmap dispose trên background thread khi đổi resolution gây crash Skia pipeline — bắt buộc hoãn dispose sang Dispatcher.UIThread | 2026-10-31 |
| G012 | Ad-hoc Header với Grid.ColumnSpan="3" trên Logo đè cột nút Back và cuộn mất Header khi lồng trong ScrollViewer | 2026-10-31 |
| G013 | Vòng lặp ghi video AVI/MJPEG cấp phát chuỗi ASCII và mảng byte heap liên tục gây giật gián đoạn — triệt tiêu bằng UTF-8 literals `u8` và `stackalloc` | 2026-10-31 |
| G014 | Socket TCP Nagle algorithm và buffer nhỏ trên Linux ZCU gây trễ 40ms và nghẽn burst frame JPEG — triệt tiêu bằng NoDelay = true và SendBufferSize = 256KB | 2026-10-31 |
| G015 | Tràn bộ đệm socket và crash bộ nhớ heap do sao chép clipboard văn bản quá lớn — triệt tiêu bằng chốt chặn MaxClipboardTextLength = 1MB | 2026-10-31 |
| G016 | Tối ưu hóa Remote Headless không màn hình vật lý và phiên RDP ảo xrdp bằng X11DisplayResolver và XSetScreenSaver anti-blanking | 2026-10-31 |
| G017 | Rác LOH (1.5–3MB/s) trên ZCU do EncodeFrameJpeg cấp phát mảng byte mỗi khung hình — triệt tiêu bằng Zero-Allocation Frame Streaming và Congestion Dropping | 2026-10-31 |
| G018 | Avalonia ListBox DoubleTapped event: `sender.DataContext` là null/window chứ không phải item model — bắt buộc trích xuất qua `e.Source` / `ListBoxItem` / `SelectedItem` | 2026-11-02 |
| G019 | Lỗi màn hình ảo `eDP-1` (`connected`) trên máy tính công nghiệp YANLING IBOX-205 (Intel Haswell-ULT i915) gây mất màn hình chính | 2026-11-03 |
| G020 | Lỗi lệch byte ABI 64-bit (`LP64`) trong `XShmSegmentInfo` làm `XShmAttach` luôn bị Xorg từ chối (`BadAccess`), gây sụt FPS | 2026-11-03 |
| G021 | Kẹt khoá dconf Kiosk (Super/Activities/Hot Corner không mở được sau khi bỏ tick) & GNOME Shell Extension không nhận diện | 2026-11-03 |
| G022 | Lỗi vỡ font / hỏi chấm (???) do PowerShell stdin heredoc giải mã chuỗi UTF-8 theo ANSI CP1252 | 2026-11-03 |
| G023 | Kiosk Deploy 2 tab bị đồng nhất do ResolveKioskScriptsDir ưu tiên Resources cũ, bash helper bị kẹt trong scope và Just Perfection lỗi 'primary is null' trên GNOME 42 | 2026-11-03 |
| G024 | Lỗi status 150 'No frameworks were found' khi deploy ZcuAgent framework-dependent do `libhostfxr.so` nằm trong app dir làm muxer bỏ qua `DOTNET_ROOT` | 2026-11-04 |
| G025 | Ngắt kết nối định kỳ 30s do thiếu làm mới heartbeat trên luồng nhận frame JPEG và thiếu cooperative yield giải phóng write lock | 2026-11-04 |
| G026 | Nghẽn CPU 100% trên Atom/Celeron và giật lag do Adaptive FPS chỉ nhìn writeMs, XSync chuột và rác WriteableBitmap 157 MB/s | 2026-11-04 |
| G027 | Bắt tay TLS tự ký thất bại (alert certificate unknown) do SslClientAuthenticationOptions không kế thừa callback constructor | 2026-11-04 |
| G028 | "Cài remote" xoá agent-tls.pfx làm đổi vân tay, CCU từ chối TLS do vân tay ghim cũ gây màn hình đen ngắt kết nối | 2026-11-04 |
| G029 | Lỗi regex Dashboard `([\d\.]+)` bỏ qua dấu phẩy thập phân locale `vi_VN` trong lệnh `top` và spike mẫu 1 khiến tính khống CPU lên 93%–99% | 2026-11-04 |

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

---

## G003 — Rò rỉ DRM DMABUFs và treo đơ VPU khi mở/đóng camera liên tục trên Rockchip RV1126

**Ngày phát hiện:** 2026-10-28

**Môi trường:** Linux ARM64 Rockchip RV1126, `librockchip_mpp.so`, VPU/RKVDEC, Avalonia 11

**Vấn đề:**
Khi mở và đóng camera nhiều lần (hoặc người dùng vào/ra liên tục giữa màn hình nhận diện khuôn mặt và màn hình chính), ứng dụng bị treo đơ hoàn toàn. RAM DRM phình thêm ~67MB sau mỗi lần mở/đóng, số lượng file descriptor `/dmabuf:` tăng từ 40 lên hơn 100+.

**Nguyên nhân gốc rễ:**
1. **Thiếu `mpi->reset(ctx)` trước khi `mpp_destroy(ctx)`:** Trong `MppCameraSource.Stop()`, khi ngắt luồng RTSP, decoder phần cứng vẫn đang giữ các khung hình dở dang trong hàng đợi của worker thread `try_proc_dec_task` (`ref_count 3`). Gọi `mpp_buffer_group_put()` khi `ref_count > 0` sẽ bị driver từ chối deallocate (`deinit with X bytes not released`), làm mồ côi toàn bộ các buffer DMABUF trong kernel DRM memory. Sau nhiều lần, kernel cạn kiệt bộ nhớ CMA (Contiguous Memory Allocator) dẫn đến ioctl VPU bị block vĩnh viễn.
2. **Xung đột vòng đời bất đồng bộ (`Task.Run` không đồng bộ tĩnh):** Trong `FaceIdentityViewModel`, việc đóng camera được đẩy ra `Task.Run` nền dùng instance lock (`new()`). Khi người dùng mở lại camera ngay lập tức, phiên mới khởi động `mpp_create`/`mpp_init` chạy song song với lệnh `mpp_destroy` của phiên cũ trên cùng thiết bị phần cứng `/dev/mpp_service`, gây tranh chấp driver và deadlock luồng.
3. **Kích thước buffer trong `HandleInfoChange`:** RV1126 VPU yêu cầu kích thước buffer `4177920` bytes cho stream 1080p (do `mpp_frame_get_buf_size` trả về). Nếu tự ý tính lại theo kích thước nhỏ hơn (`3133440`), MPP sẽ báo lỗi `mpp_buffer_create required size 4177920 reach group size limit 3133440`, toàn bộ gói tin bị lỗi `-1012` (`MPP_ERR_BUFFER_FULL`) và crash `mpp_mem_pool`.

**Khắc phục (ĐÃ XÁC NHẬN HOẠT ĐỘNG):**
1. Chuẩn hóa chuỗi teardown MPP:
   - Bước 1: Gọi `mpi->reset(ctx)` để hủy toàn bộ khung hình/gói tin dở dang, reset pipeline VPU, hạ buffer `ref_count` về 0.
   - Bước 2: Gọi `mpp_destroy(ctx)` để dừng an toàn worker thread `try_proc_dec_task`.
   - Bước 3: Gọi `mpp_buffer_group_put(group)` sau khi context đã destroy hoàn tất để giải phóng sạch toàn bộ DMABUF descriptors.
2. Đồng bộ hóa tĩnh toàn hệ thống:
   - Dùng `static readonly object s_cameraLifecycleLock` trong `FaceIdentityViewModel` và `s_mppLifecycleGate` trong `MppCameraSource`.
   - Đảm bảo phiên mới luôn đợi phiên cũ giải phóng xong phần cứng mới bắt đầu; nếu phiên mới đã bị hủy (`_isDisposed == true`) thì thoát ngay.
3. Giữ đúng kích thước `bufSize = (UIntPtr)mpp_frame_get_buf_size(frame)` và cấp 16 buffers cho buffer group.
4. Giới hạn `MALLOC_ARENA_MAX=2` qua `mallopt` và systemd service file để triệt tiêu phình unmanaged heap glibc.
5. Kết quả: VmRSS ổn định ở mức **226MB - 239MB**, DMABUFs giữ nguyên **42 descriptors**, mở/đóng camera liên tục hoàn toàn mượt mà, không còn hiện tượng treo đơ.

**Lần đầu gặp:** WF-BUGFIX / Khắc phục rò rỉ DMABUF & treo đơ camera RV1126 (2026-10-28)

---

## G004 — Triệt tiêu viền lưới tam giác (seam artifact) và sai lệch sắc độ khi dựng hình 3D glTF trong Avalonia Skia

**Ngày phát hiện:** 2026-10-28
**Môi trường:** Avalonia UI 11.x, Skia Backend, Windows / Linux
**Vấn đề:** 
1. Khi vẽ các tam giác liền kề của mô hình 3D glTF (`car.glb`) lên `DrawingContext`, xuất hiện các đường sọc lưới tam giác (wireframe seams) màu trắng/xám mờ giữa các tam giác trên nắp capo, nóc xe và cánh cửa.
2. Các mặt phẳng (hood, roof, doors) bị hiện tượng vát đa giác (faceted/origami look) với các sắc độ xám lốm đốm khác nhau thay vì là một mặt phẳng đồng màu, bóng bẩy.
3. Khi xoay con `Canvas` con kích thước dài L, rộng W một góc 90 độ bên trong `Canvas` cha, hình con bị trượt lệch tâm `(L - W)/2`.

**Nguyên nhân:** 
1. **Skia Antialiasing Seam:** Khi Skia vẽ 2 tam giác kề nhau có cùng cạnh chung, cơ chế antialiasing chỉ phủ nửa độ mờ (sub-pixel coverage) ở rìa cạnh của mỗi tam giác. Do đó nền phía dưới bị lọt qua khe giữa 2 tam giác tạo thành đường viền mờ (seams).
2. **glTF Vertex Normal Variations:** File glTF dùng `KHR_mesh_quantization` hoặc vertex normals có góc lệch nhẹ giữa các đỉnh liền kề của cùng một mặt phẳng. Khi tính diffuse lighting và specular dot product cho từng tam giác, mỗi tam giác nhận một brush/màu khác nhau.
3. **Canvas Transform Origin:** `Canvas` không tự động căn giữa control con. Khi con kích thước `L x W` đặt ở `(0, 0)` và xoay 90 độ quanh tâm `(0.5, 0.5)`, tâm của nó không trùng với tâm của vùng chứa `W x L`.

**Cách xử lý:** 
1. **Bù viền Stroke cùng màu:** Khi gọi `ctx.DrawGeometry(curBrush, pen, curGeom)`, truyền `var pen = new Pen(curBrush, 0.6)`. Nét vẽ mảnh 0.6px cùng màu sẽ hàn gắn hoàn hảo khe hở antialiasing giữa các tam giác, triệt tiêu 100% sọc lưới.
2. **Canonical Normal Snapping:** Quantize pháp tuyến của các tam giác về các hướng trực giao chuẩn (Top: $Z > 0.35 \to (0,0,1)$, Bottom: $Z < -0.35$, Sides: $X \gtrless 0.35$, Front/Rear: $Y \gtrless 0.35$). Nhờ đó toàn bộ các tam giác trên cùng một mặt phẳng đều nhận cùng 1 brush và Skia gom lại thành 1 polygon đồng nhất.
3. **Căn tâm hình học khi xoay:** Đặt tọa độ con tại `Canvas.SetLeft(cv, (W - L) / 2.0)` và `Canvas.SetTop(cv, (L - W) / 2.0)` trước khi gán `RenderTransform = RotateTransform(90)`, đảm bảo tâm xoay trùng khít tâm vùng chứa.

**Lần đầu gặp:** WF-FEATURE / Tinh chỉnh tích hợp bộ mô hình 3D glTF & giao diện bản đồ 2D/3D iPGS v4 (2026-10-28)

---

## G005 — Treo đơ TCP/SFTP và lỗi WinError 10054 khi deploy file lớn từ Windows Wi-Fi sang board RV1126 do PMTUD Black Hole

**Ngày phát hiện:** 2026-10-29
**Môi trường:** Host Windows 11 (Wi-Fi, 192.168.21.74), Target board Rockchip RV1126 Linux Ubuntu (Ethernet eth0, 192.168.21.106)
**Vấn đề:** 
1. Khi chạy script deploy (`deploy_fast_tar.py`, `deploy_pipe.py`, SFTP put, hoặc curl qua HTTP thông thường) để chuyển file lớn (>30KB đến 36MB) từ Windows sang board, kết nối luôn bị ngắt sau đúng 32KB hoặc sau ~20s với lỗi:
   `Socket exception: An existing connection was forcibly closed by the remote host (10054)` / `ConnectionResetError: [WinError 10054]`.
2. Trong khi đó, các lệnh SSH ngắn, query nhỏ, ping ngắn đều hoạt động bình thường 100%.

**Nguyên nhân:** 
1. **PMTUD (Path MTU Discovery) Black Hole:** Windows Wi-Fi và board Ethernet đều để MTU mặc định 1500. Tuy nhiên router/AP Wi-Fi văn phòng chỉ cho phép gói tin qua cầu nối Wi-Fi ↔ Ethernet với kích thước tối đa 1466 bytes (ping với payload >1438 byte có cờ Don't Fragment DF đều bị drop và báo *Destination host unreachable*).
2. **ICMP bị chặn bởi Windows Firewall:** Khi router drop gói tin TCP 1500 byte, nó gửi ICMP Fragmentation Needed về Windows. Nhưng Windows Wi-Fi profile đang ở chế độ `Public`, firewall âm thầm drop gói ICMP này. Windows không hề biết gói tin bị quá khổ nên liên tục gửi lại gói 1500 byte cho đến khi TCP timeout hoặc reset kết nối.

**Cách xử lý:** 
1. **Hạ MTU trên board eth0 xuống 1400:** Chạy `ip link set dev eth0 mtu 1400`. Khi bắt tay TCP SYN/SYN-ACK, Linux board chủ động quảng bá TCP MSS = 1360. Windows sẽ tự động đóng gói TCP payload tối đa 1360 byte (tổng gói IP = 1400 byte), hoàn toàn nằm dưới ngưỡng 1466 byte của AP Wi-Fi.
2. **Deploy bằng HTTP server nội bộ (port 8090):** Sử dụng `scripts/deploy_http_fast.py` (chạy `ThreadingHTTPServer` phục vụ file `dist/update.tar.gz` trên port 8090 đã thông suốt), board chạy `curl` tải về với tốc độ đường truyền LAN tối đa (~9 MB/s), gói 36MB tải xong trong 3.8 giây và tự động giải nén vào `/opt/ilocker/`.

**Lần đầu gặp:** WF-FEATURE / Deploy tính năng Cấu hình phân vùng lưu trữ & Thống kê sức chứa lên board RV1126 (2026-10-29)

---

## G006 — Publish Linux ARM64 cho Kiosk nhúng (RV1126B) thiếu .NET runtime hệ thống bắt buộc dùng `--self-contained true`

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Rockchip RV1126B Linux Ubuntu Minimal (Kernel 4.19, ARM64), .NET 8 / Avalonia 11
**Vấn đề:** 
Khi chạy `dotnet publish -c Release -r linux-arm64 --self-contained false` và deploy lên board, systemd service `ilocker-fb.service` crash liên tục với `status=150/n/a` và log:
`You must install or update .NET to run this application. Framework: 'Microsoft.NETCore.App', version '8.0.0' ... No frameworks were found.`

**Nguyên nhân:** 
Board RV1126B là hệ thống nhúng rút gọn (Minimal rootfs) không có gói runtime .NET 8 cài đặt sẵn tại `/usr/share/dotnet` hoặc path toàn cục. Khi publish với `--self-contained false`, file `ILocker.App.runtimeconfig.json` chỉ chứa `"framework": {"name": "Microsoft.NETCore.App", "version": "8.0.0"}` và thiếu danh sách `"includedFrameworks"`, khiến corehost từ chối khởi chạy binary cục bộ.

**Cách xử lý:** 
Luôn luôn publish ứng dụng Kiosk với tham số `--self-contained true`:
```bash
dotnet publish src/ilocker-avalonia/ILocker.App/ILocker.App.csproj -c Release -r linux-arm64 --self-contained true -o dist/publish-arm64
```
Khi đó `ILocker.App.runtimeconfig.json` sinh ra `"includedFrameworks": [{"name": "Microsoft.NETCore.App"}]` và corehost trỏ thẳng tới các assembly trong cùng thư mục `/opt/ilocker/`.

**Lần đầu gặp:** WF-REFACTOR / Kiểm tra Full UI 3 vòng & Chuẩn hóa giao diện Kiosk RV1126B (2026-10-31)

---

## G007 — Avalonia DRM/KMS direct rendering trên RV1126B: `/dev/fb0` là dummy buffer màu đen, chụp màn hình thật bắt buộc dùng ffmpeg `kmsgrab`

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Rockchip RV1126B Linux Ubuntu, DRM/KMS `/dev/dri/card0`, service `ilocker-fb.service`
**Vấn đề:** 
Khi dùng lệnh đọc framebuffer truyền thống `cat /dev/fb0 > screen.raw` hoặc `fbgrab` để chụp ảnh màn hình đang chạy trên board, ảnh thu được là một màu đen hoàn toàn (toàn byte `0x00`), mặc dù màn hình Kiosk thật vẫn đang hiển thị bình thường.

**Nguyên nhân:** 
Ứng dụng Avalonia Linux chạy trực tiếp thông qua backend Linux DRM/KMS (Direct Rendering Manager / Kernel Mode Setting) kết xuất thẳng vào DRM plane (`/dev/dri/card0`) thông qua EGL/GBM hoặc dumb buffer. Linux kernel Rockchip DRM driver không đồng bộ nội dung DRM plane ngược lại con trỏ legacy framebuffer `/dev/fb0`.

**Cách xử lý:** 
Dùng `ffmpeg` với input format `kmsgrab` để chụp trực tiếp từ DRM card device:
```bash
ffmpeg -y -device /dev/dri/card0 -f kmsgrab -i - -frames:v 1 -vf "hwdownload,format=bgr0" /tmp/board_screen.png
```
Lệnh này chụp chuẩn xác 100% từng pixel hiện hành trên màn hình kiosk 800×1280.

**Lần đầu gặp:** WF-REFACTOR / Kiểm tra Full UI 3 vòng & Chuẩn hóa giao diện Kiosk RV1126B (2026-10-31)

---

## G008 — NumericUpDown bị xén bo góc (anti-aliasing corner clipping) trong FluentTheme Avalonia do inner `#PART_TextBox` có viền & nền đục

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Avalonia UI 11.2, FluentTheme, Linux DRM & Windows Desktop
**Vấn đề:** 
Các control `NumericUpDown` và bộ stepper `Border.kz-stepper` khi đặt góc bo tròn `CornerRadius="8"` đến `CornerRadius="12"` thường bị hiện tượng 4 góc bị xén răng cưa hoặc mất hẳn nét viền ngoài (border stroke), nhìn như góc vuông cắt cụt viền cong.

**Nguyên nhân:** 
1. Trong FluentTheme, `NumericUpDown` bọc một `TextBox#PART_TextBox` và `ButtonSpinner#PART_Spinner`. Mặc định template con của `TextBox` có một `Border` bên trong với `Background` và `BorderThickness="1"`. Khi render góc nhọn của inner TextBox, nó vẽ đè lên stroke cong của outer border.
2. Nét viền nhạt `#CBD5E1` (Slate-200 / 300) kết hợp với màn hình Kiosk độ phân giải thấp/mật độ điểm ảnh thường (non-HiDPI) khiến anti-aliasing làm mờ viền góc cong dưới 0.5 opacity.
3. Thuộc tính `ClipToBounds="False"` trên `Border.kz-stepper` khiến các nút `RepeatButton` bên trong tràn nét vẽ ra ngoài viền.

**Cách xử lý:** 
1. Trong `CommonStyles.axaml`, cấu hình style cho `NumericUpDown`:
   - Đặt `ClipToBounds="True"`, `BorderBrush="#94A3B8"` (Slate-400), `BorderThickness="1.5"`.
   - Ghi đè template con `#PART_TextBox`: `BorderThickness="0"`, `Background="Transparent"`, `Padding="4,0"`.
   - Ghi đè `#PART_Spinner`: `BorderThickness="0"`, `Background="Transparent"`.
2. Trong `App.axaml`, cấu hình `Border.kz-stepper`:
   - Đặt `ClipToBounds="True"`, `BorderThickness="1.5"`, `BorderBrush="#94A3B8"`.
   - Gán `BorderBrush="Transparent"` và `BorderThickness="0"` cho `ContentPresenter` con của dec/inc buttons để không bao giờ vẽ đè lên góc bo.
3. Kết quả: Viền stepper và Numeric sắc nét hoàn hảo tại mọi góc cong, không còn hiện tượng xén cạnh trên cả màn hình desktop lẫn thiết bị ARM64.

**Lần đầu gặp:** WF-REFACTOR / Kiểm tra Full UI 3 vòng & Chuẩn hóa giao diện Kiosk RV1126B (2026-10-31)

---

## G009 — Triệt tiêu rác LOH 124MB/s và giật lag GC Gen 2 bằng Ping-Pong Double-Buffering zero-copy cùng malloc_trim(0)

**Ngày phát hiện:** 2026-10-31
**Môi trường:** .NET 8 / Avalonia 11.2 - 12 / Linux & Windows / SkiaSharp

**Vấn đề ban đầu:**
Khi nhận luồng màn hình remote độ phân giải Full HD (1920×1080) ở tốc độ 15 FPS, ứng dụng CCU ngốn RAM và bị khựng đơ giao diện định kỳ 2–3 giây một lần. Phân tích bộ nhớ cho thấy có tới 124.4 MB/s rác heap sinh ra liên tục. Bên phía ZCU Agent (Linux), bộ nhớ unmanaged của tiến trình phình dần từ 50MB lên hàng trăm MB mặc dù đã gọi `Dispose()` cho các `SKBitmap`.

**Nguyên nhân gốc rễ:**
1. Trong .NET CLR, mọi mảng byte có kích thước ≥ 85,000 bytes tự động được cấp phát thẳng vào Large Object Heap (LOH). Frame 1080p BGRA chiếm `1920 × 1080 × 4 = 8,294,400 bytes` (~8.3 MB). Việc tạo mảng trung gian `byte[] pixData = new byte[...]` và khởi tạo `new WriteableBitmap(...)` ở mỗi khung hình khiến LOH bị nạp 124.4 MB rác mỗi giây. LOH chỉ được dọn bởi Garbage Collection Gen 2 (Full GC) — tiến trình "Stop-the-world" này khóa đứng toàn bộ các luồng ứng dụng và UI trong vài chục đến hàng trăm mili-giây gây giật cục nghiêm trọng.
2. Phía Linux ZcuAgent: SkiaSharp cấp phát unmanaged bitmap buffer thông qua `malloc` của thư viện glibc. Khi giải phóng `SKBitmap`, glibc giữ lại các trang nhớ trong thread arenas free-list để tối ưu cho các lần cấp phát sau của cùng thread, dẫn đến VmRSS và VmData hiển thị trên hệ điều hành không bao giờ giảm, gây hiểu nhầm hoặc cạn kiệt RAM trên thiết bị nhúng có RAM hạn chế.

**Khắc phục (ĐÃ XÁC NHẬN HOẠT ĐỘNG):**
1. **Ping-Pong Double-Buffering phía UI:**
   - Khởi tạo 2 đối tượng `WriteableBitmap` (`_frontBuffer` và `_backBuffer`) dùng cố định xuyên suốt vòng đời session.
   - Khóa và lấy con trỏ unmanaged framebuffer: `using var fb = _backBuffer.Lock(); unsafe { byte* dst = (byte*)fb.Address; ... }`.
   - Sử dụng `Buffer.MemoryCopy` trực tiếp từ con trỏ pixel của SkiaSharp `(byte*)toRender.GetPixels().ToPointer()` sang con trỏ `dst`, triệt tiêu hoàn toàn mảng byte trung gian và không tạo `new WriteableBitmap` mỗi frame.
   - Hoán đổi con trỏ buffer `(_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer)` và thông báo Avalonia binding vẽ `_frontBuffer`.
   - Kết quả: LOH allocation giảm từ **124.4 MB/s về 0 MB/s**, hiện tượng giật lag GC biến mất 100%.
2. **Tái sử dụng SKBitmap & malloc_trim phía Agent:**
   - Duy trì đối tượng `SKBitmap _reusableBitmap` trong `JpegEncoder`, chỉ tái cấp phát khi kích thước màn hình thay đổi.
   - Thêm P/Invoke `[DllImport("libc", EntryPoint = "malloc_trim")] private static extern int MallocTrim(nuint pad);`.
   - Kích hoạt `MallocTrim(0)` định kỳ mỗi 60 giây và khi đóng session để ép glibc giải phóng unmanaged heap thừa về Linux kernel.

**Lần đầu gặp:** WF-REFACTOR / Thực thi toàn bộ cải tiến Remote Control Tool (2026-10-31)

---

## G010 — X11 XSync blocking round-trip trên motion events làm trễ chuột và nghẽn message queue; XFlush là đủ

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Linux X11, XTest extension, .NET 8 C# interop

**Vấn đề:**
Khi rê chuột nhanh hoặc liên tục, con trỏ chuột trên màn hình ZCU bị giật cục và trễ đáng kể so với thao tác thực tế tại CCU. Thậm chí có lúc làm nghẽn luôn việc đọc gói tin nhịp tim (PONG) khiến session bị ngắt kết nối oan.

**Nguyên nhân:**
Lệnh `X11.XSync(display, false)` gửi yêu cầu đồng bộ tới X server và chặn (block) luồng hiện tại để chờ phản hồi round-trip, tiêu tốn từ 100 đến 500 micro-giây mỗi lần gọi. Các sự kiện di chuyển chuột (`XTestFakeMotionEvent`) là dạng one-way và không có dữ liệu trả về từ X server. Việc ép `XSync` 60 lần/giây khi di chuột tạo ra 60 blocking round-trips không cần thiết, làm đóng băng worker thread xử lý input.

**Cách xử lý:**
1. Trong `MouseInjector.Initialize()`, gọi `XSync` một lần duy nhất để kiểm tra quyền truy cập XTest.
2. Trong hàm `Move(int x, int y)`, chỉ gọi `XTestFakeMotionEvent` kết hợp `X11.XFlush(display)`. `XFlush` lập tức đẩy toàn bộ dữ liệu từ buffer của Xlib sang Unix socket/TCP socket của X server mà không chặn luồng chờ phản hồi.
3. Kết quả: Độ trễ di chuột giảm từ ~0.5ms về dưới 0.01ms, triệt tiêu hoàn toàn nghẽn luồng và loại bỏ hiện tượng rớt session.

**Lần đầu gặp:** WF-REFACTOR / Thực thi cải tiến tầng 2 Remote Control Tool (2026-10-31)

---

## G011 — Avalonia WriteableBitmap dispose trên background thread khi đổi resolution gây crash Skia pipeline — bắt buộc hoãn dispose sang Dispatcher.UIThread

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Avalonia UI 11/12, SkiaSharp rendering backend, Windows & Linux

**Vấn đề:**
Khi máy tính ZCU xoay màn hình hoặc thay đổi độ phân giải (RandR resize event), ứng dụng CCU thỉnh thoảng bị văng đột ngột (crash) với `ObjectDisposedException` hoặc access violation trong thư viện native `libSkiaSharp`.

**Nguyên nhân:**
Luồng nhận dữ liệu TCP (background thread) trong `RemoteScreenViewModel.OnFrameReceived` phát hiện kích thước mới (`_bufferW != w || _bufferH != h`) và lập tức gọi `_frontBuffer?.Dispose()`. Tuy nhiên, tại thời điểm đó, luồng render của Avalonia UI vẫn đang giữ tham chiếu `CurrentFrame = _frontBuffer` và đang tiến hành vẽ khung hình lên màn hình. Việc giải phóng native buffer của `WriteableBitmap` trong khi render thread đang đọc bộ nhớ gây ra lỗi xung đột bộ nhớ và crash tiến trình.

**Cách xử lý:**
1. Khi phát hiện kích thước thay đổi, không gọi `.Dispose()` ngay lập tức.
2. Lưu lại các tham chiếu buffer cũ: `WriteableBitmap? oldFront = _frontBuffer; WriteableBitmap? oldBack = _backBuffer;`
3. Cấp phát buffer mới, hoán đổi và đẩy việc giải phóng buffer cũ sang luồng giao diện:
   ```csharp
   Avalonia.Threading.Dispatcher.UIThread.Post(() =>
   {
       CurrentFrame = displayBitmap;
       oldFront?.Dispose();
       oldBack?.Dispose();
   });
   ```
4. Đảm bảo UI thread đã chuyển hoàn toàn quyền vẽ sang bitmap mới trước khi bitmap cũ bị hủy.

**Lần đầu gặp:** WF-REFACTOR / Thực thi cải tiến tầng 2 Remote Control Tool (2026-10-31)

---


## G012 — Ad-hoc Header với Grid.ColumnSpan="3" trên Logo đè cột nút Back và cuộn mất Header khi lồng trong ScrollViewer

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Avalonia UI 11.2, Linux Minimal (RV1126B DRM 800×1280) & Windows
**Vấn đề:**
1. Trên một số màn hình quản trị (`AboutView`, `PasswordConfigView`, `RecognizeConfigView`), phần header được viết inline dạng `<Grid ColumnDefinitions="Auto,*,Auto">`, trong đó logo KZTEK đặt `Grid.Column="0" Grid.ColumnSpan="3"` để căn giữa màn hình. Khi độ phân giải, lề mép (margin) hoặc font size thay đổi, logo hình ảnh bị vẽ đè lên nút Back ở Column 0, làm nút Back khó bấm hoặc bị che khuất một phần.
2. Tại `PasswordConfigView`, toàn bộ Header bị đặt bên trong `ScrollViewer`, dẫn đến khi người dùng chạm cuộn xuống nhập liệu bàn phím ảo, thanh Header và nút Back bị cuộn trôi mất khỏi tầm mắt.

**Nguyên nhân:**
Không sử dụng Control dùng chung chuẩn (`LockerHeader.axaml`) và không phân tách Layout Grid dạng `RowDefinitions="Auto,*"` cố định Header ở Row 0 và nội dung cuộn ở Row 1.

**Cách xử lý:**
1. Luôn sử dụng `<ctrl:LockerHeader />` cho tất cả màn hình Admin/Cài đặt:
   - Cột 0: Nút Back chuẩn 72×72px cam (#F05922) bo góc 18px.
   - Cột 1: Title (26px Bold) và Subtitle (15px Muted) căn giữa độc lập.
   - Cột 2: Logo KZTEK hoặc nút hành động bên phải.
2. Đặt `LockerHeader` tại `Grid.Row="0"` bên ngoài `ScrollViewer` (`Grid.Row="1"`), đảm bảo Header luôn cố định trên cùng màn hình khi cuộn nội dung.

**Lần đầu gặp:** WF-REFACTOR / Kiểm tra Full UI 3 vòng & Chuẩn hóa giao diện Kiosk RV1126B (2026-10-31)

---

## G013 — Vòng lặp ghi video AVI/MJPEG cấp phát chuỗi ASCII và mảng byte heap liên tục gây giật gián đoạn — triệt tiêu bằng UTF-8 literals `u8` và `stackalloc`

**Ngày phát hiện:** 2026-10-31
**Môi trường:** .NET 8 / C# 11+, Avalonia UI Desktop, AVI RIFF Muxer

**Vấn đề:**
Khi bật tính năng "Ghi phiên làm việc" (Session Recorder) ở tốc độ 30-60 FPS, ứng dụng CCU thỉnh thoảng bị vi giật (micro-stutter) và mức tiêu thụ RAM tăng dần. Profiling bộ nhớ cho thấy hàng chục nghìn mảng byte ngắn `byte[4]` và đối tượng byte array rác liên tục được sinh ra trong Gen 0/Gen 1.

**Nguyên nhân:**
Hàm `RecordFrame` của bộ ghi hình AVI trước đây gọi:
```csharp
_stream.Write(Encoding.ASCII.GetBytes("00dc")); // Cấp phát 1 mảng byte 4 phần tử trên Heap
_stream.Write(BitConverter.GetBytes(jpegData.Length)); // Cấp phát 1 mảng byte 4 phần tử trên Heap
```
Ở 60 FPS, mỗi giây sinh ra ít nhất 120 đối tượng mảng byte rác nhỏ trên Heap chỉ để ghi header của các chunk AVI ("00dc" + chunk size). Dù mỗi mảng nhỏ, sự dồn ứ này làm GC Gen 0 liên tục phải dừng luồng (GC pause) để thu dọn.

**Cách xử lý:**
1. Áp dụng C# UTF-8 string literal `"00dc"u8` — tạo ra `ReadOnlySpan<byte>` ánh xạ trực tiếp vào static data của assembly mà không cấp phát heap.
2. Sử dụng `Span<byte> sizeBytes = stackalloc byte[4];` kết hợp `BinaryPrimitives.WriteInt32LittleEndian(sizeBytes, jpegData.Length);` để ghi kích thước khung hình hoàn toàn trên stack.
3. Ghi trực tiếp các span này vào Stream (`_stream.Write(...)`).
4. Kết quả: Tỷ lệ heap allocation của `RecordFrame` giảm về **0 bytes/frame**, loại bỏ 100% micro-stutter và rác GC khi ghi màn hình kéo dài nhiều giờ.

**Lần đầu gặp:** WF-REFACTOR / Thực thi cải tiến vòng 3 Remote Control Tool (2026-10-31)

---

## G014 — Socket TCP Nagle algorithm và buffer nhỏ trên Linux ZCU gây trễ 40ms và nghẽn burst frame JPEG — triệt tiêu bằng `NoDelay = true` và `SendBufferSize = 256KB`

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Linux ARM64/x64, .NET 8 `TcpListener` / `TcpClient`, X11 Desktop

**Vấn đề:**
Khi điều khiển màn hình từ xa qua mạng LAN, người dùng thỉnh thoảng cảm thấy thao tác chuột bị "khựng" nhẹ khoảng vài chục mili-giây, hoặc khi mở cửa sổ mới có đồ họa phức tạp (kích thước frame JPEG tăng đột biến từ 30KB lên 150KB), khung hình bị trễ một nhịp trước khi cập nhật mượt mà.

**Nguyên nhân:**
1. Thuật toán Nagle mặc định trên socket Linux (`TCP_NODELAY = false`) gom các gói tin nhỏ (như mouse/keyboard ACK hoặc frame chunk cuối) chờ đến khi nhận được ACK của gói tin trước hoặc đủ MTU (tối đa 40ms). Điều này làm tăng độ trễ tương tác.
2. Bộ đệm socket mặc định của Linux kernel (`SO_SNDBUF` ~16KB - 64KB) quá nhỏ đối với các khung hình JPEG độ phân giải cao khi có chuyển động nhanh. Khi bộ đệm đầy, `stream.WriteAsync` bị chặn cho đến khi TCP window phía client giải phóng, gây nghẽn burst frame.

**Cách xử lý:**
1. Trên ZCU Agent (`TcpServer.cs`): Ngay sau khi `_listener.AcceptTcpClientAsync()`, thiết lập:
   ```csharp
   client.NoDelay = true; // Vô hiệu hoá Nagle's algorithm, gửi gói tin tức thì
   client.SendBufferSize = 256 * 1024; // Buffer gửi 256KB hấp thụ burst frame
   client.ReceiveBufferSize = 64 * 1024;
   ```
2. Trên CCU Client (`RemoteControlClient.cs`): Đồng bộ hóa kích thước buffer nhận:
   ```csharp
   _tcp = new TcpClient { NoDelay = true, ReceiveBufferSize = 256 * 1024, SendBufferSize = 64 * 1024 };
   ```
3. Kết quả: Triệt tiêu hoàn toàn độ trễ Nagle 40ms, burst frames được truyền mượt mà với băng thông đường truyền tối đa, độ trễ RTT mạng đo được đạt mức tối ưu (< 1-3 ms trên LAN).

**Lần đầu gặp:** WF-REFACTOR / Thực thi cải tiến vòng 4 Remote Control Tool (2026-10-31)

---

## G015 — Tràn bộ đệm socket và crash bộ nhớ heap do sao chép clipboard văn bản quá lớn — triệt tiêu bằng chốt chặn `MaxClipboardTextLength = 1MB`

**Ngày phát hiện:** 2026-10-31
**Môi trường:** .NET 8 / C# 12, TCP Socket binary protocol, Remote Control Client & Agent

**Vấn đề:**
Khi người dùng vô tình nhấn Ctrl+A rồi Ctrl+C trên một tệp log dung lượng lớn (hàng chục đến hàng trăm MB) hoặc sao chép dữ liệu base64 khổng lồ, ứng dụng điều khiển cố gắng nạp toàn bộ chuỗi này vào thông điệp nhị phân `CLIP_DATA`. Hậu quả là:
1. `Encoding.UTF8.GetBytes` cấp phát mảng byte khổng lồ trên LOH (Large Object Heap), kích hoạt Full GC Gen 2 gây đứng hình giao diện.
2. Quá trình ghi luồng qua SslStream/NetworkStream làm tràn bộ đệm gửi, dẫn đến timeout socket hoặc ngắt kết nối phiên làm việc đột ngột.

**Nguyên nhân:**
Không có cơ chế kiểm soát biên độ dữ liệu tối đa cho thông điệp `CLIP_DATA` ở cả hai đầu CCU và ZCU.

**Cách xử lý:**
1. Thêm hằng số ngưỡng an toàn:
   ```csharp
   public const int MaxClipboardTextLength = 1024 * 1024; // 1MB text limit
   ```
2. Trong hàm gửi clipboard `SendClipboardTextAsync`:
   ```csharp
   if (text.Length > MaxClipboardTextLength)
   {
       text = text.Substring(0, MaxClipboardTextLength);
   }
   ```
3. Kết quả: Ngăn chặn hoàn toàn hiện tượng DoS vô ý hoặc nghẽn luồng socket, đảm bảo kết nối điều khiển từ xa duy trì ổn định tuyệt đối ngay cả khi thao tác clipboard với dữ liệu bất thường.

**Lần đầu gặp:** WF-REFACTOR / Thực thi cải tiến vòng 5 Remote Control Tool (2026-10-31)

---

## G016 — Tối ưu hóa Remote Headless không màn hình vật lý và phiên RDP ảo xrdp bằng X11DisplayResolver và XSetScreenSaver anti-blanking

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Linux ZCU (Ubuntu/Debian), X11, xrdp, Xvfb, systemd headless, .NET 8 C# 12

**Vấn đề:**
1. Trong môi trường không cắm cáp màn hình HDMI (Headless kiosk) hoặc phiên chạy background qua daemon systemd / RDP (xrdp):
   - Biến môi trường `XDG_SESSION_TYPE` không được thiết lập (`null` hoặc rỗng), khiến rào chắn ban đầu từ chối khởi động (`return 1`).
   - Phiên hiển thị ảo thường không nằm ở `:0` cố định mà được cấp phát động ở `:1`, `:10.0`, `:11.0`, v.v. Việc gọi cứng `XOpenDisplay(null)` làm sập tiến trình với ngoại lệ `XOpenDisplay failed. Is DISPLAY set correctly?`.
2. Khi không có màn hình vật lý cắm vào cổng HDMI, X server sau vài phút không có hoạt động phần cứng sẽ tự động chuyển sang chế độ DPMS Sleep / ScreenSaver Blanking, dẫn đến các khung hình JPEG chụp được từ `XShmGetImage` hoặc `XGetImage` trở thành toàn màu đen.

**Nguyên nhân:**
- Ép cứng `XDG_SESSION_TYPE == "x11"` loại trừ trường hợp daemon headless hợp lệ.
- Ép cứng kết nối Display `:0` thay vì dò tìm các Unix domain socket khả dụng tại `/tmp/.X11-unix/`.
- Chưa cấu hình tham số XSetScreenSaver để vô hiệu hóa timeout tắt màn hình ảo.

**Cách xử lý:**
1. **Nới lỏng rào chắn `XDG_SESSION_TYPE`:** Chỉ chặn nghiêm ngặt khi `sessionType == "wayland"`. Với các trường hợp khác, cho phép tiếp tục và kích hoạt bộ dò Display tự động.
2. **Hiện thực hóa `X11DisplayResolver`:**
   - Dò tìm theo thứ tự: Cấu hình `AgentOptions.Display` -> biến môi trường `DISPLAY` -> quét các file socket `/tmp/.X11-unix/X*` (ví dụ `X0` -> `:0`, `X10` -> `:10`) -> danh sách fallback `[":0", ":1", ":10.0", ":10", ":20.0"]`.
   - Mở Display đầu tiên thành công và tự động gán `Environment.SetEnvironmentVariable("DISPLAY", resolved)` để các tiến trình con và injector kế thừa thông suốt.
3. **Chống màn hình đen Anti-Blanking:** Gọi P/Invoke `XSetScreenSaver(_display, 0, 0, 0, 0)` và `XResetScreenSaver(_display)` ngay khi capturer khởi tạo.
4. **Kết quả:** ZcuAgent hoạt động trơn tru trong mọi môi trường: Headless Kiosk không cắm màn hình, máy ảo Cloud/Docker, và phiên điều khiển từ xa RDP xrdp.

**Lần đầu gặp:** WF-REFACTOR / Tối ưu hóa Remote Không Màn Hình RDP Vòng 6 (2026-10-31)

---

## G017 — Rác LOH (1.5–3MB/s) trên ZCU do EncodeFrameJpeg cấp phát mảng byte mỗi khung hình — triệt tiêu bằng Zero-Allocation Frame Streaming và Congestion Dropping

**Ngày phát hiện:** 2026-10-31
**Môi trường:** Linux ZCU (Atom/ARM64), .NET 8 C# 12, TCP Socket Streaming, JPEG 1080p 15–30 FPS

**Vấn đề:**
1. Mặc dù CCU Client đã có cơ chế Ping-Pong Double-Buffering zero-copy, trên ZcuAgent (Linux máy đích), hàm `MessageCodec.EncodeFrameJpeg` vẫn tạo ra một mảng byte mới `var buf = new byte[24 + jpeg.Length]` cho MỖI khung hình được chụp.
2. Với khung hình độ phân giải 1080p, kích thước JPEG dao động từ 70KB đến 150KB. Khi vượt ngưỡng 85,000 bytes, các mảng byte này rơi thẳng vào Large Object Heap (LOH). Ở 15–30 FPS, hệ thống sinh ra từ **1.5 MB đến 3.0 MB rác LOH mỗi giây**, buộc Garbage Collector phải kích hoạt Full GC Gen 2 định kỳ gây giật đơ vi mô (micro-stuttering) trên bo mạch nhúng.
3. Đồng thời, khi mạng bị nghẽn (TCP backpressure), ZcuAgent cố gắng gửi dồn dập các frame đã chụp vào buffer TCP khiến CCU nhận khung hình bị "trễ tích lũy" (lag backlog).

**Nguyên nhân:**
- Hàm mã hóa thông điệp frame gom header metadata và jpeg payload vào cùng một mảng byte mới cấp phát trên heap.
- Thiếu cơ chế ghi trực tiếp (scattered streaming) và nhận diện backpressure để điều tiết hoặc bỏ qua frame trung gian.

**Cách xử lý:**
1. **Zero-Allocation Frame Streaming (`WriteFrameJpegDirectAsync`):**
   - Khởi tạo bộ đệm cố định 29 bytes duy nhất (`_frameHeaderBuffer`) per-session: 5 bytes wire header (`MessageType.FrameJpeg` + `PayloadLength`) + 24 bytes metadata (`frameId`, `timestampMs`, `width`, `height`, `jpegLength`).
   - Sử dụng `MessageCodec.WriteFrameMetadataHeader` ghi trực tiếp metadata vào span đích (0 heap allocation).
   - Ghi 29 bytes header vào stream, sau đó stream trực tiếp slice `jpeg` nén từ buffer tái sử dụng của `JpegEncoder` sang socket.
   - Triệt tiêu $100\%$ việc cấp phát mảng byte cho từng khung hình.
2. **Buffer Pooling cho TCP Messages:**
   - Sử dụng `ArrayPool<byte>.Shared.Rent(HeaderSize)` trong `MessageCodec.WriteMessageAsync` và `ReadMessageAsync`, loại bỏ hoàn toàn việc cấp phát `new byte[5]` cho từng sự kiện chuột, phím, ping, pong.
3. **Congestion Frame Dropping & Latest-Frame Fast-Forward:**
   - Đo lường thời gian ghi của frame: nếu `writeMs > 1.5 * frameInterval`, ghi nhận trạng thái nghẽn socket và gọi `Task.Yield()` giải phóng buffer TCP thay vì chụp dồn dập, đảm bảo CCU luôn xem được khung hình mới nhất tại thời điểm thực.
4. **Kết quả:** ZcuAgent đạt trạng thái **Zero-Allocation** tuyệt đối trong vòng lặp stream video, rác LOH giảm về 0, độ trễ tương tác mạng duy trì ổn định < 10–20ms.

**Lần đầu gặp:** WF-REFACTOR / Chu trình Cải tiến Vòng 7 (2026-10-31)

---

## G018 — Avalonia ListBox DoubleTapped event: `sender.DataContext` là null/window chứ không phải item model — bắt buộc trích xuất qua `e.Source` / `ListBoxItem` / `SelectedItem`

**Ngày phát hiện:** 2026-11-02
**Môi trường:** Avalonia UI 11.x, Windows / Linux Desktop, `ListBox` with DataTemplate

**Vấn đề:**
Trong cửa sổ quản lý file hoặc danh sách (`RemoteCommandWindow`), khi người dùng nhấn đúp chuột (`DoubleTapped`) vào một mục thư mục trong `ListBox` (`PART_FileListBox`), sự kiện không kích hoạt mở thư mục và không có bất kỳ phản hồi nào trên giao diện.

**Nguyên nhân:**
1. Trong cơ chế định tuyến sự kiện (Routed Event) của Avalonia, khi gắn thuộc tính `DoubleTapped="OnFileItemDoubleTapped"` trực tiếp trên thẻ `<ListBox>`, tham số `sender` của event handler luôn là chính đối tượng `ListBox` (`PART_FileListBox`).
2. `ListBox.DataContext` kế thừa từ cửa sổ cha (`Window`) hoặc ViewModel toàn cục — nó KHÔNG PHẢI là đối tượng dữ liệu dòng (`RemoteFileItem`).
3. Câu lệnh kiểm tra `if (sender is Control c && c.DataContext is RemoteFileItem item)` do đó LUÔN trả về `false`, làm nuốt chửng toàn bộ thao tác nhấn đúp của người dùng.

**Cách xử lý:**
1. Trích xuất đa tầng để đảm bảo luôn lấy được item model dù người dùng click vào bất kỳ vị trí nào trên hàng (icon, text, ngày tháng, hay vùng đệm padding):
   ```csharp
   var listBox = this.FindControl<ListBox>("PART_FileListBox") ?? sender as ListBox;
   var item = (sender as Control)?.DataContext as RemoteFileItem
              ?? (e.Source as StyledElement)?.DataContext as RemoteFileItem
              ?? (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext as RemoteFileItem
              ?? listBox?.SelectedItem as RemoteFileItem;

   if (item != null && (item.IsDirectory || item.IsSymbolicLink))
   {
       e.Handled = true;
       string targetPath = !string.IsNullOrWhiteSpace(item.FullName)
           ? item.FullName
           : (this.FindControl<TextBox>("PART_FileCurrentPath")?.Text?.TrimEnd('/') + "/" + item.Name);
       _ = LoadDirectoryAsync(targetPath);
   }
   ```
2. Gắn thêm `DoubleTapped="OnFileItemDoubleTapped"` trên phần tử `StackPanel` bên trong `<ListBox.ItemTemplate>` để bắt ngay sự kiện tại cấp độ con.
3. Bổ sung sự kiện phím `KeyDown="OnFileListBoxKeyDown"` hỗ trợ `Enter` để mở thư mục và `Backspace` để lên thư mục cha (`NavigateUpAsync`).
4. Chuẩn hóa hàm `NavigateUpAsync` theo định dạng POSIX path (`TrimEnd('/')` và `LastIndexOf('/')`), độc lập với OS path separator.
5. Kết quả: Thao tác nhấn đúp chuột mở thư mục hoạt động nhạy 100%, hỗ trợ đầy đủ phím tắt bàn phím tiện dụng.

**Lần đầu gặp:** WF-BUGFIX / Sửa lỗi nhấn đúp mở thư mục trong Quản lý File SFTP (2026-11-02)

---

## G019 — Lỗi màn hình ảo `eDP-1` (`connected`) trên máy tính công nghiệp YANLING IBOX-205 (Intel Haswell-ULT i915) gây mất màn hình chính hoặc treo bộ cài Ubuntu

**Ngày phát hiện:** 2026-11-03
**Môi trường:** Máy tính công nghiệp YANLING IBOX-205 (Intel Core i5-4210U, i915 DRM), AMI Aptio BIOS 5.011, Ubuntu 22.04 LTS (`jammy`)

**Vấn đề:**
1. Khi khởi động bộ cài Ubuntu Live USB (`ubiquity`) hoặc sau khi cài đặt mới hệ điều hành trên YANLING IBOX-205 chỉ cắm 1 màn hình vật lý qua cổng `HDMI-1`, màn hình vật lý chỉ hiển thị hình nền trống hoặc bị kẹt ở màn hình đăng nhập/TTY, mọi cửa sổ chính (bộ cài `ubiquity`, hộp thoại login GDM3, thanh Dock) đều bị đẩy sang một màn hình vô hình thứ 2.
2. Khi ghi `echo off > /sys/kernel/debug/dri/1/eDP-1/force`, đọc lại `/sys/class/drm/card1-eDP-1/status` vẫn thấy `connected` nếu chưa có tiến trình DRM client (`Xorg`/`gdm3`) gọi lại `drmModeGetConnector()` để kích hoạt `drm_helper_probe_single_connector_modes()`.

**Nguyên nhân:**
1. Trong bảng VBT (Video BIOS Table) của BIOS AMI Aptio trên bo mạch công nghiệp YANLING IBOX-205, tùy chọn cổng màn hình nội bộ (`Active LFP` / `eDP-1` / `LVDS`) được bật mặc định (`Enabled`). Do chuẩn eDP là màn hình gắn trong, driver `i915` mặc định coi `card1-eDP-1` luôn ở trạng thái `connected` (`1024x768 +0+0` làm Primary), đẩy màn hình vật lý `HDMI-A-1` (`1280x1024 +1024+0`) thành màn hình phụ thứ 2.
2. Trong nhân Linux (`drm_sysfs.c` & `drm_probe_helper.c`), biến `connector->status` chỉ được tính toán lại từ `connector->force` (`DRM_FORCE_OFF`) khi hàm `fill_modes` được gọi (lúc khởi động lại `gdm3`/`Xorg` hoặc truyền từ đầu qua kernel cmdline `video=eDP-1:d`).

**Cách xử lý:**
1. **Triệt tiêu vĩnh viễn trên Hệ điều hành (4 lớp bảo vệ):**
   - **Lớp 1 (Kernel Boot DRM):** Thêm `video=eDP-1:d video=HDMI-A-1:e` vào `GRUB_CMDLINE_LINUX_DEFAULT` trong `/etc/default/grub` rồi chạy `sudo update-grub`.
   - **Lớp 2 (Runtime DRM Debugfs & Sysfs):** Ghi `echo off > /sys/kernel/debug/dri/1/eDP-1/force` và `echo off > /sys/class/drm/card1-eDP-1/status`, kết hợp udev rule `/etc/udev/rules.d/99-disable-edp.rules` (`ACTION=="add|change", SUBSYSTEM=="drm", KERNEL=="card*-eDP-1", ATTR{status}="off"`).
   - **Lớp 3 (Xorg Configuration):** Tạo `/etc/X11/xorg.conf.d/10-monitor-ignore-edp.conf` đặt `Option "Ignore" "true"` cho `eDP-1` và `Option "Primary" "true"` cho `HDMI-1` / `HDMI-A-1`.
   - **Lớp 4 (GDM3 Xorg Session):** Cấu hình `/etc/gdm3/custom.conf` với `WaylandEnable=false` và `AutomaticLoginEnable=true`, sau đó `sudo systemctl restart gdm3`. Ngay lập tức `/sys/class/drm/card1-eDP-1/status` chuyển sang `disconnected` và `xrandr` chỉ còn duy nhất màn hình `HDMI-1`.
2. **Triệt tiêu tận gốc ở tầng phần cứng BIOS (giữ nguyên kể cả khi cài lại OS mới):**
   - Vào BIOS AMI (`Delete` / `F2` lúc bật máy) → tab **Chipset** → **System Agent (SA) Configuration** → **Graphics Configuration** → chuyển **Active LFP** (hoặc **Internal Graphics eDP/LVDS**) thành **No LVDS / Disabled** và đặt **Primary Display** = **IGFX / HDMI**, sau đó nhấn `F10` lưu lại.

**Lần đầu gặp:** WF-DEVOPS / Triển khai OS & loại bỏ màn hình phụ ảo `eDP-1` trên ZCU `192.168.21.101` (2026-11-03)

---

## G020 — Lỗi lệch byte ABI 64-bit (`LP64`) trong `XShmSegmentInfo` làm `XShmAttach` luôn bị Xorg từ chối (`BadAccess`), gây sụt FPS và ngốn 90% CPU do rơi về `XGetImage`

**Ngày phát hiện:** 2026-11-03
**Môi trường:** Linux 64-bit (`x86_64` & `arm64` ABI `LP64`), .NET 8 C# P/Invoke `libXext.so.6`, Xorg Server

**Vấn đề:**
Khi `IPGS.RemoteControl.ZcuAgent` khởi động trên bất kỳ máy Linux 64-bit nào, `X11ScreenCapturer` luôn ghi log cảnh báo:
`XShmAttach rejected by X server (async BadAccess) — MIT-SHM not permitted on this display. Falling back to XGetImage.`
Do rơi về `XGetImage`, mỗi khung hình phải truyền 5.24 MB – 8.3 MB dữ liệu pixel thô qua Unix socket kèm `Marshal.Copy`, làm tiến trình `ZcuAgent` ngốn ~90% CPU và FPS thực tế bị tụt thấp (~10–13 FPS).

**Nguyên nhân:**
1. Trong giao thức dây (wire protocol) của X11, `XID` rộng 32-bit, nhưng trong C ABI của thư viện client `libX11` / `libXext` trên Linux 64-bit (`LP64`, không định nghĩa `_XSERVER64`), kiểu `ShmSeg` (`typedef XID ShmSeg; typedef unsigned long XID;`) là `unsigned long` — tức **8 bytes** (`nuint`), không phải 4 bytes (`int`)!
2. Khi khai báo `public int shmseg;` (4 bytes) ở đầu struct `XShmSegmentInfo` trong `XShmInterop.cs`, các trường tiếp theo trong C# bị lệch 4 bytes so với `libXext.so.6`:
   - C# đặt `shmid` tại offset 4 và `shmaddr` tại offset 8.
   - `libXext.so.6` ghi `shmseg` 8-byte vào offset `0..7` (ghi đè mất `shmid` của C#!) rồi đọc `shmid` tại offset `8..11` (thực chất là 4 byte thấp của con trỏ `shmaddr`!).
   - Xorg Server nhận `shmid` rác nên gọi `shmat()` thất bại và trả về lỗi bất đồng bộ `BadAccess` (`error_code=10, request_code=130, minor_code=1`).

**Cách xử lý:**
1. Trong `XShmInterop.cs`, đổi trường `shmseg` từ `int` sang `nuint` (`UIntPtr` — 8 bytes trên 64-bit LP64, 4 bytes trên 32-bit ILP32) và đặt `SHM_MODE = 0x1B6` (`0666` octal).
2. **BẮT BUỘC cấp phát `XShmSegmentInfo` trên bộ nhớ unmanaged (`Marshal.AllocHGlobal`) thay vì truyền `ref _shmInfo` (field của class managed):** Hàm `XShmCreateImage` trong `libXext.so.6` lưu trực tiếp địa chỉ con trỏ `shminfo` vào `image->obdata`, và mọi lệnh `XShmGetImage` về sau đều đọc `((XShmSegmentInfo*)image->obdata)->shmseg`. Nếu truyền `ref _shmInfo`, P/Invoke chỉ pin tạm thời trong lúc gọi hàm; khi Garbage Collector dời chỗ đối tượng `X11ScreenCapturer` trên managed heap, `image->obdata` trỏ vào vùng nhớ cũ (`resourceid=0x0`) gây lỗi `BadShmSeg` (`error_code=128, minor_code=4`).
3. Trong `X11ScreenCapturer.CaptureSHM()` và `JpegEncoder.EncodeJpeg()`, truyền trực tiếp con trỏ vùng nhớ chia sẻ `img.Data` (`PixelPtr`) vào `SKImage.FromPixels` (Zero-Copy), loại bỏ hoàn toàn lệnh `Marshal.Copy` 5.2–8.3 MB mỗi frame.
4. Phía CCU (`RemoteScreenViewModel.OnFrameReceived`), dùng `SKCodec.GetPixels` giải mã trực tiếp JPEG vào địa chỉ framebuffer của `WriteableBitmap.Lock().Address`, loại bỏ `SKBitmap` trung gian và vòng lặp `Buffer.MemoryCopy`.

**Lần đầu gặp:** WF-BUGFIX / Tối ưu FPS Remote Control trên ZCU `192.168.21.101` (2026-11-03)

---

## G021 — Lỗi kẹt dconf lock do ngược thứ tự gỡ khoá và cơ chế nạp GNOME Shell extension offline trên Ubuntu 22.04

**Ngày phát hiện:** 2026-11-03
**Môi trường:** Ubuntu 22.04.5 LTS, GNOME Shell 42.9 (X11), dconf system-db, `2-configure-system.sh` & `1-install-software.sh`

**Vấn đề:**
1. Khi quản trị viên chuyển từ chế độ Kiosk sang chế độ bảo trì (bỏ tích các ô Kiosk như `Khóa phím tắt`, `Tắt Hot Corner`, `Khóa Workspace`), sau khi chạy Deploy hệ thống vẫn bị kẹt: phím `Super` và nút `Activities` trên Top Bar không mở được Overview, `enable-hot-corners` và `dynamic-workspaces` vẫn ở giá trị `false`.
2. Sau khi cài extension GNOME cục bộ (`just-perfection`, `block-caribou`) bằng cách giải nén vào `~/.local/share/gnome-shell/extensions/`, lệnh `gnome-extensions list` và `gnome-extensions info` vẫn không nhận diện được extension (`CẢNH BÁO: extension chưa ở trạng thái ENABLED`), khiến các thiết lập ẩn/hiện Top Bar hoặc Activities không có hiệu lực cho đến khi khởi động lại máy.

**Nguyên nhân:**
1. **Lỗi ngược thứ tự dconf unlock:** File khoá hệ thống `/etc/dconf/db/local.d/locks/00-kiosk-lockdown` khoá cứng các key `/org/gnome/desktop/interface/enable-hot-corners` và `/org/gnome/mutter/dynamic-workspaces`. Trong script cũ, các lệnh `gsettings set ... true` nằm ở Mục `[1/10]` và `[2/10]` chạy **TRƯỚC** Mục `[9/10]` (nơi xoá file lock và chạy `dconf update`). Do đó, lệnh `gsettings set` bị dconf từ chối ngầm (`2>/dev/null || true`); sau đó khóa mới được gỡ ở Mục `[9/10]` nhưng không có lệnh nào thiết lập lại `true`, khiến user dconf database bị kẹt ở giá trị `false`.
2. **GNOME Shell extension cache:** GNOME Shell chỉ quét thư mục `~/.local/share/gnome-shell/extensions/` khi tiến trình `gnome-shell` khởi động. Việc copy/unzip thư mục mới vào khi shell đang chạy không kích hoạt sự kiện inotify nạp extension mới.

**Cách xử lý:**
1. **Gỡ khoá dconf TRƯỚC TIÊN (Step 0):** Trong `2-configure-system.sh`, nếu `LOCKDOWN_SHELL=0`, lập tức xoá `/etc/dconf/db/local.d/00-kiosk-lockdown` và file `locks/`, chạy `_sudo dconf update`, đồng thời gọi `gsettings reset org.gnome.mutter overlay-key` và `gsettings reset org.gnome.shell.keybindings toggle-overview` trước khi thực thi bất kỳ bước cấu hình người dùng nào.
2. **Nạp lại GNOME Shell trong 0ms trên X11:** Trong `1-install-software.sh`, sau khi giải nén và cấu hình extension, kiểm tra nếu `gnome-shell` đang chạy trên phiên X11 thì gọi `killall -9 gnome-shell`. Do unit `org.gnome.Shell@x11.service` có cấu hình `Restart=always` và `RestartSec=0ms`, systemd sẽ nạp lại GNOME Shell ngay lập tức trong <1 giây mà không đóng các ứng dụng GUI khác, quét và kích hoạt toàn bộ extension mới ngay trong phiên làm việc.
3. **Tách phạm vi Deploy độc lập (`DEPLOY_SCOPE`):** Bổ sung biến môi trường `DEPLOY_SCOPE` (`machine` | `software` | `all`) vào `2-configure-system.sh` và `KioskDeployOptions` trong C#. Nút `Deploy Config máy tính` chỉ chạy phần máy tính/desktop, nút `Deploy Config phần mềm` chỉ chạy phần autostart/watchdog/update cho app được chọn, không còn hiện tượng bấm tab này đè cấu hình tab kia.

**Lần đầu gặp:** WF-REFACTOR / Tách độc lập Deploy Kiosk & Hợp nhất Quản lý File/CMD Shell (2026-11-03)

---

## G022 — Lỗi vỡ font / hỏi chấm (???) do PowerShell stdin heredoc giải mã chuỗi UTF-8 theo ANSI CP1252

**Ngày phát hiện:** 2026-11-03
**Môi trường:** Windows PowerShell 5.1, Python CLI heredoc stdin pipe (@'...'@ | python -), Source code C# / XAML Avalonia

**Vấn đề:**
Khi chạy script tự động hoá bằng cách pipe mã nguồn inline từ PowerShell sang Python qua stdin (@'...'@ | python -), toàn bộ các ký tự tiếng Việt có dấu (ế, à, ộ, ư...) và icon emoji đa byte (🛠️, 📁, 🔄, ⬆️...) trong file nguồn C# và XAML bị biến thành các ký tự dấu hỏi chấm ??? (ví dụ: ??? File &amp; CMD, Qu?n l? File).

**Nguyên nhân:**
1. Trong Windows PowerShell 5.1 mặc định, luồng Standard Input / Output ([Console]::InputEncoding và [Console]::OutputEncoding) kế thừa theo bảng mã hệ thống OEM/ANSI (CP1252 hoặc CP437) thay vì UTF-8.
2. Khi PowerShell đọc khối chuỗi heredoc (@'...'@) và đẩy qua pipe |, nó mã hóa chuỗi thành byte theo CP1252. Python nhận stream stdin từ subprocess cũng giải mã theo CP1252, biến mọi byte UTF-8 không thuộc phạm vi ANSI 7-bit thành ký tự fallback ?.

**Cách xử lý:**
1. **Tuyệt đối không truyền chuỗi mã nguồn tiếng Việt/Emoji qua pipe PowerShell stdin:**
   - Thay vào đó, ghi file script .py độc lập trực tiếp lên đĩa bằng encoding UTF-8 chuẩn (write_file API hoặc file .py có # -*- coding: utf-8 -*-).
   - Mọi thao tác đọc/ghi file trong Python bắt buộc khai báo rõ: open(path, 'r', encoding='utf-8') và open(path, 'w', encoding='utf-8', newline='').
2. **Kiểm tra hồi quy tự động:**
   - Chạy script kiểm tra quét toàn bộ file nguồn: nếu chuỗi chứa ??? hoặc ký tự rác encoding thì lập tức cảnh báo và chặn commit.
3. **Kết quả:** Toàn bộ giao diện tiếng Việt và các biểu tượng emoji hiển thị sắc nét, chuẩn xác 100% trên cả Windows và Linux.

**Lần đầu gặp:** WF-BUGFIX / Khắc phục lỗi vỡ font tiếng Việt & emoji trong Quản lý File / CMD (2026-11-03)

---

## G023 — Kiosk Deploy 2 tab bị đồng nhất do ResolveKioskScriptsDir ưu tiên Resources cũ, bash helper bị kẹt trong scope và Just Perfection lỗi 'primary is null' trên GNOME 42

**Ngày phát hiện:** 2026-11-03
**Môi trường:** Ubuntu 22.04 LTS (GNOME Shell 42.9, X11), C# Avalonia CcuUI, `KioskDeployService.cs`, `scripts/linux-kiosk/`

**Vấn đề:**
1. Khi bấm nút `Deploy Config máy tính` hoặc `Deploy Config phần mềm`, cả 2 nút đều thực thi toàn bộ cấu hình monolithic như nhau (chạy cả extension lẫn autostart/watchdog) thay vì chạy riêng rẽ theo tab (`DEPLOY_SCOPE` bị bỏ qua).
2. Khi chạy với `DEPLOY_SCOPE=software`, bash ném lỗi: `line 437: _get_real_exec: command not found`.
3. Nút `Activities` trên Ubuntu không bấm được và Top Bar không ẩn/hiện đúng do extension `just-perfection-desktop@just-perfection` rơi vào trạng thái `State: ERROR` trong `gnome-extensions info`.

**Nguyên nhân:**
1. **Thứ tự nạp scripts trong `KioskDeployService.cs`:** Hàm `ResolveKioskScriptsDir` ưu tiên tìm thư mục `Resources/scripts` trong `bin/Debug/net8.0/` trước, nơi chứa các script cũ chưa có biến `DEPLOY_SCOPE`. Mỗi lần deploy, CcuUI upload đè bản script cũ lên ZCU khiến mọi sửa đổi ở repo gốc `scripts/` bị vô hiệu.
2. **Hàm helper bash đặt sai phạm vi:** Hàm `_get_real_exec()` và `_detect_dm()` nằm bên trong khối `if [ "$DEPLOY_SCOPE" != "software" ]; then`. Khi deploy với scope `software`, toàn bộ khối này bị bỏ qua, dẫn tới các lệnh ở mục `[8/10]` và `[10/10]` không tìm thấy hàm `_get_real_exec`.
3. **Crash Looking Glass trong GNOME 42:** Khi GNOME Shell khởi động hoặc nạp lại, `Main.layoutManager.primaryMonitor` có thể là `null` trước khi monitor geometry được tính toán xong. Extension Just Perfection gọi `_applyLookingGlassSize()` trong `applyAll()` khi `enable()`, gọi `createLookingGlass()` khiến GNOME Shell ném `TypeError: primary is null` và chuyển extension sang `State: ERROR`.

**Cách xử lý:**
1. **Đảo thứ tự tìm kiếm trong `ResolveKioskScriptsDir`:** Ưu tiên dò ngược tìm thư mục `scripts/` từ thư mục gốc mã nguồn (solution root). Đồng thời đồng bộ các script cập nhật vào toàn bộ thư mục `Resources/scripts` trong project và bin output.
2. **Đưa hàm helper lên top-level:** Chuyển `_get_real_exec()` và `_detect_dm()` lên đầu script (ngay sau `_sudo()`), đảm bảo luôn có sẵn cho mọi scope `machine`, `software`, và `all`.
3. **Tự động vá lỗi GNOME 42 cho Just Perfection:** Bổ sung bước kiểm tra và bọc an toàn trong `1-install-software.sh`:
   `if (!this._main.layoutManager || !this._main.layoutManager.primaryMonitor) return; let lookingGlass; try { lookingGlass = this._main.createLookingGlass(); } catch(e) { return; }`
   Nhờ đó Just Perfection luôn ở trạng thái `State: ENABLED`, nạp lại ngay lập tức qua `killall -9 gnome-shell` trên X11 và nút `Activities` hoạt động chuẩn xác 100%.

**Lần đầu gặp:** WF-BUGFIX / Khắc phục triệt để lỗi 2 nút Deploy Kiosk & nút Activities (2026-11-03)

---

## G024 — Lỗi status 150 'No frameworks were found' khi deploy ZcuAgent framework-dependent do `libhostfxr.so` nằm trong app dir làm muxer bỏ qua `DOTNET_ROOT`

**Ngày phát hiện:** 2026-11-04
**Môi trường:** Ubuntu 22.04 LTS (x86_64), .NET 8 Runtime cài tại `/home/ccu/.dotnet`, tiến trình systemd user service `ipgs-remote-agent.service`

**Vấn đề:**
Khi publish `IPGS.RemoteControl.ZcuAgent` bằng lệnh `dotnet publish -c Release -r linux-x64 --self-contained false` và deploy toàn bộ thư mục publish lên `/home/ccu/ipgs/remote-agent/`, service khởi động thất bại với `code=exited, status=150/n/a` và log:
```text
You must install or update .NET to run this application.
App: /home/ccu/ipgs/remote-agent/IPGS.RemoteControl.ZcuAgent
Architecture: x64
Framework: 'Microsoft.NETCore.App', version '8.0.0' (x64)
.NET location: /home/ccu/ipgs/remote-agent/
No frameworks were found.
```
Dù trong unit service đã có `Environment=DOTNET_ROOT=/home/ccu/.dotnet` và `/home/ccu/.dotnet/dotnet --list-runtimes` báo `Microsoft.NETCore.App 8.0.20`.

**Nguyên nhân:**
1. Khi chạy `dotnet publish` với RID chỉ định (`-r linux-x64`) trên SDK mới, dotnet xuất kèm các thư viện native host `libhostfxr.so` và `libhostpolicy.so` vào thư mục publish đích.
2. Khi `IPGS.RemoteControl.ZcuAgent` (apphost native binary) khởi chạy, nó tìm thấy `libhostfxr.so` ngay tại thư mục hiện hành (`/home/ccu/ipgs/remote-agent/`). Theo quy tắc dò tìm của .NET Host, sự hiện diện của `libhostfxr.so` tại thư mục ứng dụng khiến muxer mặc định coi chính thư mục này là `.NET location` và **bỏ qua biến môi trường `DOTNET_ROOT`**.
3. Do thư mục `/home/ccu/ipgs/remote-agent/` chỉ chứa app binaries mà không chứa cây thư mục `shared/Microsoft.NETCore.App/8.0.x`, `libhostfxr` kết luận `No frameworks were found` và thoát với status 150.

**Cách xử lý:**
1. Loại bỏ các file `libhost*.so` và `libclr*.so` khỏi thư mục triển khai của ứng dụng framework-dependent:
   ```bash
   rm -f /home/ccu/ipgs/remote-agent/libhost* /home/ccu/ipgs/remote-agent/libclr*
   ```
2. Khi không còn `libhostfxr.so` cục bộ trong app dir, apphost sẽ đọc biến môi trường `DOTNET_ROOT=/home/ccu/.dotnet` (hoặc `/usr/share/dotnet`), nạp `libhostfxr.so` từ runtime chuẩn của hệ điều hành và khởi động thành công 100%.
3. Trong thư mục tài nguyên nhúng `IPGS.RemoteControl.CcuUI/Resources/zcu-agent/linux-x64/`, loại bỏ vĩnh viễn các file `libhost*.so` để các phiên cài đặt tự động qua GUI không bao giờ gặp lại lỗi này.

**Lần đầu gặp:** WF-BUGFIX / Khắc phục lỗi XShm G020 & tối ưu FPS trên ZCU `192.168.21.56` (2026-11-04)

---

## G025 — Lỗi ngắt kết nối định kỳ 30s (`Broken pipe / PING timeout`) khi stream video: CCU Client chỉ cập nhật timestamp heartbeat khi nhận `Pong`, bỏ qua `FrameJpeg` và `Ping` từ server

**Ngày phát hiện:** 2026-11-04
**Môi trường:** CCU Client (`RemoteControlClient.cs`), ZCU Agent (`ClientSession.cs`), TCP streaming với `PingTimeoutMs = 30_000`

**Vấn đề:**
Sau khi tối ưu hóa ZcuAgent (sử dụng MIT-SHM, CPU hạ xuống ~7%), phiên điều khiển từ xa vẫn bị gián đoạn, màn hình chớp đen hoặc đứng hình định kỳ sau mỗi đúng 30 giây. Phía Linux ZcuAgent báo lỗi `Unable to write data to the transport connection: Broken pipe (SocketException 32) at WriteFrameJpegDirectAsync` và đóng session, sau đó CCU Client kết nối lại ngay lập tức rồi lặp lại chu kỳ 30s.

**Nguyên nhân:**
1. Trong `RemoteControlClient.cs`, tác vụ `PingSenderLoopAsync` kiểm tra biến thời gian `_lastPongTicks`: nếu `(DateTime.UtcNow - lastPong).TotalMilliseconds > PingTimeoutMs` (30 giây), client tự hủy session (`_sessionCts?.Cancel()`) và đóng socket TCP.
2. Tuy nhiên, `_lastPongTicks` **chỉ được cập nhật duy nhất khi client nhận thông điệp `MessageType.Pong`** từ server.
3. Khi nhận `MessageType.FrameJpeg` (đang truyền 15–30 frame/giây) hoặc `MessageType.Ping` từ server, client hoàn toàn không cập nhật `_lastPongTicks`.
4. Nếu gói tin `MessageType.Pong` phản hồi từ server bị trễ (do vòng lặp capture chiếm giữ `_writeLock` mà không yield, hoặc do client giải mã JPEG đồng bộ làm trễ luồng socket), `_lastPongTicks` không được làm mới. Sau đúng 30 giây, CCU Client tự kết luận "mất kết nối" và đóng socket dù video đang stream mượt mà, gây ra hiện tượng Broken pipe trên server.

**Cách xử lý:**
1. **Làm mới heartbeat trên mọi gói tin nhận từ server (`RemoteControlClient.cs`):**
   Trong `ReceiveLoopAsync`, cập nhật `Interlocked.Exchange(ref _lastPongTicks, DateTime.UtcNow.Ticks)` ngay khi nhận `MessageType.FrameJpeg` và `MessageType.Ping`. Bất kỳ dữ liệu nào nhận được từ server đều là bằng chứng xác thực đường truyền đang sống khỏe.
2. **Cooperative Yield trong `ClientSession.cs` phía ZcuAgent:**
   Trong vòng lặp `RunCaptureSendLoopAsync`, khi `sleep <= 0` (máy chụp nhanh hơn hoặc bằng thời gian frame), luôn gọi `await Task.Yield()` để nhường CPU và giải phóng `_writeLock`, cho phép vòng lặp nhận (`RunReceiveLoopAsync`) gửi các gói tin Pong/Ack tức thì mà không bị nghẽn sau video frames.
3. **Kết quả:** Phiên điều khiển duy trì liên tục > 40–120s ổn định, 0 disconnect, 0 Broken pipe, PONG phản hồi tức thì < 100ms.

**Lần đầu gặp:** WF-BUGFIX / Khắc phục ngắt kết nối định kỳ 30s trên ZCU `192.168.21.56` (2026-11-04)


---

## G026 — Nghẽn CPU 100% trên Atom/Celeron và giật lag điều khiển do thuật toán Adaptive FPS chỉ nhìn `writeMs`, `XSync` trong chuột và rác WriteableBitmap 157 MB/s

**Ngày phát hiện:** 2026-11-04
**Môi trường:** Intel Celeron J3160 / Atom Braswell (TDP 6W), Linux X11, Avalonia UI, SkiaSharp, TCP Remote Control

**Vấn đề:**
Sau khi nâng cấp XShm và vá keepalive 30s, hệ thống điều khiển vẫn bị giật lag và CPU máy con Celeron J3160 bị nghẽn tới 100% (0.0% IDLE), thao tác chuột/phím phản hồi rất chậm. Kiểm tra phát hiện 4 điểm nghẽn đồng thời:
1. `ClientSession.cs`: Thuật toán `AdaptiveQuality` chỉ đo thời gian gửi qua socket mạng (`writeMs`). Trong mạng LAN, `writeMs` luôn dưới 2ms, khiến thuật toán tưởng hệ thống rất rảnh và tiếp tục ép CPU 30 FPS liên tục dù thời gian nén JPEG mất 25–35ms.
2. `MouseInjector.Button`: Còn gọi `X11.XSync(_display, false)` (blocking round-trip đến Xorg server). Mỗi nấc cuộn chuột sinh 2 sự kiện button (10 nấc = 20 lần XSync chặn luồng).
3. `RemoteScreenViewModel.cs`: Cấp phát `new WriteableBitmap` mỗi frame (157 MB/s ở 30 FPS 1280x1024), kích hoạt Full GC Gen 2 liên tục trên CCU.
4. Quản lý dịch vụ ZcuAgent: Dịch vụ `ipgs-remote-agent` được cài đặt dưới dạng `systemd --user` (thuộc user ccu, Display :0), nếu can thiệp bằng `sudo systemctl` sẽ báo lỗi unit not found.

**Cách xử lý:**
1. **CPU-Aware Adaptive FPS (`ClientSession.cs`):**
   - Tính tổng chu kỳ khung hình `cycleMs = sw.ElapsedMilliseconds` (bao gồm Ping check + Capture + Crop + Encode + Write).
   - Xác định nghẽn khi: `(writeMs > SlowWriteMs) || (cycleMs > frameInterval * 0.85)`.
   - Khi nghẽn liên tục 3 frame: tự động hạ trần FPS (`curFps * 0.75`) và chất lượng JPEG để CPU có thời gian nghỉ (`sleep > 0`).
2. **Loại bỏ `XSync` trong `MouseInjector.Button`:**
   - Chỉ giữ `X11.XFlush(_display)`, bỏ `X11.XSync(_display, false)` tương tự như đã làm với `Move()`.
3. **Ping-Pong Buffer & WriteableBitmap Pool (`RemoteScreenViewModel.cs`):**
   - Tái sử dụng bitmap qua slot atomic `_recycledBitmap`. Sau 2 frame đầu tiên, lượng cấp phát bộ nhớ giảm về đúng 0 B/giây.
4. **Latest-Wins Coalescing chuột (`RemoteControlClient.cs`):**
   - Đóng gói toạ độ di chuột vào atomic 64-bit `_pendingMouseMove`. Khi socket bận, toạ độ cũ được ghi đè tự động, không dồn ứ hàng đợi TCP.
5. **Kết quả:** CPU idle máy con tăng vọt từ 0% lên **65% – 70% IDLE**; tải CPU của ZcuAgent giảm từ 95.9% xuống còn **8.0% – 12.9% CPU**; thao tác chuột và cuộn trang nhạy bén tức thì.

**Lần đầu gặp:** WF-BUGFIX / Triệt tiêu 5 điểm nghẽn hiệu năng trên ZCU `192.168.21.56` (2026-11-04)
---

## G027 — Bắt tay TLS tự ký thất bại (`sslv3 alert certificate unknown`) do `SslClientAuthenticationOptions` không tự kế thừa callback từ constructor của `SslStream`

**Ngày phát hiện:** 2026-11-04
**Môi trường:** .NET 8 / 9 `System.Net.Security.SslStream`, Windows 11 CCU Client, Linux ZCU Agent

**Vấn đề:**
Sau khi bật rào chắn chống hạ cấp TLS (G026 / Step 5.5), CCU Client không thể kết nối tới ZCU Agent qua TLS. Phía Linux ZcuAgent báo lỗi:
```text
System.IO.IOException: The decryption operation failed, see inner exception.
---> Interop+Crypto+OpenSslCryptographicException: error:0A000416:SSL routines::sslv3 alert certificate unknown
```
Client báo `TLS Certificate Fingerprint Mismatch` hoặc từ chối kết nối, khiến giao diện đứng yên không stream được.

**Nguyên nhân:**
1. Trong .NET (`System.Net.Security`), khi khởi tạo `new SslStream(stream, false, userCertificateValidationCallback: callback)` nhưng lại gọi phương thức quá tải `AuthenticateAsClientAsync(SslClientAuthenticationOptions options, ...)`, .NET **bỏ qua hoàn toàn callback đã truyền vào constructor của SslStream** nếu thuộc tính `options.RemoteCertificateValidationCallback` để trống (`null`). Thay vào đó, .NET chạy trình xác thực chuỗi chứng chỉ mặc định của Windows (`X509Chain`) đối chiếu với Windows Root CA store.
2. Vì chứng chỉ của ZcuAgent là tự ký (`Self-Signed ECDSA/RSA`), Windows mặc định đánh trượt với lỗi `UntrustedRoot` và gửi gói tin TLS Alert `certificate unknown (46)` về cho server, làm hỏng bắt tay TLS.
3. Khi ZcuAgent được cài đặt lại hoặc sinh chứng chỉ mới trên máy con, vân tay cũ đã lưu trong `profiles.json` (từ nhiều ngày trước) bị lệch pha với vân tay thực tế của server. Trước đây client âm thầm nuốt lỗi và lùi về TCP plaintext; khi chốt chặn cấm lùi về plaintext được kích hoạt, kết nối bị dừng lại đúng như thiết kế bảo mật.

**Cách xử lý:**
1. **Gán trực tiếp callback vào `SslClientAuthenticationOptions` (`RemoteControlClient.cs`):**
   ```csharp
   var authOptions = new SslClientAuthenticationOptions
   {
       TargetHost = _host,
       EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
       RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
       {
           if (cert is null) return false;
           var hash = cert.GetCertHashString(HashAlgorithmName.SHA256);
           if (!string.IsNullOrEmpty(ExpectedCertFingerprint))
           {
               if (!string.Equals(hash, ExpectedCertFingerprint, StringComparison.OrdinalIgnoreCase))
               {
                   _logger.LogWarning("TLS Certificate Fingerprint Mismatch! Expected {Expected}, got {Actual}",
                       ExpectedCertFingerprint, hash);
                   return false;
               }
           }
           TlsCertificateValidated?.Invoke(this, hash);
           return true; // Chấp nhận chứng chỉ tự ký khi fingerprint hợp lệ
       }
   };
   await ssl.AuthenticateAsClientAsync(authOptions, cts.Token);
   ```
2. **Cập nhật vân tay chứng chỉ trong hồ sơ (`profiles.json`):**
   Khi ZcuAgent đổi chứng chỉ, cập nhật lại `TlsCertFingerprint` trong `profiles.json` theo đúng vân tay mới của server.
3. **Kết quả:** Bắt tay TLS 1.3 (`TLS_AES_256_GCM_SHA384`) thành công 100%, bảo mật mã hoá toàn diện và xác thực đúng vân tay ghim.

**Lần đầu gặp:** WF-BUGFIX / Khắc phục lỗi bắt tay TLS trên CCU Client (2026-11-04)

---

## G028 — "Cài remote" xoá `agent-tls.pfx` → agent sinh chứng chỉ mới, CCU từ chối TLS do vân tay ghim cũ → màn hình điều khiển đen, "Đã ngắt kết nối"

**Ngày phát hiện:** 2026-11-04
**Môi trường:** ZCU `192.168.21.56`, CCU Client có chốt chặn cấm lùi về TCP plaintext (G027)

**Vấn đề:**
Sau khi cài lại ZcuAgent, mở màn hình điều khiển chỉ thấy đen + "Đã ngắt kết nối" (thử lại liên tục). Danh sách máy vẫn báo "Remote" xanh vì chỉ dò cổng 17600. Log agent: `sslv3 alert certificate unknown` (client gửi alert).

**Nguyên nhân:**
1. `ZcuRemoteInstallerService` dọn thư mục `~/ipgs/remote-agent` bằng `find … ! -name appsettings.json -exec rm -rf` → xoá luôn `agent-tls.pfx` → agent tự sinh chứng chỉ mới (vân tay mới).
2. `ComputerProfileStore.UpdateTlsFingerprint` chỉ ghi khi trường trống; `Save` và "Quét mạng" đều giữ vân tay cũ → CCU ghim mãi vân tay cũ, callback TLS trả false, không còn lùi về plaintext.

**Cách xử lý:**
1. Installer giữ `agent-tls.pfx` khi dọn thư mục (`! -name agent-tls.pfx`).
2. Sau khi cài xong (Setup Wizard và Bulk "Cài remote"): `ZcuAgentDiscoveryService.ProbeTlsFingerprintAsync` đọc vân tay hiện tại của agent → `ComputerProfileStore.ReplaceTlsFingerprint` ghi đè; không đọc được thì bỏ ghim để lần kết nối đầu tự ghim lại (TOFU).
3. Máy đang lỗi: cập nhật `TlsCertFingerprint` trong `%APPDATA%\iPGS\RemoteControl\profiles.json` theo `openssl s_client -connect 127.0.0.1:17600 </dev/null | openssl x509 -noout -fingerprint -sha256` trên ZCU (bỏ dấu `:`).

**Lần đầu gặp:** WF-BUGFIX / Không xem được màn hình ZCU `192.168.21.56` sau khi cài lại agent (2026-11-04)
---

## G029 — Lỗi regex Dashboard `([\d\.]+)` bỏ qua dấu phẩy thập phân locale `vi_VN` trong lệnh `top` và spike mẫu 1 khiến tính khống CPU lên 93%–99%

**Ngày phát hiện:** 2026-11-04
**Môi trường:** Linux Ubuntu / Lubuntu có locale `LC_NUMERIC=vi_VN`, Windows CCU Client `HealthMonitorWindow.axaml.cs`

**Vấn đề:**
Trên cửa sổ Dashboard Giám sát hệ thống (Health Monitor), chỉ số tải CPU tổng luôn hiển thị mức rất cao (93.0% – 99.0%), trong khi kiểm tra thực tế bằng `vmstat` hoặc `top` trực tiếp qua SSH thì CPU máy đích đang rảnh hơn 70% – 95% IDLE. Đồng thời bảng Top Processes hiển thị chính tiến trình `top` chiếm 11.1% CPU.

**Nguyên nhân:**
1. **Lỗi đọc số thập phân do locale tiếng Việt:** Trên máy Linux cấu hình `LC_NUMERIC=vi_VN`, lệnh `top` định dạng số thập phân bằng dấu phẩy `,` (ví dụ: `%Cpu(s): 21,2 us, 11,2 sy, 0,0 ni, 67,5 id` hoặc `92,7 id`). Đoạn code phân tích tại `HealthMonitorWindow.axaml.cs` dòng 380 sử dụng biểu thức chính quy `Regex.Match(cpuLine, @"([\d\.]+)\s*id")` chỉ chấp nhận số và dấu chấm `.`. Khi gặp chuỗi `92,7 id`, regex bỏ qua phần nguyên `92,` và chỉ bắt được chữ số `7` đứng ngay trước ` id`. Sau đó code tính: `totalCpu = 100.0 - 7.0 = 93.0%` (hoặc `67,5 id` chỉ bắt `5` -> `100 - 5 = 95.0%`).
2. **Lấy mẫu `top -b -n 1` tức thời:** Lần lấy mẫu đầu tiên của `top` tính toán tải trong khoảng thời gian khởi tạo process cực ngắn, dẫn đến hiện tượng vọt CPU tạm thời (startup spike), khiến bản thân tiến trình `top` bị tính là ăn 11% CPU.

**Cách xử lý:**
1. **Tách lớp `TopParser.cs` thuần túy (`IPGS.RemoteControl.CcuClient`):**
   - Lệnh thực thi: Ép môi trường `LC_ALL=C top -b -n 2 -d 0.5` để đảm bảo Linux trả về output chuẩn tiếng Anh và số thập phân dấu chấm.
   - Trích xuất mẫu steady-state: Lấy dòng `%Cpu` và bảng tiến trình từ lần lấy mẫu thứ 2 (`LastOrDefault(l => l.StartsWith("%Cpu"))` và `Array.FindLastIndex(...)`), loại bỏ hoàn toàn độ lệch 11% của `top`.
   - Cơ chế phòng thủ kép: Biểu thức chính quy nâng cấp thành `([\d.,]+)\s*id` và tự động `.Replace(',', '.')` trước khi parse bằng `CultureInfo.InvariantCulture`, hoạt động đúng đắn trên mọi locale của mọi bản phân phối Linux.
2. **Kết quả:** Dashboard hiển thị chỉ số CPU chính xác 100% theo thời gian thực (đo trên `192.168.21.56` hiển thị 1.9% CPU, rảnh 98.1% IDLE). Toàn bộ 140 unit tests đạt 100% PASS.

**Lần đầu gặp:** WF-BUGFIX / Sửa lỗi hiển thị CPU Dashboard trên ZCU `192.168.21.56` (2026-11-04)

---

## G030 — Dán file CCU sang ZCU: thiếu delay giữa các sự kiện phím khiến X11/Qt bỏ qua ControlModifier, file bị kẹt lại tại thư mục tạm staging

**Ngày phát hiện:** 2026-11-06  
**Môi trường:** CCU Client (`RemoteScreenControl.axaml.cs`), Linux X11 (PCManFM-Qt, Nautilus), .NET 8 / Avalonia 12

**Vấn đề:**  
Khi copy file từ Windows rồi dán (Ctrl+V) sang cửa sổ remote ZCU (đang mở thư mục trong PCManFM-Qt hoặc file manager khác):
- File được upload thành công sang ZCU và nằm trong thư mục staging `~/RemoteTransfer/.paste-xxxx/`.
- Clipboard X11 của ZCU đã giữ `text/uri-list` chính xác.
- Tuy nhiên, file KHÔNG hề được dán vào thư mục người dùng đang chọn/mở trên ZCU, mà chỉ nằm lại trong thư mục tạm `RemoteTransfer/.paste-...`, khiến người dùng nghĩ rằng hệ thống dán vào thư mục cố định thay vì thư mục đang chọn.

**Nguyên nhân:**  
Trong `RemoteScreenControl.PasteAsync`, sau khi upload xong file và đặt clipboard trên ZCU, client kích hoạt phím Ctrl+V bằng cách gửi liên tiếp các lệnh `SendKeyEvent(Control_L, true)`, `HandleKeyDown(V)`, `HandleKeyUp(V)`, `SendKeyEvent(Control_L, false)` với 0ms delay giữa các sự kiện. Do các sự kiện TCP đến dồn dập trong cùng micro-giây, Xorg server và bộ lọc sự kiện của Qt/X11 (`pcmanfm-qt`) chưa kịp cập nhật modifier state vector (`ControlMask`) thì đã nhận phím 'V', khiến ứng dụng hiểu là người dùng bấm phím 'v' thông thường (nhảy tìm kiếm file) thay vì tổ hợp phím Paste `Ctrl+V`.

**Cách xử lý:**  
1. Bổ sung khoảng đệm thời gian `await Task.Delay(100)` giữa việc nhấn giữ Ctrl và gửi phím V.
2. Giữ phím V 100ms trước khi gửi sự kiện nhả phím V.
3. Nhả Ctrl sau 100ms tiếp theo nếu người dùng không còn giữ phím Ctrl vật lý trên Windows.
4. Đã xác minh trên ZCU `192.168.21.56`: lệnh paste trong PCManFM-Qt lập tức nhân bản file từ `RemoteTransfer/.paste-...` vào đúng thư mục đang mở (`/opt/kztek/ipgsv4/`).

**Lần đầu gặp:** Báo cáo người dùng 2026-11-06 ("copy/paste từ máy remote sang máy được remote bị nhảy vào thư mục cố định").

---

## G031 — Nhận diện Headless trên máy ZCU có cấu hình `video=HDMI-A-1:e`: cổng HDMI vẫn báo `connected` khi rút cáp nhưng kích thước vật lý là `0mm x 0mm` và rơi về fallback `1024x768`

**Ngày phát hiện:** 2026-11-10  
**Môi trường:** Linux ZCU (`192.168.21.230`, Intel i915 DRM với tham số kernel `video=HDMI-A-1:e`), Xorg Server, `xrandr`

**Vấn đề:**  
1. Khi rút cáp màn hình vật lý ra khỏi máy ZCU, độ phân giải X11 tự động rơi về `1024x768` (tỉ lệ 4:3) thay vì giữ Full HD `1920x1080` hoặc tỉ lệ 5:4 (`1280x1024`) của màn hình cũ.
2. Các bộ kiểm tra headless chỉ nhìn trạng thái chuỗi `"disconnected"` trong `xrandr` đều thất bại vì tham số kernel `video=HDMI-A-1:e` (áp dụng theo G019 để giữ GNOME Mutter vẽ hình) ép cổng `HDMI-1` luôn báo `connected` ngay cả khi không cắm cáp.

**Nguyên nhân:**  
Khi kernel ép cổng `HDMI-A-1` bật (`e`) nhưng không có màn hình thực cắm vào, giao tiếp DDC/I2C không đọc được bảng EDID nào. Do đó Xorg không biết thông số màn hình, hiển thị kích thước vật lý `0mm x 0mm`, không có mode nào mang cờ `+` (preferred EDID mode) và chỉ nạp 4 mode VESA fallback cơ bản (`1024x768`, `800x600`, `848x480`, `640x480`).

**Cách xử lý:**  
1. **Phía ZcuAgent (`HeadlessResolutionAdapter.cs`):**
   - Phân biệt chính xác **màn hình vật lý thật** (`IsConnected && ((PhysicalWidthMm > 0 && PhysicalHeightMm > 0) || PreferredEdidMode != null)`) với **cổng Headless không EDID** (`0mm x 0mm` và không có mode `+`).
   - Khi phát hiện Headless không EDID: tự động tạo mode CVT `1920x1080_60.00`, gắn vào cổng và kích hoạt `1920x1080` (16:9).
   - Khi cắm lại màn hình vật lý thực (có EDID): phát hiện cổng đang chạy mode ảo `1920x1080_60.00` và tự động gọi `xrandr --output <cổng> --auto` để khôi phục đúng độ phân giải chuẩn của màn hình vật lý.
2. **Phía CCU (`RemoteScreenViewModel.cs` & `RemoteScreenWindow.axaml.cs`):**
   - Bổ sung chế độ `RemoteDisplayMode.AutoFit` tự động điều chỉnh kích thước cửa sổ CCU khớp chính xác tỷ lệ `Width / Height` của máy đích ngay khi nhận frame hoặc khi máy đích đổi độ phân giải, loại bỏ viền đen thừa mà không gây méo hình.

**Lần đầu gặp:** WF-FEATURE / Tự động nhận diện và điều chỉnh độ phân giải theo cấu hình máy remote trên `192.168.21.230` (2026-11-10)

---

## G032 (G029-WinSSH) — Windows OpenSSH Server (`sshd`): lỗi `Event 4625 NOUSER` khi `DefaultShell` là đường dẫn tương đối, lỗi `-NoLogo is not recognized` do `DefaultShellCommandOption`, và lỗi quyền NTFS `sshd_config` (`icacls`)

**Ngày phát hiện:** 2026-11-11  
**Môi trường:** Windows 10/11 (`192.168.21.14`), Windows Native OpenSSH Server (`OpenSSH.Server~~~~0.0.1.0`), `bootstrap-windows-ssh.ps1`, `autounattend.xml`

**Vấn đề:**  
Khi kiểm nghiệm thực tế cài đặt từ xa lên máy Windows `192.168.21.14` qua SSH:
1. Dù tài khoản `kztek` (mật khẩu `123456`) đã tồn tại và thuộc nhóm `Administrators`, đăng nhập SSH vẫn bị từ chối mật khẩu (`Permission denied, please try again`) và Windows Security Event Log ghi nhận `Event ID 4625 (Status: 0xC0000064 - NOUSER / invalid shell)`.
2. Khi kết nối SSH chạy lệnh từ xa (`ssh kztek@192.168.21.14 "uname -s"`), PowerShell báo lỗi: `-NoLogo : The term '-NoLogo' is not recognized as the name of a cmdlet...`.
3. Dịch vụ `sshd` từ chối khởi động hoặc bỏ qua cấu hình nếu file `C:\ProgramData\ssh\sshd_config` được tạo mới bằng PowerShell mà kế thừa quyền NTFS mặc định của thư mục cha hoặc còn giữ khối `Match Group administrators`.

**Nguyên nhân:**  
1. **`DefaultShell` đường dẫn tương đối:** Khi đặt `HKLM:\SOFTWARE\OpenSSH\DefaultShell = "powershell.exe"` (tương đối, không có đường dẫn đầy đủ), `sshd.exe` kiểm tra sự tồn tại của binary shell bằng `GetFileAttributesW` trực tiếp trên chuỗi cấu hình trước khi chấp nhận user. Vì `"powershell.exe"` không phải đường dẫn tuyệt đối nên kiểm tra thất bại, `sshd` đánh dấu user là `FAKESH`/`NOUSER` và từ chối mật khẩu ngay lập tức.
2. **`DefaultShellCommandOption = "-NoLogo -Command"`:** Khi OpenSSH Server gọi shell để chạy lệnh từ xa, nó truyền toàn bộ chuỗi `DefaultShellCommandOption` như **một** đối số duy nhất (`argv[1]`) cho `powershell.exe` thay vì tách thành 2 cờ. Do đó PowerShell hiểu nhầm `"-NoLogo -Command"` là tên lệnh cần thực thi và báo lỗi `'-NoLogo' is not recognized`.
3. **Phân quyền NTFS `sshd_config` và `Match Group administrators`:** OpenSSH trên Windows kiểm tra nghiêm ngặt ACL của `C:\ProgramData\ssh\sshd_config` (chỉ cho phép `SYSTEM` và `Administrators` có quyền ghi, không kế thừa quyền `Users` từ thư mục cha). Ngoài ra khối `Match Group administrators` mặc định ép dùng `administrators_authorized_keys` dễ gây xung đột xác thực trên máy trạm LAN.

**Cách xử lý:**  
1. **Đường dẫn tuyệt đối cho `DefaultShell`:** Luôn đặt `HKLM:\SOFTWARE\OpenSSH\DefaultShell` = `"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"`.
2. **Xóa bỏ `DefaultShellCommandOption`:** Tuyệt đối không gán `DefaultShellCommandOption`, đồng thời chạy `Remove-ItemProperty -Path "HKLM:\SOFTWARE\OpenSSH" -Name "DefaultShellCommandOption" -ErrorAction SilentlyContinue` (mặc định `sshd` tự truyền `-c` / `-Command` chuẩn xác cho `powershell.exe`).
3. **Ghi `sshd_config` sạch & chuẩn hóa ACL bằng `icacls`:**
   - Ghi `C:\ProgramData\ssh\sshd_config` chỉ gồm 5 dòng sạch (`Port 22`, `ListenAddress 0.0.0.0`, `PasswordAuthentication yes`, `PubkeyAuthentication yes`, `Subsystem sftp sftp-server.exe`), bỏ hoàn toàn `Match Group administrators`.
   - Chạy lệnh chuẩn hóa ACL:
     ```powershell
     & icacls.exe $sshdConfigPath /reset | Out-Null
     & icacls.exe $sshdConfigPath /grant "SYSTEM:(F)" /grant "Administrators:(F)" /inheritance:r | Out-Null
     ```
4. **Nhận diện tức thì Windows qua SSH Banner:** Trong `ZcuRemoteInstallerService.DetectRemoteOs(SshClient ssh)`, kiểm tra ngay `ssh.ConnectionInfo?.ServerVersion` chứa `"Windows"` (`SSH-2.0-OpenSSH_for_Windows_...`) để trả về `RemoteOsType.Windows` trong 0ms mà không cần phụ thuộc vào shell.

**Lần đầu gặp:** Thực nghiệm triển khai Zero-Touch Windows Remote Provisioning trên máy thật `192.168.21.14` (2026-11-11)

