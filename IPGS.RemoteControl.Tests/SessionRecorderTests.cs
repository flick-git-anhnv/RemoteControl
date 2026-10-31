using System.Buffers.Binary;
using System.Text;
using IPGS.RemoteControl.CcuUI.Services;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class SessionRecorderTests
{
    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SessionRecorder((Stream)null!, 1920, 1080));
    }

    [Fact]
    public void Constructor_WritesRiffAviHeaders_CorrectDimensions()
    {
        using var ms = new MemoryStream();
        using var recorder = new SessionRecorder(ms, 1280, 720, 30);

        Assert.Equal(1280, recorder.Width);
        Assert.Equal(720, recorder.Height);

        byte[] header = ms.ToArray();
        Assert.True(header.Length >= 200);

        // Check RIFF header
        Assert.Equal("RIFF", Encoding.ASCII.GetString(header, 0, 4));
        Assert.Equal("AVI ", Encoding.ASCII.GetString(header, 8, 4));
        Assert.Equal("LIST", Encoding.ASCII.GetString(header, 12, 4));
        Assert.Equal("hdrl", Encoding.ASCII.GetString(header, 20, 4));
        Assert.Equal("avih", Encoding.ASCII.GetString(header, 24, 4));
    }

    [Fact]
    public void AddFrame_WithOddByteLength_PadsToEvenAlignment()
    {
        using var ms = new MemoryStream();
        var recorder = new SessionRecorder(ms, 800, 600, 15);

        // Frame with odd length: 11 bytes
        byte[] frameData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        recorder.AddFrame(frameData);

        recorder.Dispose();

        byte[] output = ms.ToArray();
        // Look for 00dc chunk
        int idx = FindByteSequence(output, "00dc"u8.ToArray());
        Assert.True(idx >= 0);

        // Length of chunk is at idx + 4 (4 bytes little-endian)
        uint chunkLen = BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(idx + 4, 4));
        Assert.Equal(11u, chunkLen);

        // Pad byte: because 11 is odd, there is 1 pad byte, so total chunk span is 4 (ID) + 4 (Len) + 11 (data) + 1 (pad) = 20
        Assert.Equal(0, output[idx + 4 + 4 + 11]);
    }

    [Fact]
    public void Dispose_PatchesFrameCountAndWritesIdx1()
    {
        using var ms = new MemoryStream();
        var recorder = new SessionRecorder(ms, 1920, 1080, 30);

        byte[] frame1 = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        byte[] frame2 = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        recorder.AddFrame(frame1);
        recorder.AddFrame(frame2);

        recorder.Dispose();

        byte[] output = ms.ToArray();

        // Frame count at offset 48 (in avih) and 140 (in strh)
        uint avihFrames = BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(48, 4));
        uint strhFrames = BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(140, 4));

        Assert.Equal(2u, avihFrames);
        Assert.Equal(2u, strhFrames);

        // Check idx1 chunk is written
        int idx1Pos = FindByteSequence(output, "idx1"u8.ToArray());
        Assert.True(idx1Pos > 0);
        uint idx1Len = BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(idx1Pos + 4, 4));
        Assert.Equal(32u, idx1Len); // 2 frames * 16 bytes per entry
    }

    [Fact]
    public void AddFrame_AfterDispose_SilentlyIgnored()
    {
        using var ms = new MemoryStream();
        var recorder = new SessionRecorder(ms, 640, 480);
        recorder.Dispose();

        // Must not throw ObjectDisposedException
        recorder.AddFrame(new byte[] { 1, 2, 3, 4 });
        recorder.Dispose(); // Multiple dispose is idempotent
    }

    private static int FindByteSequence(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
}
