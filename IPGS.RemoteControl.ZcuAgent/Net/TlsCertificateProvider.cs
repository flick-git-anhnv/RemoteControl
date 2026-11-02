using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Cung cấp chứng chỉ TLS tự ký cho agent. Sinh 1 lần, lưu PFX cạnh agent (quyền 600) để
/// giữ nguyên qua các lần khởi động lại; nếu không ghi được đĩa thì dùng chứng chỉ trong bộ nhớ.
/// </summary>
internal static class TlsCertificateProvider
{
    private static readonly object Gate = new();
    private static X509Certificate2? _cached;

    public static X509Certificate2 GetOrCreate(AgentOptions options, ILogger logger)
    {
        lock (Gate)
        {
            if (_cached is not null) return _cached;

            var path = string.IsNullOrWhiteSpace(options.TlsCertPath)
                ? Path.Combine(AppContext.BaseDirectory, "agent-tls.pfx")
                : options.TlsCertPath;

            try
            {
                if (File.Exists(path))
                {
                    var loaded = new X509Certificate2(path);
                    if (loaded.NotAfter > DateTime.UtcNow.AddDays(30))
                        return _cached = loaded;
                    loaded.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "TLS: không đọc được chứng chỉ {Path} — sinh chứng chỉ mới", path);
            }

            var cert = CreateSelfSigned();
            try
            {
                File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx));
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                logger.LogInformation("TLS: đã tạo chứng chỉ tự ký, lưu tại {Path}", path);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "TLS: không lưu được chứng chỉ vào {Path} — dùng chứng chỉ tạm trong bộ nhớ", path);
            }
            return _cached = cert;
        }
    }

    private static X509Certificate2 CreateSelfSigned()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest(
            $"CN=ZcuAgent-{Environment.MachineName}", key, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, critical: false)); // serverAuth

        using var selfSigned = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        // Round-trip qua PFX để private key dùng được với SslStream trên mọi nền tảng.
        return new X509Certificate2(selfSigned.Export(X509ContentType.Pfx));
    }
}
