using IPGS.RemoteControl.ZcuAgent.Capture;

namespace IPGS.RemoteControl.ZcuAgent.Platform;

/// <summary>Liệt kê màn hình trên Linux qua <c>xrandr --listmonitors</c> (xem <see cref="MonitorLister"/>).</summary>
internal sealed class XrandrMonitorProvider : IMonitorProvider
{
    public Task<List<MonitorInfo>> ListAsync(CancellationToken ct) => MonitorLister.ListAsync(ct);
}
