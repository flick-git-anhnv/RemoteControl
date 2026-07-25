using System.Buffers.Binary;
using System.Text;

namespace IPGS.RemoteControl.CcuUI.Services;

/// <summary>
/// A minimalist, zero-dependency AVI (MJPEG) writer for recording remote sessions.
/// </summary>
public sealed class SessionRecorder : IDisposable
{
    private readonly Stream _stream;
    private readonly int _width;
    private readonly int _height;
    private readonly int _fps;
    
    private int _frameCount;
    private long _moviOffset;
    private readonly List<(string Id, uint Length, uint Offset)> _index = new();

    public SessionRecorder(string path, int width, int height, int fps = 15)
    {
        _width = width;
        _height = height;
        _fps = fps;
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        
        // Write placeholder RIFF header (will be patched on Dispose)
        WriteHeader();
    }

    public void AddFrame(ReadOnlySpan<byte> jpegData)
    {
        if (jpegData.Length == 0) return;

        // Ensure 2-byte alignment for chunks
        bool pad = (jpegData.Length % 2) != 0;
        uint chunkLen = (uint)(jpegData.Length + (pad ? 1 : 0));

        uint offset = (uint)(_stream.Position - _moviOffset);
        
        WriteFourCC("00dc");
        WriteUInt32((uint)jpegData.Length);
        _stream.Write(jpegData);
        if (pad) _stream.WriteByte(0);

        _index.Add(("00dc", (uint)jpegData.Length, offset));
        _frameCount++;
    }

    public void Dispose()
    {
        if (_stream.CanWrite)
        {
            // Write idx1
            long idx1Offset = _stream.Position;
            WriteFourCC("idx1");
            WriteUInt32((uint)(_index.Count * 16));
            foreach (var entry in _index)
            {
                WriteFourCC(entry.Id);
                WriteUInt32(0x10); // AVIIF_KEYFRAME
                WriteUInt32(entry.Offset);
                WriteUInt32(entry.Length);
            }

            long totalSize = _stream.Position;

            // Patch RIFF size
            _stream.Position = 4;
            WriteUInt32((uint)(totalSize - 8));

            // Patch Frames
            _stream.Position = 48;
            WriteUInt32((uint)_frameCount);

            _stream.Position = 140;
            WriteUInt32((uint)_frameCount);

            // Patch Movi size
            _stream.Position = _moviOffset - 4;
            WriteUInt32((uint)(idx1Offset - _moviOffset));

            _stream.Close();
        }
    }

    private void WriteHeader()
    {
        uint microsecondsPerFrame = (uint)(1000000 / _fps);

        WriteFourCC("RIFF");
        WriteUInt32(0); // Placeholder for total size
        WriteFourCC("AVI ");

        WriteFourCC("LIST");
        WriteUInt32(192); // hdrl size
        WriteFourCC("hdrl");

        WriteFourCC("avih");
        WriteUInt32(56);
        WriteUInt32(microsecondsPerFrame); // dwMicroSecPerFrame
        WriteUInt32(0); // dwMaxBytesPerSec
        WriteUInt32(0); // dwPaddingGranularity
        WriteUInt32(0x10); // dwFlags (AVIF_HASINDEX)
        WriteUInt32(0); // dwTotalFrames (placeholder at offset 48)
        WriteUInt32(0); // dwInitialFrames
        WriteUInt32(1); // dwStreams
        WriteUInt32(0); // dwSuggestedBufferSize
        WriteUInt32((uint)_width);
        WriteUInt32((uint)_height);
        WriteUInt32(0);
        WriteUInt32(0);
        WriteUInt32(0);
        WriteUInt32(0);

        WriteFourCC("LIST");
        WriteUInt32(116); // strl size
        WriteFourCC("strl");

        WriteFourCC("strh");
        WriteUInt32(56);
        WriteFourCC("vids");
        WriteFourCC("MJPG");
        WriteUInt32(0); // dwFlags
        WriteUInt16(0); // wPriority
        WriteUInt16(0); // wLanguage
        WriteUInt32(0); // dwInitialFrames
        WriteUInt32(1); // dwScale
        WriteUInt32((uint)_fps); // dwRate
        WriteUInt32(0); // dwStart
        WriteUInt32(0); // dwLength (placeholder at 140)
        WriteUInt32(0); // dwSuggestedBufferSize
        WriteUInt32(0); // dwQuality
        WriteUInt32(0); // dwSampleSize
        WriteUInt16(0); // rcFrame
        WriteUInt16(0);
        WriteUInt16((ushort)_width);
        WriteUInt16((ushort)_height);

        WriteFourCC("strf");
        WriteUInt32(40);
        WriteUInt32(40); // biSize
        WriteUInt32((uint)_width); // biWidth
        WriteUInt32((uint)_height); // biHeight
        WriteUInt16(1); // biPlanes
        WriteUInt16(24); // biBitCount
        WriteFourCC("MJPG"); // biCompression
        WriteUInt32((uint)(_width * _height * 3)); // biSizeImage
        WriteUInt32(0); // biXPelsPerMeter
        WriteUInt32(0); // biYPelsPerMeter
        WriteUInt32(0); // biClrUsed
        WriteUInt32(0); // biClrImportant

        WriteFourCC("LIST");
        WriteUInt32(0); // Placeholder for movi size
        WriteFourCC("movi");
        _moviOffset = _stream.Position;
    }

    private void WriteFourCC(string fcc)
    {
        var bytes = Encoding.ASCII.GetBytes(fcc);
        if (bytes.Length != 4) throw new ArgumentException("Must be 4 chars");
        _stream.Write(bytes);
    }

    private void WriteUInt16(ushort val)
    {
        var buf = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, val);
        _stream.Write(buf);
    }

    private void WriteUInt32(uint val)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, val);
        _stream.Write(buf);
    }
}
