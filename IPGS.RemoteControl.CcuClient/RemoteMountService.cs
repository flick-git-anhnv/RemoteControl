using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuClient;

public enum MountProtocol
{
    /// <summary>SMB/CIFS — máy chủ là Windows share hoặc Ubuntu chạy Samba.</summary>
    Cifs,
    /// <summary>NFS — Ubuntu ↔ Ubuntu (máy chủ cần nfs-kernel-server, xem <see cref="RemoteMountService.SetupNfsServerAsync"/>).</summary>
    Nfs
}

public sealed class RemoteMountOptions
{
    public MountProtocol Protocol { get; set; } = MountProtocol.Cifs;

    /// <summary>IP/hostname máy chứa thư mục share.</summary>
    public string ServerHost { get; set; } = string.Empty;

    /// <summary>CIFS: tên share (VD "Images"). NFS: đường dẫn export (VD "/srv/share").</summary>
    public string SharePath { get; set; } = string.Empty;

    /// <summary>Thư mục gắn trên máy khách (VD "/mnt/Images").</summary>
    public string MountPoint { get; set; } = string.Empty;

    /// <summary>CIFS: user share; rỗng = "Guest" (truy cập ẩn danh).</summary>
    public string ShareUsername { get; set; } = string.Empty;

    public string SharePassword { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
}

/// <summary>
/// Cài đặt &amp; cấu hình mount thư mục share (CIFS/NFS) trên máy Ubuntu từ xa qua SSH:
/// cài gói cần thiết (ưu tiên .deb offline trong Resources/mount-deb, fallback apt-get),
/// ghi /etc/fstab (systemd automount, nofail) và kiểm tra mount thành công.
/// Mọi tham số nội suy vào script đều được validate whitelist hoặc <see cref="ShellQuote.Quote"/>.
/// </summary>
public sealed class RemoteMountService
{
    private const string FstabTagPrefix = "# ipgs-mount:";
    private const string ExportsTagPrefix = "# ipgs-export:";
    public const string DefaultNfsClients = "192.168.0.0/16,10.0.0.0/8,172.16.0.0/12";

    private static readonly Regex HostRegex = new(@"^[A-Za-z0-9][A-Za-z0-9.-]*$", RegexOptions.Compiled);
    private static readonly Regex CifsShareRegex = new(@"^[A-Za-z0-9][A-Za-z0-9._$-]*(/[A-Za-z0-9._-]+)*$", RegexOptions.Compiled);
    private static readonly Regex PathRegex = new(@"^/[A-Za-z0-9._/-]*$", RegexOptions.Compiled);
    private static readonly Regex ShareUserRegex = new(@"^[A-Za-z0-9._@\\-]*$", RegexOptions.Compiled);
    private static readonly Regex CidrListRegex = new(@"^[0-9A-Fa-f.:/,; ]+$", RegexOptions.Compiled);
    private static readonly string[] AllowedMountRoots = { "/mnt/", "/media/", "/srv/", "/home/", "/opt/", "/data/" };

    /// <summary>Validate + chuẩn hóa mount point. Chặn "/", thư mục hệ thống và ký tự lạ.</summary>
    public static string ValidateMountPoint(string mountPoint)
    {
        string mp = (mountPoint ?? "").Trim().TrimEnd('/');
        if (!PathRegex.IsMatch(mp) || mp.Contains("..") || !AllowedMountRoots.Any(r => mp.StartsWith(r, StringComparison.Ordinal)) || mp.Length < 6)
            throw new ArgumentException(
                $"Thư mục mount '{mountPoint}' không hợp lệ — phải bắt đầu bằng /mnt/, /media/, /srv/, /home/, /opt/ hoặc /data/, chỉ gồm chữ, số, '.', '_', '-', '/' (không dấu cách).");
        return mp;
    }

    public Task<string> MountAsync(SshInstallerOptions ssh, RemoteMountOptions m, Action<string>? log = null, CancellationToken ct = default)
        => Task.Run(() => RunScript(ssh, BuildMountScript(ssh, m), m.Protocol == MountProtocol.Cifs ? "cifs-utils" : "nfs-common", log), ct);

    public Task<string> UnmountAsync(SshInstallerOptions ssh, string mountPoint, Action<string>? log = null, CancellationToken ct = default)
        => Task.Run(() => RunScript(ssh, BuildUnmountScript(ValidateMountPoint(mountPoint)), null, log), ct);

