using System.Buffers.Binary;
using System.Text;
using IPGS.RemoteControl.Protocol;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class MessageCodecTests
{
    [Fact]
    public void EncodeDecode_Hello_RoundtripsSuccessfully()
    {
        string clientName = "CCU-Manager/1.2";
        byte[] payload = MessageCodec.EncodeHello(clientName);
        var (version, name) = MessageCodec.DecodeHello(payload);

        Assert.Equal(RemoteControlConstants.ProtocolVersion, version);
        Assert.Equal(clientName, name);
    }

    [Fact]
    public void EncodeDecode_HelloAck_RoundtripsSuccessfully()
    {
        uint width = 1920;
        uint height = 1080;
        string serverName = "ZcuAgent/1.2";

        byte[] payload = MessageCodec.EncodeHelloAck(width, height, serverName);
        var (version, w, h, name) = MessageCodec.DecodeHelloAck(payload);

        Assert.Equal(RemoteControlConstants.ProtocolVersion, version);
        Assert.Equal(width, w);
        Assert.Equal(height, h);
        Assert.Equal(serverName, name);
    }

    [Fact]
    public void EncodeDecode_Auth_RoundtripsSuccessfully()
    {
        string token = "kztek-super-secure-token-2026";
        byte[] payload = MessageCodec.EncodeAuth(token);
        string decodedToken = MessageCodec.DecodeAuth(payload);

        Assert.Equal(token, decodedToken);
    }

    [Fact]
    public void EncodeDecode_MouseMove_RoundtripsSuccessfully()
    {
        int x = 1024;
        int y = 768;
        byte[] payload = MessageCodec.EncodeMouseMove(x, y);
        var (dx, dy) = MessageCodec.DecodeMouseMove(payload);

        Assert.Equal(x, dx);
        Assert.Equal(y, dy);
    }

    [Fact]
    public void EncodeDecode_MouseButton_RoundtripsSuccessfully()
    {
        var btn = MouseButton.Right;
        bool isDown = true;
        int x = 500;
        int y = 600;

        byte[] payload = MessageCodec.EncodeMouseButton(btn, isDown, x, y);
        var (dBtn, dDown, dx, dy) = MessageCodec.DecodeMouseButton(payload);

        Assert.Equal(btn, dBtn);
        Assert.Equal(isDown, dDown);
        Assert.Equal(x, dx);
        Assert.Equal(y, dy);
    }

    [Fact]
    public void EncodeDecode_KeyEvent_RoundtripsSuccessfully()
    {
        uint keysym = 0xFF0D; // Return / Enter
        bool isDown = true;

        byte[] payload = MessageCodec.EncodeKeyEvent(keysym, isDown);
        var (dKey, dDown) = MessageCodec.DecodeKeyEvent(payload);

        Assert.Equal(keysym, dKey);
        Assert.Equal(isDown, dDown);
    }

    [Fact]
    public void EncodeDecode_KeyEventRaw_RoundtripsSuccessfully()
    {
        byte rawCode = 36; // Return keycode on X11
        bool isDown = false;

        byte[] payload = MessageCodec.EncodeKeyEventRaw(rawCode, isDown);
        var (dCode, dDown) = MessageCodec.DecodeKeyEventRaw(payload);

        Assert.Equal(rawCode, dCode);
        Assert.Equal(isDown, dDown);
    }

    [Fact]
    public void EncodeDecode_PingPong_RoundtripsSuccessfully()
    {
        ulong nonce = 0x123456789ABCDEF0;
        byte[] payload = MessageCodec.EncodePingPong(nonce);
        ulong decodedNonce = MessageCodec.DecodePingPong(payload);

        Assert.Equal(nonce, decodedNonce);
    }

    [Fact]
    public void Decode_TruncatedPayload_ThrowsProtocolException()
    {
        byte[] truncated = new byte[3]; // Hello requires at least 3 bytes (version + 2-byte len)
        BinaryPrimitives.WriteUInt16BigEndian(truncated.AsSpan(1), 10);
        Assert.Throws<ProtocolException>(() => MessageCodec.DecodeHello(truncated));
    }

    [Fact]
    public void DecodeMouseMove_InvalidLength_ThrowsProtocolException()
    {
        byte[] invalid = new byte[5]; // MouseMove requires 8 bytes (4 for X, 4 for Y)
        Assert.Throws<ProtocolException>(() => MessageCodec.DecodeMouseMove(invalid));
    }
}
