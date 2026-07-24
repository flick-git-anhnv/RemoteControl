using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuClient
{
    public class KioskDeployOptions
    {
        public string Host { get; set; } = string.Empty;
        public int SshPort { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SudoPassword { get; set; } = string.Empty;

        public string KioskUser { get; set; } = string.Empty;
        public string AppExec { get; set; } = "ipgskioskavalonia";

        public bool RunInstallSoftware { get; set; } = true;
        public bool RunConfigureSystem { get; set; } = true;

        /// <summary>"hide", "show", hoặc rỗng nếu không đổi.</summary>
        public string ToggleMode { get; set; } = string.Empty;

        public string? ScriptsSourceDir { get; set; }
    }

    /// <summary>
    /// Deploy kiosk setup (ẩn Top Bar/Dock + cấu hình autologin/autostart) TỪ XA qua SSH,
    /// port từ scripts/windows-tools/KioskDeployTool.ps1 (dùng plink/pscp) sang SSH.NET
    /// để chạy trực tiếp trong CcuUI, không cần cài PuTTY.
    /// </summary>
    public class KioskDeployService
    {
        private readonly ILogger<KioskDeployService>? _logger;

        public KioskDeployService(ILogger<KioskDeployService>? logger = null)
        {
            _logger = logger;
        }

        public Task<bool> TestSshConnectionAsync(KioskDeployOptions options, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var client = new SshClient(options.Host, options.SshPort, options.Username, options.Password);
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

        public Task DeployAsync(KioskDeployOptions options, Action<string>? onLog = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                void Log(string msg) => onLog?.Invoke(msg);

                string? scriptsDir = options.ScriptsSourceDir;
                if (string.IsNullOrEmpty(scriptsDir) || !Directory.Exists(scriptsDir))
                {
                    scriptsDir = ResolveKioskScriptsDir();
                }

                if (string.IsNullOrEmpty(scriptsDir))
                {
                    throw new DirectoryNotFoundException(
                        "Không tìm thấy thư mục scripts/linux-kiosk (1-install-software.sh / 2-configure-system.sh / 3-toggle-topbar.sh).");
                }

                string script1 = Path.Combine(scriptsDir, "1-install-software.sh");
                string script2 = Path.Combine(scriptsDir, "2-configure-system.sh");
                string script3 = Path.Combine(scriptsDir, "3-toggle-topbar.sh");

                bool needToggle = !string.IsNullOrEmpty(options.ToggleMode);

                Log($"=== Bắt đầu deploy tới {options.Username}@{options.Host} ===");

                using var ssh = new SshClient(options.Host, options.SshPort, options.Username, options.Password);
                ssh.Connect();
                if (!ssh.IsConnected)
                    throw new Exception("Không thể mở kết nối SSH tới máy kiosk.");

                Log("--- [1/2] Tải script lên máy kiosk qua SFTP ---");
                using (var sftp = new SftpClient(options.Host, options.SshPort, options.Username, options.Password))
                {
                    sftp.Connect();
                    if (options.RunInstallSoftware) UploadScript(sftp, script1, options.Username, Log);
                    if (options.RunConfigureSystem) UploadScript(sftp, script2, options.Username, Log);
                    if (needToggle) UploadScript(sftp, script3, options.Username, Log);
                    sftp.Disconnect();
                }

                Log("--- [2/2] Chạy setup trên máy kiosk (có thể mất vài phút) ---");

                RunCommand(ssh, "chmod +x ~/1-install-software.sh ~/2-configure-system.sh ~/3-toggle-topbar.sh 2>/dev/null; true", Log);
                RunCommand(ssh, "export DISPLAY=:0; export DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus", Log, silent: true);

                if (!string.IsNullOrEmpty(options.SudoPassword))
                {
                    string escapedSudo = options.SudoPassword.Replace("'", "'\\''");
                    RunCommand(ssh, $"echo '{escapedSudo}' | sudo -S -v 2>&1 && echo SUDO_CACHED_OK", Log);
                }

                string envPrefix = "export DISPLAY=:0; export DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus; ";

                if (options.RunInstallSoftware)
                {
                    Log("🔄 Đang chạy 1-install-software.sh...");
                    RunCommand(ssh, envPrefix + "bash ~/1-install-software.sh", Log);
                }

                if (options.RunConfigureSystem)
                {
                    Log("🔄 Đang chạy 2-configure-system.sh...");
                    string kioskUser = string.IsNullOrEmpty(options.KioskUser) ? options.Username : options.KioskUser;
                    RunCommand(ssh, envPrefix + $"bash ~/2-configure-system.sh '{kioskUser}' '{options.AppExec}'", Log);
                }

                if (needToggle)
                {
                    Log($"🔄 Đang chạy 3-toggle-topbar.sh {options.ToggleMode}...");
                    RunCommand(ssh, envPrefix + $"bash ~/3-toggle-topbar.sh {options.ToggleMode}", Log);
                }

                ssh.Disconnect();
                Log("=== HOÀN THÀNH — nhớ RESTART máy kiosk để áp dụng autologin + autostart. ===");
            }, cancellationToken);
        }

        private static void UploadScript(SftpClient sftp, string localPath, string username, Action<string> log)
        {
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"Không tìm thấy script: {localPath}");

            string fileName = Path.GetFileName(localPath);
            string remotePath = $"/home/{username}/{fileName}";
            using var fs = File.OpenRead(localPath);
            sftp.UploadFile(fs, remotePath, true);
            log($"📤 Đã tải {fileName} lên {remotePath}");
        }

        private static void RunCommand(SshClient ssh, string commandText, Action<string> log, bool silent = false)
        {
            using var cmd = ssh.CreateCommand(commandText);
            cmd.Execute();

            if (silent) return;

            string result = (cmd.Result ?? string.Empty).TrimEnd();
            if (!string.IsNullOrEmpty(result)) log(result);

            string error = (cmd.Error ?? string.Empty).TrimEnd();
            if (!string.IsNullOrEmpty(error)) log(error);
        }

        private string? ResolveKioskScriptsDir()
        {
            var searchRoots = new System.Collections.Generic.List<string>();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir)) searchRoots.Add(baseDir);

            string currentDir = Directory.GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir) && !searchRoots.Contains(currentDir))
                searchRoots.Add(currentDir);

            foreach (var root in searchRoots)
            {
                string? dir = root;
                for (int i = 0; i < 8 && dir != null; i++)
                {
                    string candidate = Path.Combine(dir, "scripts", "linux-kiosk");
                    if (Directory.Exists(candidate) &&
                        File.Exists(Path.Combine(candidate, "1-install-software.sh")) &&
                        File.Exists(Path.Combine(candidate, "2-configure-system.sh")) &&
                        File.Exists(Path.Combine(candidate, "3-toggle-topbar.sh")))
                    {
                        return candidate;
                    }
                    dir = Directory.GetParent(dir)?.FullName;
                }
            }

            return null;
        }
    }
}
