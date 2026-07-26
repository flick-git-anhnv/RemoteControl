using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuClient
{
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
        public const string DefaultLanAllowedClientIPs = "192.168.0.0/16,10.0.0.0/8,172.16.0.0/12";

        public string AllowedClientIPs { get; set; } = DefaultLanAllowedClientIPs;
        public int TargetFps { get; set; } = 15;
        public int JpegQuality { get; set; } = 70;
        public string? PublishSourceDir { get; set; }
    }

    public class ZcuRemoteInstallerService
    {
        private readonly ILogger<ZcuRemoteInstallerService>? _logger;

        public ZcuRemoteInstallerService(ILogger<ZcuRemoteInstallerService>? logger = null)
        {
            _logger = logger;
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

        public Task InstallZcuAgentAsync(SshInstallerOptions options, Action<string, double>? onProgress = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                void Report(string msg, double percent) => onProgress?.Invoke(msg, percent);

                // S1: Username nội suy vào path /home/..., unit systemd, loginctl —
                // validate whitelist trước khi dùng để tránh command injection.
                string username = ShellQuote.ValidateUsername(options.Username, nameof(options.Username));

                Report("🔄 [1/7] Đang kết nối SSH đến ZCU...", 10);
                using var ssh = CreateSshClient(options);
                ssh.Connect();

                if (!ssh.IsConnected)
                    throw new Exception("Không thể mở kết nối SSH đến máy ZCU.");

                Report("🔍 [2/7] Kiểm tra hệ điều hành & môi trường X11...", 20);
                var sessionRes = ExecuteCommand(ssh, "echo $XDG_SESSION_TYPE");
                string sessionType = sessionRes.Trim().ToLower();
                if (sessionType == "wayland")
                {
                    Report("⚠️ Cảnh báo: Session hiện tại là Wayland. ZcuAgent yêu cầu session X11 (Ubuntu on Xorg).", 25);
                }

                Report("📦 [3/7] Cài đặt các thư viện Native X11 (libx11, libxext, libxtst)...", 35);
                ExecuteSudoCommand(ssh, "dpkg -l libx11-6 libxext6 libxtst6 wget >/dev/null 2>&1 || (systemctl stop unattended-upgrades.service 2>/dev/null || true; apt-get update -qq && apt-get install -y -qq libx11-6 libxext6 libxtst6 wget)", options.Password);

                Report("💻 [4/7] Kiểm tra & cài đặt .NET 8 Runtime...", 50);
                var dotnetCheck = ExecuteCommand(ssh, "dotnet --version 2>/dev/null || $HOME/.dotnet/dotnet --version 2>/dev/null || echo 'NOT_FOUND'");
                if (dotnetCheck.Contains("NOT_FOUND"))
                {
                    Report("⬇️ Đang tải script và cài .NET 8 Runtime...", 55);
                    ExecuteCommand(ssh, "wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh && chmod +x /tmp/dotnet-install.sh && /tmp/dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir $HOME/.dotnet && rm -f /tmp/dotnet-install.sh");
                }

                string remoteInstallDir = $"/home/{username}/ipgs/remote-agent";
                ExecuteCommand(ssh, $"mkdir -p {remoteInstallDir}");

                // Ensure PublishSourceDir is resolved
                string? publishDir = options.PublishSourceDir;
                if (string.IsNullOrEmpty(publishDir) || !Directory.Exists(publishDir) || !File.Exists(Path.Combine(publishDir, "IPGS.RemoteControl.ZcuAgent")))
                {
                    publishDir = ResolveOrBuildZcuAgentBinaries(Report);
                }

                if (!string.IsNullOrEmpty(publishDir) && Directory.Exists(publishDir))
                {
                    Report("📤 [5/7] Đang tải các tệp thực thi ZcuAgent lên ZCU qua SFTP...", 75);
                    ExecuteCommand(ssh, "systemctl --user stop ipgs-remote-agent.service 2>/dev/null || true");
                    using var sftp = CreateSftpClient(options);
                    sftp.Connect();
                    UploadDirectory(sftp, publishDir, remoteInstallDir);
                    sftp.Disconnect();
                    ExecuteCommand(ssh, $"chmod +x {remoteInstallDir}/IPGS.RemoteControl.ZcuAgent 2>/dev/null || true");
                }
                else
                {
                    // Check if file already exists on remote ZCU
                    var fileCheck = ExecuteCommand(ssh, $"[ -f {remoteInstallDir}/IPGS.RemoteControl.ZcuAgent ] && echo 'EXISTS' || echo 'MISSING'");
                    if (!fileCheck.Contains("EXISTS"))
                    {
                        throw new FileNotFoundException($"Không tìm thấy tệp thực thi IPGS.RemoteControl.ZcuAgent để tải lên ZCU. Vui lòng đảm bảo dự án ZcuAgent đã được biên dịch linux-x64.");
                    }
                }

                Report("⚙️ [5.1] Tạo và cập nhật cấu hình appsettings.json...", 78);
                // Q4: dựng JSON bằng JsonSerializer thay vì nội suy chuỗi — token chứa
                // dấu " hoặc newline sẽ được escape đúng, không phá cấu trúc JSON/heredoc.
                string jsonConfig = JsonSerializer.Serialize(new
                {
                    RemoteControl = new
                    {
                        Port = options.AgentPort,
                        Token = options.AgentToken,
                        // F06: tách chuỗi "cidr1,cidr2" thành từng phần tử — AuthManager.IsInRange
                        // parse TỪNG entry riêng; 1 entry gộp "a/16,b/8" sẽ không parse được → deny-all.
                        AllowedClientIPs = options.AllowedClientIPs
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

                ExecuteCommand(ssh, $"cat << 'EOF' > {remoteInstallDir}/appsettings.json\n{jsonConfig}\nEOF");

                Report("🛠️ [6/7] Đăng ký Systemd User Service & Lingering...", 85);
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

                Report("🛡️ [7/7] Mở cổng Firewall & Tắt khoá màn hình tự động...", 95);
                ExecuteSudoCommand(ssh, $"ufw allow {options.AgentPort}/tcp comment 'IPGS Remote Agent'", options.Password);
                ExecuteCommand(ssh, "gsettings set org.gnome.desktop.screensaver lock-enabled false 2>/dev/null || true");
                ExecuteCommand(ssh, "gsettings set org.gnome.desktop.session idle-delay 0 2>/dev/null || true");

                // Start service and check active status
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

                ssh.Disconnect();
            }, cancellationToken);
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
            foreach (var file in Directory.GetFiles(localPath))
            {
                string fileName = Path.GetFileName(file);
                string remoteFilePath = remotePath + "/" + fileName;
                using var fs = File.OpenRead(file);
                sftp.UploadFile(fs, remoteFilePath, true);
            }

            foreach (var dir in Directory.GetDirectories(localPath))
            {
                string dirName = Path.GetFileName(dir);
                string remoteDirPath = remotePath + "/" + dirName;
                if (!sftp.Exists(remoteDirPath))
                {
                    sftp.CreateDirectory(remoteDirPath);
                }
                UploadDirectory(sftp, dir, remoteDirPath);
            }
        }

        private string? ResolveOrBuildZcuAgentBinaries(Action<string, double>? report)
        {
            var searchRoots = new List<string>();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir)) searchRoots.Add(baseDir);

            string currentDir = Directory.GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir) && !searchRoots.Contains(currentDir))
                searchRoots.Add(currentDir);

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
