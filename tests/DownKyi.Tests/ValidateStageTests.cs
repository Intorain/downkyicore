using DownKyi.Application.Bilibili;
using DownKyi.Application.Downloads;
using DownKyi.Core.BiliApi.VideoStream;
using DownKyi.Core.BiliApi.VideoStream.Models;
using DownKyi.Core.Settings;
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

public sealed class ValidateStageTests
{
    [Fact]
    public void SelectionUnavailableFailureRemainsExplicitAndNonTransient()
    {
        const string message =
            "The selected playback quality, codec, or audio is no longer available.";
        var error = new OperationError(
            "download.playback.selection-unavailable",
            message,
            OperationErrorKind.NotFound);

        var failure = DownloadActivityPresenter.CreateFailure(error);

        Assert.Equal("download.playback.selection-unavailable", failure.Code);
        Assert.NotEqual("download.runtime.failed", failure.Code);
        Assert.Equal(message, failure.Message);
        Assert.False(failure.IsTransient);
    }

    [Fact]
    public async Task ValidateStageRejectsMissingRequestedMedia()
    {
        using var settings = new TestSettingsStore();
        var context = CreateContext(settings.Store.Current);
        context.MediaKind = DownloadMediaKind.Dash;
        context.MediaSucceeded = true;
        context.OutputMedia = Path.Combine(
            Path.GetTempPath(),
            $"missing-downkyi-media-{Guid.NewGuid():N}.mp4");

        var result = await new ValidateStage(new StubFfmpegMediaStreamValidator()).ExecuteAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.validate.media", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateStageRejectsMissingRecordedPublishedMediaOnRetry()
    {
        using var settings = new TestSettingsStore();
        var context = CreateContext(settings.Store.Current);
        context.PublishedArtifacts.Add("media", Path.Combine(
            Path.GetTempPath(), $"missing-published-{Guid.NewGuid():N}.mp4"));

        var result = await new ValidateStage(new StubFfmpegMediaStreamValidator()).ExecuteAsync(
            context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.validate.media", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateStageClassifiesCorruptUnpublishedArtifactForDiscard()
    {
        var artifact = Path.Combine(
            Path.GetTempPath(),
            $"invalid-unpublished-{Guid.NewGuid():N}.nfo");
        try
        {
            await File.WriteAllBytesAsync(
                artifact,
                [],
                TestContext.Current.CancellationToken);
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                requestedContent: DownloadContentSelection.None with { Cover = true });
            context.RecordUnpublishedArtifact("nfo", artifact);

            var result = await new ValidateStage(new StubFfmpegMediaStreamValidator())
                .ExecuteAsync(context, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("download.validate.unpublished-invalid", result.Error?.Code);
            Assert.Equal(OperationErrorKind.Validation, result.Error?.Kind);
        }
        finally
        {
            File.Delete(artifact);
        }
    }

    [Fact]
    public async Task ValidateStageRejectsSubtitleOnlyTaskWithoutFiles()
    {
        using var settings = new TestSettingsStore();
        var context = CreateContext(
            settings.Store.Current,
            requestedContent: DownloadContentSelection.None with { Subtitle = true });
        context.SubtitleFiles = null;

        var result = await new ValidateStage(new StubFfmpegMediaStreamValidator()).ExecuteAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.validate.no-resource", result.Error?.Code);
        Assert.Equal(OperationErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ValidateStageSucceedsWhenCoverProducesOutputAndSubtitleHasNoResource()
    {
        using var settings = new TestSettingsStore();
        var context = CreateContext(
            settings.Store.Current,
            requestedContent: DownloadContentSelection.None with
            {
                Subtitle = true,
                Cover = true
            });
        var cover = $"{context.WorkingBasePath}.{Guid.NewGuid():N}.jpg";
        try
        {
            await File.WriteAllBytesAsync(
                cover,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            context.CoverFile = cover;
            context.SubtitleFiles = null;

            var result = await new ValidateStage(new StubFfmpegMediaStreamValidator())
                .ExecuteAsync(context, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                DownloadActionResultStatus.NoResource,
                context.ActionResults.Results[DownloadActionKind.Subtitle]);
            Assert.Equal(
                DownloadActionResultStatus.Succeeded,
                context.ActionResults.Results[DownloadActionKind.Cover]);
        }
        finally
        {
            File.Delete(cover);
        }
    }

    [Fact]
    public async Task ValidateStageRejectsPartialSelectedSubtitleTrackCoverage()
    {
        using var settings = new TestSettingsStore();
        var context = CreateContext(
            settings.Store.Current,
            requestedContent: DownloadContentSelection.None with
            {
                Subtitle = true,
                SelectedSubtitleTrackIds = [11, 22]
            });
        var subtitle = Path.Combine(
            Path.GetTempPath(),
            $"partial-subtitle-{Guid.NewGuid():N}.srt");
        try
        {
            await File.WriteAllTextAsync(
                subtitle,
                "1\n00:00:00,000 --> 00:00:01,000\nhello",
                TestContext.Current.CancellationToken);
            context.SubtitleFiles = [subtitle];
            context.SubtitleTrackFiles = new Dictionary<long, string> { [11] = subtitle };

            var result = await new ValidateStage(new StubFfmpegMediaStreamValidator())
                .ExecuteAsync(context, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("download.validate.subtitle", result.Error?.Code);
        }
        finally
        {
            File.Delete(subtitle);
        }
    }

    [Theory]
    [InlineData("subtitle:missing.srt")]
    [InlineData("cover")]
    [InlineData("nfo")]
    public async Task ValidateStageRejectsMissingRecordedArtifactEvenWhenNewResponseIsEmpty(string key)
    {
        using var settings = new TestSettingsStore();
        var context = CreateContext(
            settings.Store.Current,
            requestedContent: DownloadContentSelection.None with { Subtitle = true });
        context.PublishedArtifacts[key] = Path.Combine(
            Path.GetTempPath(), $"missing-published-{Guid.NewGuid():N}.srt");
        context.SubtitleFiles = null;

        var result = await new ValidateStage(new StubFfmpegMediaStreamValidator()).ExecuteAsync(
            context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.validate.published-missing", result.Error?.Code);
    }

    [Theory]
    [InlineData(true, true, ".mp4", false, false)]
    [InlineData(true, true, ".mp4", true, true)]
    [InlineData(false, true, ".mp4", true, true)]
    [InlineData(true, false, ".mp3", true, true)]
    [InlineData(true, false, ".aac", true, true)]
    [InlineData(true, false, ".flac", true, true)]
    public async Task ValidateStageAppliesRequestedStreamShapeToReusedStagedMedia(
        bool requestAudio,
        bool requestVideo,
        string extension,
        bool canDecodeRequiredStreams,
        bool expectedSuccess)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"downkyi-stream-shape-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                DownloadContentSelection.None with
                {
                    Audio = requestAudio,
                    Video = requestVideo
                });
            context.StagingDirectory = directory;
            var stagedMedia = context.WorkingBasePath + extension;
            await File.WriteAllBytesAsync(
                stagedMedia,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            Assert.True(context.TryReuseStagedMedia());
            var validator = new StubFfmpegMediaStreamValidator(canDecodeRequiredStreams);

            var result = await new ValidateStage(validator).ExecuteAsync(
                context,
                TestContext.Current.CancellationToken);

            Assert.Equal(expectedSuccess, result.IsSuccess);
            if (!expectedSuccess)
            {
                Assert.Equal("download.validate.media", result.Error?.Code);
            }

            var call = Assert.Single(validator.Calls);
            Assert.Equal(stagedMedia, call.MediaFile);
            Assert.Equal(requestAudio, call.RequireAudio);
            Assert.Equal(requestVideo, call.RequireVideo);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateStageUsesDashDurationForThreePointVideoValidation()
    {
        var mediaFile = Path.Combine(
            Path.GetTempPath(),
            $"downkyi-validated-dash-{Guid.NewGuid():N}.mp4");
        try
        {
            await File.WriteAllBytesAsync(
                mediaFile,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                DownloadContentSelection.None with { Video = true },
                playUrl: new PlayUrl
                {
                    Dash = new PlayUrlDash { Duration = 1_355 }
                });
            context.MediaKind = DownloadMediaKind.Dash;
            context.OutputMedia = mediaFile;
            context.MediaSucceeded = true;
            var validator = new StubFfmpegMediaStreamValidator();

            var result = await new ValidateStage(validator).ExecuteAsync(
                context,
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(validator.Calls);
            Assert.True(call.RequireVideo);
            Assert.Equal(TimeSpan.FromSeconds(1_355), call.ExpectedVideoDuration);
        }
        finally
        {
            File.Delete(mediaFile);
        }
    }

    [Theory]
    [InlineData("22m35s")]
    [InlineData("22:35")]
    [InlineData("00:22:35")]
    public async Task ValidateStageUsesPersistedDurationWhenPlaybackWasReused(string durationText)
    {
        var mediaFile = Path.Combine(
            Path.GetTempPath(),
            $"downkyi-validated-retry-{Guid.NewGuid():N}.mp4");
        try
        {
            await File.WriteAllBytesAsync(
                mediaFile,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                DownloadContentSelection.None with { Video = true },
                durationText: durationText);
            context.OutputMedia = mediaFile;
            context.MediaSucceeded = true;
            var validator = new StubFfmpegMediaStreamValidator();

            var result = await new ValidateStage(validator).ExecuteAsync(
                context,
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(validator.Calls);
            Assert.Equal(TimeSpan.FromSeconds(1_355), call.ExpectedVideoDuration);
        }
        finally
        {
            File.Delete(mediaFile);
        }
    }

    [Fact]
    public async Task ValidateStageRequiresThreePointVideoValidationForMultiSegmentDurl()
    {
        var mediaFile = Path.Combine(
            Path.GetTempPath(),
            $"downkyi-validated-durl-{Guid.NewGuid():N}.mp4");
        try
        {
            await File.WriteAllBytesAsync(
                mediaFile,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                DownloadContentSelection.None with
                {
                    Video = true,
                    MediaKind = DownloadMediaKind.Durl
                });
            context.MediaKind = DownloadMediaKind.Durl;
            context.OutputMedia = mediaFile;
            context.MediaSucceeded = true;
            context.DurlDownloads =
            [
                new DurlDownloadResult(
                    new PlayUrlDurl { Order = 1, Length = 10_000 },
                    "segment-1",
                    "key-1"),
                new DurlDownloadResult(
                    new PlayUrlDurl { Order = 2, Length = 20_000 },
                    "segment-2",
                    "key-2")
            ];
            var validator = new StubFfmpegMediaStreamValidator(result: false);

            var result = await new ValidateStage(validator).ExecuteAsync(
                context,
                TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("download.validate.media", result.Error?.Code);
            var call = Assert.Single(validator.Calls);
            Assert.False(call.RequireAudio);
            Assert.True(call.RequireVideo);
            Assert.Equal(TimeSpan.FromSeconds(30), call.ExpectedVideoDuration);
        }
        finally
        {
            File.Delete(mediaFile);
        }
    }

    [Fact]
    public async Task ValidateStageChecksAudioAndThreeVideoPointsForMultiSegmentDurl()
    {
        var mediaFile = Path.Combine(
            Path.GetTempPath(),
            $"downkyi-validated-durl-audio-{Guid.NewGuid():N}.mp4");
        try
        {
            await File.WriteAllBytesAsync(
                mediaFile,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                DownloadContentSelection.None with
                {
                    Audio = true,
                    Video = true,
                    MediaKind = DownloadMediaKind.Durl
                });
            context.MediaKind = DownloadMediaKind.Durl;
            context.OutputMedia = mediaFile;
            context.MediaSucceeded = true;
            context.DurlDownloads =
            [
                new DurlDownloadResult(
                    new PlayUrlDurl { Order = 1, Length = 10_000 },
                    "segment-1",
                    "key-1"),
                new DurlDownloadResult(
                    new PlayUrlDurl { Order = 2, Length = 20_000 },
                    "segment-2",
                    "key-2")
            ];
            var validator = new StubFfmpegMediaStreamValidator();

            var result = await new ValidateStage(validator).ExecuteAsync(
                context,
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(validator.Calls);
            Assert.True(call.RequireAudio);
            Assert.True(call.RequireVideo);
            Assert.Equal(TimeSpan.FromSeconds(30), call.ExpectedVideoDuration);
        }
        finally
        {
            File.Delete(mediaFile);
        }
    }

    [Fact]
    public async Task ValidateStageRequiresDecodeEvidenceForReusedDurlMedia()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"downkyi-reused-durl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                DownloadContentSelection.None with
                {
                    Video = true,
                    MediaKind = DownloadMediaKind.Durl
                });
            context.MediaKind = DownloadMediaKind.Durl;
            context.StagingDirectory = directory;
            var stagedMedia = context.WorkingBasePath + ".mp4";
            await File.WriteAllBytesAsync(
                stagedMedia,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            Assert.True(context.TryReuseStagedMedia());
            var validator = new StubFfmpegMediaStreamValidator(result: false);

            var result = await new ValidateStage(validator).ExecuteAsync(
                context,
                TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            var call = Assert.Single(validator.Calls);
            Assert.False(call.RequireAudio);
            Assert.True(call.RequireVideo);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateStageRequiresDecodeEvidenceForPublishedMedia()
    {
        var mediaFile = Path.Combine(
            Path.GetTempPath(),
            $"downkyi-published-media-{Guid.NewGuid():N}.mp3");
        try
        {
            await File.WriteAllBytesAsync(
                mediaFile,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                DownloadContentSelection.None with { Audio = true });
            context.PublishedArtifacts["media"] = mediaFile;
            var validator = new StubFfmpegMediaStreamValidator(result: false);

            var result = await new ValidateStage(validator).ExecuteAsync(
                context,
                TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            var call = Assert.Single(validator.Calls);
            Assert.Equal(mediaFile, call.MediaFile);
            Assert.True(call.RequireAudio);
            Assert.False(call.RequireVideo);
        }
        finally
        {
            File.Delete(mediaFile);
        }
    }

}
