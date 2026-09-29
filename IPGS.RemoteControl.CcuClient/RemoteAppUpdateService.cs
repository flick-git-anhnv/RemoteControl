using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuClient
{
    public class RemoteAppUpdateOptions
    {
        public string Host { get; set; } = string.Empty;
        public int SshPort { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>Thư mục cài app trên máy đích. Cho phép tiền tố "~/" (= home của SSH user).</summary>
        public string AppDir { get; set; } = string.Empty;

        /// <summary>Tên service cần dừng/khởi động lại. Tuỳ chọn tiền tố "user:" hoặc "system:" (mặc định system).</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>File .tar.gz do <see cref="RemoteAppUpdateService.PackFolder"/> tạo ra (dùng cho Update).</summary>
        public string TarballPath { get; set; } = string.Empty;

        /// <summary>Tên bản backup dạng yyyyMMdd-HHmmss (dùng cho Restore). Rỗng = bản mới nhất.</summary>
        public string BackupName { get; set; } = string.Empty;

        /// <summary>Số bản backup giữ lại sau khi update thành công. 0 = không tự xoá.</summary>
        public int KeepBackups { get; set; } = 3;
    }

    public class RemoteAppScanResult
    {
        public List<string> AppDirs { get; } = new();
        /// <summary>Dạng "user:ten.service" / "system:ten.service".</summary>
        public List<string> Services { get; } = new();
    }

    /// <summary>
    /// Update ứng dụng bằng cách gửi thư mục DLL (nén tar.gz) lên máy đích, backup thư mục cài,
    /// ghi đè, khởi động lại service và tự rollback nếu lỗi. Có thêm khôi phục bản backup cũ.
    /// </summary>
    public class RemoteAppUpdateService
    {
        private static readonly Regex AppDirRegex = new(@"^(~|/)[A-Za-z0-9_./+-]*$", RegexOptions.Compiled);
        private static readonly Regex ServiceRegex = new(@"^[A-Za-z0-9][A-Za-z0-9._@-]*$", RegexOptions.Compiled);
        private static readonly Regex BackupNameRegex = new(@"^[0-9]{8}-[0-9]{6}$", RegexOptions.Compiled);

        #region Pack

        /// <summary>Nén toàn bộ thư mục thành tar.gz tạm (giữ quyền thực thi hợp lý cho file Linux).</summary>
        public static string PackFolder(string folder, Action<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                throw new DirectoryNotFoundException($"Không tìm thấy thư mục update: {folder}");

            var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
                throw new InvalidOperationException("Thư mục update rỗng — không có file nào để gửi.");

            string tarball = Path.Combine(Path.GetTempPath(), $"ipgs-app-update-{Guid.NewGuid():N}.tar.gz");
            using (var fs = new FileStream(tarball, FileMode.Create, FileAccess.Write))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            using (var tar = new TarWriter(gz, TarEntryFormat.Ustar, leaveOpen: false))
            {
                foreach (var file in files)
                {
                    string rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
                    var entry = new UstarTarEntry(TarEntryType.RegularFile, rel)
                    {
                        Mode = IsExecutableName(rel)
                            ? (UnixFileMode)Convert.ToInt32("755", 8)
                            : (UnixFileMode)Convert.ToInt32("644", 8),
                        ModificationTime = File.GetLastWriteTimeUtc(file),
                        DataStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)
                    };
                    using (entry.DataStream) tar.WriteEntry(entry);
                }
            }
            log?.Invoke($"📦 Đã đóng gói {files.Length} file → {new FileInfo(tarball).Length / 1024 / 1024} MB");
            return tarball;
        }

        private static bool IsExecutableName(string rel)
        {
            string name = Path.GetFileName(rel);
            return !name.Contains('.') || name.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)
                   || name.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
                   || name.Contains(".so.", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Public operations

        public Task<RemoteAppScanResult> ScanAsync(RemoteAppUpdateOptions o, CancellationToken ct = default)
            => Task.Run(() =>
            {
                using var ssh = Connect(o);
                var res = new RemoteAppScanResult();
                const string cmd =
                    "for d in /opt/kztek/*/; do case \"$d\" in *.bak-*) continue;; esac; [ -d \"$d\" ] && echo \"APP:${d%/}\"; done; " +
                    "[ -d \"$HOME/ipgs/remote-agent\" ] && echo \"APP:~/ipgs/remote-agent\"; " +
                    "systemctl list-unit-files --type=service --no-legend --plain 2>/dev/null | awk '{print $1}' | grep -iE 'kztek|ipgs' | sed 's/^/SVC:system:/'; " +
                    "XDG_RUNTIME_DIR=/run/user/$(id -u) systemctl --user list-unit-files --type=service --no-legend --plain 2>/dev/null | awk '{print $1}' | grep -iE 'kztek|ipgs' | sed 's/^/SVC:user:/'; true";
                using var c = ssh.CreateCommand(cmd, Encoding.UTF8);
                c.Execute();
                foreach (var line in (c.Result ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    string l = line.Trim();
                    if (l.StartsWith("APP:")) res.AppDirs.Add(l[4..]);
                    else if (l.StartsWith("SVC:")) res.Services.Add(l[4..]);
                }
                ssh.Disconnect();
                return res;
            }, ct);

        public Task<List<string>> ListBackupsAsync(RemoteAppUpdateOptions o, CancellationToken ct = default)
            => Task.Run(() =>
            {
                using var ssh = Connect(o);
                var env = GetEnv(ssh);
                string app = ResolveAppDir(o.AppDir, env.Home);
                using var c = ssh.CreateCommand($"ls -1d {ShellQuote.Quote(app)}.bak-* 2>/dev/null | sort -r; true", Encoding.UTF8);
                c.Execute();
                var list = new List<string>();
                foreach (var line in (c.Result ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    string name = line.Trim();
                    int i = name.LastIndexOf(".bak-", StringComparison.Ordinal);
                    if (i >= 0 && BackupNameRegex.IsMatch(name[(i + 5)..])) list.Add(name[(i + 5)..]);
                }
                ssh.Disconnect();
                return list;
            }, ct);

        public Task<string> UpdateAsync(RemoteAppUpdateOptions o, Action<string>? onLog = null, CancellationToken ct = default)
            => Task.Run(() =>
            {
                if (string.IsNullOrEmpty(o.TarballPath) || !File.Exists(o.TarballPath))
                    throw new FileNotFoundException("Chưa đóng gói thư mục update (thiếu file tar.gz).");
                return RunScriptOperation("update", o, onLog);
            }, ct);

        public Task<string> RestoreAsync(RemoteAppUpdateOptions o, Action<string>? onLog = null, CancellationToken ct = default)
            => Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(o.BackupName) && !BackupNameRegex.IsMatch(o.BackupName))
                    throw new ArgumentException($"Tên bản backup '{o.BackupName}' không hợp lệ.");
                return RunScriptOperation("restore", o, onLog);
            }, ct);

        public Task<string> RestartServiceAsync(RemoteAppUpdateOptions o, Action<string>? onLog = null, CancellationToken ct = default)
            => Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(o.ServiceName)) throw new ArgumentException("Chưa chọn service để restart.");
                return RunScriptOperation("restart", o, onLog);
            }, ct);

        #endregion

        #region Internals

        private static SshClient Connect(RemoteAppUpdateOptions o)
        {
            ShellQuote.ValidateUsername(o.Username, nameof(o.Username));
            var ssh = new SshClient(o.Host, o.SshPort > 0 ? o.SshPort : 22, o.Username, o.Password);
            ssh.Connect();
            if (!ssh.IsConnected) throw new Exception("Không thể mở kết nối SSH tới máy đích.");
            return ssh;
        }

        private static (string Home, string Uid) GetEnv(SshClient ssh)
        {
            using var c = ssh.CreateCommand("echo \"$HOME\"; id -u", Encoding.UTF8);
            c.Execute();
            var parts = (c.Result ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) throw new Exception("Không đọc được HOME/UID của SSH user trên máy đích.");
            return (parts[0].Trim().TrimEnd('/'), parts[1].Trim());
        }

        /// <summary>Validate + mở rộng "~" thành home; chặn thư mục hệ thống/gốc để tránh rm -rf nhầm.</summary>
        internal static string ResolveAppDir(string appDir, string home)
        {
            appDir = (appDir ?? "").Trim().TrimEnd('/');
            if (appDir.Length == 0 || !AppDirRegex.IsMatch(appDir) || appDir.Contains("//") ||
                appDir.Split('/').Contains(".."))
                throw new ArgumentException($"Thư mục ứng dụng '{appDir}' không hợp lệ (chỉ chữ, số, . _ + - /, không có '..').");

            string full = appDir.StartsWith("~") ? home + appDir[1..] : appDir;
            var comps = full.Split('/', StringSplitOptions.RemoveEmptyEntries);
            bool ok = full.StartsWith("/opt/") ? comps.Length >= 2 : comps.Length >= 3;
            if (!ok || full == home)
                throw new ArgumentException($"Thư mục '{full}' quá gần thư mục gốc/hệ thống — hãy chọn thư mục riêng của ứng dụng (vd /opt/kztek/<app>).");
            if (full.StartsWith("/usr/") || full.StartsWith("/bin/") || full.StartsWith("/etc/") || full.StartsWith("/var/")
                || full.StartsWith("/lib") || full.StartsWith("/boot/") || full.StartsWith("/sbin/"))
                throw new ArgumentException($"Không cho phép update thư mục hệ thống '{full}'.");
            return full;
        }

        private static (string Scope, string Name) ParseService(string service)
        {
            service = (service ?? "").Trim();
            if (service.Length == 0) return ("", "");
            string scope = "system";
            if (service.StartsWith("user:", StringComparison.OrdinalIgnoreCase)) { scope = "user"; service = service[5..]; }
            else if (service.StartsWith("system:", StringComparison.OrdinalIgnoreCase)) service = service[7..];
            if (!ServiceRegex.IsMatch(service))
                throw new ArgumentException($"Tên service '{service}' không hợp lệ.");
            return (scope, service);
        }

        private string RunScriptOperation(string mode, RemoteAppUpdateOptions o, Action<string>? onLog)
        {
            var sb = new StringBuilder();
            void Log(string m) { lock (sb) sb.AppendLine(m); onLog?.Invoke(m); }

            var (scope, svc) = ParseService(o.ServiceName);
            using var ssh = Connect(o);
            var (home, uid) = GetEnv(ssh);
            string app = ResolveAppDir(o.AppDir, home);
            bool isRoot = uid == "0";
            if (!isRoot && string.IsNullOrEmpty(o.Password))
                throw new InvalidOperationException("Máy này chưa có password SSH/sudo trong cấu hình — bổ sung rồi chạy lại.");

            string id = Guid.NewGuid().ToString("N")[..8];
            string remoteScript = $"/tmp/ipgs-app-{id}.sh";
            string remoteTar = string.Empty;
            string tarArg = string.Empty;

            using (var sftp = new SftpClient(o.Host, o.SshPort > 0 ? o.SshPort : 22, o.Username, o.Password))
            {
                sftp.Connect();
                using (var ms = new MemoryStream(new UTF8Encoding(false).GetBytes(Script.Replace("\r\n", "\n"))))
                    sftp.UploadFile(ms, remoteScript, true);

                if (mode == "update")
                {
                    remoteTar = $"/tmp/ipgs-app-{id}.tar.gz";
                    using var fs = new FileStream(o.TarballPath, FileMode.Open, FileAccess.Read);
                    long total = fs.Length; int lastPct = -10;
                    Log($"📤 Tải bản update lên máy đích ({total / 1024 / 1024} MB)...");
                    sftp.UploadFile(fs, remoteTar, true, sent =>
                    {
                        int pct = total == 0 ? 100 : (int)(sent * 100 / (ulong)total);
                        if (pct >= lastPct + 25) { lastPct = pct; Log($"   … {pct}%"); }
                    });
                    tarArg = remoteTar;
                }
                else if (mode == "restore") tarArg = o.BackupName;
                sftp.Disconnect();
            }

            string args = string.Join(' ', new[] { mode, app, scope, svc, o.KeepBackups.ToString(), o.Username, uid, tarArg }
                .Select(ShellQuote.Quote));
            string run = $"bash {remoteScript} {args}";
            Log($"🔄 Chạy {mode} trên {o.Host}: {app}" + (svc.Length > 0 ? $" (service {scope}:{svc})" : ""));

            int exit = ExecuteWithSudo(ssh, run, isRoot, o.Password, Log);
            try { using var rm = ssh.CreateCommand($"rm -f {remoteScript} {remoteTar}"); rm.Execute(); } catch { /* dọn tạm, bỏ qua */ }
            ssh.Disconnect();

            if (exit != 0)
                throw new InvalidOperationException($"{ExitMessage(exit)}\n{sb}".TrimEnd());
            return sb.ToString();
        }

        private static string ExitMessage(int code) => code switch
        {
            10 => "Không thấy thư mục ứng dụng trên máy đích.",
            11 => "Giải nén bản update thất bại.",
            12 => "Backup thất bại (đủ dung lượng ổ đĩa không?) — chưa thay đổi gì.",
            13 => "Ghi đè file thất bại — ĐÃ TỰ ROLLBACK về bản cũ.",
            14 => "Service không chạy sau update — ĐÃ TỰ ROLLBACK về bản cũ.",
            20 => "Không tìm thấy bản backup.",
            21 => "Service không chạy sau khôi phục — ĐÃ QUAY LẠI phiên bản trước khi khôi phục.",
            30 => "Service không ở trạng thái active sau restart.",
            _ => $"Lệnh trên máy đích lỗi (exit {code})."
        };

        private static int ExecuteWithSudo(SshClient ssh, string command, bool isRoot, string password, Action<string> log)
        {
            string full = isRoot ? command : $"sudo -S -p '' {command}";
            using var cmd = ssh.CreateCommand(full, Encoding.UTF8);
            var exec = cmd.ExecuteAsync();
            if (!isRoot)
            {
                // Password sudo đi qua stdin, không lộ trong command line/ps.
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        using var input = cmd.CreateInputStream();
                        var bytes = Encoding.UTF8.GetBytes(password + "\n");
                        input.Write(bytes, 0, bytes.Length);
                        input.Flush();
                        break;
                    }
                    catch (InvalidOperationException) when (attempt < 40) { Thread.Sleep(25); }
                }
            }
            exec.GetAwaiter().GetResult();

            string outText = (cmd.Result ?? "").TrimEnd();
            string errText = (cmd.Error ?? "").TrimEnd();
            if (outText.Length > 0) log(outText);
            if (errText.Length > 0) log(errText);
            return cmd.ExitStatus ?? -1;
        }

        // Script chạy trên máy đích với quyền root (sudo). Tham số:
        // MODE APP SCOPE SVC KEEP OWNER OWNER_UID ARG
        private const string Script = """
#!/bin/bash
MODE=$1; APP=$2; SCOPE=$3; SVC=$4; KEEP=$5; OWNER=$6; OWNER_UID=$7; ARG=$8
TS=$(date +%Y%m%d-%H%M%S)
SELF="$0"; STAGE=""
cleanup() { [ -n "$STAGE" ] && rm -rf "$STAGE"; [ "$MODE" = update ] && rm -f "$ARG"; rm -f "$SELF"; }
trap cleanup EXIT

svc() {
  [ -z "$SVC" ] && return 0
  if [ "$SCOPE" = user ]; then
    if [ "$(id -u)" = "$OWNER_UID" ]; then
      XDG_RUNTIME_DIR=/run/user/$OWNER_UID systemctl --user "$@" "$SVC"
    else
      runuser -u "$OWNER" -- env XDG_RUNTIME_DIR=/run/user/$OWNER_UID systemctl --user "$@" "$SVC"
    fi
  else
    systemctl "$@" "$SVC"
  fi
}
svc_up() { [ -z "$SVC" ] && return 0; svc start; sleep 3; svc is-active --quiet; }

case "$MODE" in
update)
  [ -d "$APP" ] || { echo "❌ Không thấy thư mục $APP"; exit 10; }
  STAGE=$(mktemp -d /tmp/ipgs-upd.XXXXXX)
  tar -xzf "$ARG" -C "$STAGE" || exit 11
  BK="$APP.bak-$TS"
  [ -n "$SVC" ] && { echo "⏹ Dừng service $SVC"; svc stop; }
  echo "💾 Backup → $BK"
  cp -a "$APP" "$BK" || { rm -rf "$BK"; svc start; exit 12; }
  rollback() { echo "↩ Rollback về bản cũ"; rm -rf "$APP"; cp -a "$BK" "$APP"; svc start; }
  echo "✍ Ghi đè file mới"
  cp -rf "$STAGE"/. "$APP"/ || { rollback; exit 13; }
  chown -R --reference="$BK" "$APP"
  if [ -n "$SVC" ]; then
    echo "▶ Khởi động service $SVC"
    svc_up || { svc status --no-pager -n 15 2>&1 | tail -n 15; rollback; exit 14; }
  fi
  if [ "$KEEP" -gt 0 ] 2>/dev/null; then
    ls -1d "$APP".bak-* 2>/dev/null | sort -r | tail -n +$((KEEP+1)) | while read -r d; do echo "🗑 Xoá backup cũ $d"; rm -rf "$d"; done
  fi
  echo "✅ Update xong"
  ;;
restore)
  if [ -z "$ARG" ]; then SRC=$(ls -1d "$APP".bak-* 2>/dev/null | sort | tail -n1); else SRC="$APP.bak-$ARG"; fi
  [ -n "$SRC" ] && [ -d "$SRC" ] || { echo "❌ Không có bản backup"; exit 20; }
  PRE="$APP.bak-$TS"
  [ -n "$SVC" ] && { echo "⏹ Dừng service $SVC"; svc stop; }
  echo "💾 Lưu trạng thái hiện tại → $PRE"
  cp -a "$APP" "$PRE" || { svc start; exit 12; }
  echo "♻ Khôi phục từ $SRC"
  rm -rf "$APP"; cp -a "$SRC" "$APP" || { rm -rf "$APP"; cp -a "$PRE" "$APP"; svc start; exit 13; }
  if [ -n "$SVC" ]; then
    echo "▶ Khởi động service $SVC"
    svc_up || { rm -rf "$APP"; cp -a "$PRE" "$APP"; svc start; exit 21; }
  fi
  echo "✅ Khôi phục xong"
  ;;
restart)
  svc restart; sleep 2
  svc is-active --quiet && echo "✅ Service active" || { echo "❌ Service không active"; exit 30; }
  ;;
esac
""";

        #endregion
    }
}
