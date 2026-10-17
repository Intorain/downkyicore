using System;
using System.Threading;
using System.Threading.Tasks;
using DownKyi.Application.Downloads;
using DownKyi.Domain.Downloads;
using DownKyi.ViewModels.DownloadManager;

namespace DownKyi.Services.Download;

internal sealed class DownloadTaskAdmissionService : IDisposable
{
    private readonly DownloadListState _downloadLists;
    private readonly IDownloadOutputReservationService _outputReservations;
    private readonly DownloadTaskProjectionStore _projections;
    private readonly DownloadCommittedTaskDispatcher _committedTaskDispatcher;
    private readonly IDownloadRuntimeAvailability _runtimeAvailability;
    private readonly IPhysicalOutputPathResolver _physicalOutputPathResolver;
    private readonly SemaphoreSlim _admissionGate = new(1, 1);
    private bool _disposed;

    public DownloadTaskAdmissionService(
        DownloadListState downloadLists,
        IDownloadOutputReservationService outputReservations,
        DownloadTaskProjectionStore projections,
        DownloadCommittedTaskDispatcher committedTaskDispatcher,
        IDownloadRuntimeAvailability runtimeAvailability,
        IPhysicalOutputPathResolver physicalOutputPathResolver)
    {
        _downloadLists = downloadLists ?? throw new ArgumentNullException(nameof(downloadLists));
        _outputReservations = outputReservations
            ?? throw new ArgumentNullException(nameof(outputReservations));
        _projections = projections ?? throw new ArgumentNullException(nameof(projections));
        _committedTaskDispatcher = committedTaskDispatcher
            ?? throw new ArgumentNullException(nameof(committedTaskDispatcher));
        _runtimeAvailability = runtimeAvailability
            ?? throw new ArgumentNullException(nameof(runtimeAvailability));
        _physicalOutputPathResolver = physicalOutputPathResolver
            ?? throw new ArgumentNullException(nameof(physicalOutputPathResolver));
    }

    public async Task AdmitAsync(
        DownloadingItem item,
        bool autoAddNumberSuffix,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _admissionGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            _runtimeAvailability.EnsureAcceptingTasks();
            var physicalBasePath = _physicalOutputPathResolver.ResolvePhysicalBasePath(
                item.DownloadBase.FilePath);
            var requestedClaims = DownloadActionClaims.From(
                item.DownloadBase.NeedDownloadContent,
                includesNfo: item.Metadata != null);
            var admittedBasePath = await DownloadOutputPathResolver.ResolveAdmissionCollisionAsync(
                physicalBasePath,
                autoAddNumberSuffix,
                (path, token) => _outputReservations.HasOutputClaimConflictAsync(
                    path,
                    item.DownloadBase.Cid,
                    requestedClaims,
                    DownloadOutputPathKey.UsesCaseInsensitiveComparison,
                    token),
                token => _outputReservations.GetActiveOutputReservationKeysAsync(
                    DownloadOutputPathKey.UsesCaseInsensitiveComparison,
                    token),
                cancellationToken,
                path => DownloadOutputPathResolver.HasExistingOutputClaimConflict(
                    path,
                    requestedClaims)).ConfigureAwait(true);
            item.DownloadBase.FilePath = admittedBasePath;

            await _projections.AddDownloadingAsync(item, cancellationToken).ConfigureAwait(true);

            // Once persisted, admission must finish even if the originating UI operation is canceled.
            _downloadLists.AddDownloading(item);
            var taskId = new DownloadTaskId(item.DownloadBase.Id);
            await _committedTaskDispatcher.EnqueueCommittedAsync(taskId).ConfigureAwait(true);
        }
        finally
        {
            _admissionGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _admissionGate.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
