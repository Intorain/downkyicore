using System.Text.Json;
using DownKyi.Application.Versioning;

namespace DownKyi.Infrastructure.Versioning;

public sealed class VersionCheckerService
{
    private readonly HttpClient _httpClient;
    private readonly string _repoOwner;
    private readonly string _repoName;

    public VersionCheckerService(HttpClient httpClient, string repoOwner, string repoName)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _repoOwner = repoOwner ?? throw new ArgumentNullException(nameof(repoOwner));
        _repoName = repoName ?? throw new ArgumentNullException(nameof(repoName));
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(
        bool includePrereleases,
        string? excludedVersion = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = includePrereleases
            ? $"repos/{_repoOwner}/{_repoName}/releases"
            : $"repos/{_repoOwner}/{_repoName}/releases/latest";
        var responseJson = await _httpClient
            .GetStringAsync(new Uri(endpoint, UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);

        if (includePrereleases)
        {
            var releases = JsonSerializer.Deserialize(
                responseJson,
                GitHubJsonContext.Default.GitHubReleaseArray);
            return releases?.FirstOrDefault(release => !IsExcluded(
                release.TagName,
                excludedVersion));
        }

        var latestRelease = JsonSerializer.Deserialize(
            responseJson,
            GitHubJsonContext.Default.GitHubRelease);
        return latestRelease != null && !IsExcluded(latestRelease.TagName, excludedVersion)
            ? latestRelease
            : null;
    }

    private static bool IsExcluded(string releaseVersion, string? excludedVersion)
    {
        return !string.IsNullOrWhiteSpace(excludedVersion) &&
               SemanticVersionPolicy.HasSamePrecedence(releaseVersion, excludedVersion);
    }
}
