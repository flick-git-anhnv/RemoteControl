using System.Diagnostics;
using System.Text.RegularExpressions;

namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>Một màn hình vật lý trong desktop ảo X11 (toạ độ so với góc trên-trái của root window).</summary>
internal sealed record MonitorInfo(int Index, string Name, int X, int Y, int W, int H);

/// <summary>
/// Liệt kê màn hình bằng <c>xrandr --listmonitors</c> (không cần P/Invoke thêm libXrandr).
/// Không có xrandr / lỗi → danh sách rỗng (client coi như 1 màn hình, chụp toàn desktop).
/// Dòng mẫu: <c> 0: +*HDMI-1 1920/509x1080/286+0+0  HDMI-1</c>
/// </summary>
internal static partial class MonitorLister
{
    [GeneratedRegex(@"^\s*(\d+):\s+\S+\s+(\d+)/\d+x(\d+)/\d+\+(-?\d+)\+(-?\d+)\s+(\S+)")]
    private static partial Regex LineRegex();

    public static async Task<List<MonitorInfo>> ListAsync(CancellationToken ct)
    {
        var result = new List<MonitorInfo>();
        try
        {
            var psi = new ProcessStartInfo("xrandr")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };
            psi.ArgumentList.Add("--listmonitors");

            using var proc = Process.Start(psi);
            if (proc is null) return result;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
                await proc.WaitForExitAsync(cts.Token);
                if (proc.ExitCode != 0) return result;
                result.AddRange(Parse(output));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* xrandr không có/không chạy được → rỗng */ }
        return result;
    }

    internal static IEnumerable<MonitorInfo> Parse(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var m = LineRegex().Match(line);
            if (!m.Success) continue;
            yield return new MonitorInfo(
                int.Parse(m.Groups[1].Value), m.Groups[6].Value,
                int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value),
                int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
        }
    }
}
