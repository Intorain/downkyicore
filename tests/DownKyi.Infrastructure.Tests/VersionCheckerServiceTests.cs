using System.Net;
using System.Text.Json;
using DownKyi.Infrastructure.Versioning;

namespace DownKyi.Infrastructure.Tests;

public class VersionCheckerServiceTests
{
    [Fact]
    public void GitHubReleaseJsonUsesSourceGeneratedContract()
    {
        const string json = """
            {
              "tag_name": "v1.2.3",
              "name": "Release",
              "body": "Notes",
              "prerelease": true,
              "published_at": "2026-07-10T00:00:00Z"
            }
            """;

        var release = JsonSerializer.Deserialize(json, GitHubJsonContext.Default.GitHubRelease);

        Assert.NotNull(release);
        Assert.Equal("v1.2.3", release.TagName);
        Assert.Equal("Release", release.Name);
        Assert.Equal("Notes", release.Body);
        Assert.True(release.Prerelease);
        Assert.Equal(new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc), release.PublishedAt);
    }

    [Fact]
    public async Task LatestReleaseCheckPreservesPreCanceledRequest()
    {
        using var handler = new StubHandler("{}");
        using var httpClient = CreateHttpClient(handler);
        var service = new VersionCheckerService(httpClient, "owner", "repo");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetLatestReleaseAsync(false, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task PrereleaseCheckUsesReleaseCollectionEndpoint()
    {
        using var handler = new StubHandler("[{\"tag_name\":\"v2.0.0-beta.1\"}]");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.test/")
        };
        var service = new VersionCheckerService(httpClient, "owner", "repo");

        var release = await service.GetLatestReleaseAsync(
            true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("v2.0.0-beta.1", release?.TagName);
        Assert.Equal("https://api.github.test/repos/owner/repo/releases", handler.RequestUri?.AbsoluteUri);
    }

    [Theory]
    [InlineData(null, "v2.0.0+build.2")]
    [InlineData("1.0.0", "v2.0.0+build.2")]
    [InlineData("2.0.0+build.1", null)]
    public async Task StableCheckUsesLatestEndpointAndHonorsSkippedVersion(
        string? excludedVersion,
        string? expectedVersion)
    {
        using var handler = new StubHandler("""{"tag_name":"v2.0.0+build.2"}""");
        using var httpClient = CreateHttpClient(handler);
        var service = new VersionCheckerService(httpClient, "owner", "repo");

        var release = await service.GetLatestReleaseAsync(
            false,
            excludedVersion,
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedVersion, release?.TagName);
        Assert.Equal("https://api.github.test/repos/owner/repo/releases/latest", handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task SkippedPrereleaseIgnoresEquivalentBuildAndReturnsNextRelease()
    {
        using var handler = new StubHandler("""
            [
              {"tag_name":"v2.0.0-beta.1+build.2"},
              {"tag_name":"v2.0.0-beta.2"}
            ]
            """);
        using var httpClient = CreateHttpClient(handler);
        var service = new VersionCheckerService(httpClient, "owner", "repo");

        var release = await service.GetLatestReleaseAsync(
            true,
            "2.0.0-beta.1+build.1",
            TestContext.Current.CancellationToken);

        Assert.Equal("v2.0.0-beta.2", release?.TagName);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.github.test/")
        };
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
        }
    }
}
