using DownKyi.Models;

namespace DownKyi.Tests;

public class AppInfoTests
{
    [Theory]
    [InlineData("v1.0.32", "1.0.32")]
    [InlineData("1.0.32-debug", "1.0.32-debug")]
    [InlineData("1.0.32+abcdef", "1.0.32")]
    [InlineData("v1.0.32-beta.1", "1.0.32-beta.1")]
    public void NormalizeVersionNamePreservesSemverReleaseIdentity(string input, string expected)
    {
        Assert.Equal(expected, AppInfo.NormalizeVersionName(input));
    }

    [Theory]
    [InlineData("v1.0.32", 10032)]
    [InlineData("1.2.3-debug", 10203)]
    [InlineData("not-a-version", 0)]
    public void VersionNameToCodeUsesNormalizedVersion(string input, int expected)
    {
        Assert.Equal(expected, AppInfo.VersionNameToCode(input));
    }
}
