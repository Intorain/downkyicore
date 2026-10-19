using DownKyi.Domain.Results;
using DownKyi.Services.Download;

namespace DownKyi.Desktop.Tests;

public sealed class SubtitleFailurePresentationTests
{
    [AvaloniaFact]
    public Task MissingSelectedSubtitleShowsSpecificFailureInsteadOfGenericDownloadFailure()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var error = OperationError.Unexpected(
                "download.artifact.subtitle.missing-tracks",
                "A selected subtitle was not returned.");

            var failure = DownloadActivityPresenter.CreateFailure(error);

            Assert.Equal("download.artifact.subtitle.missing-tracks", failure.Code);
            Assert.Equal("字幕下载失败", failure.Message);
            Assert.True(failure.IsTransient);
        });
    }
}
