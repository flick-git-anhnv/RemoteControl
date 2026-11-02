using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Interop;

/// <summary>
/// Trình phân giải và phát hiện X11 Display tự động cho máy đích Linux ZCU.
/// Hỗ trợ cả môi trường có màn hình vật lý, môi trường không màn hình (Headless / Kiosk không cắm cáp HDMI),
/// máy ảo, và phiên RDP ảo (xrdp) với các cổng hiển thị động (:0, :1, :10.0, :11.0, v.v.).
/// </summary>
public static class X11DisplayResolver
{
    private const string UnixSocketDir = "/tmp/.X11-unix";
    private static readonly string[] DefaultFallbackDisplays = [":0", ":1", ":10.0", ":10", ":20.0"];

    /// <summary>
    /// Trích xuất định danh Display tương ứng từ tên file Unix socket trong <c>/tmp/.X11-unix/</c>.
    /// Ví dụ: "X0" -> ":0", "X10" -> ":10". Trả về null nếu không đúng định dạng.
    /// </summary>
    public static string? ParseDisplayFromSocketName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var name = Path.GetFileName(fileName);
        if (name.StartsWith('X') && name.Length > 1 && int.TryParse(name.AsSpan(1), out var displayNum))
        {
            return $":{displayNum}";
        }

        return null;
    }

    /// <summary>
    /// Xây dựng danh sách ứng viên Display theo thứ tự ưu tiên giảm dần:
    /// 1. Cấu hình người dùng chỉ định (<paramref name="preferredDisplay"/>)
    /// 2. Biến môi trường hệ thống DISPLAY (<paramref name="envDisplay"/>)
    /// 3. Các Unix Domain Socket hiện có trong <c>/tmp/.X11-unix/X*</c>
    /// 4. Danh sách fallback mặc định (:0, :1, :10.0, :10, :20.0)
    /// </summary>
    public static List<string> GetCandidateDisplays(
        string? preferredDisplay,
        string? envDisplay,
        IEnumerable<string>? socketFileNames = null)
    {
        var candidates = new List<string>();

        // 1. Preferred display cấu hình trong appsettings
        if (!string.IsNullOrWhiteSpace(preferredDisplay))
        {
            var p = preferredDisplay.Trim();
            if (!p.StartsWith(':')) p = ":" + p;
            candidates.Add(p);
        }

        // 2. DISPLAY environment variable
        if (!string.IsNullOrWhiteSpace(envDisplay))
        {
            var e = envDisplay.Trim();
            if (!candidates.Contains(e, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(e);
            }
        }

        // 3. Socket files từ /tmp/.X11-unix
        if (socketFileNames != null)
        {
            var socketDisplays = new List<(int num, string disp)>();
            foreach (var file in socketFileNames)
            {
                var disp = ParseDisplayFromSocketName(file);
                if (disp != null && int.TryParse(disp.AsSpan(1), out var num))
                {
                    socketDisplays.Add((num, disp));
                }
            }

            // Sắp xếp: :0, :1 trước, sau đó tới các phiên xrdp :10, :11
            foreach (var item in socketDisplays.OrderBy(x => x.num))
            {
                if (!candidates.Contains(item.disp, StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(item.disp);
                }
            }
        }

        // 4. Fallback defaults
        foreach (var fb in DefaultFallbackDisplays)
        {
            if (!candidates.Contains(fb, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(fb);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Dò tìm và mở kết nối X11 Display đầu tiên thành công.
    /// Tự động cập nhật biến môi trường <c>DISPLAY</c> nếu mở thành công để các tiến trình con
    /// hoặc luồng khác kế thừa chính xác Display này.
    /// <paramref name="displayOpener"/> là delegate tùy chọn cho phép mock trong kiểm thử đơn vị.
    /// </summary>
    public static (IntPtr DisplayHandle, string ResolvedDisplayName) OpenAvailableDisplay(
        string? preferredDisplay = null,
        ILogger? logger = null,
        Func<string, IntPtr>? displayOpener = null)
    {
        var envDisplay = Environment.GetEnvironmentVariable("DISPLAY");

        IEnumerable<string>? socketFiles = null;
        if (OperatingSystem.IsLinux() && Directory.Exists(UnixSocketDir))
        {
            try
            {
                socketFiles = Directory.GetFiles(UnixSocketDir, "X*");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Không thể quét thư mục socket X11 tại {Dir}", UnixSocketDir);
            }
        }

        var candidates = GetCandidateDisplays(preferredDisplay, envDisplay, socketFiles);

        foreach (var candidate in candidates)
        {
            try
            {
                logger?.LogDebug("Đang thử kết nối X11 Display '{Display}'...", candidate);
                var handle = displayOpener != null
                    ? displayOpener(candidate)
                    : X11.XOpenDisplay(candidate);

                if (handle != IntPtr.Zero)
                {
                    logger?.LogInformation("Đã kết nối thành công X11 Display '{Display}'", candidate);
                    try
                    {
                        Environment.SetEnvironmentVariable("DISPLAY", candidate);
                    }
                    catch
                    {
                        // Bỏ qua lỗi cấp quyền set env nếu có
                    }
                    return (handle, candidate);
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Lỗi khi gọi XOpenDisplay cho '{Display}'", candidate);
            }
        }

        throw new InvalidOperationException(
            $"Không thể kết nối đến bất kỳ X11 Display nào (đã thử: {string.Join(", ", candidates)}). " +
            "Vui lòng đảm bảo X server (Xorg, Xvfb hoặc xrdp) đang chạy và biến DISPLAY hoặc cấu hình AgentOptions.Display hợp lệ.");
    }
}
