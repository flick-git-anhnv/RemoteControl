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
        /// <summary>Đường dẫn tuyệt đối tới file cài đặt ĐÃ CÓ SẴN trên máy đích. Nếu có → bỏ bước upload.</summary>
        public string RemoteInstallerPath { get; set; } = string.Empty;
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

        /// <summary>
        /// Kiểm tra tính hợp lệ của đường dẫn file cài đặt (.deb, .sh, .run cho Linux; .msi, .exe cho Windows).
        /// Nếu biết trước <paramref name="targetOs"/>, từ chối ngay khi người dùng chọn nhầm gói cài đặt khác hệ điều hành.
        /// </summary>
        public static string ValidateInstallerPackagePath(string? path, RemoteOsType? targetOs = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Đường dẫn file cài đặt không được để trống.", nameof(path));

            string trimmed = path.Trim();
            string fileName = Path.GetFileName(trimmed.Replace('\\', '/'));
            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (ext is not (".deb" or ".sh" or ".run" or ".msi" or ".exe"))
            {
                throw new ArgumentException(
                    $"Định dạng file '{fileName}' ({ext}) không được hỗ trợ. Chỉ hỗ trợ .deb, .sh, .run (Linux) và .msi, .exe (Windows).",
                    nameof(path));
            }

            if (targetOs == RemoteOsType.Windows && ext is not (".msi" or ".exe"))
            {
                throw new InvalidOperationException(
                    $"Máy đích đang chạy Windows — không thể cài đặt gói '{fileName}' ({ext}) của Linux. Vui lòng chọn bộ cài '.msi' hoặc '.exe'.");
            }

            if (targetOs == RemoteOsType.Linux && ext is (".msi" or ".exe"))
            {
                throw new InvalidOperationException(
                    $"Máy đích đang chạy Linux — không thể cài đặt gói '{fileName}' ({ext}) của Windows. Vui lòng chọn bộ cài '.deb', '.sh' hoặc '.run'.");
            }

            return trimmed;
        }

        /// <summary>Alias tương thích ngược cho <see cref="ValidateInstallerPackagePath"/>.</summary>
        public static string ValidateDebPackagePath(string? path, RemoteOsType? targetOs = null)
            => ValidateInstallerPackagePath(path, targetOs);

        /// <summary>
        /// Xây dựng câu lệnh cài đặt silent trên máy đích Windows cho gói <c>.msi</c> hoặc <c>.exe</c>.
        /// - Với <c>.msi</c>: chạy <c>msiexec.exe /i "&lt;winPath&gt;" /qn /norestart</c>
        /// - Với <c>.exe</c>: chạy <c>&amp; "&lt;winPath&gt;" /S</c>
        /// </summary>
        public static string BuildWindowsInstallCommand(string windowsLocalPath)
        {
            if (string.IsNullOrWhiteSpace(windowsLocalPath))
                throw new ArgumentException("Đường dẫn file cài đặt Windows không được để trống.", nameof(windowsLocalPath));

            string ext = Path.GetExtension(windowsLocalPath).ToLowerInvariant();
            string quotedPath = ShellQuote.QuotePowerShell(windowsLocalPath);

            if (ext == ".msi")
            {
                return $"powershell.exe -NoProfile -NonInteractive -Command \"& msiexec.exe /i {quotedPath} /qn /norestart | Out-Null; exit $LASTEXITCODE\"";
            }

            if (ext == ".exe")
            {
                return $"powershell.exe -NoProfile -NonInteractive -Command \"& {quotedPath} /S | Out-Null; exit $LASTEXITCODE\"";
            }

            throw new ArgumentException($"Định dạng '{ext}' không phải bộ cài Windows (.msi hoặc .exe).", nameof(windowsLocalPath));
        }

        public Task InstallDebPackageAsync(
            RemoteAppInstallOptions options,
            Action<string>? onLog = null,
            CancellationToken cancellationToken = default,
            Action<InstallProgress>? onProgress = null)
            => InstallAsync(options, onLog, cancellationToken, onProgress);

        public Task InstallAsync(RemoteAppInstallOptions options, Action<string>? onLog = null, CancellationToken cancellationToken = default, Action<InstallProgress>? onProgress = null)
        {
            return Task.Run(() =>
            {
                void Log(string msg) => onLog?.Invoke(msg);

                bool useRemoteFile = !string.IsNullOrEmpty(options.RemoteInstallerPath);
                if (!useRemoteFile && (string.IsNullOrEmpty(options.AppInstallerFile) || !File.Exists(options.AppInstallerFile)))
                {
                    throw new FileNotFoundException($"Không tìm thấy file cài đặt: {options.AppInstallerFile}");
                }

                string rawSourcePath = useRemoteFile ? options.RemoteInstallerPath : options.AppInstallerFile;
                ValidateInstallerPackagePath(rawSourcePath);

                Log($"=== Bắt đầu cài đặt ứng dụng tới {options.Username}@{options.Host} ===");

                using var ssh = new SshClient(options.Host, options.SshPort, options.Username, options.Password);
                ssh.Connect();
                if (!ssh.IsConnected)
                    throw new Exception("Không thể mở kết nối SSH tới máy đích.");

                RemoteOsType osType = RemoteOsDetector.DetectRemoteOs(ssh);
                ValidateInstallerPackagePath(rawSourcePath, osType);

                if (osType == RemoteOsType.Windows)
                {
                    InstallOnWindowsTarget(ssh, options, useRemoteFile, Log, onProgress);
                    ssh.Disconnect();
                    Log("=== HOÀN THÀNH CÀI ĐẶT ỨNG DỤNG (WINDOWS) ===");
                    return;
                }

                string? remoteFile = useRemoteFile
                    ? ShellQuote.ValidateRemotePath(options.RemoteInstallerPath, nameof(options.RemoteInstallerPath))
                    : null;

                // ── Bước 1: Upload App (bỏ qua nếu file đã nằm sẵn trên máy đích) ──
                if (useRemoteFile)
                {
                    Log($"--- [1/2] Dùng file có sẵn trên máy đích: {remoteFile} (bỏ qua upload) ---");
                }
                else
                {
                    Log("--- [1/2] Tải ứng dụng lên máy đích qua SFTP ---");
                    using var sftp = new SftpClient(options.Host, options.SshPort, options.Username, options.Password);
                    sftp.Connect();
                    UploadInstaller(sftp, options.AppInstallerFile, options.Username, Log, onProgress);
                    sftp.Disconnect();
                }

                // ── Bước 2: Cài đặt App ────────────────────────────────────────
                Log("--- [2/2] Chạy cài đặt trên máy đích ---");
                // dpkg/sh không báo % → thanh chạy không xác định (Percent < 0).
                onProgress?.Invoke(new InstallProgress(-1, "Đang cài đặt trên máy đích (không xác định thời gian)..."));

                string fileName = useRemoteFile
                    ? Path.GetFileName(remoteFile!)
                    : ShellQuote.ValidateFileName(Path.GetFileName(options.AppInstallerFile), nameof(options.AppInstallerFile));
                // File upload nằm ở $HOME; file có sẵn trên máy đích dùng đúng đường dẫn đã chọn.
                string target = useRemoteFile ? remoteFile! : $"$HOME/{fileName}";
                string ext = Path.GetExtension(fileName).ToLower();
                Log($"🔄 Đang chạy lệnh cài đặt cho: {fileName}...");

                if (ext == ".deb")
                {
                    int rc = RunSudoCommand(ssh, $"sudo -S -p '' dpkg -i \"{target}\"", options.SudoPassword, Log);
                    if (rc != 0)
                        throw new InvalidOperationException($"Cài đặt thất bại: dpkg -i trả mã lỗi {rc} (xem nhật ký phía trên).");
                }
                else if (ext == ".sh" || ext == ".run")
                {
                    int rc = RunSudoCommand(ssh, $"chmod +x \"{target}\" && sudo -S -p '' \"{target}\"", options.SudoPassword, Log);
                    if (rc != 0)
                        throw new InvalidOperationException($"Cài đặt thất bại: script trả mã lỗi {rc} (xem nhật ký phía trên).");
                }
                else
                {
                    Log($"⚠️ Không rõ cách cài đặt tự động cho định dạng {ext}. File nằm tại {target}");
                }

                ssh.Disconnect();
                Log("=== HOÀN THÀNH CÀI ĐẶT ỨNG DỤNG ===");
            }, cancellationToken);
        }

        private static void InstallOnWindowsTarget(
            SshClient ssh,
            RemoteAppInstallOptions options,
            bool useRemoteFile,
            Action<string> log,
            Action<InstallProgress>? onProgress)
        {
            string fileName;
            string winLocalPath;
            string? uploadedSftpPath = null;

            if (useRemoteFile)
            {
                fileName = Path.GetFileName(options.RemoteInstallerPath.Replace('\\', '/'));
                winLocalPath = ShellQuote.ToWindowsLocalPath(options.RemoteInstallerPath);
                log($"--- [1/2] Dùng bộ cài có sẵn trên máy Windows: {winLocalPath} (bỏ qua upload) ---");
            }
            else
            {
                fileName = ShellQuote.ValidateFileName(Path.GetFileName(options.AppInstallerFile), nameof(options.AppInstallerFile));
                log("--- [1/2] Tải bộ cài lên máy Windows qua SFTP ---");
                using var sftp = new SftpClient(options.Host, options.SshPort, options.Username, options.Password);
                sftp.Connect();
                string workDir = ShellQuote.NormalizeSftpPath(sftp.WorkingDirectory);
                if (!ShellQuote.IsWindowsSftpPath(workDir))
                {
                    workDir = "/C:/Users/Public";
                }

                uploadedSftpPath = ShellQuote.CombineSftpPath(workDir, fileName);
                winLocalPath = ShellQuote.ToWindowsLocalPath(uploadedSftpPath);
                UploadInstallerToPath(sftp, options.AppInstallerFile, uploadedSftpPath, log, onProgress);
                sftp.Disconnect();
            }

            log("--- [2/2] Chạy cài đặt silent trên máy Windows ---");
            onProgress?.Invoke(new InstallProgress(-1, "Đang cài đặt trên máy Windows (không xác định thời gian)..."));

            string installCmdText = BuildWindowsInstallCommand(winLocalPath);
            log($"🔄 Đang chạy lệnh cài đặt cho {fileName} ({winLocalPath})...");

            int exitCode;
            string stdOut;
            string stdErr;
            try
            {
                using var cmd = ssh.CreateCommand(installCmdText, Encoding.UTF8);
                cmd.CommandTimeout = TimeSpan.FromMinutes(15);
                cmd.Execute();
                exitCode = cmd.ExitStatus ?? -1;
                stdOut = (cmd.Result ?? string.Empty).Trim();
                stdErr = (cmd.Error ?? string.Empty).Trim();
            }
            finally
            {
                // Xóa file tạm đã tải lên sau khi cài xong
                if (!useRemoteFile && !string.IsNullOrEmpty(winLocalPath))
                {
                    try
                    {
                        using var cleanupCmd = ssh.CreateCommand(
                            $"powershell.exe -NoProfile -NonInteractive -Command \"Remove-Item -LiteralPath {ShellQuote.QuotePowerShell(winLocalPath)} -Force -ErrorAction SilentlyContinue\"");
                        cleanupCmd.Execute();
                    }
                    catch
                    {
                        // Bỏ qua lỗi xóa file tạm
                    }
                }
            }

            if (!string.IsNullOrEmpty(stdOut)) log(stdOut);
            if (!string.IsNullOrEmpty(stdErr)) log(stdErr);

            // Exit code 0 = thành công, 3010 = thành công (cần khởi động lại Windows sau)
            if (exitCode != 0 && exitCode != 3010)
            {
                throw new InvalidOperationException(
                    $"Cài đặt '{fileName}' trên Windows thất bại (mã thoát {exitCode}). {(string.IsNullOrEmpty(stdErr) ? "" : stdErr)}".Trim());
            }

            if (exitCode == 3010)
            {
                log($"✅ Cài đặt '{fileName}' thành công (mã 3010: Windows yêu cầu khởi động lại để hoàn tất).");
            }
            else
            {
                log($"✅ Đã cài đặt thành công '{fileName}' trên máy Windows.");
            }
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

                // Package chưa cài → báo lỗi rõ ràng, KHÔNG chạy bước dọn dẹp (rm -rf) bên dưới.
                int installedRc = RunSudoCommand(ssh,
                    $"dpkg-query -W -f='${{db:Status-Abbrev}}\\n' {packageName} 2>/dev/null | grep -qE '^(ii|rc|hi|iU|iF)'",
                    options.SudoPassword, null, silent: true);
                if (installedRc != 0)
                {
                    ssh.Disconnect();
                    throw new InvalidOperationException($"Package '{packageName}' chưa được cài trên máy đích — không có gì để gỡ.");
                }

                Log($"🔄 Đang chạy lệnh gỡ cài đặt (Purge) dpkg -P {packageName}...");
                int purgeRc = RunSudoCommand(ssh, $"sudo -S -p '' dpkg -P {packageName}", options.SudoPassword, Log);
                if (purgeRc != 0)
                {
                    ssh.Disconnect();
                    throw new InvalidOperationException($"Gỡ thất bại: dpkg -P trả mã lỗi {purgeRc} (xem nhật ký phía trên).");
                }

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

                // Guard: baseName rỗng hoặc bắt đầu bằng '.' sẽ làm rm -rf trúng cả /opt/kztek hoặc thư mục cha.
                if (!string.IsNullOrEmpty(baseName) && !baseName.StartsWith('.') && !packageName.StartsWith('.'))
                {
                    Log($"🧹 Đang dọn dẹp thư mục rác /opt/kztek/{baseName} (nếu có)...");
                    string rmFolderCmd = $"sudo -S -p '' rm -rf /opt/kztek/{baseName} /opt/kztek/{packageName} 2>/dev/null || true";
                    RunSudoCommand(ssh, rmFolderCmd, options.SudoPassword, null, silent: true);
                }

                RunSudoCommand(ssh, "update-desktop-database ~/.local/share/applications 2>/dev/null || true", options.SudoPassword, null, silent: true);
                RunSudoCommand(ssh, "sudo -S -p '' update-desktop-database /usr/share/applications 2>/dev/null || true", options.SudoPassword, null, silent: true);

                ssh.Disconnect();
                Log("=== HOÀN THÀNH GỠ CÀI ĐẶT ===");
            }, cancellationToken);
        }

        /// <summary>
        /// Khởi động app đã cài theo tên package: tìm file .desktop của package (dpkg -L) rồi gtk-launch;
        /// không có .desktop thì chạy file thực thi đầu tiên trong /usr/bin, /usr/games hoặc /opt của package.
        /// Chạy trong phiên desktop của user SSH (DISPLAY=:0), tách khỏi phiên SSH (setsid nohup).
        /// </summary>
        public Task LaunchAsync(RemoteAppInstallOptions options, Action<string>? onLog = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                void Log(string msg) => onLog?.Invoke(msg);

                string pkg = ShellQuote.ValidatePackageName(options.PackageName, nameof(options.PackageName));
                Log($"=== Khởi động '{pkg}' trên {options.Username}@{options.Host} ===");

                using var ssh = new SshClient(options.Host, options.SshPort, options.Username, options.Password);
                ssh.Connect();
                if (!ssh.IsConnected)
                    throw new Exception("Không thể mở kết nối SSH tới máy đích.");

                // pkg đã whitelist → nội suy an toàn. Mã thoát: 0 = đã gọi lệnh chạy, 3 = chưa cài, 4 = không tìm thấy gì để chạy.
                string script =
                    $"dpkg -L {pkg} >/dev/null 2>&1 || exit 3; " +
                    $"d=$(dpkg -L {pkg} | grep -m1 '/usr/share/applications/.*\\.desktop$'); " +
                    "if [ -n \"$d\" ]; then echo \"Dùng launcher: $d\"; " +
                    "export XAUTHORITY=\"$([ -f \"$HOME/.Xauthority\" ] && echo \"$HOME/.Xauthority\" || echo \"/run/user/$(id -u)/gdm/Xauthority\")\"; " +
                    "setsid nohup gtk-launch \"$(basename \"$d\" .desktop)\" >/dev/null 2>&1 </dev/null & exit 0; fi; " +
                    $"e=$(dpkg -L {pkg} | grep -E '^(/usr/bin|/usr/games|/opt)/' | while read f; do [ -f \"$f\" ] && [ -x \"$f\" ] && echo \"$f\" && break; done); " +
                    "if [ -n \"$e\" ]; then echo \"Chạy file: $e\"; " +
                    "export XAUTHORITY=\"$([ -f \"$HOME/.Xauthority\" ] && echo \"$HOME/.Xauthority\" || echo \"/run/user/$(id -u)/gdm/Xauthority\")\"; " +
                    "setsid nohup \"$e\" >/dev/null 2>&1 </dev/null & exit 0; fi; exit 4";

                int rc = RunSudoCommand(ssh, script, string.Empty, Log);
                ssh.Disconnect();
                if (rc == 3)
                    throw new InvalidOperationException($"Package '{pkg}' chưa được cài trên máy đích.");
                if (rc == 4)
                    throw new InvalidOperationException($"Package '{pkg}' không có file .desktop hoặc file thực thi để khởi động.");
                if (rc != 0)
                    throw new InvalidOperationException($"Khởi động thất bại (mã lỗi {rc}).");
                Log("=== ĐÃ GỬI LỆNH KHỞI ĐỘNG ===");
            }, cancellationToken);
        }

        private static void UploadInstaller(SftpClient sftp, string localPath, string username, Action<string> log, Action<InstallProgress>? onProgress)
        {
            string fileName = Path.GetFileName(localPath);
            string remotePath = $"/home/{username}/{fileName}";
            UploadInstallerToPath(sftp, localPath, remotePath, log, onProgress);
        }

        private static void UploadInstallerToPath(SftpClient sftp, string localPath, string remotePath, Action<string> log, Action<InstallProgress>? onProgress)
        {
            string fileName = Path.GetFileName(localPath);

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

        private static int RunSudoCommand(SshClient ssh, string commandText, string sudoPassword, Action<string>? log, bool silent = false)
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
            return cmd.ExitStatus ?? -1;
        }
    }
}
