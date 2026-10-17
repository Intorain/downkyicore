using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DownKyi.Application.Desktop;
using DownKyi.Application.Downloads;
using DownKyi.Core.Settings;
using DownKyi.ViewModels.DownloadManager;

namespace DownKyi.Services.Download;

internal sealed class DownloadDuplicatePolicy
{
    private readonly DownloadTaskProjectionStore _projectionStore;
    private readonly IAppDialogService _dialogService;

    public DownloadDuplicatePolicy(
        DownloadTaskProjectionStore projectionStore,
        IAppDialogService dialogService)
    {
        _projectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task<bool> ShouldSkipAsync(
        DownloadingItem requestedItem,
        RepeatDownloadStrategy strategy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestedItem);
        cancellationToken.ThrowIfCancellationRequested();

        var cid = requestedItem.DownloadBase.Cid;
        var active = await _projectionStore
            .GetDownloadingByCidAsync(cid, cancellationToken)
            .ConfigureAwait(true);
        if (active.Count > 0)
        {
            return true;
        }

        var completed = await _projectionStore
            .GetDownloadedByCidAsync(cid, cancellationToken)
            .ConfigureAwait(true);
        if (completed.Count == 0)
        {
            return false;
        }

        return strategy switch
        {
            RepeatDownloadStrategy.ReDownload => false,
            RepeatDownloadStrategy.Ask => !await ShouldRedownloadAsync(
                completed[0],
                cancellationToken).ConfigureAwait(true),
            _ => true
        };
    }

    private async Task<bool> ShouldRedownloadAsync(
        DownloadedItem item,
        CancellationToken cancellationToken)
    {
        var result = await _dialogService.ShowAsync(
            new AppDialogRequest(
                AppDialog.AlreadyDownloaded,
                new Dictionary<string, object?>
                {
                    ["message"] = $"{item.Name}已下载，是否重新下载"
                }),
            cancellationToken).ConfigureAwait(true);
        return result.Outcome == AppDialogOutcome.Accepted;
    }
}
