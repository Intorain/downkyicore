using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DownKyi.CentralTestRunner;

namespace DownKyi.Architecture.Tests;

[Collection(PowerShellProcessTestGroup.Name)]
public sealed class PublishedReleaseReadbackTests
{
    private const string Tag = "v9.8.7";
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public async Task VerifierAcceptsExactImmutablePublishedAssets()
    {
        var fixture = CreateFixture();
        try
        {
            var exitCode = await RunVerifierAsync(fixture).ConfigureAwait(true);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(fixture.Root, recursive: true);
        }
    }

    [Theory]
    [InlineData("missing-asset")]
    [InlineData("extra-asset")]
    [InlineData("wrong-size")]
    [InlineData("wrong-digest")]
    [InlineData("wrong-tag")]
    [InlineData("draft")]
    [InlineData("prerelease")]
    [InlineData("mutable")]
    public async Task VerifierRejectsPublishedReleaseIdentityDrift(string mutation)
    {
        var fixture = CreateFixture(mutation);
        try
        {
            var exitCode = await RunVerifierAsync(fixture).ConfigureAwait(true);

            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Directory.Delete(fixture.Root, recursive: true);
        }
    }

    private static ReleaseFixture CreateFixture(string? mutation = null)
    {
        var root = Path.Combine(Path.GetTempPath(), $"downkyi-release-readback-{Guid.NewGuid():N}");
        var assets = Path.Combine(root, "release-assets");
        Directory.CreateDirectory(assets);

        File.WriteAllText(Path.Combine(assets, "DownKyi-fixture.zip"), "windows-package", Encoding.UTF8);
        File.WriteAllText(Path.Combine(assets, "DownKyi-fixture-verification.zip"), "verification", Encoding.UTF8);

        var remoteAssets = new JsonArray();
        foreach (var path in Directory.EnumerateFiles(assets).Order(StringComparer.Ordinal))
        {
            var file = new FileInfo(path);
            var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            remoteAssets.Add(new JsonObject
            {
                ["name"] = file.Name,
                ["size"] = file.Length,
                ["digest"] = $"sha256:{digest}"
            });
        }

        var release = new JsonObject
        {
            ["tag_name"] = Tag,
            ["draft"] = false,
            ["prerelease"] = false,
            ["immutable"] = true,
            ["assets"] = remoteAssets
        };

        switch (mutation)
        {
            case null:
                break;
            case "missing-asset":
                remoteAssets.RemoveAt(remoteAssets.Count - 1);
                break;
            case "extra-asset":
                remoteAssets.Add(new JsonObject
                {
                    ["name"] = "unexpected.zip",
                    ["size"] = 1,
                    ["digest"] = $"sha256:{new string('0', 64)}"
                });
                break;
            case "wrong-size":
                ((JsonObject)remoteAssets[0]!)["size"] = 1;
                break;
            case "wrong-digest":
                ((JsonObject)remoteAssets[0]!)["digest"] = $"sha256:{new string('0', 64)}";
                break;
            case "wrong-tag":
                release["tag_name"] = "v0.0.0";
                break;
            case "draft":
                release["draft"] = true;
                break;
            case "prerelease":
                release["prerelease"] = true;
                break;
            case "mutable":
                release["immutable"] = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var releaseJson = Path.Combine(root, "release.json");
        File.WriteAllText(releaseJson, release.ToJsonString(), Encoding.UTF8);
        return new ReleaseFixture(root, assets, releaseJson);
    }

    private static async Task<int> RunVerifierAsync(ReleaseFixture fixture)
    {
        var startInfo = new ProcessStartInfo("pwsh") { UseShellExecute = false };
        string[] arguments =
        [
            "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
            Path.Combine(RepositoryRoot, "script", "verify-published-release.ps1"),
            "-ReleaseJsonPath", fixture.ReleaseJson,
            "-AssetDirectory", fixture.Assets,
            "-ExpectedTag", Tag
        ];
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMinutes(1));
        return await BuildProcessRunner.RunAsync(startInfo, cancellation.Token).ConfigureAwait(true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DownKyi.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed record ReleaseFixture(string Root, string Assets, string ReleaseJson);
}
