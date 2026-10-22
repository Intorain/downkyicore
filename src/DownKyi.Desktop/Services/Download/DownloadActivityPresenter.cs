using System;
using System.Threading;
using System.Threading.Tasks;
using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;
using DownKyi.Images;
using DownKyi.Models;
using DownKyi.Utils;
using DownKyi.ViewModels.DownloadManager;

namespace DownKyi.Services.Download;

internal sealed class DownloadActivityPresenter
{
    private readonly DownloadTaskProjectionStore _projectionStore;
    private readonly DownloadTaskStateWriter _stateWriter;

    public DownloadActivityPresenter(
        DownloadTaskProjectionStore projectionStore,
        DownloadTaskStateWriter stateWriter)
    {
        _projectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
        _stateWriter = stateWriter ?? throw new ArgumentNullException(nameof(stateWriter));
    }

    public void Reset(DownloadExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var downloading = GetProjection(context);
        downloading.DownloadStatusTitle = string.Empty;
        downloading.DownloadContent = string.Empty;
    }

    public Task ShowParsingAsync(
        DownloadExecutionContext context,
        CancellationToken cancellationToken)
    {
        return ShowActivityAsync(
            context,
            contentResourceKey: null,
            titleResourceKey: "Parsing",
            resetProgress: true,
            cancellationToken);
    }

    public void ShowDownloadingAudio(DownloadExecutionContext context)
    {
        ShowTransferActivity(context, "DownloadingAudio");
    }

    public void ShowDownloadingVideo(DownloadExecutionContext context)
    {
        ShowTransferActivity(context, "DownloadingVideo");
    }

    public Task ShowMuxingAsync(
        DownloadExecutionContext context,
        CancellationToken cancellationToken)
    {
        return ShowActivityAsync(
            context,
            "DownloadingVideo",
            "MixedFlow",
            resetProgress: false,
            cancellationToken);
    }

    public Task ShowConcatenatingAsync(
        DownloadExecutionContext context,
        CancellationToken cancellationToken)
    {
        return ShowActivityAsync(
            context,
            "DownloadingVideo",
            "ConcatVideos",
            resetProgress: false,
            cancellationToken);
    }

