using System.Diagnostics;
using System.Runtime.InteropServices;
using IPGS.RemoteControl.ZcuAgent.Interop;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Chụp snapshot tải thực tế của máy ZCU theo chu kỳ (~500ms):
/// 1. X-Latency Probe: Gửi XSync rỗng trên kết nối X11 riêng và đo RTT (ms).
/// 2. CPU Pressure: Đọc /proc/pressure/cpu (some avg10 %).
/// </summary>
public sealed class MachineLoadSampler : IMachineLoadSampler
{
    public readonly record struct LoadSnapshot(double XLatencyMs, double CpuPressureAvg10, long TimestampMs);

    private readonly string? _preferredDisplay;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _sampleTask;
    private IntPtr _display = IntPtr.Zero;
    private bool _disposed;

    private LoadSnapshot _current = new(-1, -1, 0);

    public MachineLoadSampler(string? preferredDisplay, ILogger? logger = null)
    {
        _preferredDisplay = preferredDisplay;
        _logger = logger;
    }

    /// <summary>Snapshot tải máy mới nhất.</summary>
    public LoadSnapshot Current => _current;

    MachineLoad IMachineLoadSampler.Current
        => new(_current.XLatencyMs, _current.CpuPressureAvg10, _current.TimestampMs);

    /// <summary>Bắt đầu luồng lấy mẫu nền.</summary>
    public void Start(CancellationToken parentCt = default)
    {
        if (_sampleTask != null) return;

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, parentCt);
        _sampleTask = Task.Run(() => SampleLoopAsync(linkedCts.Token), linkedCts.Token);
    }

    private async Task SampleLoopAsync(CancellationToken ct)
    {
        // 1. Mở kết nối X11 riêng cho probe (nếu đang chạy trên Linux/X11)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var (handle, resolved) = X11DisplayResolver.OpenAvailableDisplay(_preferredDisplay, _logger);
                _display = handle;
                if (_display != IntPtr.Zero)
                {
                    _logger?.LogInformation("MachineLoadSampler: Mở kết nối X11 riêng tới '{Display}' thành công", resolved);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "MachineLoadSampler: Không thể mở kết nối X11 để đo độ trễ");
            }
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Đo độ trễ vòng X11 (X-Latency RTT)
                double xLatencyMs = -1;
                if (_display != IntPtr.Zero)
                {
                    var t0 = Stopwatch.GetTimestamp();
                    X11.XSync(_display, false);
                    xLatencyMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                }

                // Đo áp lực CPU qua PSI (/proc/pressure/cpu)
                double cpuPressure = ReadCpuPressureAvg10();

                _current = new LoadSnapshot(xLatencyMs, cpuPressure, Environment.TickCount64);

                await Task.Delay(500, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "MachineLoadSampler: Lỗi khi lấy mẫu tải máy");
                try
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        // Đóng kết nối X11 khi thoát
        if (_display != IntPtr.Zero)
        {
            try
            {
                X11.XCloseDisplay(_display);
            }
            catch { /* ignore */ }
            _display = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Đọc /proc/pressure/cpu và trích xuất giá trị some avg10 (%)
    /// Format mẫu: "some avg10=0.00 avg60=0.00 avg300=0.00 total=123456"
    /// </summary>
    public static double ReadCpuPressureAvg10()
    {
        try
        {
            const string path = "/proc/pressure/cpu";
            if (!File.Exists(path)) return -1;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.StartsWith("some", StringComparison.OrdinalIgnoreCase))
                {
                    var span = line.AsSpan();
                    var idx = span.IndexOf("avg10=");
                    if (idx >= 0)
                    {
                        var valSpan = span.Slice(idx + 6);
                        var spaceIdx = valSpan.IndexOf(' ');
                        if (spaceIdx > 0) valSpan = valSpan.Slice(0, spaceIdx);
                        if (double.TryParse(valSpan, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var val))
                        {
                            return val;
                        }
                    }
                }
            }
        }
        catch
        {
            // Bỏ qua nếu kernel không hỗ trợ PSI hoặc không đọc được
        }
        return -1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();

        if (_display != IntPtr.Zero)
        {
            try
            {
                X11.XCloseDisplay(_display);
            }
            catch { /* ignore */ }
            _display = IntPtr.Zero;
        }
    }
}
