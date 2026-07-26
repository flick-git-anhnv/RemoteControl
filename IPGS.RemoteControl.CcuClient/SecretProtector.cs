using System.Security.Cryptography;
using System.Text;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// Mã hoá/giải mã secret (SSH password, token) lưu trong profiles.json bằng DPAPI
/// (<see cref="ProtectedData"/>, scope CurrentUser) — chỉ user Windows hiện tại giải mã được.
/// <para>
/// Định dạng lưu: <c>enc:v1:&lt;base64&gt;</c>. Giá trị KHÔNG có prefix được coi là
/// plaintext cũ (file tạo trước khi có mã hoá) — đọc được bình thường và sẽ tự
/// migrate sang dạng mã hoá ở lần save kế tiếp.
/// </para>
/// <para>
/// ⚠️ DPAPI CHỈ chạy trên Windows. Trên Linux/macOS: giữ plaintext (hành vi xác định)
/// nhưng log cảnh báo NỔI BẬT một lần — không im lặng ghi plaintext.
/// </para>
/// </summary>
internal static class SecretProtector
{
    private const string Prefix = "enc:v1:";
    private static int _nonWindowsWarned; // 0 = chưa cảnh báo (Interlocked)

    /// <summary>Giá trị đã ở dạng mã hoá chưa (có prefix <c>enc:v1:</c>)?</summary>
    public static bool IsProtected(string? value)
        => !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Mã hoá plaintext → <c>enc:v1:base64</c>. Idempotent: giá trị đã mã hoá trả về nguyên vẹn.
    /// Trên nền tảng không phải Windows: trả về plaintext + cảnh báo nổi bật (1 lần).
    /// </summary>
    public static string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        if (IsProtected(plaintext)) return plaintext;

        if (!OperatingSystem.IsWindows())
        {
            WarnNonWindowsOnce();
            return plaintext;
        }

        byte[] cipher = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext), optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(cipher);
    }

    /// <summary>
    /// Giải mã giá trị đã lưu. Plaintext cũ (không prefix) trả về nguyên vẹn (migration path).
    /// Giải mã thất bại (file copy từ user/máy khác, dữ liệu hỏng) → trả về chuỗi rỗng + log
    /// cảnh báo, KHÔNG ném exception để không làm mất toàn bộ danh sách profile.
    /// </summary>
    public static string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return stored;
        if (!IsProtected(stored)) return stored; // plaintext cũ — đọc được, migrate khi save

        if (!OperatingSystem.IsWindows())
        {
            WarnNonWindowsOnce();
            LogWarning("[SecretProtector] Gặp giá trị đã mã hoá DPAPI nhưng đang chạy trên nền tảng không phải Windows — không thể giải mã, trả về rỗng.");
            return string.Empty;
        }

        try
        {
            byte[] plain = ProtectedData.Unprotect(
                Convert.FromBase64String(stored.Substring(Prefix.Length)),
                optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            LogWarning($"[SecretProtector] Không giải mã được secret (file có thể copy từ user/máy khác hoặc hỏng): {ex.Message}. Trả về rỗng — user cần nhập lại.");
            return string.Empty;
        }
    }

    private static void WarnNonWindowsOnce()
    {
        if (Interlocked.Exchange(ref _nonWindowsWarned, 1) != 0) return;
        LogWarning(
            "⚠️⚠️⚠️ [SecretProtector] DPAPI không khả dụng trên nền tảng này (chỉ hỗ trợ Windows). " +
            "SSH password / token sẽ được lưu PLAINTEXT trong profiles.json. " +
            "Hãy đảm bảo quyền truy cập file được siết chặt hoặc không lưu mật khẩu trên máy này. ⚠️⚠️⚠️");
    }

    private static void LogWarning(string message)
    {
        System.Diagnostics.Trace.TraceWarning(message);
        try { Console.Error.WriteLine(message); } catch { /* ignore */ }
    }
}
