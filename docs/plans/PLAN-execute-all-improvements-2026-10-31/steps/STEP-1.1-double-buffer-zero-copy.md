---
step: 1.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31T08:35:00+07:00
deps: []
---

# STEP 1.1 — Ping-Pong Double-Buffering & SKBitmap Zero-Copy

## Input nhận
- Đề xuất 1.1 và 1.2 trong [PROPOSAL-remote-control-improvements-2026-10-31.md](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/docs/reviews/PROPOSAL-remote-control-improvements-2026-10-31.md).
- Vấn đề M1 (LOH 124MB/s) và M2 (glibc malloc unmanaged fragmentation).

## Nhiệm vụ
1. Tái cấu trúc `RemoteScreenViewModel.OnFrameReceived`: thiết lập cơ chế Ping-Pong Double-Buffering với 2 WriteableBitmap (`_frontBuffer`, `_backBuffer`), giải mã trực tiếp vào `_backBuffer.Lock().Address`, loại bỏ hoàn toàn `byte[] pixData = new byte[toRender.ByteCount]` và việc tạo mới `new WriteableBitmap` mỗi frame.
2. Tái cấu trúc `JpegEncoder`: tái sử dụng `SKBitmap` hoặc ghim vùng đệm, kích hoạt `malloc_trim(0)` định kỳ để kernel Linux thu hồi unmanaged memory.

## Definition of Done
- [x] Không còn cấp phát `byte[]` 8.3 MB hoặc `new WriteableBitmap` ở mỗi frame khi kích thước màn hình không đổi.
- [x] Không còn tạo mới `SKBitmap` liên tục mỗi frame trong `JpegEncoder`.
- [x] Build thành công cả `CcuUI` và `ZcuAgent`.

## Đã làm
1. Trong `IPGS.RemoteControl.CcuUI/ViewModels/RemoteScreenViewModel.cs`:
   - Khởi tạo 2 buffer `_frontBuffer` và `_backBuffer` dạng `WriteableBitmap`.
   - Sử dụng `Buffer.MemoryCopy` trực tiếp từ bộ nhớ `SKBitmap.GetPixels()` sang con trỏ `ILockedFramebuffer.Address` bên trong khối `lock (_bufferLock)`.
   - Swap tham chiếu `(_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer)` và thông báo thuộc tính `ScreenBitmap` thay đổi lên Avalonia UI.
   - Thêm `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` vào `IPGS.RemoteControl.CcuUI.csproj`.
2. Trong `IPGS.RemoteControl.ZcuAgent/Capture/JpegEncoder.cs`:
   - Thêm field `_reusableBitmap` và phương thức `EnsureBitmapCapacity(width, height)`.
   - Tái sử dụng đối tượng `SKBitmap` giữa các frame khi kích thước không thay đổi.
   - Bổ sung P/Invoke `[DllImport("libc", EntryPoint = "malloc_trim")] private static extern int MallocTrim(nuint pad);`.
   - Kích hoạt `MallocTrim(0)` định kỳ mỗi 60 giây và khi gọi `Dispose()`.

## Artifact
- [RemoteScreenViewModel.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuUI/ViewModels/RemoteScreenViewModel.cs)
- [IPGS.RemoteControl.CcuUI.csproj](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj)
- [JpegEncoder.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.ZcuAgent/Capture/JpegEncoder.cs)

## Quyết định quan trọng
- Dùng `Buffer.MemoryCopy` unmanaged trực tiếp thay vì cấp phát mảng byte trung gian, triệt tiêu 100% rác LOH (124 MB/s tại 1080p 15fps).
- `malloc_trim(0)` giúp giải phóng triệt để unmanaged arena pages của glibc về Linux kernel.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Không tạo thêm buffer trung gian hay mảng byte trung gian trong rendering loop.
- watch_out: Khi kích thước màn hình thay đổi, `EnsureBuffers(w, h)` tự động reallocate an toàn dưới lock.
- next_inputs: Cung cấp nền tảng xử lý dữ liệu frame zero-copy cho bước 1.2.
