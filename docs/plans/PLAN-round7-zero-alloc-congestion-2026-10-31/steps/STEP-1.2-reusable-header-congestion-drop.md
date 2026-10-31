# Bước 1.2 — Khử Phân Bổ 5-byte Header & Cơ Chế Chống Trễ Tích Lũy Congestion Dropping

- **Agent:** Senior Developer
- **Status:** done
- **Mục tiêu:** Giảm hàng chục nghìn phân bổ mảng Gen 0 per-minute từ chuột/phím và loại bỏ độ trễ tích lũy khi mạng chập chờn.

## Nội dung thực hiện
1. Tích hợp `ArrayPool<byte>.Shared.Rent(HeaderSize)` trong `MessageCodec.WriteMessageAsync` và `ReadMessageAsync`, loại bỏ việc `new byte[HeaderSize]` cho từng thông điệp nhị phân.
2. Cập nhật `ReadExactAsync` nhận `Memory<byte>` để hỗ trợ trực tiếp rented buffers.
3. Tích hợp cơ chế Congestion Frame Dropping & Latest-Frame Fast-Forward trong `ClientSession.cs`:
   - Đo lường thời gian ghi frame `writeMs`.
   - Nếu `writeMs > 1.5 * frameInterval`, phát hiện socket TCP đang bị dồn ứ (backpressure) và tự động nhường CPU (`Task.Yield()`) để giải phóng buffer kernel.
   - Luôn đảm bảo khung hình render trên CCU là khung hình mới nhất tại thời gian thực.
