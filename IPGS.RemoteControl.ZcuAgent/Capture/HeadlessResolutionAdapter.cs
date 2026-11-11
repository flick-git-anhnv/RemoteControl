using System.Diagnostics;
using System.Text.RegularExpressions;

namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>
/// Thông tin trạng thái của một cổng xuất hình X11 (HDMI-1, DP-1, VGA-1...) trích xuất từ <c>xrandr --current</c>.
/// </summary>
public sealed record XrandrOutputState(
    string Name,
    bool IsConnected,
    bool IsPrimary,
    int PhysicalWidthMm,
    int PhysicalHeightMm,
    string? ActiveMode,
    string? PreferredEdidMode,
    IReadOnlyList<string> AvailableModes)
{
    /// <summary>
    /// Cổng có màn hình vật lý thực sự cắm vào khi và chỉ khi báo <c>connected</c> VÀ
    /// có thông tin EDID thực tế (kích thước vật lý mm &gt; 0 hoặc có mode mang cờ <c>+</c> preferred từ EDID).
    /// Trên các máy ZCU cấu hình <c>video=HDMI-A-1:e</c> (G019), khi rút cáp HDMI thì cổng vẫn báo
    /// <c>connected</c> nhưng kích thước là <c>0mm x 0mm</c> và không có bất kỳ mode <c>+</c> nào.
    /// </summary>
    public bool HasPhysicalMonitor =>
        IsConnected && ((PhysicalWidthMm > 0 && PhysicalHeightMm > 0) || !string.IsNullOrEmpty(PreferredEdidMode));
}

/// <summary>
/// Trạng thái tổng thể của màn hình X11 từ <c>xrandr --current</c>.
/// </summary>
public sealed record XrandrScreenState(
    int CurrentWidth,
    int CurrentHeight,
    IReadOnlyList<XrandrOutputState> Outputs,
    IReadOnlySet<string> GlobalDefinedModes);

/// <summary>
/// Loại hành động điều chỉnh độ phân giải tự động trên ZCU.
/// </summary>
public enum ResolutionAdaptationKind
{
    /// <summary>Giữ nguyên trạng thái hiện tại, không cần can thiệp.</summary>
    None,
    /// <summary>Máy đang không cắm màn hình (headless) và bị kẹt ở độ phân giải fallback thấp → nâng lên 1080p.</summary>
    ApplyHeadless1080p,
    /// <summary>Vừa cắm màn hình vật lý thực (có EDID) trong khi đang chạy mode ảo headless → trả về mode chuẩn EDID.</summary>
    RestorePhysicalMonitorAuto
}

/// <summary>
/// Quyết định điều chỉnh độ phân giải của <see cref="HeadlessResolutionAdapter"/>.
/// </summary>
public sealed record ResolutionAdaptationDecision(
    ResolutionAdaptationKind Kind,
    string TargetOutput,
    string Reason);

/// <summary>
/// Tự động nhận diện cấu hình màn hình thực tế trên máy ZCU (Linux X11):
/// <list type="bullet">
///   <item>Khi KHÔNG cắm màn hình vật lý (headless / <c>0mm x 0mm</c> không có EDID, Xorg rơi về VESA 1024x768):
///         tự động tiêm modeline chuẩn CVT <c>1920x1080_60.00</c> và kích hoạt độ phân giải 1920×1080 (16:9).</item>
///   <item>Khi CÓ cắm màn hình vật lý (có EDID thực: kích thước mm &gt; 0 hoặc mode <c>+</c>):
///         tự động trả về độ phân giải gốc của màn hình vật lý (<c>xrandr --output ... --auto</c>) nếu trước đó
///         đang chạy mode ảo headless, sau đó tôn trọng hoàn toàn cấu hình màn hình thật.</item>
/// </list>
/// </summary>
public static partial class HeadlessResolutionAdapter
{
    public const string SyntheticModeName = "1920x1080_60.00";
    public const string TargetResolutionPrefix = "1920x1080";

    // Modeline CVT chuẩn VESA cho 1920x1080 @ 60Hz
    private static readonly string[] s_newModeArgs =
    [
        "--newmode", SyntheticModeName,
        "173.00", "1920", "2048", "2248", "2576",
        "1080", "1083", "1088", "1120", "-hsync", "+vsync"
    ];

