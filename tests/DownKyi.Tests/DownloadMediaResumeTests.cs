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

public sealed class DownloadMediaResumeTests
{
    [Fact]
    public async Task MediaStageReusesCompletedStagingInsteadOfTransferringAgain()
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateVideoOnlyPlayUrl(), downloadAudio: false, downloadVideo: true);
        var staging = Path.Combine(Path.GetDirectoryName(fixture.Context.Input.OutputBasePath)!,
            ".downkyi", "staging", "test-session", "test-task");
        Directory.CreateDirectory(staging);
        fixture.Context.StagingDirectory = staging;
        var completedMedia = fixture.Context.WorkingBasePath + ".mp4";
        await File.WriteAllBytesAsync(completedMedia, [7, 8, 9], TestContext.Current.CancellationToken);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(completedMedia, fixture.Context.OutputMedia);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageRejectsVideoAndAudioRequestWhenAudioCollectionIsEmpty()
    {
        var playUrl = CreateVideoOnlyPlayUrl();
        playUrl.Dash.Audio = [];
        using var fixture = await MediaStageFixture.CreateAsync(
            playUrl,
            downloadAudio: true,
            downloadVideo: true).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Null(fixture.Context.AudioFile);
        Assert.Null(fixture.Context.VideoFile);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageReusesCompletedSelectedAudioWhenRefreshedPlaybackOmitsAudio()
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateVideoOnlyPlayUrl(),
            downloadAudio: true,
            downloadVideo: true).ConfigureAwait(true);
        var completedAudio = await fixture.AddCompletedAudioTransferAsync().ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(completedAudio.FilePath, fixture.Context.AudioFile);
        Assert.Equal(completedAudio.Key, fixture.Context.AudioTransferKey);
        Assert.NotNull(fixture.Context.VideoFile);
        Assert.Single(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageRefreshesExpiredVideoWithoutRequestingCompletedAudioAgain()
    {
        var initialPlayback = CreateVideoOnlyPlayUrl();
        initialPlayback.Dash.Audio =
        [
            new PlayUrlDashVideo
            {
                Id = 30280,
                Codecs = "mp4a.40.2",
                BaseAddress = "https://example.invalid/original-audio"
            }
        ];
        var playbackRequests = new List<string>();
        var apiClient = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (request, _) =>
            {
                playbackRequests.Add(request.RequestAddress);
                return Task.FromResult(request.RequestAddress.StartsWith(
                    "https://www.bilibili.com/bangumi/play/",
                    StringComparison.Ordinal)
                    ? """
                      <script>const playurlSSRData = {"code":0,"result":{"video_info":{"quality":80,"durl":[],"dash":{"video":[{"id":80,"codecid":7,"codecs":"avc1","base_url":"https://example.invalid/refreshed-video"}],"audio":[]}}}};</script>
                      """
                    : """
                      {"code":0,"result":{"video_info":{"quality":80,"durl":[],"dash":{"video":[{"id":80,"codecid":7,"codecs":"avc1","base_url":"https://example.invalid/refreshed-video"}],"audio":[]}}}}
                      """);
            }
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            initialPlayback,
            downloadAudio: true,
            downloadVideo: true,
            apiClient: apiClient,
            streamType: PlayStreamType.Bangumi,
            backendResults:
            [
                DownloadTransferResult.Failed(
                    DownloadTransferFailureKind.ExpiredAddress,
                    "download.transfer.http-403"),
                DownloadTransferResult.Succeeded()
            ]).ConfigureAwait(true);
        var completedAudio = await fixture.AddCompletedAudioTransferAsync().ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(playbackRequests);
        Assert.Equal(
            "https://www.bilibili.com/bangumi/play/ep3489",
            playbackRequests[0]);
        Assert.Equal(completedAudio.FilePath, fixture.Context.AudioFile);
        Assert.Equal(completedAudio.Key, fixture.Context.AudioTransferKey);
        Assert.Equal(2, fixture.Backend.Requests.Count);
        Assert.All(
            fixture.Backend.Requests,
            request => Assert.DoesNotContain(
                request.Urls,
                address => address.Contains("audio", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(
            "https://example.invalid/refreshed-video",
            Assert.Single(fixture.Backend.Requests[1].Urls));
    }

    [Fact]
    public async Task MediaStageReusesCompletedSelectedVideoWhenPlaybackOnlyProvidesPendingAudio()
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateAudioOnlyPlayUrl(),
            downloadAudio: true,
            downloadVideo: true).ConfigureAwait(true);
        var completedVideo = await fixture.AddCompletedVideoTransferAsync().ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(completedVideo.FilePath, fixture.Context.VideoFile);
        Assert.Equal(completedVideo.Key, fixture.Context.VideoTransferKey);
        var request = Assert.Single(fixture.Backend.Requests);
        Assert.Equal(
            "https://example.invalid/audio",
            Assert.Single(request.Urls));
    }

    [Fact]
    public async Task MediaStageDeletesInvalidCompletedTransferBeforeRevokingDurableKey()
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateVideoOnlyPlayUrl(),
            downloadAudio: false,
            downloadVideo: true,
            backendResults:
            [
                DownloadTransferResult.Failed(
                    DownloadTransferFailureKind.Permanent,
                    "download.transfer.test-failure")
            ]).ConfigureAwait(true);
        var invalidVideo = await fixture.AddCompletedUnusableVideoTransferAsync()
            .ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(invalidVideo.FilePath));
        var task = await fixture.GetTaskAsync().ConfigureAwait(true);
        Assert.DoesNotContain(invalidVideo.Key, task.Transfer.CompletedFileKeys);
        Assert.Single(fixture.Backend.Requests);
    }

}
