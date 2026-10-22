using DownKyi.Domain.Results;
using DownKyi.Services.Download;

namespace DownKyi.Desktop.Tests;

public sealed class DownloadFailurePresentationTests
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

    [AvaloniaFact]
    public Task TransferFailureKeepsItsCodeAndExplainsTheNextStepWithoutExposingTheRawError()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var error = OperationError.Unexpected(
                "download.transfer.http-403",
                "https://example.invalid/video?token=private-value");

            var failure = DownloadActivityPresenter.CreateFailure(error);
            var details = DownloadActivityPresenter.FormatFailureDetails(failure);

            Assert.Equal("download.transfer.http-403", failure.Code);
            Assert.DoesNotContain("private-value", failure.Message, StringComparison.Ordinal);
            Assert.Contains("服务器拒绝", details, StringComparison.Ordinal);
            Assert.Contains("检查登录状态", details, StringComparison.Ordinal);
            Assert.Contains("download.transfer.http-403", details, StringComparison.Ordinal);
            Assert.DoesNotContain("private-value", details, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public Task UnknownDiagnosticCodeDoesNotDisplayUntrustedText()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var details = DownloadActivityPresenter.FormatFailureDetails(
                new DownKyi.Domain.Downloads.DownloadFailure(
                    "download.transfer.http-403?token=private-value",
                    "private-value",
                    true));

            Assert.Contains("错误代码: 未知", details, StringComparison.Ordinal);
            Assert.DoesNotContain("private-value", details, StringComparison.Ordinal);
        });
    }

}
