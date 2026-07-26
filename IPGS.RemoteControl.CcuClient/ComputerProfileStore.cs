using System.Text.Json;

namespace IPGS.RemoteControl.CcuClient;

/// <summary>
/// Quản lý lưu trữ hồ sơ máy tính ZCU và lịch sử kết nối vào file JSON trên ổ đĩa.
/// File lưu giữ mặc định tại %APPDATA%\iPGS\RemoteControl\profiles.json.
/// </summary>
public sealed class ComputerProfileStore : IComputerProfileStore
{
    private readonly string _filePath;
    private readonly object _lockObj = new();
    private readonly List<ComputerProfile> _profiles = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Singleton instance tiện dùng cho ứng dụng.
    /// </summary>
    public static ComputerProfileStore Instance { get; } = new ComputerProfileStore();

    public ComputerProfileStore(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                appData = AppDomain.CurrentDomain.BaseDirectory;
            }
            string dir = Path.Combine(appData, "iPGS", "RemoteControl");
            _filePath = Path.Combine(dir, "profiles.json");
        }

        Load();
    }

    public IReadOnlyList<ComputerProfile> GetAll()
    {
        lock (_lockObj)
        {
            return _profiles.OrderByDescending(p => p.LastConnectedAt ?? DateTimeOffset.MinValue)
                            .ThenBy(p => p.Name)
                            .ToList();
        }
    }

    public ComputerProfile? GetById(string id)
    {
        lock (_lockObj)
        {
            return _profiles.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }

    public ComputerProfile Save(ComputerProfile profile)
    {
        lock (_lockObj)
        {
            var existing = _profiles.FirstOrDefault(p => p.Id.Equals(profile.Id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Name = profile.Name;
                existing.Host = profile.Host;
                existing.Port = profile.Port;
                existing.Token = profile.Token;
                existing.Notes = profile.Notes;
                existing.SshPort = profile.SshPort;
                existing.SshUsername = profile.SshUsername;
                existing.SshPassword = profile.SshPassword;
                if (profile.LastConnectedAt.HasValue)
                {
                    existing.LastConnectedAt = profile.LastConnectedAt;
                }
                existing.LastAppInstallerPath = profile.LastAppInstallerPath;
                existing.LastUninstallPackage = profile.LastUninstallPackage;
                existing.MacAddress = profile.MacAddress;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(profile.Id))
                {
                    profile.Id = Guid.NewGuid().ToString("N");
                }
                _profiles.Add(profile);
                existing = profile;
            }

            Persist();
            return existing;
        }
    }

    public bool Delete(string id)
    {
        lock (_lockObj)
        {
            int count = _profiles.RemoveAll(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (count > 0)
            {
                Persist();
                return true;
            }
            return false;
        }
    }

    public ComputerProfile RecordConnection(string host, int port, string token, string? name = null)
    {
        lock (_lockObj)
        {
            var existing = _profiles.FirstOrDefault(p =>
                p.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && p.Port == port);

            if (existing != null)
            {
                existing.LastConnectedAt = DateTimeOffset.Now;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    existing.Token = token;
                }
                if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(existing.Name))
                {
                    existing.Name = name;
                }
                Persist();
                return existing;
            }
            else
            {
                var newProfile = new ComputerProfile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = !string.IsNullOrWhiteSpace(name) ? name : $"Máy ZCU ({host})",
                    Host = host,
                    Port = port,
                    Token = token,
                    LastConnectedAt = DateTimeOffset.Now,
                    CreatedAt = DateTimeOffset.Now
                };
                _profiles.Add(newProfile);
                Persist();
                return newProfile;
            }
        }
    }

    private void Load()
    {
        lock (_lockObj)
        {
            try
            {
                _profiles.Clear();
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var items = JsonSerializer.Deserialize<List<ComputerProfile>>(json, JsonOptions);
                    if (items != null)
                    {
                        // S7: giải mã secret DPAPI. Giá trị plaintext cũ (không prefix enc:v1:)
                        // đọc được bình thường — sẽ tự migrate sang mã hoá ở lần Persist kế tiếp.
                        foreach (var item in items)
                        {
                            item.Token = SecretProtector.Unprotect(item.Token) ?? string.Empty;
                            item.SshPassword = SecretProtector.Unprotect(item.SshPassword);
                        }
                        _profiles.AddRange(items);
                    }
                }
            }
            catch (Exception ex)
            {
                // Q8: KHÔNG nuốt im lặng — log rõ + backup file hỏng để user không mất
                // dữ liệu vĩnh viễn (có thể khôi phục tay từ file .corrupt-*).
                string warn = $"[ComputerProfileStore] Không đọc được {_filePath}: {ex.Message} — danh sách profile tạm thời rỗng.";
                System.Diagnostics.Trace.TraceWarning(warn);
                try { Console.Error.WriteLine(warn); } catch { /* ignore */ }
                try
                {
                    if (File.Exists(_filePath))
                    {
                        string backupPath = $"{_filePath}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                        File.Copy(_filePath, backupPath, overwrite: true);
                        System.Diagnostics.Trace.TraceWarning(
                            $"[ComputerProfileStore] Đã backup file hỏng sang: {backupPath}");
                    }
                }
                catch { /* backup best-effort */ }
            }
        }
    }

    private void Persist()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // S7: serialize bản sao với secret đã mã hoá DPAPI — KHÔNG mutate object
            // trong bộ nhớ (UI/binding vẫn dùng plaintext runtime).
            var storageList = _profiles.Select(CloneForStorage).ToList();
            string json = JsonSerializer.Serialize(storageList, JsonOptions);

            // Q7: ghi atomic — ghi file tạm rồi File.Move(overwrite) để crash giữa chừng
            // không phá hỏng profiles.json hiện có.
            string tmpPath = _filePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ComputerProfileStore] Error persisting profiles to {_filePath}: {ex.Message}");
        }
    }

    /// <summary>Bản sao chỉ gồm các field được lưu (bỏ state runtime), secret đã mã hoá.</summary>
    private static ComputerProfile CloneForStorage(ComputerProfile p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Host = p.Host,
        Port = p.Port,
        Token = SecretProtector.Protect(p.Token) ?? string.Empty,
        Notes = p.Notes,
        MacAddress = p.MacAddress,
        SshPort = p.SshPort,
        SshUsername = p.SshUsername,
        SshPassword = SecretProtector.Protect(p.SshPassword),
        LastConnectedAt = p.LastConnectedAt,
        LastAppInstallerPath = p.LastAppInstallerPath,
        LastUninstallPackage = p.LastUninstallPackage,
        CreatedAt = p.CreatedAt
    };
}
