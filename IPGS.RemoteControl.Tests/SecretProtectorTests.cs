using IPGS.RemoteControl.CcuClient;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class SecretProtectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Protect_NullOrEmpty_ReturnsSame(string? input)
    {
        var result = SecretProtector.Protect(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Protect_AlreadyProtected_IsIdempotent()
    {
        const string already = "enc:v1:c29tZXJhbmRvbWRhdGE=";
        var result = SecretProtector.Protect(already);
        Assert.Equal(already, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Unprotect_NullOrEmpty_ReturnsSame(string? input)
    {
        var result = SecretProtector.Unprotect(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Unprotect_LegacyPlaintextWithoutPrefix_ReturnsSame()
    {
        const string legacy = "MyPlaintextPassword123!";
        var result = SecretProtector.Unprotect(legacy);
        Assert.Equal(legacy, result);
    }

    [Fact]
    public void Protect_And_Unprotect_RoundTrip_OnWindows()
    {
        if (!OperatingSystem.IsWindows()) return; // DPAPI is Windows-specific

        const string original = "SecretP@ssw0rd!#$2026";
        var encrypted = SecretProtector.Protect(original);

        Assert.NotNull(encrypted);
        Assert.StartsWith("enc:v1:", encrypted);
        Assert.NotEqual(original, encrypted);

        var decrypted = SecretProtector.Unprotect(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Unprotect_CorruptedBase64_DoesNotThrowAndReturnsEmpty()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string corrupted = "enc:v1:NotAValidBase64String!!!@@@";
        // Should gracefully catch and return string.Empty rather than throwing
        var result = SecretProtector.Unprotect(corrupted);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void IsProtected_CorrectlyIdentifiesEncPrefix()
    {
        Assert.True(SecretProtector.IsProtected("enc:v1:abcdef"));
        Assert.False(SecretProtector.IsProtected("plaintext"));
        Assert.False(SecretProtector.IsProtected(""));
        Assert.False(SecretProtector.IsProtected(null));
    }
}
