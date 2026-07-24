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

        // ── Tab "Config máy tính" — mọi thứ liên quan đến máy/OS/UI (KHÔNG phải
        // update hay autostart phần mềm). Nửa đầu chạy qua 1-install-software.sh,
        // nửa sau qua 2-configure-system.sh — nhưng cùng nằm 1 tab vì đều là cấu
        // hình máy tính, không phải phần mềm.
        public bool HideTopBar { get; set; } = true;
        public bool HideActivities { get; set; } = true;
        public bool HideWorkspaceSwitcher { get; set; } = true;
        public bool HideDash { get; set; } = true;
        public bool InstallUnclutter { get; set; } = true;
        public bool HideVirtualKeyboard { get; set; } = true;
        public bool DisableHotCorner { get; set; } = true;

        /// <summary>
        /// Khóa còn 1 workspace tĩnh (dynamic-workspaces=false, num-workspaces=1) — chặn
        /// triệt để lỗi cử chỉ 2/3 ngón trên màn cảm ứng bị Mutter hiểu thành gesture
        /// chuyển workspace, làm app fullscreen "biến mất" sang workspace khác.
        /// </summary>
        public bool LockSingleWorkspace { get; set; } = true;
        public bool DisableDockIcons { get; set; } = true;
        public bool BlockSleep { get; set; } = true;
        public bool SkipInitialSetup { get; set; } = true;
        public bool EnableAutologin { get; set; } = true;

        /// <summary>
        /// LUÔN true — mỗi checkbox trong Tab "Config máy tính" giờ là toggle 2 chiều thật
        /// (tick = ẩn, bỏ tick = set về true/hiện lại), nên script luôn phải chạy để áp dụng
        /// đúng giá trị hiện tại, không còn khái niệm "bỏ qua vì không chọn" như trước.
        /// </summary>
        public bool RunInstallSoftware => true;

        // ── Tab "Config phần mềm" — CHỈ update phần mềm + autostart phần mềm ──
        public bool DisableSoftwareUpdate { get; set; } = true;
        public bool EnableAutostart { get; set; } = true;

        /// <summary>LUÔN true — cùng lý do như <see cref="RunInstallSoftware"/>.</summary>
        public bool RunConfigureSystem => true;

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
                        "Không tìm thấy thư mục scripts/linux-kiosk (1-install-software.sh / 2-configure-system.sh).");
                }

                string script1 = Path.Combine(scriptsDir, "1-install-software.sh");
                string script2 = Path.Combine(scriptsDir, "2-configure-system.sh");

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
                    sftp.Disconnect();
                }

                Log("--- [2/2] Chạy setup trên máy kiosk (có thể mất vài phút) ---");

                RunCommand(ssh, "chmod +x ~/1-install-software.sh ~/2-configure-system.sh 2>/dev/null; true", Log);
                RunCommand(ssh, "export DISPLAY=:0; export DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus", Log, silent: true);

                if (!string.IsNullOrEmpty(options.SudoPassword))
                {
                    string escapedSudo = options.SudoPassword.Replace("'", "'\\''");
                    RunCommand(ssh, $"echo '{escapedSudo}' | sudo -S -v 2>&1 && echo SUDO_CACHED_OK", Log);
                }

                string envPrefix = "export DISPLAY=:0; export DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus; ";

                if (options.RunInstallSoftware)
                {
                    Log("🔄 Đang chạy 1-install-software.sh (Config máy tính — phần extension/unclutter/bàn phím ảo)...");
                    string args1 = $"{B(options.HideTopBar)} {B(options.HideActivities)} {B(options.HideWorkspaceSwitcher)} {B(options.HideDash)} {B(options.InstallUnclutter)} {B(options.HideVirtualKeyboard)}";
                    RunCommand(ssh, envPrefix + $"bash ~/1-install-software.sh {args1}", Log);
                }

                if (options.RunConfigureSystem)
                {
                    Log("🔄 Đang chạy 2-configure-system.sh (Config máy tính — phần hệ thống + Config phần mềm — update/autostart)...");
                    string kioskUser = string.IsNullOrEmpty(options.KioskUser) ? options.Username : options.KioskUser;
                    string args2 = $"{B(options.DisableHotCorner)} {B(options.DisableDockIcons)} {B(options.BlockSleep)} {B(options.SkipInitialSetup)} {B(options.EnableAutologin)} {B(options.DisableSoftwareUpdate)} {B(options.EnableAutostart)} {B(options.LockSingleWorkspace)}";
                    RunCommand(ssh, envPrefix + $"bash ~/2-configure-system.sh '{kioskUser}' '{options.AppExec}' {args2}", Log);
                }

                ssh.Disconnect();
                Log("=== HOÀN THÀNH — nhớ RESTART máy kiosk để áp dụng autologin + autostart. ===");
            }, cancellationToken);
        }

        private static string B(bool value) => value ? "1" : "0";

        private static void UploadScript(SftpClient sftp, string localPath, string username, Action<string> log)
        {
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"Không tìm thấy script: {localPath}");

            string fileName = Path.GetFileName(localPath);
            string remotePath = $"/home/{username}/{fileName}";

            // Normalize CRLF → LF trước khi upload lên Linux.
            // File .sh chỉnh sửa trên Windows có thể có CRLF, khiến bash báo lỗi
            // "$'\r': command not found" hoặc "unexpected end of file".
            string content = File.ReadAllText(localPath, Encoding.UTF8);
            bool hadCrlf = content.Contains("\r\n");
            if (hadCrlf) content = content.Replace("\r\n", "\n");
            // Cũng xử lý \r đơn lẻ (Mac CR cũ) nếu có
            content = content.Replace("\r", "\n");

            byte[] bytes = Encoding.UTF8.GetBytes(content);
            using var ms = new MemoryStream(bytes);
            sftp.UploadFile(ms, remotePath, true);
            log($"📤 Đã tải {fileName} lên {remotePath}" + (hadCrlf ? " (đã convert CRLF→LF)" : ""));
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
                        File.Exists(Path.Combine(candidate, "2-configure-system.sh")))
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
