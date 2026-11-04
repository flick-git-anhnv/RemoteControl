using System.Diagnostics;
using System.Runtime.InteropServices;
using IPGS.RemoteControl.ZcuAgent.Interop;
using IPGS.RemoteControl.ZcuAgent.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>
/// Chế độ <c>--bench</c> (Bước 0 — đo đạc FPS ZCU): chạy tay trên máy đích, KHÔNG mở cổng TCP,
/// KHÔNG đụng tới service đang chạy (chỉ mở thêm 1 kết nối X11 để chụp). Đo:
/// <list type="bullet">
///   <item>thông tin CPU (tần số hiện tại / tối đa, governor) và độ phân giải;</item>
///   <item>thời gian chụp màn hình (XShmGetImage / XGetImage);</item>
///   <item>thời gian so sánh khung tĩnh (FrameChangeDetector);</item>
///   <item>thời gian nén JPEG bằng Skia (đang dùng) và libturbojpeg (nếu máy có <c>libturbojpeg0</c>).</item>
/// </list>
/// Ví dụ: <c>DISPLAY=:0 DOTNET_ROOT=$HOME/.dotnet ./IPGS.RemoteControl.ZcuAgent --bench</c>
/// </summary>
internal static class EncoderBenchmark
{
    private const int WarmupIterations  = 3;
    private const int CaptureIterations = 30;
    private const int EncodeIterations  = 20;

