using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace IPGS.RemoteControl.CcuUI.Services;

public class LicenseManagerService
{
    private static readonly string PublicKeyXml = "<RSAKeyValue><Modulus>296ICVYE/HdaJKnXl/ZLzOfPtPSe8S71LYHJrXanIRsrHbLo4VikHSPL9wna9WTwXIzxVepdU59w+veAmpbLc+XixRcuByCzFUuJPQsvB+4Z3m2wz/yyKFRQReOZQWtIDMrPJ8+LXPBWLeLIpGVqXQFxu9AOyXCd9S1s/hA498N+hT/sq9r2VXZrKmAaAUyuYJ900gQcMY2tOPVmwe3zDkNTM3eqVEcUGG19/QZTjx5xVMoixM1yA6/OmebHIH4zAK+iYGBPs8HEXB1T2iWjjRitmMDVwhVGAqnjnK2CT4rtxqNhGPhlH4WMH8C7AsjGe4J11mNKH0d/JAJM4H1oSQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kztek", "RemoteControl");
    private static readonly string LicenseFilePath = Path.Combine(AppDataFolder, "license.key");

    public static string HardwareId => GetHardwareId();

    public static bool IsLicensed { get; private set; }
    public static DateTime ExpirationDate { get; private set; }
    public static string CustomerName { get; private set; } = string.Empty;
    public static string LastError { get; private set; } = string.Empty;

    public static bool ValidateAndLoadLicense()
    {
        try
        {
            if (!File.Exists(LicenseFilePath))
            {
                LastError = "Không tìm thấy tệp license.";
                return false;
            }

            string licenseKey = File.ReadAllText(LicenseFilePath).Trim();
            return ValidateLicenseKey(licenseKey, true);
        }
        catch (Exception ex)
        {
            LastError = $"Lỗi đọc license: {ex.Message}";
            return false;
        }
    }

    public static bool ApplyLicense(string licenseKey)
    {
        if (ValidateLicenseKey(licenseKey, false))
        {
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);
                
            File.WriteAllText(LicenseFilePath, licenseKey);
            return true;
        }
        return false;
    }

    private static bool ValidateLicenseKey(string licenseKey, bool loadToMemory)
    {
        try
        {
            // Superadmin backdoor key
            if (licenseKey.Trim() == "ANHNV")
            {
                if (loadToMemory)
                {
                    IsLicensed = true;
                    ExpirationDate = DateTime.MaxValue;
                    CustomerName = "Super Admin (ANHNV)";
                    LastError = string.Empty;
                }
                return true;
            }

            var parts = licenseKey.Split('.');
            if (parts.Length != 2)
            {
                LastError = "Định dạng License Key không hợp lệ.";
                return false;
            }

            byte[] payloadBytes = Convert.FromBase64String(parts[0]);
            byte[] signatureBytes = Convert.FromBase64String(parts[1]);

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(PublicKeyXml);
                bool isSignatureValid = rsa.VerifyData(payloadBytes, CryptoConfig.MapNameToOID("SHA256"), signatureBytes);

                if (!isSignatureValid)
                {
                    LastError = "Chữ ký số License không hợp lệ (Bị giả mạo).";
                    return false;
                }
            }

            string payload = Encoding.UTF8.GetString(payloadBytes);
            // Payload format: Customer|HardwareId|ExpirationDate(yyyy-MM-dd)
            var p = payload.Split('|');
            if (p.Length < 3)
            {
                LastError = "Dữ liệu License bị thiếu.";
                return false;
            }

            string customer = p[0];
            string hwId = p[1];
            string expStr = p[2];

            if (hwId != HardwareId)
            {
                LastError = "License này không dành cho máy tính (Hardware ID) này.";
                return false;
            }

            if (!DateTime.TryParse(expStr, out DateTime expDate))
            {
                LastError = "Ngày hết hạn không hợp lệ.";
                return false;
            }

            if (DateTime.Now.Date > expDate.Date)
            {
                LastError = $"License đã hết hạn vào ngày {expDate:dd/MM/yyyy}.";
                return false;
            }

            if (loadToMemory)
            {
                IsLicensed = true;
                ExpirationDate = expDate;
                CustomerName = customer;
                LastError = string.Empty;
            }

            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Lỗi kiểm tra License: {ex.Message}";
            return false;
        }
    }

    private static string GetHardwareId()
    {
        try
        {
            var macAddr = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault(mac => !string.IsNullOrEmpty(mac));

            if (string.IsNullOrEmpty(macAddr))
            {
                macAddr = NetworkInterface.GetAllNetworkInterfaces()
                    .Select(nic => nic.GetPhysicalAddress().ToString())
                    .FirstOrDefault(mac => !string.IsNullOrEmpty(mac)) ?? "UNKNOWN_MAC";
            }

            // A simple hash of the MAC address to obscure it slightly
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(macAddr));
            
            // Format as a readable 16-char string e.g. XXXX-XXXX-XXXX-XXXX
            string hex = BitConverter.ToString(hash).Replace("-", "");
            string hwId = hex.Substring(0, 16);
            return $"{hwId.Substring(0,4)}-{hwId.Substring(4,4)}-{hwId.Substring(8,4)}-{hwId.Substring(12,4)}";
        }
        catch
        {
            return "DEFAULT-HW-ID-001";
        }
    }
}
