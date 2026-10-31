using DownKyi.Application.Bilibili;
using DownKyi.Core.BiliApi.Users;
using DownKyi.Core.BiliApi.Users.Models;
using Newtonsoft.Json;

namespace DownKyi.Core.Tests;

public sealed class PublicationSearchContractTests
{
    [Fact]
    public void FixturePreservesSearchPageCountAndMedia()
    {
        var json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "BiliApi",
            "JsonSamples",
            "space-publication-search.json"));
        var origin = JsonConvert.DeserializeObject<SpacePublicationOrigin>(json);

        Assert.NotNull(origin?.Data);
        Assert.Equal(35, origin.Data.Page.Count);
        Assert.Equal(2, origin.Data.Page.Pn);
        Assert.Equal("BV1fixture01", Assert.Single(origin.Data.List.Vlist!).Bvid);
    }

    [Fact]
    public void AvoidedSeasonRepresentativePreservesCollectionIdentity()
    {
        var origin = JsonConvert.DeserializeObject<SpacePublicationOrigin>("""
            {
              "data": {
                "list": {
                  "vlist": [
                    {
                      "mid": 42,
                      "aid": 100,
                      "bvid": "BV1fixture01",
                      "is_avoided": 1,
                      "season_id": 99,
                      "meta": {
                        "id": 99,
                        "title": "fixture collection",
                        "cover": "collection cover",
                        "mid": 42,
                        "ep_count": 26,
                        "ptime": 1700000000
                      }
                    }
                  ]
                }
              }
            }
            """);

        var video = Assert.Single(origin!.Data!.List.Vlist!);
        Assert.Equal(1, video.IsAvoided);
        Assert.Equal(99, video.SeasonId);
        Assert.Equal("fixture collection", video.Meta!.Title);
        Assert.Equal(26, video.Meta.EpisodeCount);
    }

    [Fact]
    public async Task DynamicFixturePreservesCursorAndDirectVideoShape()
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "BiliApi", "JsonSamples", "space-dynamic-feed.json"),
            TestContext.Current.CancellationToken);
        BilibiliHttpRequest? observedRequest = null;
        var client = new StubBilibiliApiClient((request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            observedRequest = request;
            return Task.FromResult(json);
        });

        var page = await client.GetDynamicPageAsync(
            42,
            "current offset",
            TestContext.Current.CancellationToken);

        Assert.True(page.HasMore);
        Assert.Equal("next.offset.2", page.Offset);
        var video = page.Items[0].Modules?.Dynamic?.Major?.Archive;
        Assert.NotNull(video);
        Assert.Equal("BV1T6PQzQEUf", video.Bvid);
        Assert.Equal(100, video.Aid);
        Assert.Equal("03:12", video.Duration);
        Assert.Equal("1.2万", video.Stat?.Play);
        Assert.NotNull(observedRequest);
        Assert.Contains("host_mid=42", observedRequest.RequestAddress, StringComparison.Ordinal);
        Assert.Contains("offset=current%20offset", observedRequest.RequestAddress, StringComparison.Ordinal);
        Assert.Contains(
            "features=itemOpusStyle%2ClistOnlyfans%2CopusBigCover%2ConlyfansVote",
            observedRequest.RequestAddress,
            StringComparison.Ordinal);
        Assert.Equal("https://space.bilibili.com/42/dynamic", observedRequest.Referer);
        Assert.True(observedRequest.IncludeCredentials);
        Assert.True(observedRequest.IncludeBuvid);
    }
}
