using IPGS.RemoteControl.CcuClient;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class TopParserTests
{
    [Fact]
    public void Parse_NullOrEmpty_ReturnsNullCpuAndEmptyProcesses()
    {
        var result = TopParser.Parse(null);
        Assert.Null(result.TotalCpuPercent);
        Assert.Empty(result.Processes);

        var emptyResult = TopParser.Parse("   \r\n  ");
        Assert.Null(emptyResult.TotalCpuPercent);
        Assert.Empty(emptyResult.Processes);
    }

    [Fact]
    public void Parse_EnglishLocale_StandardDot_CalculatesAccurately()
    {
        string sample = """
top - 13:30:00 up 2:30, 1 user, load average: 1.05, 1.10, 1.08
Tasks: 180 total, 1 running, 179 sleeping, 0 stopped, 0 zombie
%Cpu(s):  5.0 us,  2.0 sy,  0.0 ni, 93.0 id,  0.0 wa,  0.0 hi,  0.0 si,  0.0 st
MiB Mem : 3823.3 total, 2484.6 free, 537.7 used, 801.1 buff/cache

    PID USER      PR  NI    VIRT    RES    SHR S  %CPU  %MEM     TIME+ COMMAND
   7145 ccu       20   0  262.5g 426940 205212 R  12.5  10.9  68:46.79 IPGSv4
   7063 ccu       20   0  261.6g 130852  77784 S   5.0   3.3   1:36.15 IPGS.RemoteCont
    740 root      20   0  567004 105848  71204 S   2.5   2.7   2:07.35 Xorg
""";

        var result = TopParser.Parse(sample);

        // 100 - 93.0 = 7.0%
        Assert.NotNull(result.TotalCpuPercent);
        Assert.Equal(7.0, result.TotalCpuPercent!.Value, 1);

        Assert.Equal(3, result.Processes.Count);
        Assert.Equal("7145", result.Processes[0].Pid);
        Assert.Equal("12.5", result.Processes[0].Cpu);
        Assert.Equal("IPGSv4", result.Processes[0].Command);
    }

    [Fact]
    public void Parse_VietnameseLocale_CommaDecimal_CalculatesAccuratelyWithoutDroppingIntegerPart()
    {
        // Kiểm tra lỗi ngầm: vi_VN in '92,7 id' hoặc '67,5 id'.
        // Parser cũ dùng regex [\d\.]+ chỉ bắt được '7 id' hoặc '5 id', tính ra 93% hoặc 95% CPU!
        string sample = """
top - 13:34:49 up  2:39,  1 user,  load average: 1,18, 1,18, 1,09
Tasks: 185 total,   2 running, 183 sleeping,   0 stopped,   0 zombie
%Cpu(s): 21,2 us, 11,2 sy,  0,0 ni, 67,5 id,  0,0 wa,  0,0 hi,  0,0 si,  0,0 st
MiB Mem :   3823,3 total,   2181,9 free,    738,4 used,    903,0 buff/cache

    PID USER      PR  NI    VIRT    RES    SHR S  %CPU  %MEM     TIME+ COMMAND
   7145 ccu       20   0  262,5g 426940 205212 R  88,9  10,9  68:46.79 IPGSv4
  11310 ccu       20   0   15956   4224   3584 R  11,1   0,1   0:00.05 top
    740 root      20   0  567004 105848  71204 S   5,6   2,7   2:07.35 Xorg
   7063 ccu       20   0  261,6g 130852  77784 S   5,6   3,3   1:36.15 IPGS.Re+
""";

        var result = TopParser.Parse(sample);

        // 100 - 67.5 = 32.5% CPU (thay vì 100 - 5 = 95% do lỗi cũ!)
        Assert.NotNull(result.TotalCpuPercent);
        Assert.Equal(32.5, result.TotalCpuPercent!.Value, 1);

        Assert.Equal(4, result.Processes.Count);
        // Cpu và Mem đều được chuẩn hoá thành dấu chấm '.'
        Assert.Equal("88.9", result.Processes[0].Cpu);
        Assert.Equal("10.9", result.Processes[0].Mem);
    }

    [Fact]
    public void Parse_TwoIterationsBatchOutput_PicksSecondIterationForSteadyState()
    {
        // Khi chạy top -b -n 2, mẫu đầu tiên thường bị spike (top ăn 11%), mẫu 2 phản ánh tải thực tế
        string twoIterations = """
top - 13:34:49 up  2:39,  1 user,  load average: 1.18, 1.18, 1.09
%Cpu(s): 21.2 us, 11.2 sy,  0.0 ni, 67.5 id,  0.0 wa,  0.0 hi,  0.0 si,  0.0 st
    PID USER      PR  NI    VIRT    RES    SHR S  %CPU  %MEM     TIME+ COMMAND
  11310 ccu       20   0   15956   4224   3584 R  11.1   0.1   0:00.05 top
   7145 ccu       20   0  262.5g 426940 205212 R  88.9  10.9  68:46.79 IPGSv4

top - 13:34:50 up  2:39,  1 user,  load average: 1.18, 1.18, 1.09
%Cpu(s):  2.4 us,  1.4 sy,  0.0 ni, 96.2 id,  0.0 wa,  0.0 hi,  0.0 si,  0.0 st
    PID USER      PR  NI    VIRT    RES    SHR S  %CPU  %MEM     TIME+ COMMAND
   7145 ccu       20   0  262.5g 426940 205212 R  12.1  10.9  68:48.02 IPGSv4
   7063 ccu       20   0  261.6g 130852  77784 S   2.8   3.3   1:36.23 IPGS.RemoteCont
  11313 ccu       20   0   15956   4224   3584 R   1.5   0.1   0:00.09 top
""";

        var result = TopParser.Parse(twoIterations);

        // Phải lấy số liệu từ mẫu thứ 2: 100 - 96.2 = 3.8% (thay vì 100 - 67.5 = 32.5% của mẫu 1)
        Assert.NotNull(result.TotalCpuPercent);
        Assert.Equal(3.8, result.TotalCpuPercent!.Value, 1);

        // Phải lấy danh sách tiến trình từ mẫu thứ 2
        Assert.Equal(3, result.Processes.Count);
        Assert.Equal("7145", result.Processes[0].Pid);
        Assert.Equal("12.1", result.Processes[0].Cpu);
        Assert.Equal("1.5", result.Processes[2].Cpu); // top chỉ còn 1.5% ở mẫu 2
    }

    [Fact]
    public void Parse_FallbackToUs_WhenIdleMissing()
    {
        string sample = """
%Cpu(s): 18,4 us,  5,2 sy,  0,0 ni
    PID USER      PR  NI    VIRT    RES    SHR S  %CPU  %MEM     TIME+ COMMAND
   7145 ccu       20   0  262,5g 426940 205212 R  18,4  10,9  68:46.79 IPGSv4
""";

        var result = TopParser.Parse(sample);

        Assert.NotNull(result.TotalCpuPercent);
        Assert.Equal(18.4, result.TotalCpuPercent!.Value, 1);
    }
}
