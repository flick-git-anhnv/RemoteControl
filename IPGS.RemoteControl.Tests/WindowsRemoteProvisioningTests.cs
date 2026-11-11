using System;
using System.IO;
using System.Xml.Linq;
using IPGS.RemoteControl.CcuClient;
using IPGS.RemoteControl.CcuUI.Views;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class WindowsRemoteProvisioningTests
{
    // ── 1. ShellQuote: PowerShell Quoting & Windows Username Validation ──

    [Fact]
    public void QuotePowerShell_NormalString_WrapsInSingleQuotes()
    {
        Assert.Equal("'C:\\Program Files\\KztekWinAgent'", ShellQuote.QuotePowerShell(@"C:\Program Files\KztekWinAgent"));
    }

    [Fact]
    public void QuotePowerShell_SingleQuotesInside_DoublesSingleQuotes()
    {
        string input = @"C:\Users\O'Connor\App's Data";
        Assert.Equal(@"'C:\Users\O''Connor\App''s Data'", ShellQuote.QuotePowerShell(input));
    }

    [Fact]
    public void QuotePowerShell_CommandInjectionCharacters_AreNeutralizedAndNewlinesStripped()
    {
        string injection = "test; Stop-Computer -Force; $(whoami) `$env:USERNAME\r\nmalicious";
        string quoted = ShellQuote.QuotePowerShell(injection);

        Assert.StartsWith("'", quoted);
        Assert.EndsWith("'", quoted);
        Assert.DoesNotContain("\r", quoted);
        Assert.DoesNotContain("\n", quoted);
        Assert.Equal("'test; Stop-Computer -Force; $(whoami) `$env:USERNAMEmalicious'", quoted);
    }

    [Theory]
    [InlineData("kztek")]
    [InlineData("Administrator")]
    [InlineData(@"KZTEK\operator_01")]
    [InlineData("admin@kztek.local")]
    [InlineData("Kztek User 01")]
    public void ValidateWindowsUsername_ValidNames_ReturnsInput(string username)
    {
        Assert.Equal(username, ShellQuote.ValidateWindowsUsername(username, "user"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("admin;calc.exe")]
    [InlineData("admin|iex")]
    [InlineData("admin&whoami")]
    [InlineData("user$(id)")]
    [InlineData("user`calc`")]
    [InlineData("user'quote")]
    [InlineData("user\"quote")]
    [InlineData(@"..\admin")]
    [InlineData(@"DOMAIN\\user")]
    [InlineData(@"DOMAIN\")]
    public void ValidateWindowsUsername_MaliciousOrInvalidNames_ThrowsArgumentException(string invalidUser)
    {
        Assert.Throws<ArgumentException>(() => ShellQuote.ValidateWindowsUsername(invalidUser, "user"));
    }

    // ── 2. ShellQuote: Multi-OS SFTP Path Normalization & Navigation ──

    [Theory]
    [InlineData("/C:/Users/kztek", true)]
    [InlineData("C:/Users/kztek", true)]
    [InlineData(@"C:\Users\kztek", true)]
    [InlineData("/D:/", true)]
    [InlineData("/C:", true)]
    [InlineData("/home/kztek", false)]
    [InlineData("/var/log/syslog", false)]
    [InlineData("/", false)]
    [InlineData("", false)]
    public void IsWindowsSftpPath_DetectsDrivePathsCorrectly(string path, bool expected)
    {
        Assert.Equal(expected, ShellQuote.IsWindowsSftpPath(path));
    }

    [Theory]
    [InlineData(@"C:\Users\kztek\Desktop\", "/C:/Users/kztek/Desktop")]
    [InlineData("/c:/Users//kztek/", "/C:/Users/kztek")]
    [InlineData("C:", "/C:/")]
    [InlineData("/C:", "/C:/")]
    [InlineData("/D:/", "/D:/")]
    [InlineData("/home/kztek//logs/", "/home/kztek/logs")]
    [InlineData("home/kztek", "/home/kztek")]
    [InlineData("/", "/")]
    [InlineData("", "/")]
    public void NormalizeSftpPath_NormalizesWindowsAndLinuxPaths(string input, string expected)
    {
        Assert.Equal(expected, ShellQuote.NormalizeSftpPath(input));
    }

    [Theory]
    [InlineData("/C:/Users/kztek/Downloads/app.msi", @"C:\Users\kztek\Downloads\app.msi")]
    [InlineData("C:/Program Files/KztekWinAgent", @"C:\Program Files\KztekWinAgent")]
    [InlineData("/C:/", @"C:\")]
    [InlineData("/D:", @"D:\")]
    public void ToWindowsLocalPath_ConvertsSftpPathToNativeWindowsPath(string sftpPath, string expectedWinPath)
    {
        Assert.Equal(expectedWinPath, ShellQuote.ToWindowsLocalPath(sftpPath));
    }

    [Theory]
    [InlineData("/C:/Users/kztek/Desktop", "/C:/Users/kztek")]
    [InlineData("/C:/Users/kztek", "/C:/Users")]
    [InlineData("/C:/Users", "/C:/")]
    [InlineData("/C:/", "/C:/")]
    [InlineData("/C:", "/C:/")]
    [InlineData("/home/kztek/Desktop", "/home/kztek")]
    [InlineData("/home/kztek", "/home")]
    [InlineData("/home", "/")]
    [InlineData("/", "/")]
    public void GetParentSftpPath_NavigatesUpCleanlyOnBothOs(string current, string expectedParent)
    {
        Assert.Equal(expectedParent, ShellQuote.GetParentSftpPath(current));
    }

    [Theory]
    [InlineData("/C:/Users/kztek", "Downloads", "/C:/Users/kztek/Downloads")]
    [InlineData("/C:/", "Program Files", "/C:/Program Files")]
    [InlineData("/home/kztek", "setup.deb", "/home/kztek/setup.deb")]
    [InlineData("/", "opt", "/opt")]
    public void CombineSftpPath_JoinsBaseAndChildCleanly(string basePath, string child, string expected)
    {
        Assert.Equal(expected, ShellQuote.CombineSftpPath(basePath, child));
    }

    // ── 3. RemoteOsDetector & ZcuRemoteInstallerService ──

    [Theory]
    [InlineData("Linux\n", RemoteOsType.Linux)]
    [InlineData("GNU/Linux", RemoteOsType.Linux)]
    [InlineData("Darwin", RemoteOsType.Linux)]
    [InlineData("", RemoteOsType.Linux)]
    [InlineData("Windows_NT", RemoteOsType.Windows)]
    [InlineData("SSH-2.0-OpenSSH_for_Windows_9.5", RemoteOsType.Windows)]
    [InlineData("Microsoft Windows [Version 10.0.19045.5011]", RemoteOsType.Windows)]
    [InlineData("Microsoft Windows [Version 10.0.22631.4317]", RemoteOsType.Windows)]
    [InlineData("uname : The term 'uname' is not recognized as the name of a cmdlet", RemoteOsType.Windows)]
    [InlineData("FullyQualifiedErrorId : CommandNotFoundException", RemoteOsType.Windows)]
    [InlineData("CategoryInfo : ObjectNotFound: (uname:String) [], CommandNotFoundException", RemoteOsType.Windows)]
    [InlineData("'uname' is not recognized as an internal or external command", RemoteOsType.Windows)]
    [InlineData("MINGW64_NT-10.0-19045", RemoteOsType.Windows)]
    public void ParseOsDetectionOutput_IdentifiesLinuxAndWindowsCorrectly(string probeOutput, RemoteOsType expectedOs)
    {
        Assert.Equal(expectedOs, RemoteOsDetector.ParseOsDetectionOutput(probeOutput));
    }

    [Fact]
    public void ParseAgentConfigJson_ValidJsonWithBomAndComments_ExtractsPortAndToken()
    {
        string json = "\uFEFF{\n  // Comment\n  \"RemoteControl\": {\n    \"Port\": 18600,\n    \"Token\": \"secret-win-token-123\",\n  }\n}";
        var parsed = ZcuRemoteInstallerService.ParseAgentConfigJson(json);

        Assert.NotNull(parsed);
        Assert.Equal(18600, parsed.Value.Port);
        Assert.Equal("secret-win-token-123", parsed.Value.Token);
    }

    [Fact]
    public void ParseAgentConfigJson_MissingPort_DefaultsTo17600()
    {
        string json = "{ \"RemoteControl\": { \"Token\": \"abc\" } }";
        var parsed = ZcuRemoteInstallerService.ParseAgentConfigJson(json);

        Assert.NotNull(parsed);
        Assert.Equal(17600, parsed.Value.Port);
        Assert.Equal("abc", parsed.Value.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{ \"RemoteControl\": { \"Token\": \"\" } }")]
    [InlineData("invalid json")]
    public void ParseAgentConfigJson_InvalidOrEmptyToken_ReturnsNull(string json)
    {
        Assert.Null(ZcuRemoteInstallerService.ParseAgentConfigJson(json));
    }

    // ── 4. ComputerStatusChecker: Multi-OS Metrics Parsing ──

    [Fact]
    public void BuildWindowsMetricsCommand_ContainsExpectedCimQueries()
    {
        string cmd = ComputerStatusChecker.BuildWindowsMetricsCommand();
        Assert.Contains("Win32_Processor", cmd);
        Assert.Contains("Win32_OperatingSystem", cmd);
        Assert.Contains("Win32_LogicalDisk", cmd);
        Assert.Contains("InvariantCulture", cmd);
    }

    [Fact]
    public void ParseMetricsOutput_StandardThreeLines_ParsesCpuRamAndDisk()
    {
        string sample = "18.4\r\n3890 16384\r\n85.5G 237.9G 36%";
        var (cpu, ram, disk) = ComputerStatusChecker.ParseMetricsOutput(sample);

        Assert.Equal("CPU: 18.4%", cpu);
        Assert.Equal("RAM: 3890/16384 MB (24%)", ram);
        Assert.Equal("Disk: 85.5G/237.9G (36%)", disk);
    }

    [Fact]
    public void ParseMetricsOutput_CommaDecimalLocale_ParsesCorrectly()
    {
        string sample = "7,5\n2048 4096\n12,0G 40,0G 30%";
        var (cpu, ram, disk) = ComputerStatusChecker.ParseMetricsOutput(sample);

        Assert.Equal("CPU: 7.5%", cpu);
        Assert.Equal("RAM: 2048/4096 MB (50%)", ram);
        Assert.Equal("Disk: 12,0G/40,0G (30%)", disk);
    }

    [Fact]
    public void ParseMetricsOutput_EmptyOutput_ReturnsNullTuple()
    {
        var (cpu, ram, disk) = ComputerStatusChecker.ParseMetricsOutput("   \r\n  ");
        Assert.Null(cpu);
        Assert.Null(ram);
        Assert.Null(disk);
    }

    // ── 5. RemoteCommandWindow: Windows Command Translation & SFTP Helpers ──

    [Theory]
    [InlineData("sudo reboot", "Restart-Computer -Force")]
    [InlineData("sudo poweroff", "Stop-Computer -Force")]
    [InlineData("sudo shutdown -h now", "Stop-Computer -Force")]
    [InlineData("sudo systemctl restart ssh", "Restart-Service sshd -Force")]
    [InlineData("Get-Service sshd", "Get-Service sshd")]
    public void TranslateQuickCommandForWindows_MapsLinuxQuickCommandsToPowerShell(string input, string expected)
    {
        Assert.Equal(expected, RemoteCommandWindow.TranslateQuickCommandForWindows(input));
    }

    [Fact]
    public void BuildOpenTerminalHereCommand_AdaptsToWindowsAndLinuxPaths()
    {
        string winCmd = RemoteCommandWindow.BuildOpenTerminalHereCommand("/C:/Users/kztek/Projects");
        Assert.Equal("Set-Location -LiteralPath 'C:\\Users\\kztek\\Projects'; Get-ChildItem", winCmd);

        string linuxCmd = RemoteCommandWindow.BuildOpenTerminalHereCommand("/home/kztek/projects");
        Assert.Equal("cd '/home/kztek/projects' && ls -la", linuxCmd);
    }

    [Fact]
    public void BuildRecursiveDeleteCommand_AdaptsToWindowsAndLinuxPaths()
    {
        string winDel = RemoteCommandWindow.BuildRecursiveDeleteCommand("/C:/Users/kztek/OldFolder");
        Assert.Equal("powershell.exe -NoProfile -NonInteractive -Command \"Remove-Item -LiteralPath 'C:\\Users\\kztek\\OldFolder' -Recurse -Force\"", winDel);

        string linuxDel = RemoteCommandWindow.BuildRecursiveDeleteCommand("/home/kztek/old_folder");
        Assert.Equal("rm -rf '/home/kztek/old_folder'", linuxDel);
    }

    // ── 6. HealthMonitorWindow: Windows Health Output Parsing & Kill Process ──

    [Fact]
    public void ParseHealthOutput_WindowsFiveSectionOutput_ExtractsAllMetricsAndProcesses()
    {
        string raw = """
            ===UPTIME===
            3d 14:22:09
            ===CPU===
            24.6
            ===MEM===
            Mem: 16384 6144 10240
            ===DISK===
            Filesystem Size Used Avail Use% Mounted
            C: 238.0G 95.2G 142.8G 40% C:
            ===PROCS===
            4120|windows|152.3|8.4|IPGS.RemoteControl.WinAgent
            1892|windows|45.0|3.1|explorer
            """;

        HealthParseResult res = HealthMonitorWindow.ParseHealthOutput(raw);

        Assert.Equal("3d 14:22:09", res.Uptime);
        Assert.Equal(24.6, res.CpuPercent);
        Assert.Equal(16384, res.RamTotalMb);
        Assert.Equal(6144, res.RamUsedMb);
        Assert.Equal("238.0G", res.DiskTotal);
        Assert.Equal("95.2G", res.DiskUsed);
        Assert.Equal(40.0, res.DiskPercent);
        Assert.Equal(2, res.Processes.Count);
        Assert.Equal("4120", res.Processes[0].Pid);
        Assert.Equal("windows", res.Processes[0].User);
        Assert.Equal("152.3", res.Processes[0].Cpu);
        Assert.Equal("8.4", res.Processes[0].Mem);
        Assert.Equal("IPGS.RemoteControl.WinAgent", res.Processes[0].Command);
    }

    [Fact]
    public void BuildKillProcessCommand_GeneratesPowerShellForWindowsAndKillForLinux()
    {
        string winKill = HealthMonitorWindow.BuildKillProcessCommand(4120, "9", RemoteOsType.Windows);
        Assert.Equal("powershell.exe -NoProfile -NonInteractive -Command \"Stop-Process -Id 4120 -Force -ErrorAction Stop\"", winKill);

        string linuxKill = HealthMonitorWindow.BuildKillProcessCommand(4120, "15", RemoteOsType.Linux);
        Assert.Equal("LC_ALL=C kill -15 4120", linuxKill);
    }

    // ── 7. RemoteAppInstallService: Package Validation & Silent Install Command ──

    [Theory]
    [InlineData(@"C:\Installers\setup.msi", RemoteOsType.Windows)]
    [InlineData(@"C:\Installers\app_installer.exe", RemoteOsType.Windows)]
    [InlineData("/home/kztek/ipgs_1.0_amd64.deb", RemoteOsType.Linux)]
    [InlineData("/home/kztek/install.sh", RemoteOsType.Linux)]
    [InlineData("/home/kztek/bundle.run", RemoteOsType.Linux)]
    public void ValidateInstallerPackagePath_MatchingOsAndExtension_Succeeds(string path, RemoteOsType os)
    {
        Assert.Equal(path, RemoteAppInstallService.ValidateInstallerPackagePath(path, os));
    }

    [Fact]
    public void ValidateInstallerPackagePath_CrossOsMismatch_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RemoteAppInstallService.ValidateInstallerPackagePath("/home/kztek/app.deb", RemoteOsType.Windows));

        Assert.Throws<InvalidOperationException>(() =>
            RemoteAppInstallService.ValidateInstallerPackagePath(@"C:\Temp\setup.msi", RemoteOsType.Linux));
    }

    [Theory]
    [InlineData(@"C:\Temp\archive.zip")]
    [InlineData("/home/kztek/readme.txt")]
    [InlineData("")]
    public void ValidateInstallerPackagePath_UnsupportedExtension_ThrowsArgumentException(string invalidPath)
    {
        Assert.Throws<ArgumentException>(() => RemoteAppInstallService.ValidateInstallerPackagePath(invalidPath));
    }

    [Fact]
    public void BuildWindowsInstallCommand_MsiAndExe_GeneratesSilentCommands()
    {
        string msiCmd = RemoteAppInstallService.BuildWindowsInstallCommand(@"C:\Users\Public\kztek-app.msi");
        Assert.Contains("msiexec.exe /i 'C:\\Users\\Public\\kztek-app.msi' /qn /norestart", msiCmd);

        string exeCmd = RemoteAppInstallService.BuildWindowsInstallCommand(@"C:\Users\Public\kztek-setup.exe");
        Assert.Contains("& 'C:\\Users\\Public\\kztek-setup.exe' /S", exeCmd);
    }

    // ── 8. Windows Zero-Touch Bootstrap Scripts Integrity ──

    [Fact]
    public void WindowsBootstrapScripts_ExistAndContainRequiredProvisioningSettings()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "IPGS.RemoteControl.sln")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(dir);
        string scriptsDir = Path.Combine(dir!, "scripts", "windows-agent");

        string bootstrapPs1 = Path.Combine(scriptsDir, "bootstrap-windows-ssh.ps1");
        string oneClickBat = Path.Combine(scriptsDir, "1-click-enable-ssh.bat");
        string autounattendXml = Path.Combine(scriptsDir, "autounattend.xml");
        string installAgentPs1 = Path.Combine(scriptsDir, "install-win-agent.ps1");

        Assert.True(File.Exists(bootstrapPs1), "Missing bootstrap-windows-ssh.ps1");
        Assert.True(File.Exists(oneClickBat), "Missing 1-click-enable-ssh.bat");
        Assert.True(File.Exists(autounattendXml), "Missing autounattend.xml");
        Assert.True(File.Exists(installAgentPs1), "Missing install-win-agent.ps1");

        string ps1Content = File.ReadAllText(bootstrapPs1);
        Assert.Contains("sshd", ps1Content);
        Assert.Contains("DefaultShell", ps1Content);
        Assert.Contains(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", ps1Content);
        Assert.Contains("icacls", ps1Content);
        Assert.Contains("Remove-ItemProperty", ps1Content);
        Assert.DoesNotContain("-NoLogo -Command", ps1Content);
        Assert.Contains("123456", ps1Content);
        Assert.Contains("ICMPv4", ps1Content);
        Assert.Contains("AutoAdminLogon", ps1Content);

        // Verify UTF-8 BOM on .ps1 scripts for Windows PowerShell 5.1 compatibility
        byte[] ps1Bytes = File.ReadAllBytes(bootstrapPs1);
        Assert.True(ps1Bytes.Length >= 3 && ps1Bytes[0] == 0xEF && ps1Bytes[1] == 0xBB && ps1Bytes[2] == 0xBF);

        // Verify 1-click-enable-ssh.bat uses REM comments and has no UTF-8 BOM
        byte[] batBytes = File.ReadAllBytes(oneClickBat);
        Assert.False(batBytes.Length >= 3 && batBytes[0] == 0xEF && batBytes[1] == 0xBB && batBytes[2] == 0xBF);
        string batContent = File.ReadAllText(oneClickBat);
        Assert.Contains("REM", batContent);

        // Verify autounattend.xml is well-formed XML and contains required Zero-Touch settings
        string xmlContent = File.ReadAllText(autounattendXml);
        Assert.Contains("<Value>123456</Value>", xmlContent);
        Assert.Contains("BypassNRO", xmlContent);
        Assert.Contains(@"C:\Windows\Setup\Scripts\SetupComplete.cmd", xmlContent);
        Assert.True(File.Exists(Path.Combine(scriptsDir, "SetupComplete.cmd")), "Missing SetupComplete.cmd");
        Assert.DoesNotContain("DefaultShellCommandOption", xmlContent);

        XDocument doc = XDocument.Load(autounattendXml);
        Assert.NotNull(doc.Root);
        Assert.Equal("unattend", doc.Root!.Name.LocalName);
    }
}
