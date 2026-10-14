using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DownKyi.Core.FFmpeg;
using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;

namespace DownKyi.Services.Download;

internal sealed class ValidateStage : IDownloadPipelineStage
{
    private readonly IFfmpegMediaStreamValidator _mediaStreamValidator;

    public ValidateStage(IFfmpegMediaStreamValidator mediaStreamValidator)
    {
        _mediaStreamValidator = mediaStreamValidator
            ?? throw new ArgumentNullException(nameof(mediaStreamValidator));
    }

    public string Name => nameof(ValidateStage);

    public async Task<OperationResult<DownloadStageResult>> ExecuteAsync(
        DownloadExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureActive(cancellationToken);
        DownloadActionResultEvaluator.Evaluate(context);
        if (context.NeedsMedia)
        {
            var hasPublishedMedia = context.PublishedArtifacts.TryGetValue(
                "media",
                out var publishedMedia);
            var mediaFile = hasPublishedMedia ? publishedMedia : context.OutputMedia;
            if (!context.MediaSucceeded ||
                (hasPublishedMedia
                    ? !context.HasUsablePublishedMedia()
                    : !File.Exists(mediaFile)))
            {
                return DownloadStageResult.Failure(
                    "download.validate.media",
                    "The finalized media file is missing or invalid.");
            }

            if ((context.NeedsAudio || context.NeedsVideo) &&
                !await _mediaStreamValidator.ValidateRequiredStreamsAsync(
                    mediaFile!,
                    context.NeedsAudio,
                    context.NeedsVideo,
                    ResolveExpectedVideoDuration(context),
                    cancellationToken).ConfigureAwait(true))
            {
                return DownloadStageResult.Failure(
                    "download.validate.media",
                    "The finalized media file has a missing or undecodable required audio or video stream.");
            }

        }

        if (context.NeedsDanmaku &&
            DownloadArtifactsStage.GetDanmakuOutputs(
                    context.WorkingBasePath,
                    context.Input.DanmakuSettings.OutputFormat)
                .Any(output => !context.HasPublished(output.Key) && !File.Exists(output.File)))
        {
            return DownloadStageResult.Failure(
                "download.validate.danmaku",
                "The requested danmaku file was not created.");
        }

        if (context.NeedsSubtitle && !HasCompleteSubtitleOutput(context))
        {
            return DownloadStageResult.Failure(
                "download.validate.subtitle",
                "One or more requested subtitle files were not created.");
        }

        if (context.PublishedArtifacts.Values.Any(path => !File.Exists(path)))
        {
            return DownloadStageResult.Failure(
                "download.validate.published-missing",
                "A recorded published artifact is missing.");
        }

        if (!context.ActionResults.HasSucceeded)
        {
            return DownloadStageResult.Failure(
                "download.validate.no-resource",
                "None of the requested download actions produced an output.",
                OperationErrorKind.NotFound);
        }

        return DownloadStageResult.Success(Name);
    }

    private static TimeSpan ResolveExpectedVideoDuration(DownloadExecutionContext context)
    {
        if (context.MediaKind == DownloadMediaKind.Dash && context.PlayUrl?.Dash.Duration > 0)
        {
            return CreateDurationFromSeconds(context.PlayUrl.Dash.Duration);
        }

        if (context.MediaKind == DownloadMediaKind.Durl)
        {
            var playUrlDuration = SumDurlDuration(
                context.PlayUrl?.Durl.Select(item => item.Length));
            if (playUrlDuration > TimeSpan.Zero)
            {
                return playUrlDuration;
            }

            var downloadedDuration = SumDurlDuration(
                context.DurlDownloads.Select(item => item.Durl.Length));
            if (downloadedDuration > TimeSpan.Zero)
            {
                return downloadedDuration;
            }
        }

        return ParseDurationText(context.Input.Metadata.DurationText);
    }

    private static TimeSpan SumDurlDuration(IEnumerable<long>? lengths)
    {
        if (lengths == null)
        {
            return TimeSpan.Zero;
        }

        long totalMilliseconds = 0;
        try
        {
            checked
            {
                foreach (var length in lengths)
                {
                    if (length <= 0)
                    {
                        return TimeSpan.Zero;
                    }

                    totalMilliseconds += length;
                }
            }
        }
        catch (OverflowException)
        {
            return TimeSpan.Zero;
        }

        return totalMilliseconds > 0 && totalMilliseconds <= TimeSpan.MaxValue.TotalMilliseconds
            ? TimeSpan.FromMilliseconds(totalMilliseconds)
            : TimeSpan.Zero;
    }