    [GeneratedRegex(@"Screen\s+\d+:.*?current\s+(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ScreenCurrentRegex();

    [GeneratedRegex(@"^([A-Za-z0-9_\-]+)\s+(connected|disconnected)\b(.*)$")]
    private static partial Regex OutputHeaderRegex();

    [GeneratedRegex(@"(\d+)mm\s*x\s*(\d+)mm")]
    private static partial Regex PhysicalMmRegex();

    [GeneratedRegex(@"^\s+([0-9]+x[0-9]+(?:_[0-9.]+)?)\s+(.+)$")]
    private static partial Regex ModeLineRegex();

    [GeneratedRegex(@"^\s*([0-9]+x[0-9]+_[0-9.]+)\s+\([0-9a-fx]+\)", RegexOptions.IgnoreCase)]
    private static partial Regex GlobalUnattachedModeRegex();

    /// <summary>
    /// Phân tích kết quả văn bản của lệnh <c>xrandr --current</c>.
    /// </summary>
    public static XrandrScreenState ParseXrandrOutput(string xrandrText)
    {
        if (string.IsNullOrWhiteSpace(xrandrText))
        {
            return new XrandrScreenState(0, 0, [], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        int currentW = 0, currentH = 0;
        var screenMatch = ScreenCurrentRegex().Match(xrandrText);
        if (screenMatch.Success)
        {
            _ = int.TryParse(screenMatch.Groups[1].Value, out currentW);
            _ = int.TryParse(screenMatch.Groups[2].Value, out currentH);
        }

        var outputs = new List<XrandrOutputState>();
        var globalModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? curName = null;
        bool curConnected = false;
        bool curPrimary = false;
        int curMmW = 0, curMmH = 0;
        string? curActiveMode = null;
        string? curPreferredMode = null;
        var curModes = new List<string>();

        void FlushCurrentOutput()
        {
            if (curName is not null)
            {
                outputs.Add(new XrandrOutputState(
                    curName,
                    curConnected,
                    curPrimary,
                    curMmW,
                    curMmH,
                    curActiveMode,
                    curPreferredMode,
                    curModes.ToArray()));
                curName = null;
            }
        }

        foreach (var rawLine in xrandrText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            var headerMatch = OutputHeaderRegex().Match(line);
            if (headerMatch.Success)
            {
                FlushCurrentOutput();

                curName = headerMatch.Groups[1].Value;
                curConnected = string.Equals(headerMatch.Groups[2].Value, "connected", StringComparison.OrdinalIgnoreCase);
                string rest = headerMatch.Groups[3].Value;
                curPrimary = rest.Contains("primary", StringComparison.OrdinalIgnoreCase);

                curMmW = 0;
                curMmH = 0;
                var mmMatch = PhysicalMmRegex().Match(rest);
                if (mmMatch.Success)
                {
                    _ = int.TryParse(mmMatch.Groups[1].Value, out curMmW);
                    _ = int.TryParse(mmMatch.Groups[2].Value, out curMmH);
                }

                curActiveMode = null;
                curPreferredMode = null;
                curModes = [];
                continue;
            }

            if (curName is not null)
            {
                var modeMatch = ModeLineRegex().Match(line);
                if (modeMatch.Success)
                {
                    string modeName = modeMatch.Groups[1].Value;
                    string flagsPart = modeMatch.Groups[2].Value;

                    curModes.Add(modeName);
                    globalModes.Add(modeName);

                    if (flagsPart.Contains('*'))
                    {
                        curActiveMode = modeName;
                    }
                    // Chỉ coi là PreferredEdidMode nếu có dấu '+' và không phải mode ảo tự tiêm
                    if (flagsPart.Contains('+') && !string.Equals(modeName, SyntheticModeName, StringComparison.OrdinalIgnoreCase))
                    {
                        curPreferredMode ??= modeName;
                    }
                    continue;
                }
            }

            var unattachedMatch = GlobalUnattachedModeRegex().Match(line);
            if (unattachedMatch.Success)
            {
                globalModes.Add(unattachedMatch.Groups[1].Value);
            }
        }

        FlushCurrentOutput();

        return new XrandrScreenState(currentW, currentH, outputs, globalModes);
    }

    /// <summary>
    /// Đưa ra quyết định có cần chuyển đổi độ phân giải hay không dựa trên trạng thái <see cref="XrandrScreenState"/>.
    /// </summary>
    public static ResolutionAdaptationDecision DecideAction(XrandrScreenState state)
    {
        if (state.Outputs.Count == 0)
        {
            return new ResolutionAdaptationDecision(ResolutionAdaptationKind.None, string.Empty, "Không tìm thấy cổng xuất hình X11 nào.");
        }

        // 1. Kiểm tra xem có màn hình vật lý thực (có EDID) đang kết nối không
        var physicalOutput = state.Outputs.FirstOrDefault(o => o.HasPhysicalMonitor);
        if (physicalOutput is not null)
        {
            // Nếu màn hình thật vừa được cắm vào một cổng đang chạy mode ảo "1920x1080_60.00" của headless
            if (string.Equals(physicalOutput.ActiveMode, SyntheticModeName, StringComparison.OrdinalIgnoreCase))
            {
                return new ResolutionAdaptationDecision(
                    ResolutionAdaptationKind.RestorePhysicalMonitorAuto,
                    physicalOutput.Name,
                    $"Phát hiện màn hình vật lý ({physicalOutput.PhysicalWidthMm}x{physicalOutput.PhysicalHeightMm}mm, EDID={physicalOutput.PreferredEdidMode ?? "auto"}) trên {physicalOutput.Name} — khôi phục độ phân giải gốc của màn hình.");
            }

            return new ResolutionAdaptationDecision(
                ResolutionAdaptationKind.None,
                physicalOutput.Name,
                $"Đang sử dụng màn hình vật lý trên {physicalOutput.Name} (mode={physicalOutput.ActiveMode ?? "auto"}).");
        }

        // 2. Không có màn hình vật lý nào có EDID (Headless mode):
        // Tìm cổng ưu tiên: cổng connected (do kernel ép video=HDMI-A-1:e) hoặc cổng HDMI/DP/VGA đầu tiên
        var targetOutput = state.Outputs.FirstOrDefault(o => o.IsConnected && o.IsPrimary)
                        ?? state.Outputs.FirstOrDefault(o => o.IsConnected)
                        ?? state.Outputs.FirstOrDefault(o => o.Name.StartsWith("HDMI", StringComparison.OrdinalIgnoreCase))
                        ?? state.Outputs[0];

        // Nếu cổng đã ở độ phân giải 1920x1080 rồi thì không cần áp lại
        if (targetOutput.ActiveMode is not null &&
            targetOutput.ActiveMode.StartsWith(TargetResolutionPrefix, StringComparison.OrdinalIgnoreCase) &&
            state.CurrentWidth == 1920 && state.CurrentHeight == 1080)
        {
            return new ResolutionAdaptationDecision(
                ResolutionAdaptationKind.None,
                targetOutput.Name,
                $"Headless trên {targetOutput.Name} đã ở độ phân giải tối ưu {targetOutput.ActiveMode}.");
        }

        return new ResolutionAdaptationDecision(
            ResolutionAdaptationKind.ApplyHeadless1080p,
            targetOutput.Name,
            $"Phát hiện trạng thái không cắm màn hình (headless 0mm x 0mm, current={state.CurrentWidth}x{state.CurrentHeight}) trên {targetOutput.Name} — tự động nâng lên {SyntheticModeName}.");
    }

    /// <summary>
    /// Kiểm tra và áp dụng độ phân giải phù hợp trên máy đích ZCU. Không bao giờ ném ngoại lệ làm gián đoạn Agent.
    /// </summary>
    public static async Task<bool> CheckAndAdaptAsync(
        Action<string>? logInfo = null,
        Action<string>? logWarn = null,
        CancellationToken ct = default)
    {
        try
        {
            var (exitCode, stdout, _) = await RunXrandrAsync(["--current"], ct);
            if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
                return false;

            var state = ParseXrandrOutput(stdout);
            var decision = DecideAction(state);

            if (decision.Kind == ResolutionAdaptationKind.None)
                return false;

            if (decision.Kind == ResolutionAdaptationKind.RestorePhysicalMonitorAuto)
            {
                logInfo?.Invoke(decision.Reason);
                var (code, _, err) = await RunXrandrAsync(["--output", decision.TargetOutput, "--auto"], ct);
                if (code != 0)
                {
                    logWarn?.Invoke($"Không thể khôi phục --auto cho {decision.TargetOutput}: {err.Trim()}");
                    return false;
                }
                return true;
            }

            if (decision.Kind == ResolutionAdaptationKind.ApplyHeadless1080p)
            {
                logInfo?.Invoke(decision.Reason);
                var outState = state.Outputs.First(o => string.Equals(o.Name, decision.TargetOutput, StringComparison.OrdinalIgnoreCase));

                if (!state.GlobalDefinedModes.Contains(SyntheticModeName))
                {
                    await RunXrandrAsync(s_newModeArgs, ct);
                }

                if (!outState.AvailableModes.Contains(SyntheticModeName, StringComparer.OrdinalIgnoreCase))
                {
                    await RunXrandrAsync(["--addmode", decision.TargetOutput, SyntheticModeName], ct);
                }

                var (setCode, _, setErr) = await RunXrandrAsync(["--output", decision.TargetOutput, "--mode", SyntheticModeName], ct);
                if (setCode != 0)
                {
                    logWarn?.Invoke($"Không thể đặt mode {SyntheticModeName} cho {decision.TargetOutput}: {setErr.Trim()}");
                    return false;
                }

                logInfo?.Invoke($"Đã tự động chuyển {decision.TargetOutput} sang chế độ Headless Full HD ({SyntheticModeName}).");
                return true;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logWarn?.Invoke($"HeadlessResolutionAdapter gặp lỗi không mong muốn: {ex.Message}");
        }

        return false;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunXrandrAsync(
        IReadOnlyList<string> args,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo("xrandr")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = Process.Start(psi);
        if (proc is null) return (-1, string.Empty, "Process.Start returned null");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(4));

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = proc.StandardError.ReadToEndAsync(cts.Token);
        await proc.WaitForExitAsync(cts.Token);

        return (proc.ExitCode, await stdoutTask, await stderrTask);
    }
}
