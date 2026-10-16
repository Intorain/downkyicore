using DownKyi.Core.BiliApi.VideoStream;
using DownKyi.Services.Download;
using Microsoft.Extensions.Logging.Abstractions;

using static DownKyi.Tests.DownloadPipelineTestSupport;

namespace DownKyi.Tests;

public sealed class ResolvePlaybackStageTests
{
    [Fact]
    public async Task UnavailablePlaybackReturnsSelectionFailureWithoutPublishingPlayUrl()
    {
        var client = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (_, _) =>
                throw new PlaybackUnavailableException("Selected media is unavailable.")
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateVideoOnlyPlayUrl(),
            downloadAudio: false,
            downloadVideo: true,
            apiClient: client);
        var stage = new ResolvePlaybackStage(
            new TestDesktopInteractionContext().Notifications,
            fixture.Presenter,
            new DownloadPlaybackResolver(new TestWbiKeyProvider(), TimeProvider.System, client),
            NullLogger<ResolvePlaybackStage>.Instance);

        var result = await stage.ExecuteAsync(fixture.Context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Null(fixture.Context.PlayUrl);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task CompletedDashTransferIsRestoredBeforePlaybackRefresh()
    {
        var playbackRequests = 0;
        var client = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (_, _) =>
            {
                playbackRequests++;
                throw new InvalidOperationException("Playback refresh was not needed.");
            }
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateAudioOnlyPlayUrl(),
            downloadAudio: true,
            downloadVideo: false,
            apiClient: client);
        var (key, filePath) = await fixture.AddCompletedAudioTransferAsync();

        // The stored completion exists, but a newly created stage has not restored it yet.
        fixture.Context.AudioTransferKey = null;
        fixture.Context.AudioFile = null;
        var stage = new ResolvePlaybackStage(
            new TestDesktopInteractionContext().Notifications,
            fixture.Presenter,
            new DownloadPlaybackResolver(new TestWbiKeyProvider(), TimeProvider.System, client),
            NullLogger<ResolvePlaybackStage>.Instance);

        var result = await stage.ExecuteAsync(fixture.Context, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(key, fixture.Context.AudioTransferKey);
        Assert.Equal(filePath, fixture.Context.AudioFile);
        Assert.Null(fixture.Context.PlayUrl);
        Assert.Equal(0, playbackRequests);
    }
}
