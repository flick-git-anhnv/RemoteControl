using IPGS.RemoteControl.ZcuAgent.Interop;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>
/// Nén JPEG bằng libturbojpeg của hệ điều hành (có SIMD x86). Đo thật trên Celeron J3160
/// (2026-11-06): ~20ms so với ~72ms của Skia (<see cref="JpegEncoder"/>), nhanh hơn ~3.6 lần.
/// <para>
/// Không tạo trực tiếp: dùng <see cref="TryCreate"/> — trả về null nếu máy thiếu
/// <c>libturbojpeg.so.0</c> để caller dùng lại Skia. Cùng hợp đồng với <see cref="IFrameEncoder"/>:
/// bộ nhớ trả về nằm trong buffer tái sử dụng, chỉ hợp lệ đến lần gọi kế tiếp; không thread-safe.
/// </para>
/// </summary>
internal sealed class TurboJpegEncoder : IFrameEncoder, IDisposable
{
    private readonly ILogger<TurboJpegEncoder> _logger;
    private IntPtr _handle;
    private byte[] _jpegBuffer = [];

    private TurboJpegEncoder(ILogger<TurboJpegEncoder> logger, IntPtr handle)
    {
        _logger = logger;
        _handle = handle;
    }

    /// <summary>Tạo encoder; null (kèm <paramref name="error"/>) nếu không dùng được libturbojpeg.</summary>
    public static TurboJpegEncoder? TryCreate(ILogger<TurboJpegEncoder> logger, out string? error)
    {
        try
        {
            var handle = TurboJpeg.tjInitCompress();
            if (handle == IntPtr.Zero)
            {
                error = "tjInitCompress trả về NULL";
                return null;
            }
            error = null;
            return new TurboJpegEncoder(logger, handle);
        }
        catch (DllNotFoundException)
        {
            error = "không tìm thấy libturbojpeg.so.0";
            return null;
        }
        catch (EntryPointNotFoundException ex)
        {
            error = "libturbojpeg thiếu hàm: " + ex.Message;
            return null;
        }
    }

    public ReadOnlyMemory<byte> EncodeJpeg(CapturedFrame frame, int quality)
    {
        try
        {
            // NOREALLOC: libturbojpeg ghi thẳng vào buffer của ta (phải >= tjBufSize), không tự cấp phát lại.
            var capacity = (int)TurboJpeg.tjBufSize(frame.Width, frame.Height, TurboJpeg.TJSAMP_420);
            if (_jpegBuffer.Length < capacity)
                _jpegBuffer = GC.AllocateUninitializedArray<byte>(capacity);

            var jpegSize = (nuint)_jpegBuffer.Length;
            int rc;
            unsafe
            {
                fixed (byte* pOut = _jpegBuffer)
                {
                    var jpegBuf = (IntPtr)pOut;
                    if (frame.PixelPtr != IntPtr.Zero)
                    {
                        // Zero-copy từ MIT-SHM, giống đường Skia.
                        rc = TurboJpeg.tjCompress2(_handle, frame.PixelPtr, frame.Width, frame.BytesPerRow, frame.Height,
                            TurboJpeg.TJPF_BGRX, ref jpegBuf, ref jpegSize, TurboJpeg.TJSAMP_420, quality,
                            TurboJpeg.TJFLAG_NOREALLOC);
                    }
                    else
                    {
                        rc = TurboJpeg.tjCompress2(_handle, frame.PixelData, frame.Width, frame.BytesPerRow, frame.Height,
                            TurboJpeg.TJPF_BGRX, ref jpegBuf, ref jpegSize, TurboJpeg.TJSAMP_420, quality,
                            TurboJpeg.TJFLAG_NOREALLOC);
                    }
                }
            }

            if (rc != 0 || jpegSize == 0)
            {
                _logger.LogWarning("libturbojpeg nén lỗi: {Error}", TurboJpeg.LastError(_handle));
                return ReadOnlyMemory<byte>.Empty;
            }

            return _jpegBuffer.AsMemory(0, (int)jpegSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JPEG encode (libturbojpeg) failed");
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        try { TurboJpeg.tjDestroy(_handle); } catch { /* ignore */ }
        _handle = IntPtr.Zero;
    }
}
