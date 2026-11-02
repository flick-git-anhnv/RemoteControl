using IPGS.RemoteControl.CcuClient;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class ComputerProfileStoreTests : IDisposable
{
    private readonly string _tempFile;
    private readonly ComputerProfileStore _store;

    public ComputerProfileStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"profiles_test_{Guid.NewGuid():N}.json");
        _store = new ComputerProfileStore(_tempFile);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempFile)) File.Delete(_tempFile);
        }
        catch { /* ignored */ }
    }

    [Fact]
    public void Save_NewProfile_AssignsIdAndPersists()
    {
        var profile = new ComputerProfile
        {
            Name = "ZCU Test 1",
            Host = "192.168.1.100",
            Port = 17600,
            Token = "TestToken12345"
        };

        var saved = _store.Save(profile);

        Assert.NotNull(saved.Id);
        Assert.NotEmpty(saved.Id);
        Assert.Equal("ZCU Test 1", saved.Name);

        var retrieved = _store.GetById(saved.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("192.168.1.100", retrieved.Host);
    }

    [Fact]
    public void Save_ExistingProfile_PreservesTlsCertFingerprint()
    {
        var profile = new ComputerProfile
        {
            Name = "ZCU TLS Machine",
            Host = "192.168.1.200",
            Port = 17600,
            Token = "SecToken123",
            TlsCertFingerprint = "SHA256:ABCDEF1234567890"
        };

        var saved = _store.Save(profile);
        Assert.Equal("SHA256:ABCDEF1234567890", saved.TlsCertFingerprint);

        // Update profile with updated name and ensure TlsCertFingerprint is preserved (audit F-NEW-1)
        saved.Name = "ZCU TLS Machine Renamed";
        saved.TlsCertFingerprint = "SHA256:UPDATED99999";
        var updated = _store.Save(saved);

        Assert.Equal("ZCU TLS Machine Renamed", updated.Name);
        Assert.Equal("SHA256:UPDATED99999", updated.TlsCertFingerprint);

        // Reload store from disk
        var reloadedStore = new ComputerProfileStore(_tempFile);
        var loaded = reloadedStore.GetById(saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal("SHA256:UPDATED99999", loaded.TlsCertFingerprint);
    }

    [Fact]
    public void UpdateTlsFingerprint_WhenEmpty_UpdatesAndPersists()
    {
        var profile = new ComputerProfile
        {
            Name = "ZCU TOFU Target",
            Host = "192.168.1.50",
            Port = 17600,
            Token = "SecToken123",
            TlsCertFingerprint = null // Not set yet (TOFU)
        };

        _store.Save(profile);

        // Simulate TOFU callback from connection
        _store.UpdateTlsFingerprint("192.168.1.50", 17600, "SHA256:TOFU_FINGERPRINT_123");

        var retrieved = _store.GetAll().First(p => p.Host == "192.168.1.50");
        Assert.Equal("SHA256:TOFU_FINGERPRINT_123", retrieved.TlsCertFingerprint);

        // Subsequent update when already set should not overwrite
        _store.UpdateTlsFingerprint("192.168.1.50", 17600, "SHA256:DIFFERENT");
        var afterSecond = _store.GetAll().First(p => p.Host == "192.168.1.50");
        Assert.Equal("SHA256:TOFU_FINGERPRINT_123", afterSecond.TlsCertFingerprint);
    }

    [Fact]
    public void Delete_RemovesProfileFromStore()
    {
        var profile = new ComputerProfile
        {
            Name = "To Delete",
            Host = "10.0.0.1",
            Port = 17600
        };

        var saved = _store.Save(profile);
        Assert.NotNull(_store.GetById(saved.Id));

        var deleted = _store.Delete(saved.Id);
        Assert.True(deleted);
        Assert.Null(_store.GetById(saved.Id));
    }
}