    private static TimeSpan ParseDurationText(string durationText)
    {
        if (string.IsNullOrWhiteSpace(durationText))
        {
            return TimeSpan.Zero;
        }

        var trimmedDuration = durationText.Trim();
        if (trimmedDuration.Contains(':', StringComparison.Ordinal))
        {
            return ParseColonDuration(trimmedDuration);
        }

        long totalSeconds = 0;
        long component = 0;
        var hasDigits = false;
        try
        {
            checked
            {
                foreach (var character in trimmedDuration)
                {
                    if (char.IsAsciiDigit(character))
                    {
                        component = component * 10 + character - '0';
                        hasDigits = true;
                        continue;
                    }

                    if (!hasDigits)
                    {
                        return TimeSpan.Zero;
                    }

                    var multiplier = character switch
                    {
                        'h' => 60L * 60L,
                        'm' => 60L,
                        's' => 1L,
                        _ => 0L
                    };
                    if (multiplier == 0)
                    {
                        return TimeSpan.Zero;
                    }

                    totalSeconds += component * multiplier;
                    component = 0;
                    hasDigits = false;
                }
            }
        }
        catch (OverflowException)
        {
            return TimeSpan.Zero;
        }

        return !hasDigits
            ? CreateDurationFromSeconds(totalSeconds)
            : TimeSpan.Zero;
    }

    private static TimeSpan ParseColonDuration(string durationText)
    {
        var components = durationText.Split(':');
        if (components.Length is < 2 or > 3 ||
            components.Any(component => component.Length == 0) ||
            !components.All(component => long.TryParse(
                component,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _)))
        {
            return TimeSpan.Zero;
        }

        var hours = components.Length == 3
            ? long.Parse(components[0], CultureInfo.InvariantCulture)
            : 0;
        var minutes = long.Parse(components[^2], CultureInfo.InvariantCulture);
        var seconds = long.Parse(components[^1], CultureInfo.InvariantCulture);
        if (minutes >= 60 || seconds >= 60)
        {
            return TimeSpan.Zero;
        }

        try
        {
            checked
            {
                return CreateDurationFromSeconds(hours * 60L * 60L + minutes * 60L + seconds);
            }
        }
        catch (OverflowException)
        {
            return TimeSpan.Zero;
        }
    }

    private static TimeSpan CreateDurationFromSeconds(long seconds) =>
        seconds > 0 && seconds <= TimeSpan.MaxValue.TotalSeconds
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;

    private static bool HasCompleteSubtitleOutput(DownloadExecutionContext context)
    {
        if (context.SubtitleTrackFiles == null)
        {
            return context.SubtitleFiles == null
                   || context.SubtitleFiles.All(subtitle =>
                       context.HasPublished("subtitle:" + Path.GetFileName(subtitle))
                       || File.Exists(subtitle));
        }

        if (context.DefaultSubtitleFile != null
            && !context.HasPublished(DownloadArtifactWriter.DefaultSubtitleArtifactKey)
            && !DownloadFileIntegrity.Check(context.DefaultSubtitleFile).IsUsable)
        {
            return false;
        }

        if (context.Input.RequestedContent.DefaultSubtitleTrackId != null
            && context.DefaultSubtitleFile == null
            && !context.HasPublished(DownloadArtifactWriter.DefaultSubtitleArtifactKey))
        {
            return false;
        }

        IEnumerable<long> trackIds =
            context.Input.RequestedContent.SelectedSubtitleTrackIds is { } selectedTrackIds
                ? selectedTrackIds
                : context.SubtitleTrackFiles.Keys;
        return trackIds.All(trackId =>
            context.SubtitleTrackFiles.TryGetValue(trackId, out var stagedFile)
            && HasUsableSubtitleTrack(context, trackId, stagedFile));
    }

    private static bool HasUsableSubtitleTrack(
        DownloadExecutionContext context,
        long trackId,
        string stagedFile)
    {
        var artifactKey = DownloadArtifactWriter.GetSubtitleArtifactKey(trackId);
        return context.PublishedArtifacts.TryGetValue(artifactKey, out var publishedFile)
               && DownloadFileIntegrity.Check(publishedFile).IsUsable
               || DownloadFileIntegrity.Check(stagedFile).IsUsable;
    }
}
