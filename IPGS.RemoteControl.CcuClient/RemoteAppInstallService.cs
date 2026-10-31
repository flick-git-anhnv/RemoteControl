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

    /// <summary>Tiến độ cài đặt. Percent 0-100; &lt; 0 = không xác định (indeterminate).</summary>
    public readonly record struct InstallProgress(int Percent, string Text);

    public class RemoteAppInstallService
    {
        private readonly ILogger<RemoteAppInstallService>? _logger;

        public RemoteAppInstallService(ILogger<RemoteAppInstallService>? logger = null)
        {
            _logger = logger;
        }

        public Task InstallAsync(RemoteAppInstallOptions options, Action<string>? onLog = null, CancellationToken cancellationToken = default, Action<InstallProgress>? onProgress = null)
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
                    UploadInstaller(sftp, options.AppInstallerFile, options.Username, Log, onProgress);
                    sftp.Disconnect();
                }

                // ── Bước 2: Cài đặt App ────────────────────────────────────────
                Log("--- [2/2] Chạy cài đặt trên máy đích ---");
                // dpkg/sh không báo % → thanh chạy không xác định (Percent < 0).
                onProgress?.Invoke(new InstallProgress(-1, "Đang cài đặt trên máy đích (không xác định thời gian)..."));

                string fileName = ShellQuote.ValidateFileName(
                    Path.GetFileName(options.AppInstallerFile), nameof(options.AppInstallerFile));
                string ext = Path.GetExtension(fileName).ToLower();
                Log($"🔄 Đang chạy lệnh cài đặt cho: {fileName}...");

                if (ext == ".deb")
                {
                    RunSudoCommand(ssh, $"sudo -S -p '' dpkg -i \"$HOME/{fileName}\"", options.SudoPassword, Log);
                }
                else if (ext == ".sh" || ext == ".run")
                {
                    RunSudoCommand(ssh, $"chmod +x \"$HOME/{fileName}\" && sudo -S -p '' \"$HOME/{fileName}\"", options.SudoPassword, Log);
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

                Log($"🔄 Đang chạy lệnh gỡ cài đặt (Purge) dpkg -P {packageName}...");
                RunSudoCommand(ssh, $"sudo -S -p '' dpkg -P {packageName}", options.SudoPassword, Log);

                Log($"🧹 Đang dọn dẹp các shortcut (.desktop) còn sót lại...");
                string cleanupCmd = $"sudo -S -p '' find /usr/share/applications /etc/xdg/autostart ~/.local/share/applications ~/Desktop -iname \"*{packageName}*.desktop\" -delete 2>/dev/null || true";
                RunSudoCommand(ssh, cleanupCmd, options.SudoPassword, null, silent: true);

                // baseName là chuỗi con của packageName đã validate → cũng an toàn.
                string baseName = packageName.Replace("kztek-", "").Trim('-');
                if (!string.IsNullOrEmpty(baseName) && baseName != packageName)
                {
                    string cleanupCmd2 = $"sudo -S -p '' find /usr/share/applications /etc/xdg/autostart ~/.local/share/applications ~/Desktop -iname \"*{baseName}*.desktop\" -delete 2>/dev/null || true";
                    RunSudoCommand(ssh, cleanupCmd2, options.SudoPassword, null, silent: true);
                }

                Log($"🧹 Đang dọn dẹp thư mục rác /opt/kztek/{baseName} (nếu có)...");
                string rmFolderCmd = $"sudo -S -p '' rm -rf /opt/kztek/{baseName} /opt/kztek/{packageName} 2>/dev/null || true";
                RunSudoCommand(ssh, rmFolderCmd, options.SudoPassword, null, silent: true);

                RunSudoCommand(ssh, "update-desktop-database ~/.local/share/applications 2>/dev/null || true", options.SudoPassword, null, silent: true);
                RunSudoCommand(ssh, "sudo -S -p '' update-desktop-database /usr/share/applications 2>/dev/null || true", options.SudoPassword, null, silent: true);

                ssh.Disconnect();
                Log("=== HOÀN THÀNH GỠ CÀI ĐẶT ===");
            }, cancellationToken);
        }

        private static void UploadInstaller(SftpClient sftp, string localPath, string username, Action<string> log, Action<InstallProgress>? onProgress)
        {
            string fileName = Path.GetFileName(localPath);
            string remotePath = $"/home/{username}/{fileName}";

            using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read);
            long total = fs.Length;
            long sizeMb = total / 1024 / 1024;
            log($"📤 Đang tải {fileName} lên {remotePath} ({(sizeMb > 0 ? sizeMb : "<1")} MB)...");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int lastPct = -1, lastLogPct = 0;
            sftp.UploadFile(fs, remotePath, true, sent =>
            {
                int pct = total == 0 ? 100 : (int)(sent * 100UL / (ulong)total);
                if (pct == lastPct) return;
                lastPct = pct;
                double mbSent = sent / 1048576.0;
                double speed = sw.Elapsed.TotalSeconds > 0 ? mbSent / sw.Elapsed.TotalSeconds : 0;
                onProgress?.Invoke(new InstallProgress(pct, $"Đang tải lên: {pct}% ({mbSent:F0}/{total / 1048576.0:F0} MB, {speed:F1} MB/s)"));
                if (pct >= lastLogPct + 25) { lastLogPct = pct; log($"   … {pct}%"); }
            });
            log($"✅ Đã tải xong {fileName}.");
        }

        private static void RunSudoCommand(SshClient ssh, string commandText, string sudoPassword, Action<string>? log, bool silent = false)
        {
            // S2: Bảo mật sudo — không truyền password qua env hay cmdline. Mật khẩu được ghi vào STDIN channel.
            string envPrefix = "env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus";
            string wrapped = $"{envPrefix} bash -c {ShellQuote.Quote(commandText)}";
            using var cmd = ssh.CreateCommand(wrapped, Encoding.UTF8);
            var async = cmd.BeginExecute();

            if (!string.IsNullOrEmpty(sudoPassword))
            {
                try
                {
                    Stream? input = null;
                    for (int i = 0; i < 20; i++)
                    {
                        try { input = cmd.CreateInputStream(); break; }
                        catch { Thread.Sleep(50); }
                    }
                    if (input != null)
                    {
                        using (input)
                        {
                            var passBytes = Encoding.UTF8.GetBytes(sudoPassword + "\n");
                            input.Write(passBytes, 0, passBytes.Length);
                            input.Flush();
                        }
                    }
                }
                catch { /* best-effort stdin write */ }
            }

            using var stdout = new StreamReader(cmd.OutputStream, Encoding.UTF8);
            using var stderr = new StreamReader(cmd.ExtendedOutputStream, Encoding.UTF8);

            void Drain()
            {
                string? line;
                while ((line = stdout.ReadLine()) != null) { if (!silent && !string.IsNullOrWhiteSpace(line)) log?.Invoke(line.TrimEnd()); }
                while ((line = stderr.ReadLine()) != null) { if (!silent && !string.IsNullOrWhiteSpace(line)) log?.Invoke(line.TrimEnd()); }
            }

            while (!async.IsCompleted)
            {
                Drain();
                Thread.Sleep(150);
            }
            cmd.EndExecute(async);
            Drain();
        }
    }
}