    public static int Run(IServiceProvider services)
    {
        var options  = services.GetRequiredService<IOptions<AgentOptions>>().Value;
        var capturer = services.GetRequiredService<IScreenCapturer>();
        var encoder  = services.GetRequiredService<IFrameEncoder>();
        var quality  = Math.Clamp(options.JpegQuality, 1, 100);

        Console.WriteLine("=== ZcuAgent --bench ===");
        Console.WriteLine($"Thời điểm         : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        PrintCpuInfo();

        try
        {
            capturer.Initialize();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[bench] Không mở được X11 display: {ex.Message}");
            Console.Error.WriteLine("[bench] Gợi ý: chạy với DISPLAY=:0 bằng đúng user đang đăng nhập desktop (ccu).");
            return 2;
        }

        // ── Capture ─────────────────────────────────────────────────────
        CapturedFrame? frame = null;
        for (var i = 0; i < WarmupIterations; i++) frame = capturer.Capture();

        var captureTimes = new List<double>(CaptureIterations);
        for (var i = 0; i < CaptureIterations; i++)
        {
            var t = Stopwatch.GetTimestamp();
            frame = capturer.Capture();
            captureTimes.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
        }

        if (frame is null)
        {
            Console.Error.WriteLine("[bench] Capture trả về null — không chụp được màn hình.");
            return 3;
        }

        var width  = frame.Width;
        var height = frame.Height;
        var stride = frame.BytesPerRow;
        var mode   = frame.PixelPtr != IntPtr.Zero ? "MIT-SHM (XShmGetImage)" : "XGetImage (fallback, chậm hơn)";

        Console.WriteLine($"Độ phân giải      : {width}x{height}, stride {stride} B/hàng");
        Console.WriteLine($"Cách chụp         : {mode}");
        Print("Capture", captureTimes);

        // Chép 1 khung ra bộ nhớ managed để mọi encoder nén CÙNG một ảnh.
        var pixels = new byte[stride * height];
        if (frame.PixelPtr != IntPtr.Zero)
            Marshal.Copy(frame.PixelPtr, pixels, 0, pixels.Length);
        else
            Array.Copy(frame.PixelData, pixels, Math.Min(pixels.Length, frame.PixelData.Length));

        var snapshot = new CapturedFrame
        {
            PixelData   = pixels,
            Width       = width,
            Height      = height,
            BytesPerRow = stride,
        };

        // ── So sánh khung tĩnh ──────────────────────────────────────────
        var detector = new FrameChangeDetector();
        detector.HasChanged(pixels, width, height, stride);
        var compareTimes = new List<double>(EncodeIterations);
        for (var i = 0; i < EncodeIterations; i++)
        {
            var t = Stopwatch.GetTimestamp();
            detector.HasChanged(pixels, width, height, stride);
            compareTimes.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
        }
        Print("So sánh khung tĩnh", compareTimes);

        // ── Skia (encoder hiện tại) ─────────────────────────────────────
        var skiaBytes = 0;
        for (var i = 0; i < WarmupIterations; i++) encoder.EncodeJpeg(snapshot, quality);
        var skiaTimes = new List<double>(EncodeIterations);
        for (var i = 0; i < EncodeIterations; i++)
        {
            var t = Stopwatch.GetTimestamp();
            skiaBytes = encoder.EncodeJpeg(snapshot, quality).Length;
            skiaTimes.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
        }
        Print($"Nén Skia q={quality}", skiaTimes, skiaBytes);

        // ── libturbojpeg (hệ điều hành) ─────────────────────────────────
        var turboTimes = BenchTurbo(pixels, width, height, stride, quality, TurboJpeg.TJFLAG_NOREALLOC, out var turboBytes, out var turboError);
        if (turboTimes is not null)
        {
            Print($"Nén libturbojpeg q={quality}", turboTimes, turboBytes);
            var fastTimes = BenchTurbo(pixels, width, height, stride, quality,
                TurboJpeg.TJFLAG_NOREALLOC | TurboJpeg.TJFLAG_FASTDCT, out var fastBytes, out _);
            if (fastTimes is not null)
                Print($"Nén libturbojpeg q={quality} FASTDCT", fastTimes, fastBytes);
        }
        else
        {
            Console.WriteLine($"Nén libturbojpeg  : bỏ qua — {turboError}");
            Console.WriteLine("                    (cài bằng: sudo apt install -y libturbojpeg0)");
        }

        // ── Ước lượng ───────────────────────────────────────────────────
        var cap  = Median(captureTimes);
        var skia = Median(skiaTimes);
        Console.WriteLine();
        Console.WriteLine("--- Ước lượng FPS tối đa khi màn hình thay đổi liên tục (1 luồng, median) ---");
        Console.WriteLine($"Skia              : ~{1000.0 / (cap + skia):F1} fps  (capture {cap:F1} + nén {skia:F1} ms)");
        if (turboTimes is not null)
        {
            var turbo = Median(turboTimes);
            Console.WriteLine($"libturbojpeg      : ~{1000.0 / (cap + turbo):F1} fps  (capture {cap:F1} + nén {turbo:F1} ms) → nhanh hơn Skia {skia / turbo:F1}x ở bước nén");
        }
        Console.WriteLine("(Chưa tính thời gian ghi socket; mạng LAN thường chỉ vài ms.)");

        capturer.Dispose();
        return 0;
    }

    private static List<double>? BenchTurbo(byte[] pixels, int width, int height, int stride, int quality, int flags,
        out int jpegBytes, out string? error)
    {
        jpegBytes = 0;
        error = null;
        IntPtr handle = IntPtr.Zero, buffer = IntPtr.Zero;
        try
        {
            handle = TurboJpeg.tjInitCompress();
            if (handle == IntPtr.Zero) { error = "tjInitCompress trả về NULL"; return null; }

            var capacity = TurboJpeg.tjBufSize(width, height, TurboJpeg.TJSAMP_420);
            buffer = TurboJpeg.tjAlloc((int)capacity);
            if (buffer == IntPtr.Zero) { error = "tjAlloc thất bại"; return null; }

            var times = new List<double>(EncodeIterations);
            for (var i = 0; i < WarmupIterations + EncodeIterations; i++)
            {
                var jpegBuf  = buffer;
                var jpegSize = capacity;
                var t = Stopwatch.GetTimestamp();
                var rc = TurboJpeg.tjCompress2(handle, pixels, width, stride, height, TurboJpeg.TJPF_BGRX,
                    ref jpegBuf, ref jpegSize, TurboJpeg.TJSAMP_420, quality, flags);
                var ms = Stopwatch.GetElapsedTime(t).TotalMilliseconds;
                if (rc != 0) { error = TurboJpeg.LastError(handle); return null; }
                if (i >= WarmupIterations) times.Add(ms);
                jpegBytes = (int)jpegSize;
            }
            return times;
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
        finally
        {
            try { if (buffer != IntPtr.Zero) TurboJpeg.tjFree(buffer); } catch { /* ignore */ }
            try { if (handle != IntPtr.Zero) TurboJpeg.tjDestroy(handle); } catch { /* ignore */ }
        }
    }

    private static void PrintCpuInfo()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/cpuinfo");
            var model = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal))?.Split(':', 2)[1].Trim();
            var mhz   = lines.Where(l => l.StartsWith("cpu MHz", StringComparison.Ordinal))
                             .Select(l => l.Split(':', 2)[1].Trim().Split('.')[0]);
            Console.WriteLine($"CPU               : {model} — {Environment.ProcessorCount} nhân");
            Console.WriteLine($"Tần số hiện tại   : {string.Join(" / ", mhz)} MHz");
        }
        catch { /* không phải Linux */ }

        Console.WriteLine($"Governor          : {ReadSys("/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor")}");
        Console.WriteLine($"Tần số tối đa     : scaling_max {ReadKhz("/sys/devices/system/cpu/cpu0/cpufreq/scaling_max_freq")}, " +
                          $"phần cứng (cpuinfo_max) {ReadKhz("/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq")}");
    }

    private static string ReadSys(string path)
    {
        try { return File.ReadAllText(path).Trim(); } catch { return "?"; }
    }

    private static string ReadKhz(string path)
        => long.TryParse(ReadSys(path), out var khz) ? $"{khz / 1000} MHz" : "?";

    private static double Median(List<double> samples)
    {
        var sorted = samples.ToArray();
        Array.Sort(sorted);
        return sorted.Length == 0 ? 0 : sorted[sorted.Length / 2];
    }

    private static void Print(string label, List<double> samples, int bytes = -1)
    {
        var s = StreamStats.Summarize(samples);
        var min = samples.Count > 0 ? samples.Min() : 0;
        var size = bytes >= 0 ? $", {bytes / 1024.0:F0} KB" : "";
        Console.WriteLine($"{label,-18}: median {Median(samples),6:F1} ms | avg {s.AvgMs,6:F1} | p95 {s.P95Ms,6:F1} | min {min,6:F1} | max {s.MaxMs,6:F1}{size}");
    }
}
