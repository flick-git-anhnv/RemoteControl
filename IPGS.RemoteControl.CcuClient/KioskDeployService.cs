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

        /// <summary>
        /// F28: false (mặc định) = máy đích Ubuntu Desktop/GNOME Shell, dùng
        /// scripts/linux-kiosk/*.sh. true = máy đích Lubuntu (LXQt/Openbox/SDDM),
        /// dùng scripts/lubuntu-kiosk/*.sh — tham số script + cơ chế lockdown khác
        /// hẳn GNOME (không có gsettings/gnome-extensions), xem KioskDeployService.
        /// DeployAsync để biết cách map từng checkbox sang tham số của bản OS tương ứng.
        /// </summary>
        public bool IsLubuntu { get; set; } = false;

        public string KioskUser { get; set; } = string.Empty;
        /// <summary>
        /// Lệnh autostart app kiosk. KHÔNG có giá trị mặc định — phải nạp danh sách
        /// thật từ máy ZCU (LoadKioskAppsAsync) hoặc user nhập tay. Default hardcoded
        /// cũ ("ipgskioskavalonia") gây deploy autostart trỏ tới binary không tồn tại (F12).
        /// </summary>
        public string AppExec { get; set; } = string.Empty;

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
        /// F21: trước đây LUÔN true (hardcoded) — giờ settable để UI có thể tách 2 nút Deploy
        /// độc lập theo tab. Nút "Deploy Config máy tính" set true (chạy 1-install-software.sh);
        /// nút "Deploy Config phần mềm" set false (bỏ qua — script này không đụng tới
        /// autostart/watchdog nên không cần chạy lại, tránh mất thời gian tải/cài extension
        /// GNOME không liên quan khi chỉ muốn đổi app autostart).
        /// </summary>
        public bool RunInstallSoftware { get; set; } = true;

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

        /// <summary>
        /// F27: Bật tường lửa ufw (cài nếu chưa có, luôn allow OpenSSH trước khi enable
        /// để không tự khoá mất SSH). false = ufw disable (giữ nguyên rule để bật lại
        /// nhanh, không xoá rule đã cấu hình).
        /// </summary>
        public bool EnableFirewall { get; set; } = true;

        /// <summary>F21: settable — cả 2 nút Deploy (Config máy tính / Config phần mềm) đều
        /// cần script này (2-configure-system.sh xử lý chung machine + software settings),
        /// nên mặc định luôn true cho cả 2 nút.</summary>
        public bool RunConfigureSystem { get; set; } = true;

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

        /// <summary>
        /// F22: Reset màu màn hình ZCU — tắt Night Light, đưa gamma/brightness mọi output
        /// đang connected về mặc định (1.0:1.0:1.0 / 1.0). Dùng khi màn hình bị ám màu
        /// (VD xanh/tím) sau khi đổi session X11↔Wayland hoặc bật nhầm accessibility filter.
        /// Không đụng tới colord profile — verify thực tế (ZCU 192.168.0.102) cho thấy
        /// night-light/gamma là nguyên nhân phổ biến nhất, không phải colord.
        /// </summary>
        public Task<string> ResetDisplayColorAsync(string host, int port, string username, string password,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                using var ssh = new SshClient(host, port, username, password);
                ssh.Connect();
                if (!ssh.IsConnected)
                    throw new Exception("Không thể kết nối SSH tới máy để reset màu màn hình.");

                using var cmd = ssh.CreateCommand(
                    "gsettings set org.gnome.settings-daemon.plugins.color night-light-enabled false 2>&1; " +
                    "for out in $(DISPLAY=:0 xrandr --query 2>/dev/null | grep ' connected' | cut -d' ' -f1); do " +
                    "  DISPLAY=:0 xrandr --output \"$out\" --gamma 1.0:1.0:1.0 --brightness 1.0 2>&1; " +
                    "  echo \"reset gamma/brightness: $out\"; " +
                    "done",
                    Encoding.UTF8);
                cmd.Execute();
                ssh.Disconnect();

                string result = (cmd.Result ?? string.Empty).Trim();
                string error = (cmd.Error ?? string.Empty).Trim();
                return string.IsNullOrEmpty(result) ? error : result;
            }, cancellationToken);
        }

        public Task DeployAsync(KioskDeployOptions options, Action<string>? onLog = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                void Log(string msg) => onLog?.Invoke(msg);

                // F28: ScriptsSourceDir do caller truyền tay (VD test) LUÔN ưu tiên, bất kể
                // OS — chỉ tự resolve theo IsLubuntu khi không có override.
                string kioskSubDir = options.IsLubuntu ? "lubuntu-kiosk" : "linux-kiosk";
                string? scriptsDir = options.ScriptsSourceDir;
                if (string.IsNullOrEmpty(scriptsDir) || !Directory.Exists(scriptsDir))
                {
                    scriptsDir = ResolveKioskScriptsDir(kioskSubDir);
                }

                if (string.IsNullOrEmpty(scriptsDir))
                {
                    throw new DirectoryNotFoundException(
                        $"Không tìm thấy thư mục scripts/{kioskSubDir} (1-install-software.sh / 2-configure-system.sh).");
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

                    // F16: upload sẵn 2 zip extension GNOME Shell (Just Perfection,
                    // Block Caribou 36) đã nhúng trong CcuUI/Resources/gnome-extensions —
                    // để 1-install-software.sh cài OFFLINE, không cần curl extensions.gnome.org.
                    // Không tìm thấy resource (build CcuUI cũ) → bỏ qua, script tự fallback
                    // về tải mạng như trước.
                    // F28: gnome-extensions (Just Perfection/Block Caribou) CHỈ dùng trên
                    // GNOME Shell — Lubuntu/LXQt không có gnome-extensions, bỏ qua upload.
                    if (options.RunInstallSoftware && !options.IsLubuntu)
                    {
                        string? extDir = ResolveResourceDir("gnome-extensions");
                        if (!string.IsNullOrEmpty(extDir))
                        {
                            const string remoteExtDir = "gnome-ext-offline";
                            string remoteExtDirFull = $"/home/{options.Username}/{remoteExtDir}";
                            if (!sftp.Exists(remoteExtDirFull)) sftp.CreateDirectory(remoteExtDirFull);
                            foreach (var zipFile in Directory.GetFiles(extDir, "*.zip"))
                            {
                                using var fs = File.OpenRead(zipFile);
                                sftp.UploadFile(fs, $"{remoteExtDirFull}/{Path.GetFileName(zipFile)}", true);
                            }
                            Log($"📤 Đã tải {Directory.GetFiles(extDir, "*.zip").Length} zip extension GNOME Shell (offline) lên {remoteExtDirFull}");
                        }
                    }

                    // F17: gói .deb curl/unzip/unclutter (Ubuntu 22.04 amd64) nhúng sẵn — dùng
                    // được cho CẢ Ubuntu lẫn Lubuntu (cùng base Ubuntu, cùng kiến trúc amd64).
                    // 1-install-software.sh (cả 2 bản OS) cài bằng dpkg -i thay vì apt ra mạng.
                    if (options.RunInstallSoftware)
                    {
                        string? debDir = ResolveResourceDir("kiosk-deb");
                        if (!string.IsNullOrEmpty(debDir))
                        {
                            const string remoteDebDir = "kiosk-deb-offline";
                            string remoteDebDirFull = $"/home/{options.Username}/{remoteDebDir}";
                            if (!sftp.Exists(remoteDebDirFull)) sftp.CreateDirectory(remoteDebDirFull);
                            foreach (var debFile in Directory.GetFiles(debDir, "*.deb"))
                            {
                                using var fs = File.OpenRead(debFile);
                                sftp.UploadFile(fs, $"{remoteDebDirFull}/{Path.GetFileName(debFile)}", true);
                            }
                            Log($"📤 Đã tải {Directory.GetFiles(debDir, "*.deb").Length} gói .deb (curl/unzip/unclutter offline) lên {remoteDebDirFull}");
                        }
                    }

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
                // F16: GEXT_OFFLINE_DIR trỏ 1-install-software.sh tới thư mục zip extension
                // offline vừa upload ở trên (nếu có) — script tự bỏ qua nếu thư mục rỗng/không tồn tại.
                string envCmd = $"env DISPLAY=:0 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus KIOSK_SUDO_PASS={ShellQuote.Quote(options.SudoPassword)} GEXT_OFFLINE_DIR=$HOME/gnome-ext-offline KIOSK_DEB_OFFLINE_DIR=$HOME/kiosk-deb-offline";

                if (options.RunInstallSoftware)
                {
                    Log("🔄 Đang chạy 1-install-software.sh (Config máy tính — phần extension/unclutter/bàn phím ảo)...");
                    // F28: Lubuntu (LXQt) chỉ có 3 tham số — không có khái niệm Activities/
                    // Workspace/Dash/bàn phím ảo GNOME. Ánh xạ HideTopBar→hide_panel (cùng
                    // ý nghĩa "ẩn thanh trên/panel"), DisableDesktopIcons→hide_desktop_icons
                    // (tái dùng checkbox có sẵn, không cần thêm UI riêng).
                    string args1 = options.IsLubuntu
                        ? $"{B(options.HideTopBar)} {B(options.DisableDesktopIcons)} {B(options.InstallUnclutter)}"
                        : $"{B(options.HideTopBar)} {B(options.HideActivities)} {B(options.HideWorkspaceSwitcher)} {B(options.HideDash)} {B(options.InstallUnclutter)} {B(options.HideVirtualKeyboard)}";
                    RunCommand(ssh, $"{envCmd} bash ~/1-install-software.sh {args1}", Log,
                        throwOnError: true, errorContext: "1-install-software.sh");
                }

                if (options.RunConfigureSystem)
                {
                    Log("🔄 Đang chạy 2-configure-system.sh (Config máy tính — phần hệ thống + Config phần mềm — update/autostart)...");
                    string kioskUser = string.IsNullOrEmpty(options.KioskUser) ? options.Username : options.KioskUser;

                    // Không còn default hardcoded: AppExec rỗng → 2-configure-system.sh sẽ
                    // rơi về "${2:-ipgskioskavalonia}" và đăng ký autostart trỏ tới binary
                    // có thể không tồn tại (F12). Chặn ngay tại đây thay vì deploy sai.
                    //
                    // F24: chỉ chặn khi THẬT SỰ cần AppExec — tức Autostart hoặc Watchdog
                    // đang bật. Trước đây chặn VÔ ĐIỀU KIỆN bất cứ khi nào RunConfigureSystem
                    // = true, kể cả khi deploy chỉ để áp dụng cấu hình máy (hot corner/dock/
                    // autologin/...) và cả Autostart lẫn Watchdog đều đã tắt — verify thật:
                    // nút "Deploy Config máy tính" (F21, không bắt buộc App exec ở tầng UI)
                    // vẫn bị chặn ở đây vì check này độc lập, không biết Autostart/Watchdog
                    // có bật hay không.
                    if (string.IsNullOrWhiteSpace(options.AppExec) && (options.EnableAutostart || options.EnableWatchdog))
                        throw new Exception(
                            "Chưa chọn lệnh autostart app (App exec) — cần thiết vì Autostart hoặc Watchdog đang bật. " +
                            "Bấm '🔄 Nạp DS' để nạp danh sách ứng dụng thật từ máy ZCU rồi chọn, hoặc nhập tay lệnh.");

                    // F28: 2 bản OS có SỐ THAM SỐ VÀ Ý NGHĨA KHÁC NHAU — không dùng chung
                    // args2 được. Lubuntu (scripts/lubuntu-kiosk/2-configure-system.sh) chỉ
                    // có 8 tham số kể từ $3 (không có hotcorner/ubuntu-dock/desktop-icons/
                    // initial-setup riêng — LXQt không có các khái niệm này):
                    //   $3=block_sleep $4=enable_autologin $5=disable_sw_update
                    //   $6=enable_autostart $7=lock_single_desktop $8=lockdown_shell
                    //   $9=enable_watchdog $10=enable_firewall
                    // Ubuntu/GNOME (scripts/linux-kiosk/2-configure-system.sh) có 12 tham số
                    // kể từ $3:
                    //   $3=disable_hotcorner $4=disable_ubuntu_dock $5=disable_desktop_icons
                    //   $6=block_sleep $7=skip_initial_setup $8=enable_autologin
                    //   $9=disable_sw_update $10=enable_autostart $11=lock_single_workspace
                    //   $12=lockdown_shell $13=enable_watchdog $14=enable_firewall (F27)
                    string args2 = options.IsLubuntu
                        ? $"{B(options.BlockSleep)} {B(options.EnableAutologin)} {B(options.DisableSoftwareUpdate)} {B(options.EnableAutostart)} {B(options.LockSingleWorkspace)} {B(options.LockdownShell)} {B(options.EnableWatchdog)} {B(options.EnableFirewall)}"
                        : $"{B(options.DisableHotCorner)} {B(options.DisableUbuntuDock)} {B(options.DisableDesktopIcons)} {B(options.BlockSleep)} {B(options.SkipInitialSetup)} {B(options.EnableAutologin)} {B(options.DisableSoftwareUpdate)} {B(options.EnableAutostart)} {B(options.LockSingleWorkspace)} {B(options.LockdownShell)} {B(options.EnableWatchdog)} {B(options.EnableFirewall)}";
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

        /// <summary>
        /// F19: stream stdout/stderr THEO THỜI GIAN THỰC thay vì đợi cmd.Execute() chạy
        /// xong toàn bộ script mới hiện log 1 lần — script kiosk (1-install-software.sh,
        /// 2-configure-system.sh) có thể chạy 1-3 phút, trước đây UI đứng yên suốt lúc đó
        /// khiến người dùng tưởng bị treo. Dùng BeginExecute + đọc OutputStream/
        /// ExtendedOutputStream song song trên 2 thread nền, gọi log() ngay khi có dòng mới.
        /// </summary>
        private static void RunCommand(SshClient ssh, string commandText, Action<string> log,
            bool silent = false, bool throwOnError = false, string? errorContext = null)
        {
            // Dùng UTF-8 để decode output — mặc định SSH.NET là ASCII gây garbled text
            // với ký tự tiếng Việt (UTF-8 bytes bị decode thành Latin-1 → "CÃ i" thay vì "Cài").
            using var cmd = ssh.CreateCommand(commandText, Encoding.UTF8);
            var asyncResult = cmd.BeginExecute();

            var allLines = new List<string>();
            var linesLock = new object();

            void PumpStream(Stream stream)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (!asyncResult.IsCompleted || !reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (line is null) break;
                    lock (linesLock) allLines.Add(line);
                    if (!silent) log(line);
                }
            }

            var stdoutTask = Task.Run(() => PumpStream(cmd.OutputStream));
            var stderrTask = Task.Run(() => PumpStream(cmd.ExtendedOutputStream));
            Task.WaitAll(stdoutTask, stderrTask);
            cmd.EndExecute(asyncResult);

            // F08: KHÔNG nuốt exit code — script setup fail phải nổi lên thành lỗi deploy
            // thật, không được để flow tiếp tục tới "HOÀN THÀNH" (báo thành công giả).
            int exitStatus = cmd.ExitStatus ?? 0;
            if (throwOnError && exitStatus != 0)
            {
                // Chỉ lấy vài dòng cuối cho message — log đầy đủ đã in real-time ở trên.
                string detail = allLines.Count > 5
                    ? string.Join("\n", allLines[^5..])
                    : string.Join("\n", allLines);
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

        /// <summary>
        /// F16: tìm thư mục resource offline (Resources/&lt;name&gt;) đã nhúng sẵn vào output
        /// publish CcuUI (xem CcuUI.csproj — Content Include="Resources\**"). Trả về null
        /// nếu build hiện tại chưa có resource này — caller tự fallback về đường mạng/tìm source.
        /// </summary>
        private string? ResolveResourceDir(string relativeName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(baseDir, "Resources", relativeName);
            return Directory.Exists(candidate) ? candidate : null;
        }

        /// <summary>
        /// F28: subDirName = "linux-kiosk" (Ubuntu/GNOME, mặc định) hoặc "lubuntu-kiosk"
        /// (Lubuntu/LXQt) — cùng cơ chế resolve (resource nhúng ưu tiên, fallback tìm
        /// trong source repo), chỉ khác tên thư mục con.
        /// </summary>
        private string? ResolveKioskScriptsDir(string subDirName = "linux-kiosk")
        {
            // F16: ưu tiên scripts đã nhúng sẵn trong output CcuUI — không cần source repo
            // đi kèm khi build ra máy khác.
            string? embedded = ResolveResourceDir(Path.Combine("scripts", subDirName));
            if (!string.IsNullOrEmpty(embedded) &&
                File.Exists(Path.Combine(embedded, "1-install-software.sh")) &&
                File.Exists(Path.Combine(embedded, "2-configure-system.sh")))
            {
                return embedded;
            }

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
                    string candidate = Path.Combine(dir, "scripts", subDirName);
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
