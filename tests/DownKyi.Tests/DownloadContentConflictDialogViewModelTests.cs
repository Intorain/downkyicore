using DownKyi.Application.Desktop;
using DownKyi.Domain.Downloads;
using DownKyi.Services.Download;
using DownKyi.ViewModels.Dialogs;

namespace DownKyi.Tests;

public sealed class DownloadContentConflictDialogViewModelTests
{
    [Fact]
    public void AvailableChoiceReturnsTypedDecisionWithApplyToAll()
    {
        var viewModel = new DownloadContentConflictDialogViewModel
        {
            ApplyToAll = true
        };
        AppDialogResult? result = null;
        viewModel.CloseRequested += (_, value) => result = value;
        viewModel.OnDialogOpened(DownloadContentConflictDialogContract.CreateRequest(CreatePrompt()));

        viewModel.UseAvailableContentCommand.Execute(null);

        Assert.NotNull(result);
        Assert.Equal(AppDialogOutcome.Accepted, result.Outcome);
        var decision = Assert.IsType<DownloadContentConflictDecision>(
            result.Parameters[DownloadContentConflictDialogContract.DecisionParameter]);
        Assert.Equal(DownloadContentConflictAction.UseAvailableContent, decision.Action);
        Assert.True(decision.ApplyToAll);
    }

    [Fact]
    public void SkipChoiceReturnsTypedSkipDecision()
    {
        var viewModel = new DownloadContentConflictDialogViewModel();
        AppDialogResult? result = null;
        viewModel.CloseRequested += (_, value) => result = value;
        viewModel.OnDialogOpened(DownloadContentConflictDialogContract.CreateRequest(CreatePrompt()));

        viewModel.SkipPageCommand.Execute(null);

        var decision = Assert.IsType<DownloadContentConflictDecision>(
            Assert.IsType<AppDialogResult>(result).Parameters[DownloadContentConflictDialogContract.DecisionParameter]);
        Assert.Equal(DownloadContentConflictAction.SkipPage, decision.Action);
        Assert.False(decision.ApplyToAll);
    }

    [Fact]
    public void DialogRequiresAnExplicitChoice()
    {
        var viewModel = new DownloadContentConflictDialogViewModel();

        Assert.False(viewModel.CanCloseDialog());
    }

    [Fact]
    public void LowerQualityReplacementEnablesExistingDialogPresentation()
    {
        var viewModel = new DownloadContentConflictDialogViewModel();
        var prompt = CreatePrompt(new DownloadQualitySubstitutions(
            new DownloadQualitySubstitution(80, "1080P 高清", 64, "720P 高清"),
            new DownloadQualitySubstitution(30280, "高质量", 30232, "中质量")));

        viewModel.OnDialogOpened(DownloadContentConflictDialogContract.CreateRequest(prompt));

        Assert.True(viewModel.HasQualitySubstitution);
    }

    [Fact]
    public void QualityBatchSummarizesOutcomesAndKeepsEveryAffectedTitle()
    {
        var viewModel = new DownloadContentConflictDialogViewModel();

        viewModel.OnDialogOpened(DownloadContentConflictDialogContract.CreateRequest(
            CreateQualityBatchPrompt()));

        Assert.True(viewModel.IsQualitySubstitutionBatch);
        Assert.False(viewModel.ShowApplyToAll);
        Assert.Equal(
            ["第一批 · 第一集", "第二批 · 第一集", "第三批 · 第一集"],
            viewModel.QualitySubstitutionItems.Select(item => item.PageName).ToArray());
    }

    [Fact]
    public void QualityBatchDecisionAlwaysAppliesToDisplayedItems()
    {
        var viewModel = new DownloadContentConflictDialogViewModel();
        AppDialogResult? result = null;
        viewModel.CloseRequested += (_, value) => result = value;
        viewModel.OnDialogOpened(DownloadContentConflictDialogContract.CreateRequest(
            CreateQualityBatchPrompt()));

        viewModel.SkipPageCommand.Execute(null);

        var decision = Assert.IsType<DownloadContentConflictDecision>(
            Assert.IsType<AppDialogResult>(result).Parameters[DownloadContentConflictDialogContract.DecisionParameter]);
        Assert.Equal(DownloadContentConflictAction.SkipPage, decision.Action);
        Assert.True(decision.ApplyToAll);
    }

    [Fact]
    public async Task NonAcceptedResultFailsClosed()
    {
        var dialogs = new StubDialogService(new AppDialogResult(
            AppDialogOutcome.Canceled,
            new Dictionary<string, object?>()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DownloadContentConflictDialogContract.ShowAsync(
                dialogs,
                CreatePrompt(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AcceptedResultWithoutTypedDecisionFailsClosed()
    {
        var dialogs = new StubDialogService(new AppDialogResult(
            AppDialogOutcome.Accepted,
            new Dictionary<string, object?>()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DownloadContentConflictDialogContract.ShowAsync(
                dialogs,
                CreatePrompt(),
                TestContext.Current.CancellationToken));
    }

    private static DownloadContentConflictPrompt CreatePrompt(
        DownloadQualitySubstitutions? substitutions = null) => new(
        "page",
        new DownloadContentConflict(
            DownloadContentSelection.All,
            new DownloadMediaCapabilities(DownloadMediaOutputModes.VideoOnly),
            DownloadContentSelection.All with { Audio = false },
            substitutions ?? DownloadQualitySubstitutions.None));

    private static DownloadContentConflictPrompt CreateQualityBatchPrompt()
    {
        static DownloadContentConflictPromptItem CreateItem(
            string pageName,
            int selectedQuality,
            string selectedName) => new(
            pageName,
            new DownloadContentConflict(
                DownloadContentSelection.None with { Video = true },
                new DownloadMediaCapabilities(DownloadMediaOutputModes.VideoOnly),
                DownloadContentSelection.None with { Video = true },
                new DownloadQualitySubstitutions(
                    new DownloadQualitySubstitution(
                        80,
                        "1080P 高清",
                        selectedQuality,
                        selectedName),
                    Audio: null)));

        return DownloadContentConflictPrompt.QualitySubstitutionBatch(
        [
            CreateItem("第一批 · 第一集", 64, "720P 高清"),
            CreateItem("第二批 · 第一集", 64, "720P 高清"),
            CreateItem("第三批 · 第一集", 32, "480P 清晰")
        ]);
    }

    private sealed class StubDialogService(AppDialogResult result) : IAppDialogService
    {
        public Task<AppDialogResult> ShowAsync(
            AppDialogRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}
