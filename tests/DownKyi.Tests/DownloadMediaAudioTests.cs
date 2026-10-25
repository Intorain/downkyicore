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

public sealed class DownloadMediaAudioTests
{
    [Fact]
    public async Task AudioOnlyMediaStageTransfersOnlySelectedAudio()
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateAudioOnlyPlayUrl(),
            downloadAudio: true,
            downloadVideo: false).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(fixture.Context.AudioFile);
        Assert.Null(fixture.Context.VideoFile);
        var request = Assert.Single(fixture.Backend.Requests);
        Assert.Equal("https://example.invalid/audio", Assert.Single(request.Urls));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MediaStageDoesNotSuppressMissingAudioForIncompleteOrUnusableTransfer(
        bool markCompleted,
        bool writeUsableFile)
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateVideoOnlyPlayUrl(),
            downloadAudio: true,
            downloadVideo: true).ConfigureAwait(true);
        await fixture.AddAudioTransferAsync(markCompleted, writeUsableFile).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Null(fixture.Context.AudioFile);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageRejectsAudioOnlyRequestWhenSourceHasNoAudio()
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateVideoOnlyPlayUrl(),
            downloadAudio: true,
            downloadVideo: false).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageRejectsMissingSelectedAudioWhenSourceHasOtherAudio()
    {
        var playUrl = CreateVideoOnlyPlayUrl();
        playUrl.Dash.Audio =
        [
            new PlayUrlDashVideo
            {
                Id = 30280,
                Codecs = "mp4a.40.2",
                BaseAddress = "https://example.invalid/audio"
            }
        ];
        using var fixture = await MediaStageFixture.CreateAsync(
            playUrl,
            downloadAudio: true,
            downloadVideo: true,
            selectedAudioId: 30232).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public void MediaStageSelectsDolbyWhenOrdinaryAudioIsUnavailable()
    {
        using var settings = new TestSettingsStore();
        var dolby = new PlayUrlDashVideo
        {
            Id = 30250,
            BaseAddress = "https://example.invalid/dolby"
        };
        var context = CreateContext(
            settings.Store.Current,
            audioCodecId: 30250,
            playUrl: new PlayUrl
            {
                Dash = new PlayUrlDash
                {
                    Dolby = new PlayUrlDashDolby { Audio = [dolby] }
                }
            });

        Assert.Same(dolby, DownloadMediaStage.SelectAudio(context));
    }

    [Fact]
    public void MediaStageSelectsFlacWhenOrdinaryAudioIsUnavailable()
    {
        using var settings = new TestSettingsStore();
        var flac = new PlayUrlDashVideo
        {
            Id = 30251,
            BaseAddress = "https://example.invalid/flac"
        };
        var context = CreateContext(
            settings.Store.Current,
            audioCodecId: 30251,
            playUrl: new PlayUrl
            {
                Dash = new PlayUrlDash
                {
                    Flac = new PlayUrlDashFlac { Audio = flac }
                }
            });

        Assert.Same(flac, DownloadMediaStage.SelectAudio(context));
    }

    [Theory]
    [InlineData(30250)]
    [InlineData(30251)]
    public void MediaStageFallsBackToOrdinaryAudioWhenSpecialDescriptorIsUnavailable(
        int audioCodecId)
    {
        using var settings = new TestSettingsStore();
        var ordinary = new PlayUrlDashVideo
        {
            Id = audioCodecId,
            BaseAddress = "https://example.invalid/ordinary-audio"
        };
        var context = CreateContext(
            settings.Store.Current,
            audioCodecId: audioCodecId,
            playUrl: new PlayUrl
            {
                Dash = new PlayUrlDash { Audio = [ordinary] }
            });

        Assert.Same(ordinary, DownloadMediaStage.SelectAudio(context));
    }
}
