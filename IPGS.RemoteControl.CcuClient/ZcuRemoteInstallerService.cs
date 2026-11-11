using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuClient
{
    /// <summary>
    /// Hệ điều hành của máy đích nhận diện qua SSH.
    /// </summary>
    public enum RemoteOsType
    {
        Linux,
        Windows
    }

    /// <summary>
    /// Helper tĩnh nhận diện HĐH máy đích qua SSH (dùng chung cho Installer, StatusChecker,
    /// RemoteCommandWindow, BulkActionWindow, HealthMonitorWindow, RemoteAppInstallService).
    /// </summary>
    public static class RemoteOsDetector
    {
        public static RemoteOsType ParseOsDetectionOutput(string? output)
            => ZcuRemoteInstallerService.ParseOsDetectionOutput(output);

        public static RemoteOsType DetectRemoteOs(SshClient ssh)
            => ZcuRemoteInstallerService.DetectRemoteOs(ssh);

        public static Task<RemoteOsType> DetectRemoteOsAsync(SshClient ssh, CancellationToken cancellationToken = default)
            => ZcuRemoteInstallerService.DetectRemoteOsAsync(ssh, cancellationToken);
    }

    public class SshInstallerOptions
    {
        public string Host { get; set; } = string.Empty;
        public int SshPort { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string KeyFilePath { get; set; } = string.Empty;
        public string KeyPassphrase { get; set; } = string.Empty;

        public int AgentPort { get; set; } = 17600;
        public string AgentToken { get; set; } = string.Empty;

        /// <summary>
        /// F06: mặc định giới hạn 3 dải LAN riêng (RFC 1918) thay vì mở toàn mạng
        /// <c>0.0.0.0/0</c> — mọi ZCU/CCU nội bộ (VD 192.168.0.101) vẫn kết nối bình thường,
        /// nhưng IP public/ngoài LAN bị chặn ngay tầng whitelist của agent.
        /// Hỗ trợ nhiều CIDR phân tách bằng dấu phẩy/chấm phẩy.
        /// </summary>
        public const string DefaultLanAllowedClientIPs = "0.0.0.0/0";

        public string AllowedClientIPs { get; set; } = DefaultLanAllowedClientIPs;
        public int TargetFps { get; set; } = 30;
        public int JpegQuality { get; set; } = 70;
        public string? PublishSourceDir { get; set; }
    }

    public class ZcuRemoteInstallerService
    {
        public const string WindowsDefaultInstallDir = @"C:\Program Files\KztekWinAgent";
        public const string WindowsScheduledTaskName = "KztekWinAgent";
        public const string WindowsAgentExeName = "IPGS.RemoteControl.WinAgent.exe";
        public const string LinuxAgentExeName = "IPGS.RemoteControl.ZcuAgent";

        private readonly ILogger<ZcuRemoteInstallerService>? _logger;

        public ZcuRemoteInstallerService(ILogger<ZcuRemoteInstallerService>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Phân tích chuỗi output từ lệnh dò HĐH (<c>uname -s</c>, <c>cmd.exe /c ver</c>, <c>$env:OS</c>)
        /// hoặc chuỗi <c>ServerVersion</c> của SSH banner để xác định máy đích là Linux hay Windows.
        /// </summary>
        public static RemoteOsType ParseOsDetectionOutput(string? output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return RemoteOsType.Linux;

            if (output.Contains("OpenSSH_for_Windows", StringComparison.OrdinalIgnoreCase)
                || output.Contains("Microsoft Windows", StringComparison.OrdinalIgnoreCase)
                || output.Contains("Windows_NT", StringComparison.OrdinalIgnoreCase)
                || output.Contains("MINGW", StringComparison.OrdinalIgnoreCase)
                || output.Contains("MSYS", StringComparison.OrdinalIgnoreCase)
                || output.Contains("CYGWIN", StringComparison.OrdinalIgnoreCase)
                || output.Contains("The term 'uname' is not recognized", StringComparison.OrdinalIgnoreCase)
                || output.Contains("is not recognized as an internal or external command", StringComparison.OrdinalIgnoreCase)
                || output.Contains("FullyQualifiedErrorId", StringComparison.OrdinalIgnoreCase)
                || output.Contains("CommandNotFoundException", StringComparison.OrdinalIgnoreCase)
                || output.Contains("ObjectNotFound", StringComparison.OrdinalIgnoreCase)
                || output.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            {
                return RemoteOsType.Windows;
            }

            if (output.Contains("Linux", StringComparison.OrdinalIgnoreCase)
                || output.Contains("GNU", StringComparison.OrdinalIgnoreCase)
                || output.Contains("Darwin", StringComparison.OrdinalIgnoreCase))
            {
                return RemoteOsType.Linux;
            }

            return RemoteOsType.Linux;
        }

        /// <summary>
        /// Tự động nhận diện hệ điều hành của máy đích qua phiên SSH đang mở.
        /// Ưu tiên kiểm tra tức thì <c>ssh.ConnectionInfo?.ServerVersion</c> (0ms, không phụ thuộc shell),
        /// sau đó fallback qua <c>uname -s</c> và <c>cmd.exe /c ver</c>.
        /// </summary>
        public static RemoteOsType DetectRemoteOs(SshClient ssh)
        {
            if (ssh is null) throw new ArgumentNullException(nameof(ssh));

            string? serverVersion = ssh.ConnectionInfo?.ServerVersion;
            if (!string.IsNullOrWhiteSpace(serverVersion)
                && serverVersion.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            {
                return RemoteOsType.Windows;
            }

            try
            {
                using var unameCmd = ssh.CreateCommand("uname -s");
                unameCmd.CommandTimeout = TimeSpan.FromSeconds(8);
                unameCmd.Execute();
                string unameOut = (unameCmd.Result ?? string.Empty).Trim();
                if (unameOut.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                    return RemoteOsType.Linux;

                string combinedUname = unameOut + "\n" + (unameCmd.Error ?? string.Empty);
                if (ParseOsDetectionOutput(combinedUname) == RemoteOsType.Windows)
                    return RemoteOsType.Windows;
            }
            catch
            {
                // Bỏ qua và thử tiếp lệnh dò Windows
            }

            try
            {
                using var verCmd = ssh.CreateCommand("cmd.exe /c ver");
                verCmd.CommandTimeout = TimeSpan.FromSeconds(8);
                verCmd.Execute();
                string combinedVer = (verCmd.Result ?? string.Empty) + "\n" + (verCmd.Error ?? string.Empty);
                return ParseOsDetectionOutput(combinedVer);
            }
            catch
            {
                return RemoteOsType.Linux;
            }
        }

        public static Task<RemoteOsType> DetectRemoteOsAsync(SshClient ssh, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => DetectRemoteOs(ssh), cancellationToken);
        }

        /// <summary>
        /// Parse nội dung JSON của <c>appsettings.json</c> để lấy <c>(Port, Token)</c>.
        /// </summary>
        public static (int Port, string Token)? ParseAgentConfigJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            string cleaned = json.Trim().TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(cleaned)) return null;

            try
            {
                using var doc = JsonDocument.Parse(cleaned, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
                if (!doc.RootElement.TryGetProperty("RemoteControl", out var rc)) return null;
                string token = rc.TryGetProperty("Token", out var t) ? t.GetString() ?? "" : "";
                int port = rc.TryGetProperty("Port", out var p) && p.TryGetInt32(out int pv) ? pv : 17600;
                return string.IsNullOrEmpty(token) ? null : (port, token);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public Task<bool> TestSshConnectionAsync(SshInstallerOptions options, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var client = CreateSshClient(options);
                    client.Connect();
                    bool isConnected = client.IsConnected;
                    client.Disconnect();
                    return isConnected;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Lỗi kết nối SSH đến {Host}:{Port}", options.Host, options.SshPort);
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Đọc lại Port/Token của Remote Agent đã cài từ file appsettings.json trên máy đích qua SSH
        /// (hỗ trợ cả Linux <c>/home/&lt;user&gt;/ipgs/remote-agent/appsettings.json</c> và
        /// Windows <c>C:\Program Files\KztekWinAgent\appsettings.json</c>).
        /// </summary>
        public Task<(int Port, string Token)?> ReadAgentConfigAsync(SshInstallerOptions options, CancellationToken cancellationToken = default)
        {
            return Task.Run<(int Port, string Token)?>(() =>
            {
                ShellQuote.ValidateWindowsUsername(options.Username, nameof(options.Username));
                using var ssh = CreateSshClient(options);
                ssh.Connect();

                RemoteOsType osType = DetectRemoteOs(ssh);
                string json;
                if (osType == RemoteOsType.Windows)
                {
                    const string winCfgPath = @"C:\Program Files\KztekWinAgent\appsettings.json";
                    json = ExecuteCommand(ssh,
                        $"powershell.exe -NoProfile -NonInteractive -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; if (Test-Path '{winCfgPath}') {{ Get-Content -Raw -Encoding UTF8 '{winCfgPath}' }}\"");
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        json = ExecuteCommand(ssh, $"cmd.exe /c type \"{winCfgPath}\" 2>nul");
                    }
                }
                else
                {
                    string username = ShellQuote.ValidateUsername(options.Username, nameof(options.Username));
                    json = ExecuteCommand(ssh, $"cat /home/{username}/ipgs/remote-agent/appsettings.json 2>/dev/null");
                }

                ssh.Disconnect();
                return ParseAgentConfigJson(json);
            }, cancellationToken);
        }

        /// <summary>
        /// Khởi động lại ứng dụng Remote Agent trên máy đích (Linux systemd user service hoặc Windows Scheduled Task).
        /// Trả về trạng thái service sau khi restart ("active" nếu thành công).
        /// </summary>
        public Task<string> RestartAgentAsync(SshInstallerOptions options, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                ShellQuote.ValidateWindowsUsername(options.Username, nameof(options.Username));
                using var ssh = CreateSshClient(options);
                ssh.Connect();

                RemoteOsType osType = DetectRemoteOs(ssh);
                if (osType == RemoteOsType.Windows)
                {
                    const string psRestart =
                        "Stop-ScheduledTask -TaskName 'KztekWinAgent' -ErrorAction SilentlyContinue; " +
                        "Get-Process -Name 'IPGS.RemoteControl.WinAgent' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; " +
                        "Start-Sleep -Milliseconds 500; " +
                        "Start-ScheduledTask -TaskName 'KztekWinAgent' -ErrorAction SilentlyContinue; " +
                        "Start-Sleep -Seconds 2; " +
                        "if (Get-Process -Name 'IPGS.RemoteControl.WinAgent' -ErrorAction SilentlyContinue) { Write-Output 'active' } " +
                        "elseif ((Get-ScheduledTask -TaskName 'KztekWinAgent' -ErrorAction SilentlyContinue).State -eq 'Running') { Write-Output 'active' } " +
                        "else { Write-Output 'inactive' }";
                    string winOut = ExecuteCommand(ssh, $"powershell.exe -NoProfile -NonInteractive -Command \"{psRestart}\"").Trim();
                    ssh.Disconnect();
                    return winOut.Contains("active", StringComparison.OrdinalIgnoreCase) && !winOut.Contains("inactive", StringComparison.OrdinalIgnoreCase)
                        ? "active"
                        : (string.IsNullOrWhiteSpace(winOut) ? "inactive" : winOut);
                }

                ShellQuote.ValidateUsername(options.Username, nameof(options.Username));
                // SSH exec không có XDG_RUNTIME_DIR → systemctl --user cần biến này.
                const string env = "export XDG_RUNTIME_DIR=/run/user/$(id -u); ";
                ExecuteCommand(ssh, env + "systemctl --user restart ipgs-remote-agent.service");
                Thread.Sleep(2000);
                string status = ExecuteCommand(ssh, env + "systemctl --user is-active ipgs-remote-agent.service").Trim();
                ssh.Disconnect();
                return status;
            }, cancellationToken);
        }

        public Task InstallZcuAgentAsync(SshInstallerOptions options, Action<string, double>? onProgress = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                void Report(string msg, double percent) => onProgress?.Invoke(msg, percent);

                ShellQuote.ValidateWindowsUsername(options.Username, nameof(options.Username));

                Report("🔄 [1/8] Đang kết nối SSH đến máy đích...", 10);
                using var ssh = CreateSshClient(options);
                ssh.Connect();

                if (!ssh.IsConnected)
                    throw new Exception("Không thể mở kết nối SSH đến máy đích.");

                Report("🔍 [2/8] Đang tự động nhận diện hệ điều hành máy đích...", 16);
                RemoteOsType osType = DetectRemoteOs(ssh);

                if (osType == RemoteOsType.Windows)
                {
                    InstallWindowsAgentCore(ssh, options, Report, cancellationToken);
                }
                else
                {
                    InstallLinuxAgentCore(ssh, options, Report);
                }

                ssh.Disconnect();
            }, cancellationToken);
        }

        private void InstallLinuxAgentCore(SshClient ssh, SshInstallerOptions options, Action<string, double> Report)
        {
            // S1: Username nội suy vào path /home/..., unit systemd, loginctl —
            // validate whitelist trước khi dùng để tránh command injection.
            string username = ShellQuote.ValidateUsername(options.Username, nameof(options.Username));

            Report("🐧 [2/8] Đã nhận diện HĐH Linux — kiểm tra môi trường X11...", 20);
            var sessionRes = ExecuteCommand(ssh, "echo $XDG_SESSION_TYPE");
            string sessionType = sessionRes.Trim().ToLower();
            if (sessionType == "wayland")
            {
                Report("⚠️ Cảnh báo: Session hiện tại là Wayland. ZcuAgent yêu cầu session X11 (Ubuntu on Xorg).", 25);
            }

            const string remoteOfflineDir = "/tmp/ipgs-offline";
            ExecuteCommand(ssh, $"mkdir -p {remoteOfflineDir}");

            // F15: X11 libs + .NET Runtime giờ được cài OFFLINE từ resource nhúng sẵn
            // trong CcuUI (Resources/x11-deb, Resources/dotnet-runtime) — không còn gọi
            // apt-get/wget ra mạng. Nếu build CcuUI cũ chưa có resource, fallback về
            // apt-get/wget như trước để không phá luồng cài đặt hiện có.
            Report("📦 [3/8] Cài đặt các thư viện Native X11 (libx11, libxext, libxtst)...", 35);
            string? x11DebDir = ResolveResourceDir("x11-deb");
            if (!string.IsNullOrEmpty(x11DebDir))
            {
                using (var sftpDeb = CreateSftpClient(options))
                {
                    sftpDeb.Connect();
                    string remoteDebDir = $"{remoteOfflineDir}/x11-deb";
                    if (!sftpDeb.Exists(remoteDebDir)) sftpDeb.CreateDirectory(remoteDebDir);
                    foreach (var debFile in Directory.GetFiles(x11DebDir, "*.deb"))
                    {
                        using var fs = File.OpenRead(debFile);
                        sftpDeb.UploadFile(fs, $"{remoteDebDir}/{Path.GetFileName(debFile)}", true);
                    }
                    sftpDeb.Disconnect();
                }
                ExecuteSudoCommand(ssh, $"dpkg -l libx11-6 libxext6 libxtst6 >/dev/null 2>&1 || dpkg -i {remoteOfflineDir}/x11-deb/*.deb", options.Password);
            }
            else
            {
                ExecuteSudoCommand(ssh, "dpkg -l libx11-6 libxext6 libxtst6 wget >/dev/null 2>&1 || (systemctl stop unattended-upgrades.service 2>/dev/null || true; apt-get update -qq && apt-get install -y -qq libx11-6 libxext6 libxtst6 wget)", options.Password);
            }

            // xclip: bắt buộc cho đồng bộ clipboard hai chiều (agent gọi xclip). Best-effort — cần mạng/apt
            // trên máy đích; thiếu xclip thì remote vẫn chạy, chỉ riêng clipboard không đồng bộ.
            Report("📋 Kiểm tra xclip (đồng bộ clipboard)...", 45);
            if (string.IsNullOrWhiteSpace(ExecuteCommand(ssh, "command -v xclip || true")))
            {
                string? xclipDebDir = ResolveResourceDir("xclip-deb");
                string codename = ExecuteCommand(ssh, ". /etc/os-release 2>/dev/null; echo $VERSION_CODENAME").Trim();
                string arch = ExecuteCommand(ssh, "dpkg --print-architecture 2>/dev/null").Trim();
                if (!string.IsNullOrEmpty(xclipDebDir) && codename == "jammy" && arch == "amd64")
                {
                    using (var sftpX = CreateSftpClient(options))
                    {
                        sftpX.Connect();
                        string remoteX = $"{remoteOfflineDir}/xclip-deb";
                        if (!sftpX.Exists(remoteX)) sftpX.CreateDirectory(remoteX);
                        foreach (var debFile in Directory.GetFiles(xclipDebDir, "*.deb"))
                        {
                            using var fs = File.OpenRead(debFile);
                            sftpX.UploadFile(fs, $"{remoteX}/{Path.GetFileName(debFile)}", true);
                        }
                        sftpX.Disconnect();
                    }
                    ExecuteSudoCommand(ssh, $"dpkg -i --skip-same-version {remoteOfflineDir}/xclip-deb/*.deb >/dev/null 2>&1 || true", options.Password);
                }
                if (string.IsNullOrWhiteSpace(ExecuteCommand(ssh, "command -v xclip || true")))
                    ExecuteSudoCommand(ssh, "apt-get update -qq; apt-get install -y -qq xclip || true", options.Password);
            }
            if (string.IsNullOrWhiteSpace(ExecuteCommand(ssh, "command -v xclip || true")))
                Report("⚠️ Máy đích chưa có xclip (không cài được qua apt) — clipboard sẽ không đồng bộ. Cài thủ công: sudo apt install xclip", 46);

            // libturbojpeg: nén JPEG bằng SIMD nhanh ~3,6 lần so với Skia.
            Report("🚀 Kiểm tra libturbojpeg (nén JPEG SIMD)...", 47);
            const string turboCheckCmd = "ldconfig -p 2>/dev/null | grep -q libturbojpeg.so.0 && echo ok || true";
            if (string.IsNullOrWhiteSpace(ExecuteCommand(ssh, turboCheckCmd)))
            {
                string? turboDebDir = ResolveResourceDir("libturbojpeg-deb");
                string turboCodename = ExecuteCommand(ssh, ". /etc/os-release 2>/dev/null; echo $VERSION_CODENAME").Trim();
                string turboArch = ExecuteCommand(ssh, "dpkg --print-architecture 2>/dev/null").Trim();
                if (!string.IsNullOrEmpty(turboDebDir) && turboCodename == "jammy" && turboArch == "amd64")
                {
                    using (var sftpT = CreateSftpClient(options))
                    {
                        sftpT.Connect();
                        string remoteT = $"{remoteOfflineDir}/libturbojpeg-deb";
                        if (!sftpT.Exists(remoteT)) sftpT.CreateDirectory(remoteT);
                        foreach (var debFile in Directory.GetFiles(turboDebDir, "*.deb"))
                        {
                            using var fs = File.OpenRead(debFile);
                            sftpT.UploadFile(fs, $"{remoteT}/{Path.GetFileName(debFile)}", true);
                        }
                        sftpT.Disconnect();
                    }
                    ExecuteSudoCommand(ssh, $"dpkg -i --skip-same-version {remoteOfflineDir}/libturbojpeg-deb/*.deb >/dev/null 2>&1 || true", options.Password);
                }
                if (string.IsNullOrWhiteSpace(ExecuteCommand(ssh, turboCheckCmd)))
                {
                    ExecuteSudoCommand(ssh,
                        "sh -c 'apt-get update -qq; apt-get install -y -qq libturbojpeg || apt-get install -y -qq libturbojpeg0'",
                        options.Password);
                }
            }
            if (string.IsNullOrWhiteSpace(ExecuteCommand(ssh, turboCheckCmd)))
                Report("⚠️ Máy đích chưa có libturbojpeg — agent sẽ nén bằng Skia (chậm hơn ~3 lần). Cài thủ công: sudo apt install libturbojpeg", 47);

            Report("💻 [4/8] Kiểm tra & cài đặt .NET 8 Runtime...", 50);
            var dotnetCheck = ExecuteCommand(ssh, "dotnet --list-runtimes 2>/dev/null || $HOME/.dotnet/dotnet --list-runtimes 2>/dev/null || echo 'NOT_FOUND'");
            if (!dotnetCheck.Contains("Microsoft.NETCore.App"))
            {
                string? dotnetRuntimeDir = ResolveResourceDir("dotnet-runtime");
                string? dotnetTarball = dotnetRuntimeDir is null ? null : Directory.GetFiles(dotnetRuntimeDir, "*.tar.gz").FirstOrDefault();
                if (dotnetTarball is not null)
                {
                    Report("📤 Đang tải bộ cài .NET 8 Runtime (offline) lên ZCU...", 55);
                    using var sftpDotnet = CreateSftpClient(options);
                    sftpDotnet.Connect();
                    using (var fs = File.OpenRead(dotnetTarball))
                    {
                        sftpDotnet.UploadFile(fs, $"{remoteOfflineDir}/dotnet-runtime.tar.gz", true);
                    }
                    sftpDotnet.Disconnect();
                    ExecuteCommand(ssh, $"mkdir -p $HOME/.dotnet && tar -xzf {remoteOfflineDir}/dotnet-runtime.tar.gz -C $HOME/.dotnet");
                }
                else
                {
                    Report("⬇️ Đang tải script và cài .NET 8 Runtime...", 55);
                    ExecuteCommand(ssh, "wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh && chmod +x /tmp/dotnet-install.sh && /tmp/dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir $HOME/.dotnet && rm -f /tmp/dotnet-install.sh");
                }
            }

            string remoteInstallDir = $"/home/{username}/ipgs/remote-agent";
            ExecuteCommand(ssh, $"mkdir -p {remoteInstallDir}");

            // Ensure PublishSourceDir is resolved for Linux ZcuAgent
            string? publishDir = options.PublishSourceDir;
            if (string.IsNullOrEmpty(publishDir) || !Directory.Exists(publishDir) || !File.Exists(Path.Combine(publishDir, LinuxAgentExeName)))
            {
                publishDir = ResolveOrBuildZcuAgentBinaries(Report);
            }

            if (!string.IsNullOrEmpty(publishDir) && Directory.Exists(publishDir))
            {
                Report("📤 [5/8] Đang tải các tệp thực thi ZcuAgent lên ZCU qua SFTP...", 75);
                ExecuteCommand(ssh, "systemctl --user stop ipgs-remote-agent.service 2>/dev/null || true");
                // G028: GIỮ cả appsettings.json và agent-tls.pfx
                ExecuteCommand(ssh, $"find {remoteInstallDir} -mindepth 1 -maxdepth 1 ! -name appsettings.json ! -name agent-tls.pfx -exec rm -rf {{}} + 2>/dev/null || true");
                using var sftp = CreateSftpClient(options);
                sftp.Connect();
                UploadDirectory(sftp, publishDir, remoteInstallDir);
                sftp.Disconnect();
                // G024: loại bỏ libhost*.so / libclr*.so nếu bản build là framework-dependent để muxer dùng DOTNET_ROOT
                if (!File.Exists(Path.Combine(publishDir, "libcoreclr.so")))
                {
                    ExecuteCommand(ssh, $"rm -f {remoteInstallDir}/libhost* {remoteInstallDir}/libclr* 2>/dev/null || true");
                }
                ExecuteCommand(ssh, $"chmod +x {remoteInstallDir}/IPGS.RemoteControl.ZcuAgent 2>/dev/null || true");
            }
            else
            {
                var fileCheck = ExecuteCommand(ssh, $"[ -f {remoteInstallDir}/IPGS.RemoteControl.ZcuAgent ] && echo 'EXISTS' || echo 'MISSING'");
                if (!fileCheck.Contains("EXISTS"))
                {
                    throw new FileNotFoundException("Không tìm thấy tệp thực thi IPGS.RemoteControl.ZcuAgent để tải lên ZCU. Vui lòng đảm bảo dự án ZcuAgent đã được biên dịch linux-x64.");
                }
            }

            Report("⚙️ [6/8] Tạo và cập nhật cấu hình appsettings.json...", 78);
            string jsonConfig = BuildAgentConfigJson(options);
            ExecuteCommand(ssh, $"cat << 'EOF' > {remoteInstallDir}/appsettings.json\n{jsonConfig}\nEOF");

            Report("🛠️ [7/8] Đăng ký Systemd User Service & Lingering...", 85);
            string unitFileContent = $@"[Unit]
Description=IPGS Remote Control ZCU Agent
After=graphical-session.target
Wants=graphical-session.target

[Service]
Type=simple
ExecStart={remoteInstallDir}/IPGS.RemoteControl.ZcuAgent
WorkingDirectory={remoteInstallDir}
Environment=DOTNET_ROOT=/home/{username}/.dotnet
Environment=PATH=/home/{username}/.dotnet:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
Environment=DISPLAY=:0
Environment=XDG_SESSION_TYPE=x11
Restart=on-failure
RestartSec=5
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=default.target";

            ExecuteCommand(ssh, $"mkdir -p /home/{username}/.config/systemd/user");
            ExecuteCommand(ssh, $"cat << 'EOF' > /home/{username}/.config/systemd/user/ipgs-remote-agent.service\n{unitFileContent}\nEOF");
            ExecuteCommand(ssh, "systemctl --user daemon-reload && systemctl --user enable ipgs-remote-agent.service || true");
            ExecuteSudoCommand(ssh, $"loginctl enable-linger {username}", options.Password);

            Report("🛡️ [8/8] Mở cổng Firewall & Tắt khoá màn hình tự động...", 95);
            ExecuteSudoCommand(ssh, $"ufw allow {options.AgentPort}/tcp comment 'IPGS Remote Agent'", options.Password);

            Report("⚡ Đặt CPU governor = performance (giữ xung ổn định cho stream)...", 96);
            string cpuPerfUnit = @"[Unit]
Description=IPGS - CPU governor performance
After=multi-user.target

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart=/bin/sh -c 'echo performance | tee /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor >/dev/null'

[Install]
WantedBy=multi-user.target".Replace("\r", "");
            ExecuteCommand(ssh, $"cat << 'EOF' > /tmp/ipgs-cpu-performance.service\n{cpuPerfUnit}\nEOF");
            ExecuteSudoCommand(ssh, "install -m 644 /tmp/ipgs-cpu-performance.service /etc/systemd/system/ipgs-cpu-performance.service", options.Password);
            ExecuteSudoCommand(ssh, "systemctl daemon-reload", options.Password);
            ExecuteSudoCommand(ssh, "systemctl enable --now ipgs-cpu-performance.service", options.Password);
            ExecuteCommand(ssh, "gsettings set org.gnome.desktop.screensaver lock-enabled false 2>/dev/null || true");
            ExecuteCommand(ssh, "gsettings set org.gnome.desktop.session idle-delay 0 2>/dev/null || true");

            ExecuteSudoCommand(ssh,
                $"for sid in $(loginctl list-sessions --no-legend 2>/dev/null | awk -v u={ShellQuote.Quote(username)} '$3==u{{print $1}}'); do loginctl unlock-session \"$sid\" 2>/dev/null || true; done",
                options.Password);

            ExecuteCommand(ssh, "systemctl --user restart ipgs-remote-agent.service || true");
            var activeCheck = ExecuteCommand(ssh, "systemctl --user is-active ipgs-remote-agent.service || echo 'inactive'");
            string activeStatus = activeCheck.Trim();

            if (activeStatus == "active")
            {
                Report($"✅ CÀI ĐẶT THÀNH CÔNG! ZcuAgent service đang HOẠT ĐỘNG trên cổng {options.AgentPort}.", 100);
            }
            else
            {
                Report($"⚠️ Service ở trạng thái '{activeStatus}'. Đang thử khởi động lại...", 98);
                ExecuteCommand(ssh, $"chmod +x {remoteInstallDir}/IPGS.RemoteControl.ZcuAgent 2>/dev/null || true");
                ExecuteCommand(ssh, "systemctl --user restart ipgs-remote-agent.service || true");
                Report($"✅ Đã gửi lệnh khởi động ZcuAgent service trên cổng {options.AgentPort}.", 100);
            }
        }

        private void InstallWindowsAgentCore(SshClient ssh, SshInstallerOptions options, Action<string, double> Report, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Bước 2/8: Kiểm tra phiên bản Windows và .NET 8 Runtime trên máy đích
            string winProbePs =
                "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
                "$cap = (Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue).Caption; " +
                "Write-Output ('WIN_CAPTION:' + $cap); " +
                "try { & dotnet --list-runtimes 2>$null } catch {}; " +
                "if (Test-Path 'C:\\Program Files\\dotnet\\dotnet.exe') { & 'C:\\Program Files\\dotnet\\dotnet.exe' --list-runtimes 2>$null }";
            string probeOut = ExecuteCommand(ssh, $"powershell.exe -NoProfile -NonInteractive -Command \"{winProbePs}\"");

            string winCaption = "Windows";
            foreach (var line in probeOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("WIN_CAPTION:", StringComparison.OrdinalIgnoreCase))
                {
                    string val = line.Substring("WIN_CAPTION:".Length).Trim();
                    if (!string.IsNullOrEmpty(val)) winCaption = val;
                }
            }

            bool hasDotnet8Runtime = probeOut.Contains("Microsoft.NETCore.App 8.", StringComparison.OrdinalIgnoreCase);
            Report($"🪟 [2/8] Đã nhận diện {winCaption} (.NET 8 Runtime: {(hasDotnet8Runtime ? "Đã có" : "Chưa có — dùng gói self-contained")})...", 25);

            // Bước 3/8: Tìm hoặc publish gói cài đặt IPGS.RemoteControl.WinAgent (win-x64)
            Report("📦 [3/8] Đang chuẩn bị bộ cài IPGS.RemoteControl.WinAgent (win-x64)...", 35);
            string? publishDir = FindOrPublishWinAgentFolder(options.PublishSourceDir, selfContained: !hasDotnet8Runtime, Report);
            if (string.IsNullOrEmpty(publishDir) || !Directory.Exists(publishDir) || !File.Exists(Path.Combine(publishDir, WindowsAgentExeName)))
            {
                throw new FileNotFoundException(
                    "Không tìm thấy hoặc không thể publish IPGS.RemoteControl.WinAgent.exe (win-x64). " +
                    "Vui lòng kiểm tra project IPGS.RemoteControl.WinAgent trên máy CCU.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Bước 4/8 & 5/8: Kết nối SFTP, tạo thư mục staging và đẩy bộ cài + install-win-agent.ps1 lên máy Windows
            Report("🔌 [4/8] Đang mở kết nối SFTP tới máy Windows...", 48);
            string sftpStagingDir;
            string winStagingDir;

            using (var sftp = CreateSftpClient(options))
            {
                sftp.Connect();
                string workDir = ShellQuote.NormalizeSftpPath(sftp.WorkingDirectory);
                if (!ShellQuote.IsWindowsSftpPath(workDir))
                {
                    workDir = "/C:/Users/Public";
                }

                sftpStagingDir = ShellQuote.CombineSftpPath(workDir, "kztek-winagent-staging");
                winStagingDir = ShellQuote.ToWindowsLocalPath(sftpStagingDir);

                // Làm sạch và tạo thư mục staging qua PowerShell trước khi upload SFTP
                string prepStagePs =
                    $"if (Test-Path -LiteralPath {ShellQuote.QuotePowerShell(winStagingDir)}) {{ " +
                    $"Remove-Item -LiteralPath {ShellQuote.QuotePowerShell(winStagingDir)} -Recurse -Force -ErrorAction SilentlyContinue }}; " +
                    $"New-Item -ItemType Directory -Force -Path {ShellQuote.QuotePowerShell(winStagingDir)} | Out-Null";
                ExecuteCommand(ssh, $"powershell.exe -NoProfile -NonInteractive -Command \"{prepStagePs}\"");

                if (!sftp.Exists(sftpStagingDir))
                {
                    sftp.CreateDirectory(sftpStagingDir);
                }

                Report($"📤 [5/8] Đang tải bộ cài WinAgent lên {winStagingDir} qua SFTP...", 60);
                UploadDirectory(sftp, publishDir, sftpStagingDir);

                // Upload install-win-agent.ps1 với UTF-8 BOM để Windows PowerShell 5.1 đọc chuẩn tiếng Việt 100%
                string scriptRemoteSftpPath = ShellQuote.CombineSftpPath(sftpStagingDir, "install-win-agent.ps1");
                byte[] scriptBytes = GetWinAgentInstallerScriptBytesWithBom();
                using (var ms = new MemoryStream(scriptBytes))
                {
                    sftp.UploadFile(ms, scriptRemoteSftpPath, true);
                }

                sftp.Disconnect();
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Bước 6/8 & 7/8: Thực thi install-win-agent.ps1 (cấu hình appsettings.json, bảo toàn agent-tls.pfx,
            // mở Firewall cổng Agent + ICMP Ping, tắt Sleep/Lock Screen, đăng ký & chạy Scheduled Task KztekWinAgent)
            Report("⚙️ [6/8] Đang cài đặt WinAgent, cấu hình Firewall & Scheduled Task...", 80);

            string[] allowedIps = (options.AllowedClientIPs ?? SshInstallerOptions.DefaultLanAllowedClientIPs)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (allowedIps.Length == 0)
            {
                allowedIps = new[] { SshInstallerOptions.DefaultLanAllowedClientIPs };
            }
            string allowedIpsPsExpr = "@(" + string.Join(",", allowedIps.Select(ShellQuote.QuotePowerShell)) + ")";
            string winScriptPath = winStagingDir.TrimEnd('\\') + @"\install-win-agent.ps1";

            string runInstallPs =
                "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
                $"Set-Location -LiteralPath {ShellQuote.QuotePowerShell(winStagingDir)}; " +
                $"& {ShellQuote.QuotePowerShell(winScriptPath)} " +
                $"-InstallDir {ShellQuote.QuotePowerShell(WindowsDefaultInstallDir)} " +
                $"-Port {options.AgentPort} " +
                $"-Token {ShellQuote.QuotePowerShell(options.AgentToken)} " +
                $"-AllowedClientIPs {allowedIpsPsExpr} " +
                $"-TargetFps {options.TargetFps} " +
                $"-JpegQuality {options.JpegQuality} " +
                "-StartImmediately";

            Report("🛡️ [7/8] Đang mở cổng Firewall, tắt Sleep/Lock Screen & khởi chạy Scheduled Task 'KztekWinAgent'...", 90);
            var (installOut, installErr, exitCode) = ExecuteCommandDetailed(
                ssh,
                $"powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{runInstallPs}\"");

            if (exitCode != 0)
            {
                _logger?.LogWarning("install-win-agent.ps1 trả về mã {ExitCode}. Output: {Out}, Error: {Err}", exitCode, installOut, installErr);
            }

            // Bước 8/8: Xác minh kết quả cài đặt & dọn thư mục staging
            Report("🔍 [8/8] Đang kiểm tra trạng thái hoạt động của WinAgent & dọn tệp tạm...", 96);
            string verifyPs =
                $"Set-Location -LiteralPath 'C:\\'; " +
                $"Remove-Item -LiteralPath {ShellQuote.QuotePowerShell(winStagingDir)} -Recurse -Force -ErrorAction SilentlyContinue; " +
                "Start-Sleep -Seconds 1; " +
                "if (Get-Process -Name 'IPGS.RemoteControl.WinAgent' -ErrorAction SilentlyContinue) { Write-Output 'STATUS:ACTIVE' } " +
                "elseif ((Get-ScheduledTask -TaskName 'KztekWinAgent' -ErrorAction SilentlyContinue).State -eq 'Running') { Write-Output 'STATUS:ACTIVE' } " +
                "elseif (Test-Path 'C:\\Program Files\\KztekWinAgent\\IPGS.RemoteControl.WinAgent.exe') { " +
                "  Start-ScheduledTask -TaskName 'KztekWinAgent' -ErrorAction SilentlyContinue; " +
                "  Start-Sleep -Seconds 1; " +
                "  if (Get-Process -Name 'IPGS.RemoteControl.WinAgent' -ErrorAction SilentlyContinue) { Write-Output 'STATUS:ACTIVE' } " +
                "  else { Write-Output 'STATUS:INSTALLED' } " +
                "} else { Write-Output 'STATUS:MISSING' }";

            string verifyOut = ExecuteCommand(ssh, $"powershell.exe -NoProfile -NonInteractive -Command \"{verifyPs}\"");

            if (verifyOut.Contains("STATUS:ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                Report($"✅ CÀI ĐẶT THÀNH CÔNG! WinAgent đang HOẠT ĐỘNG trên máy Windows (cổng {options.AgentPort}).", 100);
            }
            else if (verifyOut.Contains("STATUS:INSTALLED", StringComparison.OrdinalIgnoreCase))
            {
                Report($"✅ Đã cài đặt WinAgent và đăng ký Scheduled Task 'KztekWinAgent' trên cổng {options.AgentPort} (sẽ tự chạy khi user đăng nhập Desktop).", 100);
            }
            else
            {
                string detail = string.Join(" | ", new[] { installErr.Trim(), installOut.Trim() }.Where(s => !string.IsNullOrEmpty(s)));
                throw new InvalidOperationException(
                    $"Cài đặt WinAgent trên máy Windows không thành công (cần tài khoản thuộc nhóm Administrators). Chi tiết: {detail}");
            }
        }

        private static string BuildAgentConfigJson(SshInstallerOptions options)
        {
            return JsonSerializer.Serialize(new
            {
                RemoteControl = new
                {
                    Port = options.AgentPort,
                    Token = options.AgentToken,
                    AllowedClientIPs = (options.AllowedClientIPs ?? SshInstallerOptions.DefaultLanAllowedClientIPs)
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    TargetFps = options.TargetFps,
                    JpegQuality = options.JpegQuality,
                    MaxFrameBytes = 8388608
                },
                Logging = new
                {
                    LogLevel = new Dictionary<string, string>
                    {
                        ["Default"] = "Information",
                        ["Microsoft.Hosting.Lifetime"] = "Information"
                    }
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        private SshClient CreateSshClient(SshInstallerOptions opts)
        {
            if (!string.IsNullOrEmpty(opts.KeyFilePath) && File.Exists(opts.KeyFilePath))
            {
                var pk = string.IsNullOrEmpty(opts.KeyPassphrase)
                    ? new PrivateKeyFile(opts.KeyFilePath)
                    : new PrivateKeyFile(opts.KeyFilePath, opts.KeyPassphrase);
                return new SshClient(opts.Host, opts.SshPort, opts.Username, pk);
            }
            return new SshClient(opts.Host, opts.SshPort, opts.Username, opts.Password);
        }

        private SftpClient CreateSftpClient(SshInstallerOptions opts)
        {
            if (!string.IsNullOrEmpty(opts.KeyFilePath) && File.Exists(opts.KeyFilePath))
            {
                var pk = string.IsNullOrEmpty(opts.KeyPassphrase)
                    ? new PrivateKeyFile(opts.KeyFilePath)
                    : new PrivateKeyFile(opts.KeyFilePath, opts.KeyPassphrase);
                return new SftpClient(opts.Host, opts.SshPort, opts.Username, pk);
            }
            return new SftpClient(opts.Host, opts.SshPort, opts.Username, opts.Password);
        }

        /// <summary>
        /// Q6: trả về string Result thay vì object SshCommand — bản cũ `using var cmd`
        /// rồi `return cmd` khiến caller đọc `.Result` trên object đã Dispose.
        /// </summary>
        private string ExecuteCommand(SshClient ssh, string commandText)
        {
            using var cmd = ssh.CreateCommand(commandText);
            cmd.Execute();
            return cmd.Result ?? string.Empty;
        }

        private (string Output, string Error, int ExitStatus) ExecuteCommandDetailed(SshClient ssh, string commandText)
        {
            using var cmd = ssh.CreateCommand(commandText);
            cmd.Execute();
            return (cmd.Result ?? string.Empty, cmd.Error ?? string.Empty, cmd.ExitStatus ?? 0);
        }

        private void ExecuteSudoCommand(SshClient ssh, string commandText, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                ssh.RunCommand($"sudo -n {commandText} 2>/dev/null || true");
            }
            else
            {
                // Q12: dùng helper ShellQuote chung thay escape tay.
                ssh.RunCommand($"echo {ShellQuote.Quote(password)} | sudo -S {commandText} 2>/dev/null || true");
            }
        }

        private void UploadDirectory(SftpClient sftp, string localPath, string remotePath)
        {
            string normRemote = ShellQuote.NormalizeSftpPath(remotePath);
            foreach (var file in Directory.GetFiles(localPath))
            {
                string fileName = Path.GetFileName(file);
                string remoteFilePath = ShellQuote.CombineSftpPath(normRemote, fileName);
                using var fs = File.OpenRead(file);
                sftp.UploadFile(fs, remoteFilePath, true);
            }

            foreach (var dir in Directory.GetDirectories(localPath))
            {
                string dirName = Path.GetFileName(dir);
                string remoteDirPath = ShellQuote.CombineSftpPath(normRemote, dirName);
                if (!sftp.Exists(remoteDirPath))
                {
                    sftp.CreateDirectory(remoteDirPath);
                }
                UploadDirectory(sftp, dir, remoteDirPath);
            }
        }

        /// <summary>
        /// F15: tìm thư mục resource offline (Resources/&lt;name&gt;) đã được nhúng sẵn vào
        /// output publish của CcuUI (xem CcuUI.csproj — Content Include="Resources\**").
        /// Trả về null nếu build CcuUI hiện tại chưa có resource này (bản cũ) — caller tự
        /// fallback về đường mạng.
        /// </summary>
        private string? ResolveResourceDir(string relativeName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(baseDir, "Resources", relativeName);
            return Directory.Exists(candidate) ? candidate : null;
        }

        /// <summary>
        /// Tìm đường dẫn tới <c>install-win-agent.ps1</c> từ mã nguồn hoặc thư mục Resources của CcuUI.
        /// </summary>
        public string? ResolveWinAgentInstallerScriptPath()
        {
            var searchRoots = GetSearchRoots();
            foreach (var root in searchRoots)
            {
                string? dir = root;
                for (int i = 0; i < 8 && dir != null; i++)
                {
                    string c1 = Path.Combine(dir, "scripts", "windows-agent", "install-win-agent.ps1");
                    if (File.Exists(c1)) return c1;

                    string c2 = Path.Combine(dir, "Resources", "scripts", "windows-agent", "install-win-agent.ps1");
                    if (File.Exists(c2)) return c2;

                    string c3 = Path.Combine(dir, "IPGS.RemoteControl.CcuUI", "Resources", "scripts", "windows-agent", "install-win-agent.ps1");
                    if (File.Exists(c3)) return c3;

                    dir = Directory.GetParent(dir)?.FullName;
                }
            }
            return null;
        }

        private byte[] GetWinAgentInstallerScriptBytesWithBom()
        {
            string? scriptPath = ResolveWinAgentInstallerScriptPath();
            string scriptText = (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                ? File.ReadAllText(scriptPath, Encoding.UTF8)
                : BuildFallbackWinAgentInstallScript();

            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            byte[] preamble = utf8WithBom.GetPreamble();
            byte[] body = utf8WithBom.GetBytes(scriptText.TrimStart('\uFEFF'));
            byte[] result = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
            return result;
        }

        private static string BuildFallbackWinAgentInstallScript()
        {
            return @"param(
    [string]   $InstallDir       = (Join-Path $env:ProgramFiles 'KztekWinAgent'),
    [int]      $Port             = 17600,
    [string[]] $AllowedClientIPs = @('0.0.0.0/0'),
    [string]   $Token            = '',
    [int]      $TargetFps        = 30,
    [int]      $JpegQuality      = 70,
    [switch]   $StartImmediately = $true
)
$ErrorActionPreference = 'Stop'
$TaskName = 'KztekWinAgent'
$Exe = 'IPGS.RemoteControl.WinAgent.exe'
Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
Get-Process -Name 'IPGS.RemoteControl.WinAgent' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$src = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$tlsPath = Join-Path $InstallDir 'agent-tls.pfx'
$keepCert = Test-Path $tlsPath
Get-ChildItem $src -Force | Where-Object { $_.Name -ne 'install-win-agent.ps1' -and -not ($keepCert -and $_.Name -eq 'agent-tls.pfx') } | Copy-Item -Destination $InstallDir -Recurse -Force
$cfgObj = @{
    RemoteControl = @{
        Port = $Port
        Token = $Token
        AllowedClientIPs = @($AllowedClientIPs)
        TargetFps = $TargetFps
        JpegQuality = $JpegQuality
        CpuAdaptive = $false
        MaxFrameBytes = 8388608
    }
}
$cfgPath = Join-Path $InstallDir 'appsettings.json'
[IO.File]::WriteAllText($cfgPath, ($cfgObj | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
Remove-NetFirewallRule -DisplayName 'Kztek WinAgent' -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName 'Kztek WinAgent' -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Any -Enabled True | Out-Null
Remove-NetFirewallRule -DisplayName 'KZTEK Allow ICMPv4 Ping' -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName 'KZTEK Allow ICMPv4 Ping' -Direction Inbound -Action Allow -Protocol ICMPv4 -IcmpType 8 -Profile Any -Enabled True | Out-Null
try {
    & powercfg.exe /change monitor-timeout-ac 0
    & powercfg.exe /change standby-timeout-ac 0
} catch {}
$desktopUser = (Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue).UserName
if ([string]::IsNullOrWhiteSpace($desktopUser)) { $desktopUser = ""$env:USERDOMAIN\$env:USERNAME"" }
$exePath = Join-Path $InstallDir $Exe
$logPath = Join-Path $InstallDir 'agent.log'
$action = New-ScheduledTaskAction -Execute $exePath -Argument ""--hide-console --log `""$logPath`"""" -WorkingDirectory $InstallDir
$trigger = New-ScheduledTaskTrigger -AtLogOn
$trigger.UserId = $desktopUser
$principal = New-ScheduledTaskPrincipal -UserId $desktopUser -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
if ($StartImmediately) { Start-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue }
";
        }

        private static List<string> GetSearchRoots()
        {
            var searchRoots = new List<string>();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir)) searchRoots.Add(baseDir);

            string currentDir = Directory.GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir) && !searchRoots.Contains(currentDir))
                searchRoots.Add(currentDir);

            return searchRoots;
        }

        /// <summary>
        /// Tìm thư mục chứa bộ cài <c>IPGS.RemoteControl.WinAgent.exe</c> (<c>win-x64</c>) hoặc tự động
        /// chạy <c>dotnet publish</c> từ project <c>IPGS.RemoteControl.WinAgent.csproj</c>.
        /// </summary>
        public string? FindOrPublishWinAgentFolder(string? preferredDir, bool selfContained, Action<string, double>? report = null)
        {
            static bool HasWinAgentBinary(string? folder, bool requireSelfContained)
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    return false;
                if (!File.Exists(Path.Combine(folder, WindowsAgentExeName)))
                    return false;
                if (requireSelfContained && !File.Exists(Path.Combine(folder, "coreclr.dll")))
                    return false;
                return true;
            }

            if (HasWinAgentBinary(preferredDir, selfContained))
                return preferredDir;

            string? embedded = ResolveResourceDir(Path.Combine("win-agent", "win-x64"));
            if (HasWinAgentBinary(embedded, selfContained))
                return embedded;

            var searchRoots = GetSearchRoots();

            // 1. Kiểm tra các thư mục publish win-x64 đã build sẵn
            foreach (var root in searchRoots)
            {
                string? dir = root;
                for (int i = 0; i < 8 && dir != null; i++)
                {
                    string[] candidates = selfContained
                        ? new[]
                        {
                            Path.Combine(dir, "IPGS.RemoteControl.WinAgent", "publish", "win-x64-sc"),
                            Path.Combine(dir, "IPGS.RemoteControl.WinAgent", "publish", "win-x64"),
                            Path.Combine(dir, "publish", "win-x64-sc"),
                            Path.Combine(dir, "publish", "win-x64")
                        }
                        : new[]
                        {
                            Path.Combine(dir, "IPGS.RemoteControl.WinAgent", "publish", "win-x64"),
                            Path.Combine(dir, "IPGS.RemoteControl.WinAgent", "publish", "win-x64-sc"),
                            Path.Combine(dir, "publish", "win-x64"),
                            Path.Combine(dir, "publish", "win-x64-sc"),
                            Path.Combine(dir, "IPGS.RemoteControl.WinAgent", "bin", "Release", "net8.0-windows", "win-x64", "publish"),
                            Path.Combine(dir, "IPGS.RemoteControl.WinAgent", "bin", "Release", "net8.0-windows")
                        };

                    foreach (var cand in candidates)
                    {
                        if (HasWinAgentBinary(cand, selfContained))
                            return cand;
                    }

                    dir = Directory.GetParent(dir)?.FullName;
                }
            }

            // 2. Tự động gọi dotnet publish cho IPGS.RemoteControl.WinAgent.csproj
            foreach (var root in searchRoots)
            {
                string? projDir = root;
                for (int i = 0; i < 8 && projDir != null; i++)
                {
                    string csprojPath = Path.Combine(projDir, "IPGS.RemoteControl.WinAgent", "IPGS.RemoteControl.WinAgent.csproj");
                    if (!File.Exists(csprojPath))
                    {
                        csprojPath = Path.Combine(projDir, "IPGS.RemoteControl.WinAgent.csproj");
                    }

                    if (File.Exists(csprojPath))
                    {
                        bool[] modesToTry = selfContained ? new[] { true, false } : new[] { false };
                        foreach (bool scMode in modesToTry)
                        {
                            try
                            {
                                string modeLabel = scMode ? "self-contained" : "framework-dependent";
                                report?.Invoke($"⚙️ Đang tự động biên dịch 'dotnet publish' cho WinAgent (win-x64, {modeLabel})...", 40);
                                string outSubFolder = scMode ? "win-x64-sc" : "win-x64";
                                string outDir = Path.Combine(Path.GetDirectoryName(csprojPath)!, "publish", outSubFolder);
                                var psi = new System.Diagnostics.ProcessStartInfo(
                                    "dotnet",
                                    $"publish \"{csprojPath}\" -c Release -r win-x64 --self-contained {(scMode ? "true" : "false")} -p:PublishSingleFile=false -o \"{outDir}\"")
                                {
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                };

                                using (var proc = System.Diagnostics.Process.Start(psi))
                                {
                                    if (proc is null)
                                    {
                                        _logger?.LogWarning("Không khởi động được tiến trình 'dotnet publish' cho WinAgent.");
                                    }
                                    else if (!proc.WaitForExit(90000))
                                    {
                                        _logger?.LogWarning("'dotnet publish' WinAgent quá 90s — kill tiến trình để tránh orphan.");
                                        try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                                    }
                                    else if (proc.ExitCode != 0)
                                    {
                                        _logger?.LogWarning("'dotnet publish' WinAgent ({Mode}) thoát với mã lỗi {ExitCode}.", modeLabel, proc.ExitCode);
                                    }
                                }

                                if (HasWinAgentBinary(outDir, scMode))
                                    return outDir;
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "Không thể tự động publish WinAgent.");
                            }
                        }
                    }

                    projDir = Directory.GetParent(projDir)?.FullName;
                }
            }

            // Fallback cuối cùng: nếu tìm thấy bất kỳ bản build WinAgent.exe nào (kể cả Debug/Release)
            foreach (var root in searchRoots)
            {
                string? dir = root;
                for (int i = 0; i < 8 && dir != null; i++)
                {
                    string[] fallbackDirs =
                    {
                        Path.Combine(dir, "IPGS.RemoteControl.WinAgent", "bin", "Release", "net8.0-windows"),
                        Path.Combine(dir, "IPGS.RemoteControl.WinAgent", "bin", "Debug", "net8.0-windows")
                    };
                    foreach (var fb in fallbackDirs)
                    {
                        if (HasWinAgentBinary(fb, requireSelfContained: false))
                            return fb;
                    }
                    dir = Directory.GetParent(dir)?.FullName;
                }
            }

            return null;
        }

        private string? ResolveOrBuildZcuAgentBinaries(Action<string, double>? report)
        {
            // F15: ưu tiên binary ZcuAgent đã nhúng sẵn trong output CcuUI trước khi
            // tìm/publish từ source — đây là đường offline chính, không cần .NET SDK trên CCU.
            string? embedded = ResolveResourceDir(Path.Combine("zcu-agent", "linux-x64"));
            if (!string.IsNullOrEmpty(embedded) && File.Exists(Path.Combine(embedded, "IPGS.RemoteControl.ZcuAgent")))
                return embedded;

            var searchRoots = GetSearchRoots();

            // 1. Check existing published binaries up to 8 levels up
            foreach (var root in searchRoots)
            {
                string? dir = root;
                for (int i = 0; i < 8 && dir != null; i++)
                {
                    string candidate1 = Path.Combine(dir, "publish", "linux-x64");
                    if (Directory.Exists(candidate1) && File.Exists(Path.Combine(candidate1, "IPGS.RemoteControl.ZcuAgent")))
                        return candidate1;

                    string candidate2 = Path.Combine(dir, "IPGS.RemoteControl.ZcuAgent", "publish", "linux-x64");
                    if (Directory.Exists(candidate2) && File.Exists(Path.Combine(candidate2, "IPGS.RemoteControl.ZcuAgent")))
                        return candidate2;

                    dir = Directory.GetParent(dir)?.FullName;
                }
            }

            // 2. Auto-trigger dotnet publish if project exists locally
            foreach (var root in searchRoots)
            {
                string? projDir = root;
                for (int i = 0; i < 8 && projDir != null; i++)
                {
                    string csprojPath = Path.Combine(projDir, "IPGS.RemoteControl.ZcuAgent", "IPGS.RemoteControl.ZcuAgent.csproj");
                    if (!File.Exists(csprojPath))
                    {
                        csprojPath = Path.Combine(projDir, "IPGS.RemoteControl.ZcuAgent.csproj");
                    }

                    if (File.Exists(csprojPath))
                    {
                        try
                        {
                            report?.Invoke("⚙️ Đang tự động biên dịch 'dotnet publish' cho ZcuAgent (linux-x64)...", 72);
                            string outDir = Path.Combine(Path.GetDirectoryName(csprojPath)!, "publish", "linux-x64");
                            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"publish \"{csprojPath}\" -c Release -r linux-x64 --self-contained false -o \"{outDir}\"")
                            {
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            // Q5: using + kiểm tra kết quả WaitForExit; quá 45s → kill
                            // process tree để tránh dotnet publish orphan rò handle.
                            using (var proc = System.Diagnostics.Process.Start(psi))
                            {
                                if (proc is null)
                                {
                                    _logger?.LogWarning("Không khởi động được tiến trình 'dotnet publish' cho ZcuAgent.");
                                }
                                else if (!proc.WaitForExit(45000))
                                {
                                    _logger?.LogWarning("'dotnet publish' ZcuAgent quá 45s — kill tiến trình để tránh orphan.");
                                    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                                }
                                else if (proc.ExitCode != 0)
                                {
                                    _logger?.LogWarning("'dotnet publish' ZcuAgent thoát với mã lỗi {ExitCode}.", proc.ExitCode);
                                }
                            }

                            if (Directory.Exists(outDir) && File.Exists(Path.Combine(outDir, "IPGS.RemoteControl.ZcuAgent")))
                                return outDir;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Không thể tự động publish ZcuAgent.");
                        }
                    }
                    projDir = Directory.GetParent(projDir)?.FullName;
                }
            }

            return null;
        }
    }
}
