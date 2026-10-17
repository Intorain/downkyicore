using DownKyi.Application.Desktop;
using DownKyi.Services.Download;
using DownKyi.ViewModels.Dialogs;

namespace DownKyi.Tests;

public sealed class DownloadDuplicateBatchDialogViewModelTests
{
    [Fact]
    public void PromptShowsEveryCompletedDuplicateAndActiveSkipCount()
    {
        var viewModel = new DownloadDuplicateBatchDialogViewModel();

        viewModel.OnDialogOpened(DownloadDuplicateBatchDialogContract.CreateRequest(
            new DownloadDuplicateBatchPrompt(["第一集", "第二集"], activeDuplicateCount: 1)));

        Assert.Equal(["第一集", "第二集"], viewModel.CompletedItemNames);
        Assert.True(viewModel.HasActiveDuplicates);
        Assert.False(viewModel.CanCloseDialog());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitChoiceReturnsTypedDecision(bool redownload)
    {
        var viewModel = new DownloadDuplicateBatchDialogViewModel();
        AppDialogResult? result = null;
        viewModel.CloseRequested += (_, value) => result = value;
        viewModel.OnDialogOpened(DownloadDuplicateBatchDialogContract.CreateRequest(
            new DownloadDuplicateBatchPrompt(["第一集"], activeDuplicateCount: 0)));

        if (redownload)
        {
            viewModel.RedownloadCommand.Execute(null);
        }
        else
        {
            viewModel.SkipCommand.Execute(null);
        }

        Assert.Equal(
            redownload
                ? DownloadDuplicateBatchAction.RedownloadCompleted
                : DownloadDuplicateBatchAction.SkipCompleted,
            Assert.IsType<DownloadDuplicateBatchAction>(
                Assert.IsType<AppDialogResult>(result)
                    .Parameters[DownloadDuplicateBatchDialogContract.DecisionParameter]));
    }
}
