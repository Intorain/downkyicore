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
            Assert.Contains("地址已失效", details, StringComparison.Ordinal);
            Assert.Contains("已经尝试切换并刷新地址", details, StringComparison.Ordinal);
            Assert.Contains("download.transfer.http-403", details, StringComparison.Ordinal);
            Assert.DoesNotContain("private-value", details, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public Task UnauthorizedTransferExplainsAccessRecovery()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var details = DownloadActivityPresenter.FormatFailureDetails(
                new DownKyi.Domain.Downloads.DownloadFailure(
                    "download.transfer.http-401",
                    "下载失败",
                    true));

            Assert.Contains("服务器拒绝", details, StringComparison.Ordinal);
            Assert.Contains("检查登录状态", details, StringComparison.Ordinal);
            Assert.Contains("download.transfer.http-401", details, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public Task SubtitleIoFailuresExplainTheirDistinctRecoveryPaths()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var responseDetails = DownloadActivityPresenter.FormatFailureDetails(
                new DownKyi.Domain.Downloads.DownloadFailure(
                    DownloadArtifactWriter.SubtitleResponseReadFailureCode,
                    "下载失败",
                    true));
            var outputDetails = DownloadActivityPresenter.FormatFailureDetails(
                new DownKyi.Domain.Downloads.DownloadFailure(
                    DownloadArtifactWriter.SubtitleOutputWriteFailureCode,
                    "下载失败",
                    true));

            Assert.Contains("网络连接", responseDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("下载目录权限", responseDetails, StringComparison.Ordinal);
            Assert.Contains("下载目录权限", outputDetails, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public Task SimilarLookingUnknownCodeDoesNotInheritARecoveryPath()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var details = DownloadActivityPresenter.FormatFailureDetails(
                new DownKyi.Domain.Downloads.DownloadFailure(
                    "download.artifact.subtitle.future.io",
                    "下载失败",
                    true));

            Assert.Contains("暂时无法判断具体原因", details, StringComparison.Ordinal);
            Assert.DoesNotContain("下载目录权限", details, StringComparison.Ordinal);
            Assert.DoesNotContain("所选字幕", details, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public Task ReviewFindingsUseTheirExactRecoveryMessage()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var expectations = new[]
            {
                ("download.validate.subtitle", "所选字幕"),
                ("download.validate.danmaku", "附加文件"),
                ("download.publish.reconcile", "文件发布的状态无法安全确认"),
                ("download.transfer.legacy-cleanup-failed", "旧下载器")
            };
            foreach (var (code, expectedText) in expectations)
            {
                var details = DownloadActivityPresenter.FormatFailureDetails(
                    new DownKyi.Domain.Downloads.DownloadFailure(code, "下载失败", false));
                Assert.Contains(expectedText, details, StringComparison.Ordinal);
            }
        });
    }

    [AvaloniaFact]
    public Task AmbiguousArtifactIoDoesNotClaimTheStorageIsAtFault()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            AssertAmbiguousArtifactIo("download.artifact.cover.io");
            AssertAmbiguousArtifactIo("download.artifact.danmaku.io");
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

    private static void AssertAmbiguousArtifactIo(string code)
    {
        var details = DownloadActivityPresenter.FormatFailureDetails(
            new DownKyi.Domain.Downloads.DownloadFailure(code, "下载失败", true));
        Assert.Contains("附加文件", details, StringComparison.Ordinal);
        Assert.DoesNotContain("本地下载文件无法写入", details, StringComparison.Ordinal);
    }

}