    /// <summary>Chạy trên máy CHỦ Ubuntu: cài nfs-kernel-server và export thư mục cho các dải IP máy khách.</summary>
    public Task<string> SetupNfsServerAsync(SshInstallerOptions ssh, string exportPath, string allowedClients, Action<string>? log = null, CancellationToken ct = default)
        => Task.Run(() => RunScript(ssh, BuildNfsServerScript(ssh, exportPath, allowedClients), "nfs-kernel-server", log), ct);

    // ───────────────────────────── script builders ─────────────────────────────

    private static string BuildMountScript(SshInstallerOptions ssh, RemoteMountOptions m)
    {
        string mp = ValidateMountPoint(m.MountPoint);
        if (!HostRegex.IsMatch(m.ServerHost ?? "")) throw new ArgumentException($"Địa chỉ máy chủ '{m.ServerHost}' không hợp lệ.");
        if (!ShareUserRegex.IsMatch(m.ShareUsername ?? "")) throw new ArgumentException("Tên đăng nhập share chứa ký tự không hợp lệ.");

        string source, fstype, opts, prep;
        string common = "_netdev,nofail,x-systemd.automount,x-systemd.network-timeout=30" + (m.ReadOnly ? ",ro" : "");
        string safeName = Regex.Replace(mp, "[^A-Za-z0-9]", "_").Trim('_');

        if (m.Protocol == MountProtocol.Cifs)
        {
            if (!CifsShareRegex.IsMatch(m.SharePath ?? "")) throw new ArgumentException($"Tên share '{m.SharePath}' không hợp lệ (không dùng dấu cách).");
            string user = string.IsNullOrEmpty(m.ShareUsername) ? "Guest" : m.ShareUsername;
            string credFile = $"/etc/ipgs-cifs/{safeName}.cred";
            // Credentials nằm trong file 600 (root) — không để password trong /etc/fstab (world-readable).
            prep = $"mkdir -p /etc/ipgs-cifs && chmod 700 /etc/ipgs-cifs\n" +
                   $"printf 'username=%s\\npassword=%s\\n' {ShellQuote.Quote(user)} {ShellQuote.Quote(m.SharePassword ?? "")} > {credFile}\n" +
                   $"chmod 600 {credFile}\n";
            source = $"//{m.ServerHost}/{m.SharePath}".Replace("$", "\\$"); // share ẩn kiểu C$ không được bị shell expand
            fstype = "cifs";
            opts = $"credentials={credFile},uid=$UID_,gid=$GID_,file_mode=0777,dir_mode=0777,iocharset=utf8,{common}";
        }
        else
        {
            if (!PathRegex.IsMatch(m.SharePath ?? "") || (m.SharePath ?? "").Length < 2 || m.SharePath.Contains(".."))
                throw new ArgumentException($"Đường dẫn export NFS '{m.SharePath}' không hợp lệ (VD /srv/share).");
            prep = "";
            source = $"{m.ServerHost}:{m.SharePath}";
            fstype = "nfs";
            opts = $"{common},x-systemd.idle-timeout=600,x-systemd.mount-timeout=30";
        }

        string checkBin = m.Protocol == MountProtocol.Cifs ? "/sbin/mount.cifs" : "/sbin/mount.nfs";
        string pkg = m.Protocol == MountProtocol.Cifs ? "cifs-utils" : "nfs-common";
        string tag = FstabTagPrefix + mp;
        string uidUser = ShellQuote.ValidateUsername(ssh.Username, nameof(ssh.Username));

        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("set -e");
        sb.AppendLine("export DEBIAN_FRONTEND=noninteractive");
        sb.Append(InstallPackageBlock(checkBin, pkg));
        sb.AppendLine($"UID_=$(id -u {uidUser}); GID_=$(id -g {uidUser})");
        sb.AppendLine($"MP={ShellQuote.Quote(mp)}");
        sb.AppendLine("mkdir -p \"$MP\"");
        sb.Append(prep);
        sb.AppendLine("[ -f /etc/fstab.ipgs.bak ] || cp /etc/fstab /etc/fstab.ipgs.bak");
        sb.Append(RemoveTaggedBlock("/etc/fstab", tag));
        sb.AppendLine($"printf '%s\\n%s\\n' {ShellQuote.Quote(tag)} \"{source} $MP {fstype} {opts} 0 0\" >> /etc/fstab");
        sb.AppendLine("systemctl daemon-reload");
        sb.AppendLine("mount -a 2>/dev/null || true");
        // Chạm vào thư mục để kích hoạt automount rồi kiểm tra đã gắn thật chưa.
        sb.AppendLine("timeout 40 ls \"$MP\" >/dev/null 2>&1 || true");
        sb.AppendLine($"if findmnt -n -M \"$MP\" -t {fstype},nfs4 >/dev/null 2>&1; then");
        sb.AppendLine("  echo \"MOUNT_OK: $(findmnt -n -o SOURCE,FSTYPE -M \"$MP\" -t " + fstype + ",nfs4 | head -1)\"");
        sb.AppendLine("  df -h \"$MP\" | tail -1");
        sb.AppendLine("else");
        sb.AppendLine("  echo 'MOUNT_FAIL: share was not mounted. Last mount output:'");
        sb.AppendLine("  mount \"$MP\" 2>&1 | tail -3 || true");
        sb.AppendLine("  exit 3");
        sb.AppendLine("fi");
        return sb.ToString();
    }

