using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DownKyi.Application.Diagnostics;
using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;
using DownKyi.ViewModels.DownloadManager;
using Microsoft.Extensions.Logging;

namespace DownKyi.Services.Download;

internal sealed class DownloadTaskFileService
{
    private readonly ILogger<DownloadTaskFileService> _logger;
    private readonly DownloadTaskStaging? _staging;
    private readonly DownloadTaskStateWriter? _stateWriter;

    public DownloadTaskFileService(
        ILogger<DownloadTaskFileService> logger,
        DownloadTaskStaging? staging = null,
        DownloadTaskStateWriter? stateWriter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _staging = staging;
        _stateWriter = stateWriter;
    }

    public Task<DownloadFileDeletionResult> DeleteGeneratedFilesAsync(
        DownloadingItem downloading,
        DownloadTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloading);
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();
        if (downloading.DownloadBase?.Id != task.Id.Value)
        {
            throw new InvalidOperationException("Download task identity changed during deletion.");
        }

        if (_staging != null)
        {
            return Task.FromResult(_staging.CleanupTask(task)
                ? new DownloadFileDeletionResult(1, 0)
                : new DownloadFileDeletionResult(1, 1));
        }

        return Task.FromResult(new DownloadFileDeletionResult(0, 0));
    }

    public Task<DownloadFileDeletionResult> DeleteLegacyUnpublishedArtifactsAsync(
        DownloadTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();
        var attemptedCount = 0;
        var failedCount = 0;
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(task.Output.BasePath))
            ?? throw new ArgumentException(
                "Output base path must have a directory.", nameof(task));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var published = new HashSet<string>(
            task.Output.PublishedArtifacts.Values.Select(Path.GetFullPath),
            comparison);

        foreach (var fileName in task.Plan.TransferFiles.Values.Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName is "." or ".." ||
                fileName != Path.GetFileName(fileName))
            {
                failedCount++;
                _logger.LogWarningMessage(
                    "Legacy transfer artifact has an invalid recorded file name.");
                continue;
            }

            var transferFile = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
            foreach (var path in new[] { transferFile, $"{transferFile}.aria2", $"{transferFile}.download" })
            {
                if (published.Contains(path))
                {
                    continue;
                }

                attemptedCount++;
                if (!TryDeleteLegacyArtifact(path))
                {
                    failedCount++;
                }
            }
        }

        attemptedCount++;
        if (_staging == null)
        {
            failedCount++;
            _logger.LogWarningMessage("Legacy transfer staging cleanup is unavailable.");
        }
        else if (!_staging.CleanupTask(task))
        {
            failedCount++;
        }

        return Task.FromResult(new DownloadFileDeletionResult(attemptedCount, failedCount));
    }

    public Task<DownloadFileDeletionResult> DeleteUnpublishedArtifactsAsync(
        DownloadTaskId taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        if (_staging == null)
        {
            _logger.LogWarningMessage("Task-private staging cleanup is unavailable.");
            return Task.FromResult(new DownloadFileDeletionResult(1, 1));
        }

        return Task.FromResult(_staging.CleanupTask(taskId)
            ? new DownloadFileDeletionResult(1, 0)
            : new DownloadFileDeletionResult(1, 1));
    }

    public async Task<OperationResult> PublishAsync(
        DownloadExecutionContext context,
        string key,
        string stagedFile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedFile);
        if (_staging == null || _stateWriter == null || context.StagingDirectory == null)
        {
            throw new InvalidOperationException("Task-private staging is required for publication.");
        }

        if (!context.ArtifactsValidated)
        {
            return OperationResult.Failure(new OperationError(
                "download.publish.unvalidated",
                "Only validated download artifacts may be published.",
                OperationErrorKind.Validation));
        }

        var fullSource = Path.GetFullPath(stagedFile);
        var stagingPath = Path.GetFullPath(context.StagingDirectory);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(fullSource), stagingPath, comparison))
        {
            throw new InvalidOperationException("Only a task-private staged file may be published.");
        }

        if (!DownloadFileIntegrity.Check(fullSource).IsUsable)
        {
            return OperationResult.Failure(new OperationError(
                "download.publish.invalid",
                "An invalid staged artifact cannot be published.",
                OperationErrorKind.Validation));
        }

        var outputRoot = Path.GetDirectoryName(Path.GetFullPath(context.Input.OutputBasePath))!;
        var destination = Path.Combine(outputRoot, Path.GetFileName(fullSource));
        context.EnsureActive(cancellationToken);
        var publishing = await FingerprintAsync(key, fullSource, cancellationToken).ConfigureAwait(true);
        await _stateWriter.BeginPublishingArtifactAsync(
            context.TaskId, publishing, cancellationToken).ConfigureAwait(true);
        context.EnsureActive(cancellationToken);
        try
        {
            File.Move(fullSource, destination, overwrite: false);
        }
        catch (IOException)
        {
            return OperationResult.Failure(new OperationError(
                "download.publish.collision",
                "The destination already exists or could not be published.",
                OperationErrorKind.Conflict));
        }

        await _stateWriter.RecordPublishedArtifactAsync(
            context.TaskId, publishing, destination, CancellationToken.None).ConfigureAwait(true);
        context.PublishedArtifacts[key] = destination;
        return OperationResult.Success();
    }

    public async Task<(DownloadTask Task, bool CanRun)> ReconcilePublishingAsync(
        DownloadTask task,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var publishing = task.Output.PublishingArtifact;
        if (publishing == null)
        {
            return (task, true);
        }

        if (_staging == null || _stateWriter == null)
        {
            throw new InvalidOperationException("Task-private staging is required for publication recovery.");
        }

        var directory = _staging.GetDirectory(
            task.Id, task.Output.BasePath, task.Output.StagingToken);
        var stagedFile = Path.Combine(directory, publishing.FileName);
        var destination = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(task.Output.BasePath))!,
            publishing.FileName);
        var stagedExists = File.Exists(stagedFile);
        var destinationExists = File.Exists(destination);
        if (stagedExists)
        {
            if (destinationExists ||
                !await MatchesFingerprintAsync(stagedFile, publishing, cancellationToken)
                    .ConfigureAwait(true))
            {
                return (task, false);
            }

            if (task.Phase == DownloadPhase.Canceled)
            {
                var cleared = await _stateWriter.ClearPublishingArtifactAsync(
                    task.Id, cancellationToken).ConfigureAwait(true);
                return (cleared, false);
            }

            return (task, true); // The existing staged product can retry its non-overwrite move.
        }

        if (!destinationExists)
        {
            var cleared = await _stateWriter.ClearPublishingArtifactAsync(
                task.Id, cancellationToken).ConfigureAwait(true);
            return (cleared, true);
        }

        if (!await MatchesFingerprintAsync(destination, publishing, cancellationToken)
                .ConfigureAwait(true))
        {
            return (task, false);
        }

        var published = await _stateWriter.RecordPublishedArtifactAsync(
            task.Id, publishing, destination, cancellationToken).ConfigureAwait(true);
        return (published, true);
    }

    private static async Task<DownloadPublishingArtifact> FingerprintAsync(
        string key,
        string path,
        CancellationToken cancellationToken)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("A redirected file cannot be published.");
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = stream.Length;
        var digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return new DownloadPublishingArtifact(key, Path.GetFileName(path), length,
            Convert.ToHexString(digest));
    }

    private static async Task<bool> MatchesFingerprintAsync(
        string path,
        DownloadPublishingArtifact expected,
        CancellationToken cancellationToken)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }

        var actual = await FingerprintAsync(expected.Key, path, cancellationToken)
            .ConfigureAwait(false);
        return actual.Length == expected.Length && actual.Sha256 == expected.Sha256;
    }

    public void CleanupStaging(DownloadTaskId taskId) => _staging?.CleanupTask(taskId);

    private bool TryDeleteLegacyArtifact(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                _logger.LogWarningMessage(
                    "Legacy transfer artifact is an unexpected directory.");
                return false;
            }

            File.Delete(path);
            if (File.Exists(path) || Directory.Exists(path))
            {
                _logger.LogWarningMessage(
                    "Legacy transfer artifact remained after deletion.");
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarningMessage("Legacy transfer artifact cleanup failed.", exception);
            return false;
        }
    }

}

internal readonly record struct DownloadFileDeletionResult(int AttemptedCount, int FailedCount)
{
    public bool Succeeded => FailedCount == 0;
}
