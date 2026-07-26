using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>
/// Encodes a <see cref="CapturedFrame"/> to JPEG bytes using SkiaSharp.
/// Cross-platform; native assets for Linux are pulled in via
/// <c>SkiaSharp.NativeAssets.Linux</c> NuGet. See TDD §6.3.
/// </summary>
internal sealed class JpegEncoder : IFrameEncoder
{
    private readonly ILogger<JpegEncoder> _logger;

    /// <summary>
    /// Reused output buffer (audit Q3 — GC pressure): avoids one fresh byte[] per frame
    /// from <c>SKData.ToArray()</c> (~50–300KB × 15fps). Grow-only. Safe because the
    /// encoder is called from a single capture loop and the result is consumed
    /// synchronously before the next EncodeJpeg call.
    /// </summary>
    private byte[] _jpegBuffer = [];

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
            // X11 default: BGRA8888 (blue channel first).
            // SKColorType.Bgra8888 matches the native X11 layout on little-endian x86_64.
            var info = new SKImageInfo(
                width:     frame.Width,
                height:    frame.Height,
                colorType: SKColorType.Bgra8888,
                alphaType: SKAlphaType.Opaque);

            using var bitmap = new SKBitmap(info);
            using var pixmap = bitmap.PeekPixels()!;

            // Copy managed byte[] → unmanaged SkiaSharp pixel buffer
            var srcSpan  = frame.PixelData.AsSpan();
            var dstPtr   = bitmap.GetPixels();
            var rowBytes = frame.BytesPerRow;

            unsafe
            {
                byte* dst = (byte*)dstPtr.ToPointer();
                fixed (byte* src = srcSpan)
                {
                    for (var row = 0; row < frame.Height; row++)
                        Buffer.MemoryCopy(
                            src + row * rowBytes,
                            dst + row * info.RowBytes,
                            info.RowBytes,
                            Math.Min(rowBytes, info.RowBytes));
                }
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data  = image.Encode(SKEncodedImageFormat.Jpeg, quality);

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
            return _jpegBuffer.AsMemory(0, size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JPEG encode failed");
            return ReadOnlyMemory<byte>.Empty;
        }
    }
}