    private static string BuildUnmountScript(string mp)
    {
        string safeName = Regex.Replace(mp, "[^A-Za-z0-9]", "_").Trim('_');
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine($"MP={ShellQuote.Quote(mp)}");
        sb.AppendLine("systemctl stop \"$(systemd-escape -p --suffix=automount \"$MP\")\" 2>/dev/null || true");
        sb.AppendLine("umount -l \"$MP\" 2>/dev/null || true");
        sb.Append(RemoveTaggedBlock("/etc/fstab", FstabTagPrefix + mp));
        sb.AppendLine($"rm -f /etc/ipgs-cifs/{safeName}.cred");
        sb.AppendLine("systemctl daemon-reload");
        sb.AppendLine("echo \"UNMOUNT_OK: removed mount and fstab entry for $MP\"");
        return sb.ToString();
    }

    private static string BuildNfsServerScript(SshInstallerOptions ssh, string exportPath, string allowedClients)
    {
        if (!PathRegex.IsMatch(exportPath ?? "") || exportPath!.Length < 6 || exportPath.Contains("..")
            || !AllowedMountRoots.Any(r => exportPath.StartsWith(r, StringComparison.Ordinal)))
            throw new ArgumentException($"Thư mục export '{exportPath}' không hợp lệ (phải nằm dưới /srv, /mnt, /media, /home, /opt hoặc /data).");
        if (string.IsNullOrWhiteSpace(allowedClients)) allowedClients = DefaultNfsClients;
        if (!CidrListRegex.IsMatch(allowedClients)) throw new ArgumentException("Danh sách IP/dải mạng máy khách không hợp lệ.");
        string uidUser = ShellQuote.ValidateUsername(ssh.Username, nameof(ssh.Username));

        // all_squash → mọi user máy khách ghi bằng quyền của user SSH trên máy chủ, tránh lệch UID.
        string entries = string.Join(' ', allowedClients.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => $"{c}(rw,sync,no_subtree_check,all_squash,anonuid=$UID_,anongid=$GID_)"));
        string tag = ExportsTagPrefix + exportPath;

        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("set -e");
        sb.AppendLine("export DEBIAN_FRONTEND=noninteractive");
        sb.Append(InstallPackageBlock("/usr/sbin/exportfs", "nfs-kernel-server"));
        sb.AppendLine($"UID_=$(id -u {uidUser}); GID_=$(id -g {uidUser})");
        sb.AppendLine($"EP={ShellQuote.Quote(exportPath)}");
        sb.AppendLine($"mkdir -p \"$EP\" && chown $UID_:$GID_ \"$EP\" && chmod 775 \"$EP\"");
        sb.AppendLine("touch /etc/exports");
        sb.Append(RemoveTaggedBlock("/etc/exports", tag));
        sb.AppendLine($"printf '%s\\n%s\\n' {ShellQuote.Quote(tag)} \"$EP {entries}\" >> /etc/exports");
        sb.AppendLine("systemctl enable --now nfs-server >/dev/null 2>&1 || true");
        sb.AppendLine("exportfs -ra");
        sb.AppendLine("ufw allow from any to any port 2049 proto tcp >/dev/null 2>&1 || true");
        sb.AppendLine("echo \"NFS_SERVER_OK: exported $EP\"; exportfs -v | head -5");
        return sb.ToString();
    }

