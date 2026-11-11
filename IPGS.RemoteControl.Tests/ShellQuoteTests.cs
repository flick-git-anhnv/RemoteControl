using IPGS.RemoteControl.CcuClient;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class ShellQuoteTests
{
    [Fact]
    public void Quote_NormalString_WrapsInSingleQuotes()
    {
        string input = "hello_world";
        string quoted = ShellQuote.Quote(input);

        Assert.Equal("'hello_world'", quoted);
    }

    [Fact]
    public void Quote_StringWithSingleQuotes_EscapesProperly()
    {
        string input = "can't stop";
        string quoted = ShellQuote.Quote(input);

        // 'can'\''t stop'
        Assert.Equal("'can'\\''t stop'", quoted);
    }

    [Fact]
    public void Quote_StringWithCommandInjection_IsDefanged()
    {
        string injection = "test; rm -rf /; $(whoami)";
        string quoted = ShellQuote.Quote(injection);

        Assert.StartsWith("'", quoted);
        Assert.EndsWith("'", quoted);
        Assert.DoesNotContain("''", quoted);
    }

    [Theory]
    [InlineData("valid_file.deb")]
    [InlineData("setup-agent-1.2.tar.gz")]
    [InlineData("app_v1.0.sh")]
    [InlineData("package~1.deb")]
    public void ValidateFileName_ValidNames_Succeeds(string validName)
    {
        string result = ShellQuote.ValidateFileName(validName, "param");
        Assert.Equal(validName, result);
    }

    [Theory]
    [InlineData("/home/kztek/app_1.0~rc1_amd64.deb")]
    [InlineData("/opt/setup/install-v2.sh")]
    public void ValidateRemotePath_ValidPaths_Succeeds(string path)
    {
        Assert.Equal(path, ShellQuote.ValidateRemotePath(path, "param"));
    }

    [Theory]
    [InlineData("relative/file.deb")]
    [InlineData("/home/kztek/")]
    [InlineData("/home/../etc/passwd")]
    [InlineData("/home/kztek/a b.deb")]
    [InlineData("/home/kztek/a;rm -rf ~.deb")]
    [InlineData("/home/kztek/$(id).deb")]
    [InlineData("/home/kztek/a\"b.deb")]
    [InlineData("")]
    public void ValidateRemotePath_MaliciousPaths_ThrowsArgumentException(string path)
    {
        Assert.Throws<ArgumentException>(() => ShellQuote.ValidateRemotePath(path, "param"));
    }

    [Theory]
    [InlineData("file;evil.sh")]
    [InlineData("../secret")]
    [InlineData("file $(rm -rf)")]
    [InlineData("file`id`")]
    [InlineData("path/to/file")]
    public void ValidateFileName_MaliciousNames_ThrowsArgumentException(string malicious)
    {
        Assert.Throws<ArgumentException>(() => ShellQuote.ValidateFileName(malicious, "param"));
    }

    [Theory]
    [InlineData("kztek")]
    [InlineData("ubuntu")]
    [InlineData("user123")]
    public void ValidateUsername_ValidUsernames_Succeeds(string validUser)
    {
        string result = ShellQuote.ValidateUsername(validUser, "user");
        Assert.Equal(validUser, result);
    }

    [Theory]
    [InlineData("user;evil")]
    [InlineData("user$(id)")]
    [InlineData("-rf")]
    public void ValidateUsername_MaliciousUsernames_ThrowsArgumentException(string malicious)
    {
        Assert.Throws<ArgumentException>(() => ShellQuote.ValidateUsername(malicious, "user"));
    }
}
