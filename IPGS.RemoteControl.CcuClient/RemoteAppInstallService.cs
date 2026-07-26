using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuClient
{
    public class RemoteAppInstallOptions
    {
        public string Host { get; set; } = string.Empty;
        public int SshPort { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SudoPassword { get; set; } = string.Empty;
        public string AppInstallerFile { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
    }

    public class RemoteAppInstallService
    {
        private readonly ILogger<RemoteAppInstallService>? _logger;

        public RemoteAppInstallService(ILogger<RemoteAppInstallService>? logger = null)
        {
            _logger = logger;
        }

        public Task InstallAsync(RemoteAppInstallOptions options, Action<string>? onLog = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                void Log(string msg) => onLog?.Invoke(msg);

                if (string.IsNullOrEmpty(options.AppInstallerFile) || !File.Exists(options.AppInstallerFile))
                {
                    throw new FileNotFoundException($"Không tìm thấy file cài đặt: {options.AppInstallerFile}");
                }

                Log($"=== Bắt đầu cài đặt ứng dụng tới {options.Username}@{options.Host} ===");

                using var ssh = new SshClient(options.Host, options.SshPort, options.Username, options.Password);
                ssh.Connect();
                if (!ssh.IsConnected)
                    throw new Exception("Không thể mở kết nối SSH tới máy đích.");

                // ── Bước 1: Upload App ────────────────────────────────────────
                Log("--- [1/2] Tải ứng dụng lên máy đích qua SFTP ---");
                using (var sftp = new SftpClient(options.Host, options.SshPort, options.Username, options.Password))
                {
                    sftp.Connect();
                    UploadInstaller(sftp, options.AppInstallerFile, options.Username, Log);
                    sftp.Disconnect();
                }

                // ── Bước 2: Cài đặt App ────────────────────────────────────────
                Log("--- [2/2] Chạy cài đặt trên máy đích ---");

                // S1/Q12: dùng ShellQuote.Quote cho password (thay escape lặp tay).
                string envCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus KIOSK_SUDO_PASS={ShellQuote.Quote(options.SudoPassword)}";

                // S1: fileName nội suy vào TRONG chuỗi bash -c '...' đã single-quote nên
                // không thể Quote lồng — validate whitelist, từ chối tên chứa ' ; $() space...
                string fileName = ShellQuote.ValidateFileName(
                    Path.GetFileName(options.AppInstallerFile), nameof(options.AppInstallerFile));
                string ext = Path.GetExtension(fileName).ToLower();
                Log($"🔄 Đang chạy lệnh cài đặt cho: {fileName}...");

                if (ext == ".deb")
                {
                    RunCommand(ssh, $"{envCmd} bash -c 'echo \"$KIOSK_SUDO_PASS\" | sudo -S dpkg -i \"$HOME/{fileName}\"'", Log);
                }
                else if (ext == ".sh" || ext == ".run")
                {
                    // A5: trước đây là `sudo -S ./~/{fileName}` — `~` sau `./` là literal,
                    // đường dẫn ./~/file không tồn tại → nhánh .sh/.run LUÔN thất bại.
                    RunCommand(ssh, $"{envCmd} bash -c 'chmod +x \"$HOME/{fileName}\" && echo \"$KIOSK_SUDO_PASS\" | sudo -S \"$HOME/{fileName}\"'", Log);
                }
                else
                {
                    Log($"⚠️ Không rõ cách cài đặt tự động cho định dạng {ext}. Đã upload file lên ~/{fileName}");
                }

                ssh.Disconnect();
                Log("=== HOÀN THÀNH CÀI ĐẶT ỨNG DỤNG ===");
            }, cancellationToken);
        }

        public Task UninstallAsync(RemoteAppInstallOptions options, Action<string>? onLog = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                void Log(string msg) => onLog?.Invoke(msg);

                if (string.IsNullOrWhiteSpace(options.PackageName))
                {
                    throw new ArgumentException("Thiếu tên package để gỡ cài đặt.");
                }

                // S1: PackageName nội suy vào dpkg -P / find -iname / rm -rf bên trong
                // bash -c '...' → validate whitelist, chặn 'foo';rm -rf ~;' và tương tự.
                string packageName = ShellQuote.ValidatePackageName(options.PackageName, nameof(options.PackageName));

                Log($"=== Bắt đầu gỡ cài đặt '{packageName}' trên {options.Username}@{options.Host} ===");

                using var ssh = new SshClient(options.Host, options.SshPort, options.Username, options.Password);
                ssh.Connect();
                if (!ssh.IsConnected)
                    throw new Exception("Không thể mở kết nối SSH tới máy đích.");

                string envCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus KIOSK_SUDO_PASS={ShellQuote.Quote(options.SudoPassword)}";

                Log($"🔄 Đang chạy lệnh gỡ cài đặt (Purge) dpkg -P {packageName}...");
                RunCommand(ssh, $"{envCmd} bash -c 'echo \"$KIOSK_SUDO_PASS\" | sudo -S dpkg -P {packageName}'", Log);

                Log($"🧹 Đang dọn dẹp các shortcut (.desktop) còn sót lại...");
                string cleanupCmd = $"echo \"$KIOSK_SUDO_PASS\" | sudo -S find /usr/share/applications /etc/xdg/autostart ~/.local/share/applications ~/Desktop -iname \"*{packageName}*.desktop\" -delete 2>/dev/null || true";
                RunCommand(ssh, $"{envCmd} bash -c '{cleanupCmd}'", null, silent: true);

                // baseName là chuỗi con của packageName đã validate → cũng an toàn.
                string baseName = packageName.Replace("kztek-", "").Trim('-');
                if (!string.IsNullOrEmpty(baseName) && baseName != packageName)
                {
                    string cleanupCmd2 = $"echo \"$KIOSK_SUDO_PASS\" | sudo -S find /usr/share/applications /etc/xdg/autostart ~/.local/share/applications ~/Desktop -iname \"*{baseName}*.desktop\" -delete 2>/dev/null || true";
                    RunCommand(ssh, $"{envCmd} bash -c '{cleanupCmd2}'", null, silent: true);
                }

                Log($"🧹 Đang dọn dẹp thư mục rác /opt/kztek/{baseName} (nếu có)...");
                string rmFolderCmd = $"echo \"$KIOSK_SUDO_PASS\" | sudo -S rm -rf /opt/kztek/{baseName} /opt/kztek/{packageName} 2>/dev/null || true";
                RunCommand(ssh, $"{envCmd} bash -c '{rmFolderCmd}'", null, silent: true);

                RunCommand(ssh, $"{envCmd} bash -c 'update-desktop-database ~/.local/share/applications 2>/dev/null || true'", null, silent: true);
                RunCommand(ssh, $"{envCmd} bash -c 'echo \"$KIOSK_SUDO_PASS\" | sudo -S update-desktop-database /usr/share/applications 2>/dev/null || true'", null, silent: true);

                ssh.Disconnect();
                Log("=== HOÀN THÀNH GỠ CÀI ĐẶT ===");
            }, cancellationToken);
        }

        private static void UploadInstaller(SftpClient sftp, string localPath, string username, Action<string> log)
        {
            string fileName = Path.GetFileName(localPath);
            string remotePath = $"/home/{username}/{fileName}";

            using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read);
            long sizeMb = fs.Length / 1024 / 1024;
            log($"📤 Đang tải {fileName} lên {remotePath} ({(sizeMb > 0 ? sizeMb : "<1")} MB)...");
            sftp.UploadFile(fs, remotePath, true);
            log($"✅ Đã tải xong {fileName}.");
        }

        private static void RunCommand(SshClient ssh, string commandText, Action<string>? log, bool silent = false)
        {
            using var cmd = ssh.CreateCommand(commandText, Encoding.UTF8);
            cmd.Execute();

            if (silent || log is null) return;

            string result = (cmd.Result ?? string.Empty).TrimEnd();
            if (!string.IsNullOrEmpty(result)) log(result);

            string error = (cmd.Error ?? string.Empty).TrimEnd();
            if (!string.IsNullOrEmpty(error)) log(error);
        }
    }
}
