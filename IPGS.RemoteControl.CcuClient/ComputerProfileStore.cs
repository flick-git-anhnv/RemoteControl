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
                        _profiles.AddRange(items);
                    }
                }
            }
            catch
            {
                // Fallback nếu file hỏng hoặc lỗi IO
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
            string json = JsonSerializer.Serialize(_profiles, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ComputerProfileStore] Error persisting profiles to {_filePath}: {ex.Message}");
        }
    }
}
