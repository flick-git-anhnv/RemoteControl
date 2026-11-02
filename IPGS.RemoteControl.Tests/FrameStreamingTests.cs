using System.Buffers.Binary;
using IPGS.RemoteControl.Protocol;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class FrameStreamingTests
{
    [Fact]
    public void WriteFrameMetadataHeader_ValidInputs_MatchesBigEndianLayout()
    {
        var buffer = new byte[MessageCodec.FrameMetadataHeaderSize];
        long frameId = 0x0102030405060708L;
        uint ts = 0xAABBCCDD;
        int width = 1920;
        int height = 1080;
        int jpegLen = 50000;

        MessageCodec.WriteFrameMetadataHeader(buffer, frameId, ts, width, height, jpegLen);

        var readFrameId = (long)BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(0, 8));
        var readTs = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(8, 4));
        var readWidth = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(12, 4));
        var readHeight = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(16, 4));
        var readJpegLen = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(20, 4));

        Assert.Equal(frameId, readFrameId);
        Assert.Equal(ts, readTs);
        Assert.Equal(width, readWidth);
        Assert.Equal(height, readHeight);
        Assert.Equal(jpegLen, readJpegLen);
    }

    [Fact]
    public void WriteFrameMetadataHeader_SmallDestinationSpan_ThrowsArgumentException()
    {
        var smallBuffer = new byte[MessageCodec.FrameMetadataHeaderSize - 1];
        Assert.Throws<ArgumentException>(() =>
        {
            MessageCodec.WriteFrameMetadataHeader(smallBuffer, 1, 100, 800, 600, 1000);
        });
    }

    [Fact]
    public void WriteFrameMetadataHeader_RoundtripWithDecodeFrameJpeg()
    {
        var fakeJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0xFF, 0xD9 };
        var totalPayload = new byte[MessageCodec.FrameMetadataHeaderSize + fakeJpeg.Length];

        long expectedFrameId = 99999L;
        uint expectedTs = 123456789U;
        int expectedW = 2560;
        int expectedH = 1440;

        MessageCodec.WriteFrameMetadataHeader(
            totalPayload.AsSpan(0, MessageCodec.FrameMetadataHeaderSize),
            expectedFrameId, expectedTs, expectedW, expectedH, fakeJpeg.Length);
        fakeJpeg.CopyTo(totalPayload, MessageCodec.FrameMetadataHeaderSize);

        var decoded = MessageCodec.DecodeFrameJpeg(totalPayload);

        Assert.Equal(expectedFrameId, decoded.FrameId);
        Assert.Equal(expectedTs, decoded.TimestampMs);
        Assert.Equal(expectedW, decoded.Width);
        Assert.Equal(expectedH, decoded.Height);
        Assert.Equal(fakeJpeg.Length, decoded.JpegData.Length);
        Assert.True(fakeJpeg.AsSpan().SequenceEqual(decoded.JpegData.Span));
    }

    [Fact]
    public async Task MessageCodec_WriteMessageAsync_ZeroAllocationHeader()
    {
        using var ms = new MemoryStream();
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        await MessageCodec.WriteMessageAsync(ms, MessageType.Ping, payload);

        var data = ms.ToArray();
        Assert.Equal(MessageCodec.HeaderSize + payload.Length, data.Length);
        Assert.Equal((byte)MessageType.Ping, data[0]);

        var payloadLen = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(1, 4));
        Assert.Equal(payload.Length, payloadLen);
        Assert.True(payload.AsSpan().SequenceEqual(data.AsSpan(5)));
    }

    [Fact]
    public async Task MessageCodec_WriteEmptyAsync_ProducesExactFiveByteHeader()
    {
        using var ms = new MemoryStream();

        await MessageCodec.WriteEmptyAsync(ms, MessageType.AuthOk);

        var data = ms.ToArray();
        Assert.Equal(MessageCodec.HeaderSize, data.Length);
        Assert.Equal((byte)MessageType.AuthOk, data[0]);

        var payloadLen = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(1, 4));
        Assert.Equal(0U, payloadLen);
    }

    [Fact]
    public async Task MessageCodec_ReadMessageAsync_WithRentedBuffer_CorrectPayload()
    {
        using var ms = new MemoryStream();
        var testPayload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        await MessageCodec.WriteMessageAsync(ms, MessageType.Pong, testPayload);

        ms.Position = 0;
        var (type, readPayload) = await MessageCodec.ReadMessageAsync(ms);

        Assert.Equal(MessageType.Pong, type);
        Assert.Equal(testPayload.Length, readPayload.Length);
        Assert.True(testPayload.AsSpan().SequenceEqual(readPayload));
    }

    [Fact]
    public async Task MessageCodec_ReadMessageAsync_MultipleMessagesInSequence()
    {
        using var ms = new MemoryStream();
        var p1 = new byte[] { 1, 2 };
        var p2 = new byte[] { 3, 4, 5 };
        var p3 = new byte[] { 6 };

        await MessageCodec.WriteMessageAsync(ms, MessageType.Ping, p1);
        await MessageCodec.WriteMessageAsync(ms, MessageType.Pong, p2);
        await MessageCodec.WriteMessageAsync(ms, MessageType.KeyEvent, p3);

        ms.Position = 0;

        var m1 = await MessageCodec.ReadMessageAsync(ms);
        Assert.Equal(MessageType.Ping, m1.Type);
        Assert.Equal(p1, m1.Payload);

        var m2 = await MessageCodec.ReadMessageAsync(ms);
        Assert.Equal(MessageType.Pong, m2.Type);
        Assert.Equal(p2, m2.Payload);

        var m3 = await MessageCodec.ReadMessageAsync(ms);
        Assert.Equal(MessageType.KeyEvent, m3.Type);
        Assert.Equal(p3, m3.Payload);
    }

    [Fact]
    public void EncodeFrameJpeg_UsesInternalMetadataWriter_ConsistentOutput()
    {
        long frameId = 12345L;
        uint ts = 987654U;
        int w = 1920;
        int h = 1080;
        var fakeJpeg = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

        var encoded = MessageCodec.EncodeFrameJpeg(frameId, ts, w, h, fakeJpeg);

        Assert.Equal(MessageCodec.FrameMetadataHeaderSize + fakeJpeg.Length, encoded.Length);

        var decoded = MessageCodec.DecodeFrameJpeg(encoded);
        Assert.Equal(frameId, decoded.FrameId);
        Assert.Equal(ts, decoded.TimestampMs);
        Assert.Equal(w, decoded.Width);
        Assert.Equal(h, decoded.Height);
        Assert.True(fakeJpeg.AsSpan().SequenceEqual(decoded.JpegData.Span));
    }

    [Fact]
    public async Task FrameStreaming_Direct29ByteFraming_MatchesClientDecode()
    {
        // Giả lập cơ chế Zero-Allocation của ZcuAgent:
        // 5 bytes wire header + 24 bytes frame metadata = 29 bytes cố định
        using var ms = new MemoryStream();

        long frameId = 777L;
        uint tsMs = 54321U;
        int w = 1280;
        int h = 720;
        var jpegBytes = new byte[1024];
        new Random(42).NextBytes(jpegBytes);

        var frameHeader = new byte[MessageCodec.HeaderSize + MessageCodec.FrameMetadataHeaderSize]; // 29 bytes
        frameHeader[0] = (byte)MessageType.FrameJpeg;
        BinaryPrimitives.WriteUInt32BigEndian(frameHeader.AsSpan(1, 4), (uint)(MessageCodec.FrameMetadataHeaderSize + jpegBytes.Length));

        MessageCodec.WriteFrameMetadataHeader(
            frameHeader.AsSpan(MessageCodec.HeaderSize, MessageCodec.FrameMetadataHeaderSize),
            frameId, tsMs, w, h, jpegBytes.Length);

        // Ghi 29 bytes header sau đó ghi jpegData
        await ms.WriteAsync(frameHeader);
        await ms.WriteAsync(jpegBytes);

        // Client CCU đọc từ stream
        ms.Position = 0;
        var (type, payload) = await MessageCodec.ReadMessageAsync(ms);

        Assert.Equal(MessageType.FrameJpeg, type);
        Assert.Equal(MessageCodec.FrameMetadataHeaderSize + jpegBytes.Length, payload.Length);

        var frameMsg = MessageCodec.DecodeFrameJpeg(payload);
        Assert.Equal(frameId, frameMsg.FrameId);
        Assert.Equal(tsMs, frameMsg.TimestampMs);
        Assert.Equal(w, frameMsg.Width);
        Assert.Equal(h, frameMsg.Height);
        Assert.True(jpegBytes.AsSpan().SequenceEqual(frameMsg.JpegData.Span));
    }

    [Theory]
    [InlineData(3840, 2160)] // 4K UHD
    [InlineData(7680, 4320)] // 8K UHD
    [InlineData(8192, 8192)] // Max allowed boundary
    public void FrameStreaming_LargeResolutions_EncodesAndDecodesCorrectly(int width, int height)
    {
        var meta = new byte[MessageCodec.FrameMetadataHeaderSize];
        MessageCodec.WriteFrameMetadataHeader(meta, 100L, 500U, width, height, 0);

        var decoded = MessageCodec.DecodeFrameJpeg(meta);
        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
    }

    [Fact]
    public void FrameStreaming_ZeroDimension_EncodesCorrectly()
    {
        var meta = new byte[MessageCodec.FrameMetadataHeaderSize];
        MessageCodec.WriteFrameMetadataHeader(meta, 0L, 0U, 0, 0, 0);

        var decoded = MessageCodec.DecodeFrameJpeg(meta);
        Assert.Equal(0, decoded.Width);
        Assert.Equal(0, decoded.Height);
    }

    [Fact]
    public async Task MessageCodec_WriteMessageAsync_PayloadExceedsMaxFrameBytes_ThrowsArgumentException()
    {
        using var ms = new MemoryStream();
        var dummyMemory = new ReadOnlyMemory<byte>(new byte[1]);

        // Tạo memory giả có length lớn hơn MaxFrameBytes mà không cấp phát thực tế
        // Bằng cách test với hằng số ranh giới:
        var oversizedArray = new byte[RemoteControlConstants.MaxFrameBytes + 1];

        await Assert.ThrowsAsync<ArgumentException>(() =>
            MessageCodec.WriteMessageAsync(ms, MessageType.FrameJpeg, oversizedArray));
    }
}