    /// <summary>Cài gói nếu thiếu: thử .deb offline (Ubuntu 22.04) trước, không được thì apt-get.</summary>
    private static string InstallPackageBlock(string checkBinary, string pkg) =>
        $"if [ ! -x {checkBinary} ]; then\n" +
        $"  CODENAME=$(. /etc/os-release; echo \"$VERSION_CODENAME\")\n" +
        $"  if [ \"$CODENAME\" = jammy ] && ls /tmp/ipgs-offline/mount-deb/{pkg}/*.deb >/dev/null 2>&1; then\n" +
        $"    dpkg -i --skip-same-version /tmp/ipgs-offline/mount-deb/{pkg}/*.deb >/dev/null 2>&1 || true\n" +
        $"  fi\n" +
        $"  [ -x {checkBinary} ] || {{ apt-get update -qq; apt-get install -y -qq {pkg}; }}\n" +
        $"  [ -x {checkBinary} ] || {{ echo 'Cannot install {pkg}: no matching offline .deb and no network.'; exit 2; }}\n" +
        $"fi\n";

    /// <summary>Xóa cặp dòng "tag + entry" cũ khỏi file cấu hình để chạy lại nhiều lần không bị trùng.</summary>
    private static string RemoveTaggedBlock(string file, string tag) =>
        $"awk -v t={ShellQuote.Quote(tag)} '$0==t{{skip=1;next}} skip{{skip=0;next}} {{print}}' {file} > /tmp/ipgs-conf.new && cat /tmp/ipgs-conf.new > {file} && rm -f /tmp/ipgs-conf.new\n";

    // ───────────────────────────── execution ─────────────────────────────

    private static string RunScript(SshInstallerOptions o, string script, string? offlinePkg, Action<string>? log)
    {
        ShellQuote.ValidateUsername(o.Username, nameof(o.Username));
        using var ssh = new SshClient(o.Host, o.SshPort, o.Username, o.Password);
        ssh.Connect();
        string id = Guid.NewGuid().ToString("N")[..8];
        string remoteScript = $"/tmp/ipgs-mount-{id}.sh";

        using (var sftp = new SftpClient(o.Host, o.SshPort, o.Username, o.Password))
        {
            sftp.Connect();
            using (var ms = new MemoryStream(new UTF8Encoding(false).GetBytes(script.Replace("\r\n", "\n"))))
                sftp.UploadFile(ms, remoteScript, true);

            // Upload .deb offline (nhỏ); script chỉ dùng chúng khi máy đích thiếu công cụ.
            string? debDir = offlinePkg == null ? null : ResolveDebDir(offlinePkg);
            if (debDir != null)
            {
                string remoteDir = $"/tmp/ipgs-offline/mount-deb/{offlinePkg}";
                using var mk = ssh.CreateCommand($"mkdir -p {remoteDir}");
                mk.Execute();
                log?.Invoke($"📤 Tải gói {offlinePkg} (offline) lên máy đích...");
                foreach (var deb in Directory.GetFiles(debDir, "*.deb"))
                {
                    using var fs = File.OpenRead(deb);
                    sftp.UploadFile(fs, $"{remoteDir}/{Path.GetFileName(deb)}", true);
                }
            }
            sftp.Disconnect();
        }

        log?.Invoke("⚙️ Đang cấu hình trên máy đích...");
        // Password sudo đi qua stdin của lệnh echo trên máy đích (cùng mẫu với ZcuRemoteInstallerService).
        string run = string.IsNullOrEmpty(o.Password)
            ? $"sudo -n bash {remoteScript} 2>&1"
            : $"echo {ShellQuote.Quote(o.Password)} | sudo -S -p '' bash {remoteScript} 2>&1";
        using var cmd = ssh.CreateCommand(run, Encoding.UTF8);
        cmd.Execute();
        string output = (cmd.Result ?? "").Trim();

        using (var rm = ssh.CreateCommand($"rm -f {remoteScript}")) rm.Execute();
        ssh.Disconnect();

        if (cmd.ExitStatus != 0 || output.Contains("MOUNT_FAIL"))
            throw new InvalidOperationException(string.IsNullOrEmpty(output) ? $"Lệnh trên máy đích thất bại (mã {cmd.ExitStatus}). Kiểm tra quyền sudo." : output);

        log?.Invoke(output);
        return output;
    }

    private static string? ResolveDebDir(string pkg)
    {
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "mount-deb", pkg);
        return Directory.Exists(dir) && Directory.GetFiles(dir, "*.deb").Length > 0 ? dir : null;
    }
}
