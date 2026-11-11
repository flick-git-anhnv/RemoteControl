using System;
using System.Text.RegularExpressions;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// Helper dùng chung chống command injection cho MỌI tham số nội suy vào lệnh shell
/// chạy qua SSH (RemoteAppInstallService, ZcuRemoteInstallerService, KioskDeployService).
/// <para>
/// Hai cơ chế bổ trợ nhau:
/// <list type="bullet">
///   <item><see cref="Quote"/> — bọc single-quote POSIX chuẩn (escape <c>'</c> thành
///     <c>'\''</c>) cho giá trị tự do như password. CHỈ an toàn khi giá trị KHÔNG nằm
///     bên trong một chuỗi <c>bash -c '...'</c> đã được single-quote sẵn.</item>
///   <item>Các hàm <c>Validate*</c> — whitelist ký tự cho giá trị nội suy vào bên trong
///     chuỗi đã single-quote (tên file, package name, username) nơi Quote không lồng được.
///     Ném <see cref="ArgumentException"/> với thông báo rõ nếu chứa ký tự nguy hiểm.</item>
/// </list>
/// </para>
/// </summary>
public static class ShellQuote
{
    // Review 4.1: cho phép thêm '~' và '%' TRỪ ký tự đầu — tên file .deb chuẩn Debian
    // thường chứa '~' trong version (vd: pkg_1.0~rc1_amd64.deb) và '%' khi encode epoch
    // (vd: pkg_1%3a2.0_amd64.deb). Cả hai đều VÔ HẠI trong ngữ cảnh nội suy: giá trị nằm
    // trong "$HOME/..." (double-quote) bên trong bash -c '...' — '~' không tilde-expand
    // (không ở đầu word + trong quote), '%' không có nghĩa shell. Ký tự đầu vẫn giới hạn
    // alphanumeric nên không thể bắt đầu bằng '~' hay '-' (option injection).
    private static readonly Regex FileNameRegex        = new(@"^[A-Za-z0-9][A-Za-z0-9._+~%-]*$", RegexOptions.Compiled);
    private static readonly Regex PackageNameRegex     = new(@"^[A-Za-z0-9][A-Za-z0-9._+-]*$", RegexOptions.Compiled);
    // Đường dẫn tuyệt đối trên máy đích: chỉ cho phép ký tự an toàn, không cho '..', dấu cách, quote, $ ; ( ).
    private static readonly Regex RemotePathRegex      = new(@"^/[A-Za-z0-9._+~%/-]+$", RegexOptions.Compiled);
    private static readonly Regex UsernameRegex        = new(@"^[a-zA-Z_][a-zA-Z0-9._-]*$",    RegexOptions.Compiled);
    private static readonly Regex WindowsUsernameRegex = new(@"^[a-zA-Z0-9_][a-zA-Z0-9._\- @\\]*$", RegexOptions.Compiled);
    private static readonly Regex WindowsSftpDriveRegex = new(@"^/?([A-Za-z]):(?<rest>[/\\].*)?$", RegexOptions.Compiled);

    /// <summary>
    /// Bọc giá trị trong single-quote POSIX: <c>'</c> bên trong → <c>'\''</c>.
    /// Newline/CR bị loại bỏ (SSH command là chuỗi 1 dòng — newline phá cấu trúc lệnh).
    /// </summary>
    public static string Quote(string value)
    {
        value ??= string.Empty;
        value = value.Replace("\n", string.Empty).Replace("\r", string.Empty);
        return "'" + value.Replace("'", "'\\''") + "'";
    }

    /// <summary>
    /// Bọc giá trị trong single-quote PowerShell chuẩn: <c>'</c> bên trong → <c>''</c>.
    /// Trong PowerShell single-quoted string, mọi ký tự đặc biệt (<c>$ ` " ; | &amp;</c>) đều là literal.
    /// Newline/CR bị loại bỏ để đảm bảo an toàn khi truyền lệnh 1 dòng qua SSH.
    /// </summary>
    public static string QuotePowerShell(string value)
    {
        value ??= string.Empty;
        value = value.Replace("\n", string.Empty).Replace("\r", string.Empty);
        return "'" + value.Replace("'", "''") + "'";
    }

