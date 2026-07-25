using System.Buffers.Binary;
using System.Text;

namespace IPGS.RemoteControl.Protocol;

/// <summary>
/// Low-level framing helpers for the binary TCP protocol described in TDD §5.1.
/// <para>
/// Wire frame layout (big-endian):
/// <code>
///  Offset  Size   Field
///  ------  -----  -----------------------------------------------
///  0       1      MessageType  (enum byte)
///  1       4      PayloadLength  (uint32 BE, bytes in payload only)
///  5       N      Payload
/// </code>
/// </para>
/// All encode/decode methods are synchronous; framing I/O is async.
/// </summary>
public static class MessageCodec
{
    public const int HeaderSize = 5; // 1 (type) + 4 (length)

    // ── Framing I/O ───────────────────────────────────────────────────────

    /// <summary>Write a complete framed message to <paramref name="stream"/>.</summary>
    public static async Task WriteMessageAsync(
        Stream stream,
        MessageType type,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct = default)
    {
        if (payload.Length > RemoteControlConstants.MaxFrameBytes)
            throw new ArgumentException(
                $"Payload {payload.Length} B exceeds MaxFrameBytes {RemoteControlConstants.MaxFrameBytes} B");

        var header = new byte[HeaderSize];
        header[0] = (byte)type;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(1), (uint)payload.Length);

        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        if (payload.Length > 0)
            await stream.WriteAsync(payload, ct).ConfigureAwait(false);
    }

    /// <inheritdoc cref="WriteMessageAsync(Stream,MessageType,ReadOnlyMemory{byte},CancellationToken)"/>
    public static Task WriteMessageAsync(
        Stream stream, MessageType type, byte[] payload, CancellationToken ct = default)
        => WriteMessageAsync(stream, type, payload.AsMemory(), ct);

    /// <summary>Write a header-only message with zero payload (AUTH_OK, BYE, etc.).</summary>
    public static Task WriteEmptyAsync(Stream stream, MessageType type, CancellationToken ct = default)
        => WriteMessageAsync(stream, type, ReadOnlyMemory<byte>.Empty, ct);

    /// <summary>
    /// Read one complete framed message.
    /// Throws <see cref="EndOfStreamException"/> if the connection is closed mid-read,
    /// or <see cref="ProtocolException"/> if PayloadLength exceeds MaxFrameBytes.
    /// </summary>
    public static async Task<(MessageType Type, byte[] Payload)> ReadMessageAsync(
        Stream stream, CancellationToken ct = default)
    {
        var header = new byte[HeaderSize];
        await ReadExactAsync(stream, header, ct).ConfigureAwait(false);

        var type    = (MessageType)header[0];
        var length  = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(1));

        if (length > RemoteControlConstants.MaxFrameBytes)
            throw new ProtocolException(
                $"Payload length {length} B exceeds MaxFrameBytes {RemoteControlConstants.MaxFrameBytes} B — closing");

        var payload = new byte[length];
        if (length > 0)
            await ReadExactAsync(stream, payload, ct).ConfigureAwait(false);

        return (type, payload);
    }

    // ── Payload encoders ──────────────────────────────────────────────────

    /// <summary>Encode HELLO payload (TDD §5.2).</summary>
    public static byte[] EncodeHello(string clientName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(clientName);
        var buf = new byte[1 + 2 + nameBytes.Length];
        buf[0] = RemoteControlConstants.ProtocolVersion;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf, 3);
        return buf;
    }

    /// <summary>Encode HELLO_ACK payload (TDD §5.2).</summary>
    public static byte[] EncodeHelloAck(uint screenW, uint screenH, string serverName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(serverName);
        var buf = new byte[1 + 4 + 4 + 2 + nameBytes.Length];
        buf[0] = RemoteControlConstants.ProtocolVersion;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(1), screenW);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(5), screenH);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(9), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf, 11);
        return buf;
    }

    /// <summary>Encode AUTH payload (TDD §5.2). Token is sent as UTF-8 plaintext (v1).</summary>
    public static byte[] EncodeAuth(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var buf = new byte[2 + tokenBytes.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0), (ushort)tokenBytes.Length);
        tokenBytes.CopyTo(buf, 2);
        return buf;
    }

    /// <summary>Encode AUTH_FAIL payload.</summary>
    public static byte[] EncodeAuthFail(string reason)
    {
        var reasonBytes = Encoding.UTF8.GetBytes(reason);
        var buf = new byte[2 + reasonBytes.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0), (ushort)reasonBytes.Length);
        reasonBytes.CopyTo(buf, 2);
        return buf;
    }

    /// <summary>Encode FRAME_JPEG payload (TDD §5.2).</summary>
    public static byte[] EncodeFrameJpeg(long frameId, uint timestampMs, int width, int height, ReadOnlySpan<byte> jpeg)
    {
        // header: u64 frameId + u32 ts + u32 w + u32 h + u32 jpegLen = 24 bytes
        var buf = new byte[24 + jpeg.Length];
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(0),  (ulong)frameId);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(8),  timestampMs);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(12), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(16), (uint)height);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(20), (uint)jpeg.Length);
        jpeg.CopyTo(buf.AsSpan(24));
        return buf;
    }

    /// <summary>Encode MOUSE_MOVE payload (TDD §5.2).</summary>
    public static byte[] EncodeMouseMove(int x, int y)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(0), x);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(4), y);
        return buf;
    }

    /// <summary>Encode MOUSE_BUTTON payload (TDD §5.2).</summary>
    public static byte[] EncodeMouseButton(MouseButton button, bool isDown, int x, int y)
    {
        var buf = new byte[10];
        buf[0] = (byte)button;
        buf[1] = isDown ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(2), x);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(6), y);
        return buf;
    }

    /// <summary>Encode PING / PONG payload.</summary>
    public static byte[] EncodePingPong(ulong nonce)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buf, nonce);
        return buf;
    }

    // ── Phase 6 Enterprise Features ───────────────────────────────────────

    /// <summary>Encode UTF-8 string payload (ChatText, ClipboardData, SysInfoResp).</summary>
    public static byte[] EncodeStringMessage(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>Encode boolean payload (PrivacyMode).</summary>
    public static byte[] EncodeBooleanMessage(bool value)
    {
        return new byte[] { value ? (byte)1 : (byte)0 };
    }

    // ── Payload decoders ──────────────────────────────────────────────────

    public static (byte Version, uint ScreenW, uint ScreenH, string ServerName) DecodeHelloAck(byte[] payload)
    {
        var version = payload[0];
        var w       = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(1));
        var h       = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(5));
        var nameLen = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(9));
        var name    = Encoding.UTF8.GetString(payload, 11, Math.Min(nameLen, payload.Length - 11));
        return (version, w, h, name);
    }

    public static string DecodeAuth(byte[] payload)
    {
        var len   = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0));
        return Encoding.UTF8.GetString(payload, 2, Math.Min(len, payload.Length - 2));
    }

    public static string DecodeAuthFail(byte[] payload)
    {
        if (payload.Length < 2) return "Unknown reason";
        var len = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0));
        return Encoding.UTF8.GetString(payload, 2, Math.Min(len, payload.Length - 2));
    }

    public static FrameJpegMessage DecodeFrameJpeg(byte[] payload)
    {
        var frameId   = (long)BinaryPrimitives.ReadUInt64BigEndian(payload.AsSpan(0));
        var tsMs      = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(8));
        var width     = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(12));
        var height    = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(16));
        var jpegLen   = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(20));
        var jpeg      = payload.AsMemory(24, jpegLen);
        return new FrameJpegMessage(frameId, tsMs, width, height, jpeg);
    }

    public static (int X, int Y) DecodeMouseMove(byte[] payload)
    {
        var x = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(0));
        var y = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4));
        return (x, y);
    }

    public static (MouseButton Button, bool IsDown, int X, int Y) DecodeMouseButton(byte[] payload)
    {
        var button = (MouseButton)payload[0];
        var down   = payload[1] != 0;
        var x      = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(2));
        var y      = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(6));
        return (button, down, x, y);
    }

    public static ulong DecodePingPong(byte[] payload)
        => BinaryPrimitives.ReadUInt64BigEndian(payload.AsSpan(0));

    /// <summary>
    /// Encode KEY_EVENT payload (TDD §17.2): u32 keysym (big-endian) + u8 isDown.
    /// Fixed 5 bytes. <paramref name="isDown"/> true = press, false = release.
    /// </summary>
    public static byte[] EncodeKeyEvent(uint keysym, bool isDown)
    {
        var buf = new byte[5];
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0), keysym);
        buf[4] = isDown ? (byte)1 : (byte)0;
        return buf;
    }

    /// <summary>
    /// Decode KEY_EVENT payload (TDD §17.2). Expects exactly 5 bytes.
    /// Returns (Keysym, IsDown) where IsDown=true means key press, false=release.
    /// </summary>
    public static (uint Keysym, bool IsDown) DecodeKeyEvent(byte[] payload)
    {
        var keysym = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(0));
        var isDown = payload[4] != 0;
        return (keysym, isDown);
    }

    // ── Phase 6 Enterprise Features ───────────────────────────────────────

    /// <summary>Decode UTF-8 string payload (ChatText, ClipboardData, SysInfoResp).</summary>
    public static string DecodeStringMessage(byte[] payload)
    {
        return Encoding.UTF8.GetString(payload);
    }

    /// <summary>Decode boolean payload (PrivacyMode).</summary>
    public static bool DecodeBooleanMessage(byte[] payload)
    {
        if (payload.Length == 0) return false;
        return payload[0] != 0;
    }

    // ── Internal ──────────────────────────────────────────────────────────

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Connection closed by remote peer during read");
            offset += read;
        }
    }
}

/// <summary>Decoded FRAME_JPEG message payload (TDD §5.2 MessageType 0x10).</summary>
public sealed record FrameJpegMessage(
    long FrameId,
    uint TimestampMs,
    int Width,
    int Height,
    ReadOnlyMemory<byte> JpegData);
