using DownKyi.Application.Downloads;
using DownKyi.Domain.Downloads;

namespace DownKyi.Application.Tests;

public sealed class DownloadAddCoordinatorTests
{
    [Fact]
    public async Task CancelingDirectorySelectionDoesNotAddADownload()
    {
        var addWasCalled = false;

        var result = await DownloadAddCoordinator.AddToDownloadIfSelectionAcceptedAsync(
            () => Task.FromResult(DownloadSelectionResult.Canceled),
            _ =>
            {
                addWasCalled = true;
                return Task.FromResult(1);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(DownloadAddFlowOutcome.SelectionCanceled, result.Outcome);
        Assert.Null(result.Value);
        Assert.False(addWasCalled);
    }

    [Fact]
    public async Task AcceptedSelectionReachesTheDownloadOperation()
    {
        DownloadAddSelection? receivedSelection = null;
        var requestedContent = DownloadContentSelection.None with { Video = true };

        var result = await DownloadAddCoordinator.AddToDownloadIfSelectionAcceptedAsync(
            () => Task.FromResult(DownloadSelectionResult.Accepted(
                new DownloadAddSelection("D:\\Downloads", requestedContent))),
            selection =>
            {
                receivedSelection = selection;
                return Task.FromResult(2);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(DownloadAddFlowOutcome.Completed, result.Outcome);
        Assert.Equal(2, result.Value);
        Assert.Equal("D:\\Downloads", receivedSelection!.Directory);
        Assert.Same(requestedContent, receivedSelection.RequestedContent);
    }

}
