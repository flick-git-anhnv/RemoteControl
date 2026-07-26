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
internal static class ShellQuote
{
    // Review 4.1: cho phép thêm '~' và '%' TRỪ ký tự đầu — tên file .deb chuẩn Debian
    // thường chứa '~' trong version (vd: pkg_1.0~rc1_amd64.deb) và '%' khi encode epoch
    // (vd: pkg_1%3a2.0_amd64.deb). Cả hai đều VÔ HẠI trong ngữ cảnh nội suy: giá trị nằm
    // trong "$HOME/..." (double-quote) bên trong bash -c '...' — '~' không tilde-expand
    // (không ở đầu word + trong quote), '%' không có nghĩa shell. Ký tự đầu vẫn giới hạn
    // alphanumeric nên không thể bắt đầu bằng '~' hay '-' (option injection).
    private static readonly Regex FileNameRegex    = new(@"^[A-Za-z0-9][A-Za-z0-9._+~%-]*$", RegexOptions.Compiled);
    private static readonly Regex PackageNameRegex = new(@"^[A-Za-z0-9][A-Za-z0-9._+-]*$", RegexOptions.Compiled);
    private static readonly Regex UsernameRegex    = new(@"^[a-zA-Z_][a-zA-Z0-9._-]*$",    RegexOptions.Compiled);

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
}
