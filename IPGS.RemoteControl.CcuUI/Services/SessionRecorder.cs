using System.Buffers.Binary;

namespace IPGS.RemoteControl.CcuUI.Services;

/// <summary>
/// A minimalist, zero-dependency AVI (MJPEG) writer for recording remote sessions.
/// Completely zero-allocation: uses UTF-8 literals and stack memory for all headers and chunks.
/// </summary>
public sealed class SessionRecorder : IDisposable
{
    private readonly Stream _stream;
    private readonly int _width;
    private readonly int _height;
    private readonly int _fps;

    // A3: AddFrame runs on CcuClient's TCP receive thread while Dispose runs on UI thread
    // Must serialize with lock + _disposed flag to prevent ObjectDisposedException
    private readonly object _sync = new();
    private bool _disposed;

    private int _frameCount;
    private long _moviOffset;
    private readonly List<(uint Offset, uint Length)> _index = new();

    /// <summary>Chiều rộng khung hình đã khai báo trong AVI header.</summary>
    public int Width => _width;

    /// <summary>Chiều cao khung hình đã khai báo trong AVI header.</summary>
    public int Height => _height;

    /// <summary>Khởi tạo bộ ghi hình AVI tới luồng Stream bất kỳ (hỗ trợ MemoryStream trong unit test).</summary>
    public SessionRecorder(Stream stream, int width, int height, int fps = 15)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _width  = width;
        _height = height;
        _fps    = fps > 0 ? fps : 15;

        // Write placeholder RIFF header (will be patched on Dispose)
        WriteHeader();
    }

    /// <summary>Khởi tạo bộ ghi hình AVI ghi trực tiếp vào đường dẫn tệp tin trên đĩa.</summary>
    public SessionRecorder(string path, int width, int height, int fps = 15)
        : this(new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None), width, height, fps)
    {
    }

    public void AddFrame(ReadOnlySpan<byte> jpegData)
    {
        if (jpegData.Length == 0) return;

        lock (_sync)
        {
            // Guard sau khi Dispose: frame tới muộn từ receive-thread chỉ bị bỏ qua êm
            if (_disposed || !_stream.CanWrite) return;

            // Ensure 2-byte alignment for chunks
            bool pad = (jpegData.Length % 2) != 0;

            uint offset = (uint)(_stream.Position - _moviOffset);

            // Audit F-ROUND3-2: Zero-allocation chunk writing
            WriteFourCC("00dc"u8);
            WriteUInt32((uint)jpegData.Length);
            _stream.Write(jpegData);
            if (pad) _stream.WriteByte(0);

            _index.Add((offset, (uint)jpegData.Length));
            _frameCount++;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;

            if (!_stream.CanWrite || !_stream.CanSeek)
            {
                _stream.Dispose();
                return;
            }

            // Write idx1
            long idx1Offset = _stream.Position;
            WriteFourCC("idx1"u8);
            WriteUInt32((uint)(_index.Count * 16));
            foreach (var (offset, length) in _index)
            {
                WriteFourCC("00dc"u8);
                WriteUInt32(0x10); // AVIIF_KEYFRAME
                WriteUInt32(offset);
                WriteUInt32(length);
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
            _stream.Position = _moviOffset - 8;
            WriteUInt32((uint)(idx1Offset - _moviOffset + 4));

            _stream.Flush();
            _stream.Dispose();
        }
    }

    private void WriteHeader()
    {
        uint microsecondsPerFrame = (uint)(1000000 / _fps);

        WriteFourCC("RIFF"u8);
        WriteUInt32(0); // Placeholder for total size
        WriteFourCC("AVI "u8);

        WriteFourCC("LIST"u8);
        WriteUInt32(192); // hdrl size
        WriteFourCC("hdrl"u8);

        WriteFourCC("avih"u8);
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

        WriteFourCC("LIST"u8);
        WriteUInt32(116); // strl size
        WriteFourCC("strl"u8);

        WriteFourCC("strh"u8);
        WriteUInt32(56);
        WriteFourCC("vids"u8);
        WriteFourCC("MJPG"u8);
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

        WriteFourCC("strf"u8);
        WriteUInt32(40);
        WriteUInt32(40); // biSize
        WriteUInt32((uint)_width); // biWidth
        WriteUInt32((uint)_height); // biHeight
        WriteUInt16(1); // biPlanes
        WriteUInt16(24); // biBitCount
        WriteFourCC("MJPG"u8); // biCompression
        WriteUInt32((uint)(_width * _height * 3)); // biSizeImage
        WriteUInt32(0); // biXPelsPerMeter
        WriteUInt32(0); // biYPelsPerMeter
        WriteUInt32(0); // biClrUsed
        WriteUInt32(0); // biClrImportant

        WriteFourCC("LIST"u8);
        WriteUInt32(0); // Placeholder for movi size
        WriteFourCC("movi"u8);
        _moviOffset = _stream.Position;
    }

    private void WriteFourCC(ReadOnlySpan<byte> fcc)
    {
        _stream.Write(fcc);
    }

    private void WriteUInt16(ushort val)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, val);
        _stream.Write(buf);
    }

    private void WriteUInt32(uint val)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, val);
        _stream.Write(buf);
    }
}
