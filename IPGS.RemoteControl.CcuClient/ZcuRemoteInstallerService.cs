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

                Report("🔍 [2/7] Kiểm tra hệ điều hành & môi trường hiển thị (X11/Wayland)...", 20);
                // F-wayland: đọc XDG_SESSION_TYPE của phiên đồ hoạ ĐANG chạy (không phải phiên
                // SSH hiện tại, luôn là headless) — dò qua loginctl vì agent chạy dưới graphical
                // session của user, còn `echo $XDG_SESSION_TYPE` qua SSH thường trả rỗng.
                var sessionRes = ExecuteCommand(ssh,
                    $"loginctl show-user {ShellQuote.Quote(username)} -p Sessions --value 2>/dev/null | tr ' ' '\\n' | " +
                    "xargs -I{} loginctl show-session {} -p Type --value 2>/dev/null | grep -v '^tty$\\|^unspecified$' | head -1");
                string sessionType = sessionRes.Trim().ToLower();
                if (string.IsNullOrEmpty(sessionType))
                {
                    sessionType = "x11"; // fallback an toàn — giữ hành vi cũ nếu không dò được
                }

                if (sessionType == "wayland")
                {
                    Report("ℹ️ Session hiện tại là GNOME Wayland — dùng đường Mutter D-Bus (ScreenCast/RemoteDesktop) thay vì XTest/XShm.", 25);
                }
                else if (sessionType != "x11")
                {
                    Report($"⚠️ Cảnh báo: session type '{sessionType}' không xác định — giả định X11.", 25);
                    sessionType = "x11";
                }

                const string remoteOfflineDir = "/tmp/ipgs-offline";
                ExecuteCommand(ssh, $"mkdir -p {remoteOfflineDir}");

                // F15: X11 libs + .NET Runtime giờ được cài OFFLINE từ resource nhúng sẵn
                // trong CcuUI (Resources/x11-deb, Resources/dotnet-runtime) — không còn gọi
                // apt-get/wget ra mạng. Nếu build CcuUI cũ chưa có resource, fallback về
                // apt-get/wget như trước để không phá luồng cài đặt hiện có.
                Report("📦 [3/7] Cài đặt các thư viện Native X11 (libx11, libxext, libxtst)...", 35);
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

                // F-wayland: WaylandScreenCapturer shells out to gst-launch-1.0 (pipewiresrc)
                // — chỉ có sẵn từ apt, chưa có gói offline nhúng như x11-deb. Cài online;
                // nếu máy Wayland không có mạng, cài đặt sẽ log lỗi rõ ràng thay vì agent
                // fail âm thầm lúc runtime khi không tìm thấy gst-launch-1.0.
                if (sessionType == "wayland")
                {
                    Report("📦 [3.1/7] Cài đặt GStreamer + PipeWire plugin cho capture Wayland...", 40);
                    ExecuteSudoCommand(ssh,
                        "dpkg -l gstreamer1.0-tools gstreamer1.0-plugins-base gstreamer1.0-pipewire >/dev/null 2>&1 || " +
                        "(systemctl stop unattended-upgrades.service 2>/dev/null || true; apt-get update -qq && " +
                        "apt-get install -y -qq gstreamer1.0-tools gstreamer1.0-plugins-base gstreamer1.0-pipewire)",
                        options.Password);
                }

                Report("💻 [4/7] Kiểm tra & cài đặt .NET 8 Runtime...", 50);
                // F20: dùng --list-runtimes thay --version — máy chỉ cài .NET RUNTIME
                // (framework-dependent, không có SDK) khiến `dotnet --version` luôn lỗi
                // "No .NET SDKs were found" dù runtime đã cài đủ, làm installer tưởng
                // NOT_FOUND và tải lại runtime mỗi lần chạy (verify thật trên ZCU
                // 192.168.0.102 — `dotnet --version` fail nhưng `--list-runtimes` báo
                // đúng "Microsoft.NETCore.App 8.0.20 [...]").
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
Environment=XDG_SESSION_TYPE={sessionType}
{(sessionType == "x11" ? "Environment=DISPLAY=:0\n" : "")}Restart=on-failure
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

                // F18: tắt lock-enabled/idle-delay ở trên chỉ ngăn khoá màn hình TRONG
                // TƯƠNG LAI — không tự mở khoá phiên ĐANG bị khoá sẵn từ trước lúc cài
                // (toàn bộ cài đặt chạy qua SSH, không ai chạm màn hình ZCU). Nếu phiên đã
                // lock do idle trước khi cài xong, ZcuAgent vẫn capture đúng màn hình đang
                // hiển thị — mà màn hình lock/blank thì capture ra đen (gotcha ghi trong
                // docs/devops/DEPLOY-remote-control.md mục 6.4). Chủ động unlock mọi
                // session của user ngay sau khi tắt khoá màn hình để không cần thao tác
                // tay ở lần cài đầu.
                ExecuteSudoCommand(ssh,
                    $"for sid in $(loginctl list-sessions --no-legend 2>/dev/null | awk -v u={ShellQuote.Quote(username)} '$3==u{{print $1}}'); do loginctl unlock-session \"$sid\" 2>/dev/null || true; done",
                    options.Password);

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

        private string? ResolveOrBuildZcuAgentBinaries(Action<string, double>? report)
        {
            // F15: ưu tiên binary ZcuAgent đã nhúng sẵn trong output CcuUI trước khi
            // tìm/publish từ source — đây là đường offline chính, không cần .NET SDK trên CCU.
            string? embedded = ResolveResourceDir(Path.Combine("zcu-agent", "linux-x64"));
            if (!string.IsNullOrEmpty(embedded) && File.Exists(Path.Combine(embedded, "IPGS.RemoteControl.ZcuAgent")))
                return embedded;

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
