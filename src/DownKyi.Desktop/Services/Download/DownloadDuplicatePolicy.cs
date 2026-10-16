using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DownKyi.Application.Desktop;
using DownKyi.Application.Downloads;
using DownKyi.Core.Settings;
using DownKyi.Domain.Downloads;
using DownKyi.ViewModels.DownloadManager;

namespace DownKyi.Services.Download;

internal sealed class DownloadDuplicatePolicy
{
    private readonly IDownloadedListProjection _downloadedListProjection;
    private readonly DownloadTaskProjectionStore _projectionStore;
    private readonly IPhysicalOutputPathResolver _physicalOutputPathResolver;
    private readonly IAppDialogService _dialogService;

    public DownloadDuplicatePolicy(
        IDownloadedListProjection downloadedListProjection,
        DownloadTaskProjectionStore projectionStore,
        IPhysicalOutputPathResolver physicalOutputPathResolver,
        IAppDialogService dialogService)
    {
        _downloadedListProjection = downloadedListProjection
            ?? throw new ArgumentNullException(nameof(downloadedListProjection));
        _projectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
        _physicalOutputPathResolver = physicalOutputPathResolver
            ?? throw new ArgumentNullException(nameof(physicalOutputPathResolver));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task<DownloadDuplicateResolution> ResolveAsync(
        DownloadingItem requestedItem,
        RepeatDownloadStrategy strategy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestedItem);
        cancellationToken.ThrowIfCancellationRequested();

        var requestedContent = requestedItem.DownloadBase.NeedDownloadContent;
        var remainingContent = requestedContent;
        var hasMatchingOutputOwner = false;
        var cid = requestedItem.DownloadBase.Cid;
        var active = await LoadActiveCandidatesAsync(cid, cancellationToken).ConfigureAwait(true);
        foreach (var item in active)
        {
            var matchesOutputOwner = DownloadActionCoverage.MatchesOutputOwner(
                item,
                requestedItem,
                _physicalOutputPathResolver);
            hasMatchingOutputOwner |= matchesOutputOwner;
            var reduced = DownloadActionCoverage.RemoveCoveredActions(
                item,
                remainingContent,
                matchesOutputOwner,
                requireUsableArtifacts: false);
            remainingContent = reduced;
            if (!remainingContent.HasAnyRequestedAction)
            {
                return DownloadDuplicateResolution.FullyCovered(requestedContent);
            }
        }

        var candidates = await LoadCompletedCandidatesAsync(cid, cancellationToken).ConfigureAwait(true);
        if (strategy == RepeatDownloadStrategy.ReDownload)
        {
            hasMatchingOutputOwner |= candidates.Any(item =>
                DownloadActionCoverage.MatchesOutputOwner(
                    item,
                    requestedItem,
                    _physicalOutputPathResolver));
            return new DownloadDuplicateResolution(
                requestedContent,
                AllowExistingBasePath: hasMatchingOutputOwner);
        }

        foreach (var item in candidates.ToArray())
        {
            var matchesOutputOwner = DownloadActionCoverage.MatchesOutputOwner(
                item,
                requestedItem,
                _physicalOutputPathResolver);
            hasMatchingOutputOwner |= matchesOutputOwner;
            var reduced = DownloadActionCoverage.RemoveCoveredActions(
                item,
                remainingContent,
                matchesOutputOwner,
                requireUsableArtifacts: true);
            if (reduced == remainingContent)
            {
                continue;
            }

            if (strategy == RepeatDownloadStrategy.Ask
                && await ShouldRedownloadAsync(item, cancellationToken).ConfigureAwait(true))
            {
                candidates.Remove(item);
                continue;
            }

            remainingContent = reduced;
            if (!remainingContent.HasAnyRequestedAction)
            {
                return DownloadDuplicateResolution.FullyCovered(requestedContent);
            }
        }

        return new DownloadDuplicateResolution(
            remainingContent,
            AllowExistingBasePath: hasMatchingOutputOwner);
    }

    internal async Task<bool> ShouldSkipAsync(
        DownloadingItem requestedItem,
        RepeatDownloadStrategy strategy,
        CancellationToken cancellationToken) =>
        (await ResolveAsync(
            requestedItem,
            strategy,
            cancellationToken).ConfigureAwait(true)).IsFullyCovered;

    private async Task<List<DownloadingItem>> LoadActiveCandidatesAsync(
        long cid,
        CancellationToken cancellationToken)
    {
        var candidates = await _projectionStore
            .GetDownloadingByCidAsync(cid, cancellationToken)
            .ConfigureAwait(true);
        return new List<DownloadingItem>(candidates);
    }

    private async Task<List<DownloadedItem>> LoadCompletedCandidatesAsync(
        long cid,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var downloadedItems = await _projectionStore
            .GetDownloadedByCidAsync(cid, cancellationToken)
            .ConfigureAwait(true);
        return new List<DownloadedItem>(downloadedItems);
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
        if (result.Outcome != AppDialogOutcome.Accepted)
        {
            return false;
        }

        await _projectionStore
            .RemoveDownloadedAsync(item, cancellationToken)
            .ConfigureAwait(true);
        _downloadedListProjection.RemoveDownloaded(item);
        return true;
    }

}

internal sealed record DownloadDuplicateResolution(
    DownloadContentSelection RemainingContent,
    bool AllowExistingBasePath)
{
    public bool IsFullyCovered => !RemainingContent.HasAnyRequestedAction;

    public static DownloadDuplicateResolution FullyCovered(
        DownloadContentSelection requestedContent) =>
        new(DownloadContentSelection.None, requestedContent.HasAnyRequestedAction);
}
