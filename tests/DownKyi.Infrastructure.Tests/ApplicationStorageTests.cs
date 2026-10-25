using DownKyi.Infrastructure.Storage;

namespace DownKyi.Infrastructure.Tests;

public sealed class ApplicationStorageTests
{
    [Fact]
    public void MigratedStoragePreservesTheConfiguredDataLayout()
    {
        // The assembly fixture supplies a private root before storage is initialized.
        var configuredRoot = Environment.GetEnvironmentVariable("DOWNKYI_DATA_DIR");
        Assert.False(string.IsNullOrWhiteSpace(configuredRoot));
        var root = Path.GetFullPath(configuredRoot);

        Assert.Equal(root, ApplicationStorage.GetRoot());
        Assert.Equal(Path.Combine(root, "Config", "Settings"), ApplicationStorage.GetSettings());
        Assert.Equal(Path.Combine(root, "Config", "Login"), ApplicationStorage.GetLogin());
        Assert.Equal(Path.Combine(root, "Storage", "Data.db"), ApplicationStorage.GetDbPath());
        Assert.Equal(Path.Combine(root, "Storage", "Download.db"), ApplicationStorage.GetDownload());
        Assert.Equal(Path.Combine(root, "Media"), ApplicationStorage.GetMedia());
        Assert.Equal(Path.Combine(root, "Cache"), ApplicationStorage.GetCache());
        Assert.Equal(Path.Combine(root, "Logs"), ApplicationStorage.GetLogsDir());
        Assert.Equal(Path.Combine(root, "Aria"), ApplicationStorage.GetAriaDir());
        foreach (var name in new[] { "Config", "Storage", "Media", "Cache", "Logs", "Aria" })
        {
            Assert.True(Directory.Exists(Path.Combine(root, name)));
        }
    }
}