    public Task ShowDownloadingArtifactAsync(
        DownloadExecutionContext context,
        string contentResourceKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentResourceKey);
        return ShowActivityAsync(
            context,
            contentResourceKey,
            "WhileDownloading",
            resetProgress: false,
            cancellationToken);
    }

    public static DownloadFailure CreateRetryableFailure()
    {
        return new DownloadFailure(
            "download.runtime.failed",
            DictionaryResource.GetString("DownloadFailed"),
            true);
    }

    public static DownloadFailure CreateFailure(OperationError? error)
    {
        if (error == null)
        {
            return CreateRetryableFailure();
        }

        if (error.Kind == OperationErrorKind.NotFound)
        {
            return new DownloadFailure(
                error.Code,
                error.Message,
                false);
        }

        if (error.Code.StartsWith("download.artifact.subtitle.", StringComparison.Ordinal))
        {
            return new DownloadFailure(
                error.Code,
                DictionaryResource.GetString("SubtitleDownloadFailed"),
                true);
        }

        if (!TlsFailureClassifier.IsTlsErrorCode(error.Code))
        {
            return new DownloadFailure(
                error.Code,
                DictionaryResource.GetString("DownloadFailed"),
                true);
        }

        return new DownloadFailure(
            error.Code,
            DictionaryResource.GetString(TlsFailureClassifier.GetResourceKey(error.Code)),
            false);
    }

    public static string FormatFailureDetails(DownloadFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var resourceKey = GetFailureDetailsResourceKey(failure.Code);
        var code = IsSafeDiagnosticCode(failure.Code)
            ? failure.Code
            : DictionaryResource.GetString("DownloadFailureUnknownCode");
        return $"{DictionaryResource.GetString(resourceKey)}\n\n" +
               $"{DictionaryResource.GetString("DownloadFailureCode")}: {code}";
    }

    private static string GetFailureDetailsResourceKey(string code)
    {
        if (code == DownloadTaskStateWriter.LegacyBackendRemovedFailureCode)
        {
            return "DownloadFailureLegacyDetails";
        }

        if (code == "download.runtime.unavailable")
        {
            return "DownloadFailureRuntimeDetails";
        }

        if (TlsFailureClassifier.IsTlsErrorCode(code))
        {
            return "DownloadFailureTlsDetails";
        }

        if (code == "download.transfer.http-429")
        {
            return "DownloadFailureRateLimitDetails";
        }

        if (code == "download.transfer.http-403")
        {
            return "DownloadFailureAccessDetails";
        }

        if (code is "download.playback.selection-unavailable"
            or "download.media.recreate-task"
            or "download.resolve.recreate-task"
            or "download.media.contract"
            or "download.media.durl-audio-only"
            or "download.validate.no-resource"
            or "download.transfer.no-address"
            or "download.transfer.http-404")
        {
            return "DownloadFailureUnavailableDetails";
        }

        if (code is "download.transfer.disk"
            or "download.media.directory"
            or "download.resolve.directory"
            or "download.publish.collision"
            || code.EndsWith(".permission", StringComparison.Ordinal)
            || code.EndsWith(".io", StringComparison.Ordinal)
            || code.Contains(".cleanup", StringComparison.Ordinal))
        {
            return "DownloadFailureStorageDetails";
        }

        if (code is "download.transfer.invalid-media"
            or "download.media.durl-manifest"
            || code.StartsWith("download.validate.", StringComparison.Ordinal))
        {
            return "DownloadFailureInvalidOutputDetails";
        }

        if (code.StartsWith("download.artifact.subtitle.", StringComparison.Ordinal))
        {
            return "DownloadFailureSubtitleDetails";
        }

        if (code.StartsWith("download.artifact.", StringComparison.Ordinal))
        {
            return "DownloadFailureArtifactDetails";
        }

        if (code.StartsWith("download.mux.", StringComparison.Ordinal)
            || code.StartsWith("download.finalize.", StringComparison.Ordinal))
        {
            return "DownloadFailureProcessingDetails";
        }

        if (code is "download.transfer.network"
            or "download.transfer.timeout"
            or "download.transfer.http-408"
            or "download.transfer.resume-rejected"
            || code.StartsWith("download.transfer.http-5", StringComparison.Ordinal))
        {
            return "DownloadFailureNetworkDetails";
        }

        return "DownloadFailureGenericDetails";
    }

    private static bool IsSafeDiagnosticCode(string code)
    {
        if (code.Length is < 1 or > 100
            || !code.StartsWith("download.", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in code)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not ('.' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    public static string CreateDirectoryError(string path)
    {
        return $"{path}{DictionaryResource.GetString("DirectoryError")}";
    }

    private void ShowTransferActivity(
        DownloadExecutionContext context,
        string contentResourceKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        var downloading = GetProjection(context);
        downloading.DownloadStatusTitle = DictionaryResource.GetString("WhileDownloading");
        downloading.DownloadContent = DictionaryResource.GetString(contentResourceKey);
        downloading.DownloadingFileSize = string.Empty;
        downloading.Progress = 0;
        downloading.SpeedDisplay = string.Empty;
    }

    private async Task ShowActivityAsync(
        DownloadExecutionContext context,
        string? contentResourceKey,
        string titleResourceKey,
        bool resetProgress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var downloading = GetProjection(context);
        downloading.DownloadStatusTitle = DictionaryResource.GetString(titleResourceKey);
        downloading.DownloadContent = contentResourceKey == null
            ? string.Empty
            : DictionaryResource.GetString(contentResourceKey);
        downloading.DownloadingFileSize = string.Empty;
        downloading.SpeedDisplay = string.Empty;
        if (resetProgress)
        {
            downloading.Progress = 0;
        }

        await _stateWriter.UpdateActivityAsync(
            context.TaskId,
            downloading.DownloadContent,
            downloading.DownloadStatusTitle,
            cancellationToken).ConfigureAwait(true);
    }

    private DownloadingItem GetProjection(DownloadExecutionContext context)
    {
        return _projectionStore.GetRequiredDownloadingProjection(context.TaskId);
    }
}
