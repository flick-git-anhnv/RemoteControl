using System.Runtime.InteropServices;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>
/// Encodes a <see cref="CapturedFrame"/> to JPEG bytes using SkiaSharp.
/// Cross-platform; native assets for Linux are pulled in via
/// <c>SkiaSharp.NativeAssets.Linux</c> NuGet. See TDD §6.3.
/// </summary>
internal sealed class JpegEncoder : IFrameEncoder, IDisposable
{
    private readonly ILogger<JpegEncoder> _logger;

    /// <summary>
    /// Reused output buffer (audit Q3 — GC pressure): avoids one fresh byte[] per frame
    /// from <c>SKData.ToArray()</c> (~50–300KB × 15fps). Grow-only. Safe because the
    /// encoder is called from a single capture loop and the result is consumed
    /// synchronously before the next EncodeJpeg call.
    /// </summary>
    private byte[] _jpegBuffer = [];

    /// <summary>
    /// Reused unmanaged SKBitmap instance across frames (audit M2): eliminates ~8.3MB/frame
    /// malloc/free churn on glibc heap at 1080p × 15fps (~125MB/s unmanaged churn).
    /// Only reallocated when screen dimensions change.
    /// </summary>
    private SKBitmap? _reusableBitmap;
    private int _framesSinceTrim;
    private const int TrimIntervalFrames = 900; // ~60s at 15fps

    [DllImport("libc", EntryPoint = "malloc_trim")]
    private static extern int MallocTrim(nuint pad);

    public JpegEncoder(ILogger<JpegEncoder> logger) => _logger = logger;

    /// <summary>
    /// Encode <paramref name="frame"/> to JPEG bytes.
    /// X11 delivers pixels as BGRA8888 (or BGRX8888 with ignored alpha).
    /// Returns an empty memory if encoding fails. The returned memory is backed by a
    /// reused internal buffer — valid only until the next call (see IFrameEncoder doc).
    /// </summary>
    public ReadOnlyMemory<byte> EncodeJpeg(CapturedFrame frame, int quality)
    {
        try
        {
            var info = new SKImageInfo(
                width:     frame.Width,
                height:    frame.Height,
                colorType: SKColorType.Bgra8888,
                alphaType: SKAlphaType.Opaque);

            // Zero-Copy encode: nén JPEG trực tiếp từ con trỏ MIT-SHM (PixelPtr) hoặc pinned buffer (audit F-NEW-3)
            // Loại bỏ hoàn toàn 5.2–8.3MB Buffer.MemoryCopy / Marshal.Copy per-frame trên CPU ZCU
            using var data = frame.PixelPtr != IntPtr.Zero
                ? EncodeFromPointer(info, frame.PixelPtr, frame.BytesPerRow, quality)
                : EncodeDirect(info, frame.PixelData, frame.BytesPerRow, quality);

            if (data is null || data.Size == 0)
            {
                _logger.LogWarning("SKImage.Encode returned empty data");
                return ReadOnlyMemory<byte>.Empty;
            }

            // Copy into the reused buffer instead of data.ToArray() (audit Q3)
            var size = (int)data.Size;
            if (_jpegBuffer.Length < size)
                _jpegBuffer = GC.AllocateUninitializedArray<byte>(size);
            data.AsSpan().CopyTo(_jpegBuffer);

            // G002: Periodic unmanaged heap trimming to prevent glibc arena fragmentation on Linux
            if (++_framesSinceTrim >= TrimIntervalFrames)
            {
                _framesSinceTrim = 0;
                TrimProcessMemory();
            }

            return _jpegBuffer.AsMemory(0, size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JPEG encode failed");
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    private static SKData? EncodeFromPointer(SKImageInfo info, IntPtr pixelPtr, int rowBytes, int quality)
    {
        using var image = SKImage.FromPixels(info, pixelPtr, rowBytes);
        return image?.Encode(SKEncodedImageFormat.Jpeg, quality);
    }

    private static SKData? EncodeDirect(SKImageInfo info, byte[] pixelData, int rowBytes, int quality)
    {
        unsafe
        {
            fixed (byte* pSrc = pixelData)
            {
                using var image = SKImage.FromPixels(info, (IntPtr)pSrc, rowBytes);
                return image?.Encode(SKEncodedImageFormat.Jpeg, quality);
            }
        }
    }

    public static void TrimProcessMemory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try { MallocTrim(0); } catch { }
        }
    }

    public void Dispose()
    {
        _reusableBitmap?.Dispose();
        _reusableBitmap = null;
        TrimProcessMemory();
    }
}
