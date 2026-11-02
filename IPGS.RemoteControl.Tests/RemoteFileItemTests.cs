using IPGS.RemoteControl.CcuUI.Views;
using Xunit;

namespace IPGS.RemoteControl.Tests;

public class RemoteFileItemTests
{
    [Fact]
    public void RemoteFileItem_Directory_HasFolderIconAndBold()
    {
        var item = new RemoteFileItem
        {
            Name = ".anydesk",
            FullName = "/home/kztek/.anydesk",
            IsDirectory = true
        };

        Assert.Equal("📁", item.Icon);
        Assert.Equal(Avalonia.Media.FontWeight.Bold, item.FontWeight);
        Assert.Equal("", item.SizeText);
    }

    [Fact]
    public void RemoteFileItem_SymbolicLink_HasFolderIconAndBold()
    {
        var item = new RemoteFileItem
        {
            Name = "symlink_dir",
            FullName = "/home/kztek/symlink_dir",
            IsDirectory = false,
            IsSymbolicLink = true
        };

        Assert.Equal("📁", item.Icon);
        Assert.Equal(Avalonia.Media.FontWeight.Bold, item.FontWeight);
        Assert.Equal("", item.SizeText);
    }

    [Fact]
    public void RemoteFileItem_RegularFile_HasFileIconAndNormalWeight()
    {
        var item = new RemoteFileItem
        {
            Name = "appsettings.json",
            FullName = "/home/kztek/appsettings.json",
            IsDirectory = false,
            IsSymbolicLink = false,
            Size = 2048
        };

        Assert.Equal("📄", item.Icon);
        Assert.Equal(Avalonia.Media.FontWeight.Normal, item.FontWeight);
        Assert.Equal("2.0 KB", item.SizeText);
    }

    [Theory]
    [InlineData("/home/kztek", "/home")]
    [InlineData("/home", "/")]
    [InlineData("/", "/")]
    [InlineData("/var/log/nginx", "/var/log")]
    [InlineData("/home/kztek/.config/systemd/user", "/home/kztek/.config/systemd")]
    public void PosixParentPath_CalculatesCorrectly(string currentPath, string expectedParent)
    {
        string path = currentPath.Trim();
        string parent;
        if (path == "/" || string.IsNullOrEmpty(path))
        {
            parent = "/";
        }
        else
        {
            string trimmed = path.TrimEnd('/');
            int lastSlash = trimmed.LastIndexOf('/');
            parent = lastSlash <= 0 ? "/" : trimmed.Substring(0, lastSlash);
        }

        Assert.Equal(expectedParent, parent);
    }

    [Fact]
    public void TargetPath_FallsBackWhenFullNameEmpty()
    {
        var item = new RemoteFileItem
        {
            Name = ".dotnet",
            FullName = "",
            IsDirectory = true
        };

        string currentPath = "/home/kztek";
        string targetPath = !string.IsNullOrWhiteSpace(item.FullName)
            ? item.FullName
            : (currentPath.TrimEnd('/') + "/" + item.Name);

        Assert.Equal("/home/kztek/.dotnet", targetPath);
    }
}
