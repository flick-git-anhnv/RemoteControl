using System.Runtime.InteropServices;

namespace IPGS.RemoteControl.ZcuAgent.Interop;

/// <summary>
/// P/Invoke tối thiểu tới TurboJPEG API (gói Ubuntu/Debian <c>libturbojpeg0</c>, file
/// <c>libturbojpeg.so.0</c>). Hiện CHỈ dùng cho chế độ <c>--bench</c> để so sánh với bộ nén
/// Skia đang dùng.
/// <para>
/// GOTCHA: libjpeg-turbo bên trong <c>libSkiaSharp.so</c> 2.88 được build KHÔNG có SIMD x86
/// (không có chuỗi JSIMD_FORCE* trong binary), còn bản libturbojpeg của hệ điều hành có
/// SSE2/AVX2 → thường nén nhanh hơn rõ rệt. Benchmark để đo con số thật trên từng máy.
/// </para>
/// <para>
/// Dùng API "legacy" 2.x (tjInitCompress/tjCompress2) — vẫn được export ở libjpeg-turbo 3.x.
/// <c>unsigned long</c> trong C là 8 byte trên Linux x64 (LP64) → <see cref="nuint"/>.
/// DllImport được resolve lười (lần gọi đầu) → máy không có thư viện sẽ ném
/// <see cref="DllNotFoundException"/> tại chỗ gọi, caller tự bắt.
/// </para>
/// </summary>
internal static class TurboJpeg
{
    private const string Lib = "libturbojpeg.so.0";

    // enum TJPF
    public const int TJPF_BGRX = 3;
    // enum TJSAMP
    public const int TJSAMP_420 = 2;
    // flags
    public const int TJFLAG_NOREALLOC = 1024;
    public const int TJFLAG_FASTDCT   = 2048;

    [DllImport(Lib)]
    public static extern IntPtr tjInitCompress();

    [DllImport(Lib)]
    public static extern int tjDestroy(IntPtr handle);

    [DllImport(Lib)]
    public static extern nuint tjBufSize(int width, int height, int jpegSubsamp);

    [DllImport(Lib)]
    public static extern IntPtr tjAlloc(int bytes);

    [DllImport(Lib)]
    public static extern void tjFree(IntPtr buffer);

    [DllImport(Lib)]
    public static extern int tjCompress2(
        IntPtr handle, byte[] srcBuf, int width, int pitch, int height, int pixelFormat,
        ref IntPtr jpegBuf, ref nuint jpegSize, int jpegSubsamp, int jpegQual, int flags);

    [DllImport(Lib)]
    public static extern IntPtr tjGetErrorStr2(IntPtr handle);

    public static string LastError(IntPtr handle)
        => Marshal.PtrToStringAnsi(tjGetErrorStr2(handle)) ?? "unknown TurboJPEG error";
}