    /// <summary>
    /// Validate tên file installer (nội suy vào trong <c>bash -c '...'</c>).
    /// Chỉ cho phép chữ, số, <c>. _ + - ~ %</c> (ký tự đầu phải là chữ/số);
    /// không cho dấu cách, quote, <c>; $ ( ) /</c>.
    /// </summary>
    public static string ValidateFileName(string fileName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !FileNameRegex.IsMatch(fileName))
            throw new ArgumentException(
                $"Tên file '{fileName}' chứa ký tự không hợp lệ (chỉ cho phép chữ, số, '.', '_', '+', '-') — từ chối để tránh command injection.",
                paramName);
        return fileName;
    }

    /// <summary>
    /// Validate tên package Debian (nội suy vào <c>dpkg -P</c>, <c>find -iname</c>, <c>rm -rf</c>).
    /// </summary>
    public static string ValidatePackageName(string packageName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(packageName) || !PackageNameRegex.IsMatch(packageName))
            throw new ArgumentException(
                $"Tên package '{packageName}' chứa ký tự không hợp lệ (chỉ cho phép chữ, số, '.', '_', '+', '-') — từ chối để tránh command injection.",
                paramName);
        return packageName;
    }

    /// <summary>
    /// Validate đường dẫn file tuyệt đối trên máy đích (nội suy vào <c>"..."</c> trong <c>bash -c '...'</c>).
    /// </summary>
    public static string ValidateRemotePath(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path) || !RemotePathRegex.IsMatch(path) || path.Contains("..") || path.EndsWith('/'))
            throw new ArgumentException(
                $"Đường dẫn '{path}' không hợp lệ (phải là đường dẫn tuyệt đối tới file, chỉ gồm chữ, số, '.', '_', '+', '-', '~', '%', '/', không có dấu cách) — từ chối để tránh command injection.",
                paramName);
        return path;
    }

    /// <summary>
    /// Validate username Linux (nội suy vào đường dẫn <c>/home/...</c>, unit systemd,
    /// <c>loginctl enable-linger</c>).
    /// </summary>
    public static string ValidateUsername(string username, string paramName)
    {
        if (string.IsNullOrWhiteSpace(username) || !UsernameRegex.IsMatch(username))
            throw new ArgumentException(
                $"Username '{username}' chứa ký tự không hợp lệ (chỉ cho phép chữ, số, '.', '_', '-') — từ chối để tránh command injection.",
                paramName);
        return username;
    }

    /// <summary>
    /// Validate username Windows hoặc đa nền tảng (cho phép <c>DOMAIN\user</c>, <c>user@domain</c>,
    /// khoảng trắng trong tên user Windows, nhưng chặn tuyệt đối ký tự command injection
    /// <c>; | &amp; $ ` " ' &lt; &gt; ( ) \r \n</c> và <c>..</c>).
    /// </summary>
    public static string ValidateWindowsUsername(string username, string paramName)
    {
        if (string.IsNullOrWhiteSpace(username)
            || !WindowsUsernameRegex.IsMatch(username)
            || username.Contains("..")
            || username.Contains(@"\\")
            || username.EndsWith('\\'))
        {
            throw new ArgumentException(
                $"Username '{username}' chứa ký tự không hợp lệ — từ chối để tránh command injection.",
                paramName);
        }
        return username;
    }

    /// <summary>
    /// Kiểm tra đường dẫn SFTP/từ xa có phải đường dẫn ổ đĩa Windows (VD: <c>/C:/Users/kztek</c>,
    /// <c>C:/Users/kztek</c>, <c>C:\Users\kztek</c>, <c>/C:</c>) hay không.
    /// </summary>
    public static bool IsWindowsSftpPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return WindowsSftpDriveRegex.IsMatch(path.Trim());
    }

    /// <summary>
    /// Chuẩn hóa đường dẫn SFTP cho cả Linux (<c>/home/kztek</c>) và Windows OpenSSH SFTP
    /// (<c>/C:/Users/kztek</c>, <c>C:\Users\kztek</c> → <c>/C:/Users/kztek</c>; gốc ổ đĩa → <c>/C:/</c>).
    /// </summary>
    public static string NormalizeSftpPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        string trimmed = path.Trim().Replace('\\', '/');

        var m = WindowsSftpDriveRegex.Match(trimmed);
        if (m.Success)
        {
            string drive = m.Groups[1].Value.ToUpperInvariant();
            string rest = (m.Groups["rest"].Value ?? string.Empty).Replace('\\', '/');
            while (rest.Contains("//", StringComparison.Ordinal))
                rest = rest.Replace("//", "/", StringComparison.Ordinal);
            rest = rest.Trim('/');
            return string.IsNullOrEmpty(rest) ? $"/{drive}:/" : $"/{drive}:/{rest}";
        }

        while (trimmed.Contains("//", StringComparison.Ordinal))
            trimmed = trimmed.Replace("//", "/", StringComparison.Ordinal);

        if (!trimmed.StartsWith('/'))
            trimmed = "/" + trimmed;

        return trimmed.Length > 1 ? trimmed.TrimEnd('/') : "/";
    }

    /// <summary>
    /// Chuyển đổi đường dẫn SFTP Windows (<c>/C:/Users/kztek/sub</c> hoặc <c>C:/Users/kztek</c>)
    /// sang đường dẫn cục bộ chuẩn Windows (<c>C:\Users\kztek\sub</c>, gốc ổ đĩa → <c>C:\</c>)
    /// để truyền vào PowerShell / cmd.exe qua SSH.
    /// </summary>
    public static string ToWindowsLocalPath(string? sftpOrWinPath)
    {
        if (string.IsNullOrWhiteSpace(sftpOrWinPath)) return @"C:\";
        string normalized = NormalizeSftpPath(sftpOrWinPath);
        var m = WindowsSftpDriveRegex.Match(normalized);
        if (m.Success)
        {
            string drive = m.Groups[1].Value.ToUpperInvariant();
            string rest = (m.Groups["rest"].Value ?? string.Empty).Trim('/').Replace('/', '\\');
            return string.IsNullOrEmpty(rest) ? $"{drive}:\\" : $"{drive}:\\{rest}";
        }
        return normalized.Replace('/', '\\');
    }

    /// <summary>
    /// Tính toán thư mục cha trên SFTP cho cả Linux (<c>/home/kztek</c> → <c>/home</c> → <c>/</c>)
    /// và Windows OpenSSH (<c>/C:/Users/kztek</c> → <c>/C:/Users</c> → <c>/C:/</c>).
    /// </summary>
    public static string GetParentSftpPath(string? currentPath)
    {
        string norm = NormalizeSftpPath(currentPath);
        if (IsWindowsSftpPath(norm))
        {
            // Định dạng chuẩn hóa là "/C:/" (gốc) hoặc "/C:/Users/kztek"
            if (norm.Length <= 4 && norm.EndsWith(":/", StringComparison.Ordinal))
                return norm;

            int lastSlash = norm.LastIndexOf('/');
            if (lastSlash <= 3) // "/C:"
                return norm.Substring(0, 3) + "/";
            return norm.Substring(0, lastSlash);
        }

        if (norm == "/" || string.IsNullOrEmpty(norm))
            return "/";

        int idx = norm.LastIndexOf('/');
        return idx <= 0 ? "/" : norm.Substring(0, idx);
    }

    /// <summary>
    /// Ghép đường dẫn thư mục SFTP và tên file/thư mục con an toàn cho cả Linux và Windows.
    /// </summary>
    public static string CombineSftpPath(string? basePath, string? childName)
    {
        string normBase = NormalizeSftpPath(basePath);
        string cleanChild = (childName ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(cleanChild)) return normBase;
        return normBase.EndsWith('/') ? normBase + cleanChild : normBase + "/" + cleanChild;
    }
}
