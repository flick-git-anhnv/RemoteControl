using System.Globalization;
using System.Text.RegularExpressions;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// Đại diện cho một tiến trình được phân tích từ đầu ra của lệnh top trên Linux.
/// </summary>
public record ParsedProcessItem(string Pid, string User, string Cpu, string Mem, string Command);

/// <summary>
/// Kết quả phân tích tổng tải CPU và danh sách tiến trình từ lệnh top.
/// </summary>
public record TopParseResult(double? TotalCpuPercent, IReadOnlyList<ParsedProcessItem> Processes);

/// <summary>
/// Bộ phân tích đầu ra của lệnh top trên Linux.
/// Hỗ trợ cả định dạng số chuẩn tiếng Anh (dấu chấm '.') và các locale châu Âu / Việt Nam (dấu phẩy ',').
/// Hỗ trợ trích xuất số liệu từ lần lấy mẫu thứ 2 của lệnh top (steady-state) để loại bỏ startup spike.
/// </summary>
public static class TopParser
{
    /// <summary>
    /// Lệnh top chuẩn: chạy batch mode với 2 lần lấy mẫu, khoảng cách 0.5s, ép LC_ALL=C để chuẩn hoá output.
    /// -w 512: nới độ rộng dòng để cột COMMAND không bị top cắt cụt thành "tên+".
    /// </summary>
    public const string DefaultTopCommand = "LC_ALL=C top -b -n 2 -d 0.5 -w 512";

    /// <summary>
    /// Phân tích chuỗi đầu ra từ lệnh top để tính tổng tải CPU và danh sách top tiến trình.
    /// </summary>
    public static TopParseResult Parse(string? topOutput, int maxProcesses = 5)
    {
        if (string.IsNullOrWhiteSpace(topOutput))
        {
            return new TopParseResult(null, Array.Empty<ParsedProcessItem>());
        }

        var lines = topOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // 1. Phân tích CPU: Lấy dòng %Cpu từ lần lấy mẫu cuối cùng (mẫu steady-state)
        double? totalCpu = null;
        var cpuLine = lines.LastOrDefault(l => l.StartsWith("%Cpu") || l.Contains("Cpu(s)"));
        if (cpuLine != null)
        {
            // Bắt trường idle (% id). Hỗ trợ cả số nguyên, số thập phân dấu chấm (.) và dấu phẩy (,)
            var idleMatch = Regex.Match(cpuLine, @"(\d+(?:[.,]\d+)?)\s*id");
            if (idleMatch.Success)
            {
                string idleStr = idleMatch.Groups[1].Value.Replace(',', '.');
                if (double.TryParse(idleStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double idleVal))
                {
                    totalCpu = Math.Clamp(100.0 - idleVal, 0.0, 100.0);
                }
            }

            // Fallback: nếu không tìm thấy idle, tính dựa trên % us (user CPU)
            if (totalCpu == null)
            {
                var usMatch = Regex.Match(cpuLine, @"(\d+(?:[.,]\d+)?)\s*us");
                if (usMatch.Success)
                {
                    string usStr = usMatch.Groups[1].Value.Replace(',', '.');
                    if (double.TryParse(usStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double usVal))
                    {
                        totalCpu = Math.Clamp(usVal, 0.0, 100.0);
                    }
                }
            }
        }

        // 2. Phân tích Processes: Lấy bảng tiến trình từ lần lấy mẫu cuối cùng
        var processes = new List<ParsedProcessItem>();
        int pidIndex = Array.FindLastIndex(lines, l => l.Contains("PID") && l.Contains("USER"));
        if (pidIndex >= 0)
        {
            for (int i = pidIndex + 1; i < lines.Length && processes.Count < maxProcesses; i++)
            {
                var parts = lines[i].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 12)
                {
                    processes.Add(new ParsedProcessItem(
                        Pid: parts[0],
                        User: parts[1],
                        Cpu: parts[8].Replace(',', '.'),
                        Mem: parts[9].Replace(',', '.'),
                        Command: parts[11]));
                }
                else if (parts.Length >= 11)
                {
                    processes.Add(new ParsedProcessItem(
                        Pid: parts[0],
                        User: parts[1],
                        Cpu: parts[8].Replace(',', '.'),
                        Mem: parts[9].Replace(',', '.'),
                        Command: parts[10]));
                }
            }
        }

        return new TopParseResult(totalCpu, processes);
    }
}
