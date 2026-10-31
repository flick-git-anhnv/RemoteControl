# Bước 2.1 — Xây Dựng Bộ Unit Test Tự Động Cho Zero-Allocation Frame Streaming

- **Agent:** Junior Developer
- **Status:** done
- **Mục tiêu:** Mở rộng bộ kiểm thử tự động vượt mốc 100 tests, bảo đảm độ phủ trọn vẹn.

## Nội dung thực hiện
1. Tạo tệp kiểm thử `IPGS.RemoteControl.Tests/FrameStreamingTests.cs` với **14 ca kiểm thử chuyên sâu**:
   - `WriteFrameMetadataHeader_ValidInputs_MatchesBigEndianLayout`: Kiểm tra đúng cấu trúc big-endian của metadata.
   - `WriteFrameMetadataHeader_SmallDestinationSpan_ThrowsArgumentException`: Kiểm tra kiểm soát biên độ span.
   - `WriteFrameMetadataHeader_RoundtripWithDecodeFrameJpeg`: Kiểm tra round-trip giữa serialization và deserialization.
   - `MessageCodec_WriteMessageAsync_ZeroAllocationHeader`: Kiểm tra tính toàn vẹn của rented wire header.
   - `MessageCodec_WriteEmptyAsync_ProducesExactFiveByteHeader`: Kiểm tra header 5-byte cho thông điệp không payload.
   - `MessageCodec_ReadMessageAsync_WithRentedBuffer_CorrectPayload`: Kiểm tra đọc payload từ memory pool.
   - `MessageCodec_ReadMessageAsync_MultipleMessagesInSequence`: Kiểm tra đọc chuỗi thông điệp tuần tự.
   - `EncodeFrameJpeg_UsesInternalMetadataWriter_ConsistentOutput`: Kiểm tra tính nhất quán giữa hai phương thức.
   - `FrameStreaming_Direct29ByteFraming_MatchesClientDecode`: Giả lập đầy đủ luồng 29 bytes trực tiếp từ ZCU sang CCU.
   - `FrameStreaming_LargeResolutions_EncodesAndDecodesCorrectly`: Kiểm thử độ phân giải 4K (3840x2160) và 8K (7680x4320).
   - `FrameStreaming_ZeroDimension_EncodesCorrectly`: Kiểm thử ranh giới kích thước 0.
   - `MessageCodec_WriteMessageAsync_PayloadExceedsMaxFrameBytes_ThrowsArgumentException`: Kiểm tra giới hạn 8MB.
2. Tổng số bài test tự động của giải pháp tăng từ 94 lên **108 unit tests pass 100%**.
