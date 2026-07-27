using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuClient
{
    /// <summary>Một ứng dụng có thể dùng làm kiosk app — kết quả quét từ máy ZCU.</summary>
    public record KioskAppEntry(
        string Name,           // Tên thân thiện (từ .desktop Name=) — có thể rỗng
        string ExecCommand,    // Lệnh thực thi (Exec= đã loại %placeholder)
        bool IsRecommended,    // true nếu tên/lệnh chứa "ipgs" hoặc "kiosk"
        bool ExistsOnSystem    // true nếu binary tìm thấy trên PATH hoặc đường dẫn tuyệt đối
    );

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

        /// <summary>
        /// F09: Khoá lối thoát kiosk bằng dconf system-wide + lock (/etc/dconf/db/local.d/).
        /// Vô hiệu phím Super (Activities overview), Alt+F2, Ctrl+Alt+T, Alt+Tab, Alt+F4,
        /// log out/user switching — user KHÔNG tự đổi lại được (key bị dconf lock).
        /// false = gỡ khoá (chế độ bảo trì cho quản trị viên).
        /// </summary>
        public bool LockdownShell { get; set; } = true;
        /// <summary>Tắt extension ubuntu-dock@ubuntu.com (thanh dock bên cạnh màn hình Ubuntu). Mặc định: true.</summary>
        public bool DisableUbuntuDock { get; set; } = true;

        /// <summary>
        /// Tắt extension ding@rastersoft.com (Desktop Icons NG — icon trên màn hình nền).
        /// false (mặc định) = GIỮ icon desktop — cần để click shortcut app mở được (F14).
        /// </summary>
        public bool DisableDesktopIcons { get; set; } = false;
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

        /// <summary>
        /// F10: Cài systemd USER service tự khởi động lại app kiosk khi app bị đóng/crash
        /// (Restart=always + StartLimit chống vòng lặp vô hạn khi binary chưa tồn tại).
        /// Khi bật, app do service quản lý — autostart .desktop của app bị bỏ để tránh
        /// chạy 2 instance. false = gỡ service (app quay về autostart .desktop nếu bật).
        /// </summary>
        public bool EnableWatchdog { get; set; } = true;

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

                // ── Bước 1: Upload scripts ────────────────────────────────────────
                Log("--- [1/2] Tải script lên máy kiosk qua SFTP ---");
                using (var sftp = new SftpClient(options.Host, options.SshPort, options.Username, options.Password))
                {
                    sftp.Connect();
                    if (options.RunInstallSoftware) UploadScript(sftp, script1, options.Username, Log);
                    if (options.RunConfigureSystem) UploadScript(sftp, script2, options.Username, Log);
                    sftp.Disconnect();
                }

                // ── Bước 2: Chạy scripts ─────────────────────────────────────────
                Log("--- [2/2] Chạy setup trên máy kiosk (có thể mất vài phút) ---");

                RunCommand(ssh, "chmod +x ~/1-install-software.sh ~/2-configure-system.sh 2>/dev/null; true", Log);

                // ── Truyền sudo password qua biến môi trường KIOSK_SUDO_PASS ──────
                //
                // VẤN ĐỀ GỐC: SSH.NET exec channel gọi execve() trực tiếp, KHÔNG qua shell.
                // Vì vậy dấu ';' và 'export' trong command string bị ignore hoàn toàn.
                // Ví dụ: "export KIOSK_SUDO_PASS='pw'; bash ~/script.sh" — exec channel
                // nhận chuỗi này như 1 file/program tên "export KIOSK_SUDO_PASS=..." → fail.
                //
                // FIX: Dùng lệnh `env VAR=val bash ~/script.sh` vì:
                //   - `env` là binary (/usr/bin/env), exec trực tiếp được, không cần shell.
                //   - `env` thiết lập biến môi trường RỒI exec bash — bash con kế thừa biến.
                //   - Script bash con chạy `bash ~/script.sh` cũng kế thừa từ bash cha.
                //   - Hàm _sudo() trong script dùng: echo "$KIOSK_SUDO_PASS" | sudo -S cmd
                //     → sudo đọc password từ stdin, không cần TTY.
                //
                // Escape password để an toàn trong env VAR='...' syntax — dùng helper
                // ShellQuote chung (Q12): single-quote bao quanh, ' bên trong → '\''.
                //
                // env command syntax: env VAR1=val1 VAR2=val2 command [args...]
                // Không cần shell để parse — env exec trực tiếp.
                string envCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus KIOSK_SUDO_PASS={ShellQuote.Quote(options.SudoPassword)}";

                if (options.RunInstallSoftware)
                {
                    Log("🔄 Đang chạy 1-install-software.sh (Config máy tính — phần extension/unclutter/bàn phím ảo)...");
                    string args1 = $"{B(options.HideTopBar)} {B(options.HideActivities)} {B(options.HideWorkspaceSwitcher)} {B(options.HideDash)} {B(options.InstallUnclutter)} {B(options.HideVirtualKeyboard)}";
                    RunCommand(ssh, $"{envCmd} bash ~/1-install-software.sh {args1}", Log,
                        throwOnError: true, errorContext: "1-install-software.sh");
                }

                if (options.RunConfigureSystem)
                {
                    Log("🔄 Đang chạy 2-configure-system.sh (Config máy tính — phần hệ thống + Config phần mềm — update/autostart)...");
                    string kioskUser = string.IsNullOrEmpty(options.KioskUser) ? options.Username : options.KioskUser;
                    // Thứ tự phải khớp tham số trong 2-configure-system.sh ($3..$13):
                    // $3=disable_hotcorner $4=disable_ubuntu_dock $5=disable_desktop_icons
                    // $6=block_sleep $7=skip_initial_setup $8=enable_autologin
                    // $9=disable_sw_update $10=enable_autostart $11=lock_single_workspace
                    // $12=lockdown_shell $13=enable_watchdog
                    string args2 = $"{B(options.DisableHotCorner)} {B(options.DisableUbuntuDock)} {B(options.DisableDesktopIcons)} {B(options.BlockSleep)} {B(options.SkipInitialSetup)} {B(options.EnableAutologin)} {B(options.DisableSoftwareUpdate)} {B(options.EnableAutostart)} {B(options.LockSingleWorkspace)} {B(options.LockdownShell)} {B(options.EnableWatchdog)}";
                    // S1: quote đúng chuẩn POSIX — bản cũ '{kioskUser}' không escape '
                    // bên trong nên giá trị chứa ' có thể break-out khỏi quote.
                    // F08: throwOnError — trước đây exit code của script bị bỏ qua hoàn toàn,
                    // script fail (VD autologin không ghi được do sudo sai mật khẩu) vẫn
                    // hiện "HOÀN THÀNH" → user tưởng autologin đã cài mà thực tế chưa.
                    RunCommand(ssh, $"{envCmd} bash ~/2-configure-system.sh {ShellQuote.Quote(kioskUser)} {ShellQuote.Quote(options.AppExec)} {args2}", Log,
                        throwOnError: true, errorContext: "2-configure-system.sh (autologin/cấu hình hệ thống)");
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

            // Đọc file — Encoding.UTF8 tự strip BOM khi đọc nếu có.
            string content = File.ReadAllText(localPath, Encoding.UTF8);

            // Strip BOM thủ công phòng trường hợp ReadAllText không xử lý
            // (PowerShell 5 viết UTF-8 with BOM; BOM trước #!/bin/bash khiến bash lỗi
            // "No such file or directory" trên line 1).
            if (content.Length > 0 && content[0] == '\uFEFF')
                content = content.Substring(1);

            bool hadCrlf = content.Contains("\r\n");
            // Normalize CRLF→LF và CR đơn lẻ (tránh "$'\r': command not found")
            content = content.Replace("\r\n", "\n").Replace("\r", "\n");

            // Ghi không BOM (UTF8Encoding(false)) — bắt buộc cho shell script trên Linux.
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            using var ms = new MemoryStream(bytes);
            sftp.UploadFile(ms, remotePath, true);
            string note = hadCrlf ? " (CRLF→LF)" : "";
            log($"📤 Đã tải {fileName} lên {remotePath}{note}");
        }

        private static void RunCommand(SshClient ssh, string commandText, Action<string> log,
            bool silent = false, bool throwOnError = false, string? errorContext = null)
        {
            // Dùng UTF-8 để decode output — mặc định SSH.NET là ASCII gây garbled text
            // với ký tự tiếng Việt (UTF-8 bytes bị decode thành Latin-1 → "CÃ i" thay vì "Cài").
            using var cmd = ssh.CreateCommand(commandText, Encoding.UTF8);
            cmd.Execute();

            string result = (cmd.Result ?? string.Empty).TrimEnd();
            string error = (cmd.Error ?? string.Empty).TrimEnd();

            if (!silent)
            {
                if (!string.IsNullOrEmpty(result)) log(result);
                if (!string.IsNullOrEmpty(error)) log(error);
            }

            // F08: KHÔNG nuốt exit code — script setup fail phải nổi lên thành lỗi deploy
            // thật, không được để flow tiếp tục tới "HOÀN THÀNH" (báo thành công giả).
            int exitStatus = cmd.ExitStatus ?? 0;
            if (throwOnError && exitStatus != 0)
            {
                string detail = !string.IsNullOrEmpty(error) ? error : result;
                // Chỉ lấy vài dòng cuối cho message — log đầy đủ đã in ở trên.
                var lines = detail.Split('\n');
                if (lines.Length > 5) detail = string.Join("\n", lines[^5..]);
                throw new Exception(
                    $"{errorContext ?? "Lệnh trên máy kiosk"} thất bại (exit {exitStatus}). {detail}".TrimEnd());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // LoadKioskAppsAsync — quét danh sách ứng dụng có thể dùng làm kiosk app
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Script Python chạy trên ZCU để quét ứng dụng. Output: 4 cột tab-separated.
        /// Cột: PRIORITY(1/0) | NAME | EXEC | EXISTS(1/0)
        /// Lọc: bỏ NoDisplay=true, Hidden=true; chuẩn hoá Exec (loại %placeholder).
        /// Kiểm tra binary: đường dẫn tuyệt đối → test -x; tên lệnh → tìm trong PATH.
        /// Ưu tiên 1 = tên/lệnh chứa "ipgs" hoặc "kiosk".
        /// </summary>
        private static readonly string _scanScriptContent =
@"import os, re, glob
r = []
def scan_desktop(path):
    try:
        c = open(path, encoding='utf-8', errors='ignore').read()
        if re.search(r'^NoDisplay\s*=\s*true', c, re.M | re.I): return
        if re.search(r'^Hidden\s*=\s*true', c, re.M | re.I): return
        nm = re.search(r'^Name\s*=(.*)', c, re.M)
        ex = re.search(r'^Exec\s*=(.*)', c, re.M)
        if nm and ex:
            name = nm.group(1).strip()
            exec_cmd = re.sub(r'\s*%[uUfFiIcCkK]\s*', '', ex.group(1)).strip()
            if exec_cmd: r.append((name, exec_cmd))
    except: pass
for p in (glob.glob('/usr/share/applications/*.desktop') +
          glob.glob(os.path.expanduser('~/.local/share/applications/*.desktop'))):
    if os.path.isfile(p): scan_desktop(p)
for pattern in ['/opt/kztek/*/run.sh', '/opt/kztek/*/bin/*', '/usr/bin/ipgs*', '/usr/local/bin/ipgs*']:
    for f in glob.glob(pattern):
        try:
            if os.path.isfile(f) and (os.access(f, os.X_OK) or f.endswith('.sh')): r.append(('', f))
        except: pass
seen = set()
for name, exec_cmd in r:
    if exec_cmd in seen: continue
    seen.add(exec_cmd)
    prio = '1' if any(k in exec_cmd.lower() or k in name.lower() for k in ['ipgs', 'kiosk']) else '0'
    bin_part = exec_cmd.split()[0] if exec_cmd else ''
    if os.path.isabs(bin_part):
        exists = '1' if os.access(bin_part, os.X_OK) else '0'
    else:
        exists = '1' if any(os.access(os.path.join(d, bin_part), os.X_OK) for d in os.environ.get('PATH', '/usr/bin:/usr/local/bin').split(':') if d) else '0'
    print(prio + '\t' + name + '\t' + exec_cmd + '\t' + exists)
";

        /// <summary>
        /// Kết nối SSH tới máy ZCU, quét danh sách ứng dụng có thể dùng làm kiosk app:
        /// .desktop trong /usr/share/applications + ~/.local/share/applications (lọc NoDisplay/Hidden),
        /// binary kztek tại /opt/kztek/, /usr/bin/ipgs*, /usr/local/bin/ipgs*.
        /// Kết quả đã sắp xếp: recommended (ipgs/kiosk) trước, còn lại theo thứ tự abc.
        /// ExistsOnSystem=true nếu lệnh tìm thấy trên PATH hoặc đường dẫn tuyệt đối tồn tại.
        /// </summary>
        public Task<List<KioskAppEntry>> LoadKioskAppsAsync(
            string host, int port, string username, string password,
            CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var results = new List<KioskAppEntry>();
                // Tên ngẫu nhiên để tránh xung đột khi nhiều deploy chạy song song
                string remotePath = $"/tmp/.kz_scan_{Environment.TickCount64 & 0xFFFF}.py";

                using var ssh = new SshClient(host, port, username, password);
                ssh.Connect();
                if (!ssh.IsConnected)
                    throw new Exception("Không thể kết nối SSH tới máy kiosk để quét ứng dụng.");

                // Upload script quét — normalize CRLF→LF, không BOM (giống UploadScript)
                using (var sftp = new SftpClient(host, port, username, password))
                {
                    sftp.Connect();
                    string normalized = _scanScriptContent
                        .Replace("\r\n", "\n").Replace("\r", "\n");
                    byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                        .GetBytes(normalized);
                    using var ms = new MemoryStream(bytes);
                    sftp.UploadFile(ms, remotePath, true);
                    sftp.Disconnect();
                }

                try
                {
                    using var cmd = ssh.CreateCommand($"python3 {remotePath}", Encoding.UTF8);
                    cmd.Execute();

                    string output = cmd.Result ?? string.Empty;
                    string error  = cmd.Error  ?? string.Empty;

                    if (cmd.ExitStatus != 0 && string.IsNullOrWhiteSpace(output))
                        throw new Exception(
                            $"Không thể chạy python3 trên máy ZCU. {error.Trim()}".TrimEnd());

                    // Parse: PRIORITY\tNAME\tEXEC\tEXISTS
                    foreach (var line in output.Split('\n',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var parts = line.Split('\t');
                        if (parts.Length < 4) continue;
                        bool recommended = parts[0] == "1";
                        string name      = parts[1];
                        string execCmd   = parts[2];
                        bool exists      = parts[3] == "1";
                        if (!string.IsNullOrWhiteSpace(execCmd))
                            results.Add(new KioskAppEntry(name, execCmd, recommended, exists));
                    }
                }
                finally
                {
                    // Dọn file tạm — không throw nếu lỗi
                    try
                    {
                        using var clean = ssh.CreateCommand(
                            $"rm -f {remotePath}", Encoding.UTF8);
                        clean.Execute();
                    }
                    catch { /* best-effort cleanup */ }
                }

                ssh.Disconnect();

                // Sắp xếp: recommended trước (abc), rồi còn lại (abc)
                results.Sort((a, b) =>
                {
                    if (a.IsRecommended != b.IsRecommended)
                        return a.IsRecommended ? -1 : 1;
                    string aKey = !string.IsNullOrEmpty(a.Name) ? a.Name : a.ExecCommand;
                    string bKey = !string.IsNullOrEmpty(b.Name) ? b.Name : b.ExecCommand;
                    return string.Compare(aKey, bKey, StringComparison.OrdinalIgnoreCase);
                });

                return results;
            }, ct);
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
