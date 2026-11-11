using IPGS.RemoteControl.WinAgent.Capture;
using IPGS.RemoteControl.ZcuAgent;
using IPGS.RemoteControl.ZcuAgent.Capture;
using static IPGS.RemoteControl.WinAgent.Native.NativeMethods;

namespace IPGS.RemoteControl.WinAgent.Platform;

/// <summary>
/// Liệt kê màn hình bằng <c>EnumDisplayMonitors</c>. Toạ độ trừ đi gốc virtual screen
/// (<see cref="GdiScreenCapturer.VirtualScreen"/>) để không âm và khớp khung hình đã chụp — màn hình đặt bên
/// trái/trên màn hình chính có toạ độ Windows âm, nhưng ClientSession.CropToMonitor yêu cầu X,Y ≥ 0.
/// </summary>
internal sealed class Win32MonitorProvider : IMonitorProvider
{
    public Task<List<MonitorInfo>> ListAsync(CancellationToken ct) => Task.FromResult(Enumerate());

    private static List<MonitorInfo> Enumerate()
    {
        var (vl, vt, _, _) = GdiScreenCapturer.VirtualScreen();
        var found = new List<(MONITORINFOEX Info, bool Primary)>();

        MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
        {
            var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfoW(hMonitor, ref info))
                found.Add((info, (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        // Màn hình chính lên đầu (index 0) cho dễ chọn; còn lại giữ thứ tự hệ thống.
        var ordered = found.OrderByDescending(f => f.Primary).ToList();
        var result = new List<MonitorInfo>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i].Info.rcMonitor;
            var name = ordered[i].Info.szDevice?.TrimStart('\\', '.') ?? $"DISPLAY{i + 1}";
            result.Add(new MonitorInfo(i, name, r.left - vl, r.top - vt, r.right - r.left, r.bottom - r.top));
        }
        return result;
    }
}
