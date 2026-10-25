using DownKyi.Application.Downloads;
using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;
using DownKyi.Services.Download;
using DownKyi.ViewModels.DownloadManager;
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

        var result = await new ValidateStage().ExecuteAsync(
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

        var result = await new ValidateStage().ExecuteAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.validate.published-missing", result.Error?.Code);
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

            var result = await new ValidateStage()
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

        var result = await new ValidateStage().ExecuteAsync(
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

            var result = await new ValidateStage()
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

            var result = await new ValidateStage()
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

        var result = await new ValidateStage().ExecuteAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.validate.published-missing", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateStageAcceptsCompletedNonEmptyMediaWithoutDecodeInspection()
    {
        var mediaFile = Path.Combine(
            Path.GetTempPath(),
            $"downkyi-completed-media-{Guid.NewGuid():N}.mp4");
        try
        {
            await File.WriteAllBytesAsync(
                mediaFile,
                [1, 2, 3],
                TestContext.Current.CancellationToken);
            using var settings = new TestSettingsStore();
            var context = CreateContext(
                settings.Store.Current,
                DownloadContentSelection.None with { Video = true });
            context.OutputMedia = mediaFile;
            context.MediaSucceeded = true;

            var result = await new ValidateStage().ExecuteAsync(
                context,
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }
        finally
        {
            File.Delete(mediaFile);
        }
    }
}
