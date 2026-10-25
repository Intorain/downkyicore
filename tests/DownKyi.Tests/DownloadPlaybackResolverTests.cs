using DownKyi.Application.Bilibili;
using DownKyi.Application.Downloads;
using DownKyi.Core.BiliApi.VideoStream;
using DownKyi.Core.BiliApi.VideoStream.Models;
using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;
using DownKyi.Infrastructure.Downloads;
using DownKyi.Infrastructure.Time;
using DownKyi.Models;
using DownKyi.Services.Download;
using DownKyi.ViewModels.DownloadManager;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using static DownKyi.Tests.DownloadPipelineTestSupport;

namespace DownKyi.Tests;

public sealed class DownloadPlaybackResolverTests
{
    [Fact]
    public async Task AudioOnlyRefreshProbesWithValidQualityInsteadOfZero()
    {
        using var settings = new TestSettingsStore();
        string? requestedAddress = null;
        var client = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (request, _) =>
            {
                requestedAddress = request.RequestAddress;
                return Task.FromResult(
                    """
                    {"code":0,"data":{"dash":{"audio":[{"id":30280,"base_url":"https://media.invalid/audio.m4s"}]}}}
                    """);
            }
        };
        var context = CreateContext(
            settings.Store.Current,
            requestedContent: DownloadContentSelection.None with { Audio = true },
            audioCodecId: 30280,
            streamType: PlayStreamType.Video);
        var resolver = new DownloadPlaybackResolver(
            new TestWbiKeyProvider(),
            TimeProvider.System,
            client);

        var result = await resolver.ResolveAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains("qn=127", requestedAddress, StringComparison.Ordinal);
        Assert.DoesNotContain("qn=0", requestedAddress, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrdinaryResumeUsesWebpageBeforeApi()
    {
        using var settings = new TestSettingsStore();
        var requests = new List<string>();
        var client = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (request, _) =>
            {
                requests.Add(request.RequestAddress);
                return Task.FromResult(request.RequestAddress.StartsWith(
                    "https://www.bilibili.com/video/",
                    StringComparison.Ordinal)
                    ? """
                      <script>window.__playinfo__={"code":0,"data":{"dash":{"video":[{"id":80,"codecid":12,"base_url":"https://web.invalid/hevc"}],"audio":[]}}}</script>
                      """
                    : """
                      {"code":0,"data":{"dash":{"video":[{"id":80,"codecid":7,"base_url":"https://api.invalid/avc"}],"audio":[]}}}
                      """);
            }
        };
        var context = CreateContext(
            settings.Store.Current,
            requestedContent: DownloadContentSelection.None with { Video = true },
            resolutionId: 80,
            videoCodecName: "H.264/AVC",
            streamType: PlayStreamType.Video);
        var resolver = new DownloadPlaybackResolver(
            new TestWbiKeyProvider(),
            TimeProvider.System,
            client);

        var result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.TryGetValue(out var playback));
        Assert.Equal("https://api.invalid/avc", Assert.Single(playback.Dash.Video).BaseAddress);
        Assert.Equal(2, requests.Count);
    }

}
