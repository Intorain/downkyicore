using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DownKyi.Core.FFmpeg;
using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;

namespace DownKyi.Services.Download;

internal sealed class MuxStage : IDownloadPipelineStage
{
    private readonly DownloadActivityPresenter _presenter;
    private readonly IFfmpegMediaMuxer _ffmpegProcessor;
    private readonly DownloadTaskStateWriter _stateWriter;
    private readonly DownloadTransferArtifactInvalidator _artifactInvalidator;

    public MuxStage(
        DownloadActivityPresenter presenter,
        IFfmpegMediaMuxer ffmpegProcessor,
        DownloadTaskStateWriter stateWriter,
        DownloadTransferArtifactInvalidator artifactInvalidator)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _ffmpegProcessor = ffmpegProcessor ?? throw new ArgumentNullException(nameof(ffmpegProcessor));
        _stateWriter = stateWriter ?? throw new ArgumentNullException(nameof(stateWriter));
        _artifactInvalidator = artifactInvalidator
            ?? throw new ArgumentNullException(nameof(artifactInvalidator));
    }

    public string Name => nameof(MuxStage);

    public async Task<OperationResult<DownloadStageResult>> ExecuteAsync(
        DownloadExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureActive(cancellationToken);
        if (!context.NeedsMedia)
        {
            context.MediaSucceeded = true;
            return DownloadStageResult.Success(Name);
        }

        if (context.TryReuseStagedMedia())
        {
            return DownloadStageResult.Success(Name);
        }

        return context.MediaKind switch
        {
            DownloadMediaKind.Dash => await MuxDashAsync(
                context,
                cancellationToken).ConfigureAwait(true),
            DownloadMediaKind.Durl => await MuxDurlAsync(
                context,
                cancellationToken).ConfigureAwait(true),
            _ => DownloadStageResult.Failure(
                "download.mux.media-kind",
                "The resolved media type cannot be finalized.")
        };
    }

    private async Task<OperationResult<DownloadStageResult>> MuxDashAsync(
        DownloadExecutionContext context,
        CancellationToken cancellationToken)
    {
        await _presenter.ShowMuxingAsync(context, cancellationToken).ConfigureAwait(true);
        var finalFile = DownloadOutputLayout.GetDashMediaPath(
            context.WorkingBasePath,
            context.VideoFile != null,
            context.Input.Metadata.AudioCodec.Id,
            context.Input.VideoSettings.IsTranscodingAacToMp3);
        var result = await _ffmpegProcessor.MergeMediaAsync(
            context.Input.VideoSettings,
            context.AudioFile,
            context.VideoFile,
            finalFile,
            overwriteDestination: false,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Excluded,
            cancellationToken).ConfigureAwait(true);
        var invalidation = result.Succeeded
            ? DownloadTransferArtifactInvalidationOutcome.None
            : await InvalidateSourcesAsync(
                context,
                result,
                cancellationToken).ConfigureAwait(true);
        await DownloadOutputRecorder.RecordFileSizeAsync(
            context.TaskId,
            result.Succeeded ? finalFile : null,
            _stateWriter,
            cancellationToken).ConfigureAwait(true);
        context.OutputMedia = result.Succeeded ? finalFile : null;
        context.MediaSucceeded = result.Succeeded;
        return result.Succeeded
            ? DownloadStageResult.Success(Name)
            : DownloadStageResult.Failure(
                GetFailureCode("download.mux.dash", invalidation),
                "Audio and video streams could not be finalized.");
    }

    private async Task<OperationResult<DownloadStageResult>> MuxDurlAsync(
        DownloadExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.DurlDownloads.Count == 0)
        {
            return DownloadStageResult.Failure(
                "download.mux.durl-input",
                "Segmented media files are unavailable.");
        }

        if (context.DurlDownloads.Count == 1)
        {
            await _presenter.ShowMuxingAsync(context, cancellationToken).ConfigureAwait(true);
            var finalFile = DownloadOutputLayout.GetDurlMediaPath(context.WorkingBasePath);
            var mergeResult = await _ffmpegProcessor.MergeMediaAsync(
                context.Input.VideoSettings,
                audio: null,
                video: context.DurlDownloads[0].FilePath,
                destination: finalFile,
                overwriteDestination: false,
                embeddedAudioMode: GetEmbeddedAudioMode(context),
                cancellationToken).ConfigureAwait(true);
            var singleInvalidation = mergeResult.Succeeded
                ? DownloadTransferArtifactInvalidationOutcome.None
                : await InvalidateSourcesAsync(
                    context,
                    mergeResult,
                    cancellationToken).ConfigureAwait(true);
            await DownloadOutputRecorder.RecordFileSizeAsync(
                context.TaskId,
                mergeResult.Succeeded ? finalFile : null,
                _stateWriter,
                cancellationToken).ConfigureAwait(true);
            context.OutputMedia = mergeResult.Succeeded ? finalFile : null;
            context.MediaSucceeded = mergeResult.Succeeded;
            return mergeResult.Succeeded
                ? DownloadStageResult.Success(Name)
                : DownloadStageResult.Failure(
                    GetFailureCode("download.mux.durl", singleInvalidation),
                    "The media segment could not be finalized.");
        }

        await _presenter.ShowConcatenatingAsync(context, cancellationToken).ConfigureAwait(true);
        var outputPath = DownloadOutputLayout.GetDurlMediaPath(context.WorkingBasePath);
        var segments = context.DurlDownloads
            .OrderBy(download => download.Durl.Order)
            .Select(download => new FfmpegConcatSegment(
                download.Durl.Order,
                download.FilePath,
                TimeSpan.FromMilliseconds(download.Durl.Length)))
            .ToArray();
        var result = await _ffmpegProcessor.ConcatDurlVideosAsync(
            context.Input.VideoSettings,
            segments,
            outputPath,
            overwriteDestination: false,
            embeddedAudioMode: GetEmbeddedAudioMode(context),
            cancellationToken: cancellationToken).ConfigureAwait(true);
        var invalidation = result.Succeeded
            ? DownloadTransferArtifactInvalidationOutcome.None
            : await InvalidateSourcesAsync(
                context,
                result,
                cancellationToken).ConfigureAwait(true);
        await DownloadOutputRecorder.RecordFileSizeAsync(
            context.TaskId,
            result.Succeeded ? result.OutputPath : null,
            _stateWriter,
            cancellationToken).ConfigureAwait(true);
        context.OutputMedia = result.Succeeded ? result.OutputPath : null;
        context.MediaSucceeded = result.Succeeded;
        return result.Succeeded
            ? DownloadStageResult.Success(Name)
            : DownloadStageResult.Failure(
                GetFailureCode("download.mux.concat", invalidation),
                "Segmented media could not be concatenated.");
    }

    private static FfmpegEmbeddedAudioMode GetEmbeddedAudioMode(
        DownloadExecutionContext context) =>
        context.NeedsAudio
            ? FfmpegEmbeddedAudioMode.Required
            : FfmpegEmbeddedAudioMode.Excluded;

    private async Task<DownloadTransferArtifactInvalidationOutcome> InvalidateSourcesAsync(
        DownloadExecutionContext context,
        FfmpegOperationResult operationResult,
        CancellationToken cancellationToken)
    {
        if (operationResult.FailureKind != FfmpegOperationFailureKind.InvalidInput ||
            operationResult.InvalidInputPaths.Count == 0)
        {
            return DownloadTransferArtifactInvalidationOutcome.None;
        }

        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var invalidPaths = operationResult.InvalidInputPaths
            .Select(Path.GetFullPath)
            .ToHashSet(pathComparer);
        var sources = new[]
            {
                (Key: context.AudioTransferKey, FilePath: context.AudioFile),
                (Key: context.VideoTransferKey, FilePath: context.VideoFile)
            }
            .Concat(context.DurlDownloads.Select(download =>
                (Key: (string?)download.TransferKey, FilePath: (string?)download.FilePath)));
        var invalidSources = sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Key) &&
                             !string.IsNullOrWhiteSpace(source.FilePath) &&
                             invalidPaths.Contains(Path.GetFullPath(source.FilePath)))
            .Select(source => new DownloadTransferArtifact(source.Key!, source.FilePath!))
            .Distinct()
            .ToArray();
        if (invalidSources.Length == 0)
        {
            return DownloadTransferArtifactInvalidationOutcome.None;
        }

        return await _artifactInvalidator.InvalidateAsync(
            context.TaskId,
            context.StagingDirectory,
            invalidSources,
            cancellationToken).ConfigureAwait(true);
    }

    private static string GetFailureCode(
        string defaultCode,
        DownloadTransferArtifactInvalidationOutcome invalidation)
    {
        return invalidation switch
        {
            DownloadTransferArtifactInvalidationOutcome.Invalidated => "download.mux.invalid-source",
            DownloadTransferArtifactInvalidationOutcome.CleanupFailed =>
                "download.mux.invalid-source-cleanup",
            _ => defaultCode
        };
    }

}
