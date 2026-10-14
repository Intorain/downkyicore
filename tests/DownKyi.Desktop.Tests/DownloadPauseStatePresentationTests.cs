using DownKyi.Domain.Downloads;
using DownKyi.Images;
using DownKyi.Models;
using DownKyi.Services.Download;

namespace DownKyi.Desktop.Tests;

public sealed class DownloadPauseStatePresentationTests
{
    [AvaloniaFact]
    public async Task WaitingPausingAndPausedRemainDistinctInTheDownloadList()
    {
        await AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var queued = CreateTask();
            var pausing = queued
                .Start(DateTimeOffset.UnixEpoch.AddSeconds(1))
                .RequireValue()
                .Pause(DateTimeOffset.UnixEpoch.AddSeconds(2))
                .RequireValue();
            var paused = pausing
                .ConfirmPaused(DateTimeOffset.UnixEpoch.AddSeconds(3))
                .RequireValue();

            var queuedItem = DownloadTaskProjectionMapper.ToDownloadingItem(queued);
            var pausingItem = DownloadTaskProjectionMapper.ToDownloadingItem(pausing);
            var pausedItem = DownloadTaskProjectionMapper.ToDownloadingItem(paused);

            Assert.Equal(DownloadStatus.WaitForDownload, queuedItem.Downloading.DownloadStatus);
            Assert.Equal("等待中……", queuedItem.DownloadStatusTitle);
            Assert.Same(ButtonIcon.Pause, queuedItem.StartOrPause);

            Assert.Equal(DownloadStatus.PauseStarted, pausingItem.Downloading.DownloadStatus);
            Assert.Equal("暂停中……", pausingItem.DownloadStatusTitle);
            Assert.Same(ButtonIcon.Start, pausingItem.StartOrPause);

            Assert.Equal(DownloadStatus.Pause, pausedItem.Downloading.DownloadStatus);
            Assert.Equal("已暂停", pausedItem.DownloadStatusTitle);
            Assert.Same(ButtonIcon.Start, pausedItem.StartOrPause);
        }).ConfigureAwait(true);
    }

    [AvaloniaFact]
    public async Task RetiredLegacyFailureHidesTheUnavailableRetryAction()
    {
        await AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var queued = CreateTask();
            var failed = DownloadTask.Restore(
                queued.Id,
                queued.Metadata,
                queued.Plan,
                queued.Output,
                DownloadPhase.Failed,
                DownloadProgress.None,
                DownloadTransferState.LegacyEmpty,
                new DownloadFailure(
                    "download.transfer.legacy-backend-removed",
                    "The unfinished legacy download was removed and marked as failed.",
                    false),
                completion: null,
                version: 1,
                queued.CreatedAtUtc,
                queued.CreatedAtUtc.AddSeconds(1));

            var item = DownloadTaskProjectionMapper.ToDownloadingItem(failed);

            Assert.Equal(DownloadStatus.DownloadFailed, item.Downloading.DownloadStatus);
            Assert.Same(ButtonIcon.Retry, item.StartOrPause);
            Assert.False(item.IsToggleVisible);
        }).ConfigureAwait(true);
    }

    private static DownloadTask CreateTask()
    {
        return DownloadTask.Create(
            new DownloadTaskId("pause-presentation"),
            new DownloadTaskMetadata(
                new DownloadMediaIdentity("BV1", 1, 1, 0, 1, 1),
                "title",
                "part",
                "00:01",
                "avc1",
                new DownloadQuality(80, "1080P"),
                new DownloadQuality(30280, "AAC"),
                string.Empty,
                string.Empty,
                0),
            new DownloadPlan(DownloadContentSelection.None, [], 0, nfoRequest: null),
            new DownloadOutput("pause-presentation", null),
            DateTimeOffset.UnixEpoch);
    }
}
