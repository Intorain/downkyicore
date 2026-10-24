using DownKyi.Infrastructure.Settings;

namespace DownKyi.Infrastructure.Tests;

public sealed class LegacySettingsDecryptorTests
{
    [Fact]
    public void DecryptReadsLegacySettingsFixture()
    {
        const string encrypted = "i6EBvaCTFm2YmW+K0GmZJOHlxsm68jObYhX0WtNSieo=";

        var decrypted = LegacySettingsDecryptor.Decrypt(encrypted, "YO1J$4#p");

        Assert.Equal("{\"fixture\":\"legacy-settings\"}", decrypted);
    }
}
