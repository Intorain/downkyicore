using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using DownKyi.Application.Desktop;
using DownKyi.Domain.Downloads;
using DownKyi.Platform;
using DownKyi.Services.Download;
using DownKyi.Utils;
using DownKyi.ViewModels.Dialogs;
using DownKyi.Views.Dialogs;

namespace DownKyi.Desktop.Tests;

public sealed class DownloadContentConflictDialogPresentationTests
{
    [AvaloniaFact]
    public Task QualitySubstitutionAndBatchMeaningAreVisibleInExistingDialog()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var viewModel = new DownloadContentConflictDialogViewModel();
            var substitutions = new DownloadQualitySubstitutions(
                new DownloadQualitySubstitution(80, "1080P 高清", 64, "720P 高清"),
                new DownloadQualitySubstitution(30280, "高质量", 30232, "中质量"));
            var prompt = new DownloadContentConflictPrompt(
                "page",
                new DownloadContentConflict(
                    DownloadContentSelection.All,
                    new DownloadMediaCapabilities(DownloadMediaOutputModes.AudioVideo),
                    DownloadContentSelection.All,
                    substitutions));

            viewModel.OnDialogOpened(DownloadContentConflictDialogContract.CreateRequest(prompt));

            Assert.True(viewModel.HasQualitySubstitution);
            Assert.Equal("一键确认", viewModel.ApplyToAllText);
            Assert.Equal(
                "画质：1080P 高清 → 720P 高清；音质：高质量 → 中质量",
                viewModel.QualitySubstitution);
            Assert.Equal(
                "“一键确认”表示为本次批量中的每一集分别使用实际可用且最接近原设置的替代画质或音质。\n\n" +
                "不同集数可能使用不同的画质或音质；不会强制所有集数使用相同品质，也不会增加原本未勾选的下载内容。\n\n" +
                "仍可选择跳过下载。",
                DictionaryResource.GetString("ApplyToAllQualitySubstitutionExplanation"));
        });
    }

    [AvaloniaFact]
    public Task QualityBatchShowsGroupedImpactAndExpandableReadableTitles()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var viewModel = new DownloadContentConflictDialogViewModel();
            viewModel.OnDialogOpened(DownloadContentConflictDialogContract.CreateRequest(
                CreateQualityBatchPrompt()));
            var view = new DownloadContentConflictDialog
            {
                DataContext = viewModel
            };
            var window = new DialogWindow
            {
                SizeToContent = Avalonia.Controls.SizeToContent.Manual,
                Width = 680,
                Height = 520,
                Content = view
            };

            try
            {
                window.Show();
                var expander = Assert.IsType<Expander>(
                    view.FindControl<Expander>("AffectedQualityItemsExpander"));
                expander.IsExpanded = true;
                window.UpdateLayout();

                Assert.Equal("下载品质需要确认", viewModel.Title);
                Assert.Equal(
                    "本次下载列表中有 3 个视频无法取得首选画质或音质。确认后将按下列实际可用品质下载。",
                    viewModel.Message);
                Assert.Equal(
                    ["画质：1080P 高清 → 720P 高清（2 个视频）", "画质：1080P 高清 → 480P 清晰（1 个视频）"],
                    viewModel.QualitySubstitutionGroups);
                Assert.Equal("查看受影响的 3 个视频", expander.Header);
                Assert.Equal(HorizontalAlignment.Stretch, expander.HorizontalAlignment);
                Assert.Equal(HorizontalAlignment.Stretch, expander.HorizontalContentAlignment);
                Assert.Equal(
                    ["第一批 · 第一集", "第二批 · 第一集", "第三批 · 第一集"],
                    viewModel.QualitySubstitutionItems.Select(item => item.PageName).ToArray());
                var groups = Assert.IsType<ItemsControl>(
                    view.FindControl<ItemsControl>("QualitySubstitutionGroupsList"));
                var affectedItems = Assert.IsType<ItemsControl>(
                    view.FindControl<ItemsControl>("AffectedQualityItemsList"));
                Assert.Same(viewModel.QualitySubstitutionGroups, groups.ItemsSource);
                Assert.Same(viewModel.QualitySubstitutionItems, affectedItems.ItemsSource);
                Assert.NotNull(affectedItems.ItemTemplate);
                Assert.All(
                    view.GetVisualDescendants().OfType<TextBlock>()
                        .Where(text => !string.IsNullOrEmpty(text.Text)),
                    text => Assert.True(text.Bounds.Width <= 720));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [AvaloniaFact]
    public Task ManyDistinctQualityGroupsUseBoundedSummaryOutsideDecisionActions()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var viewModel = new DownloadContentConflictDialogViewModel();
            viewModel.OnDialogOpened(DownloadContentConflictDialogContract.CreateRequest(
                CreateQualityBatchPromptWithDistinctGroups(24)));
            var view = new DownloadContentConflictDialog
            {
                DataContext = viewModel
            };

            var groupsScroll = Assert.IsType<ScrollViewer>(
                view.FindControl<ScrollViewer>("QualitySubstitutionGroupsScrollViewer"));
            var groups = Assert.IsType<ItemsControl>(
                view.FindControl<ItemsControl>("QualitySubstitutionGroupsList"));
            var useAvailable = Assert.IsType<Button>(
                view.FindControl<Button>("UseAvailableContentButton"));
            var skip = Assert.IsType<Button>(
                view.FindControl<Button>("SkipDownloadPageButton"));

            Assert.Equal(24, viewModel.QualitySubstitutionGroups.Count);
            Assert.Same(viewModel.QualitySubstitutionGroups, groups.ItemsSource);
            Assert.Equal(160, groupsScroll.MaxHeight);
            Assert.Equal(
                ScrollBarVisibility.Disabled,
                groupsScroll.HorizontalScrollBarVisibility);
            Assert.Equal(
                ScrollBarVisibility.Auto,
                groupsScroll.VerticalScrollBarVisibility);
            Assert.Contains(groupsScroll, groups.GetLogicalAncestors());
            Assert.DoesNotContain(groupsScroll, useAvailable.GetLogicalAncestors());
            Assert.DoesNotContain(groupsScroll, skip.GetLogicalAncestors());
        });
    }

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

    private static DownloadContentConflictPrompt CreateQualityBatchPromptWithDistinctGroups(
        int count)
    {
        return DownloadContentConflictPrompt.QualitySubstitutionBatch(
            Enumerable.Range(1, count)
                .Select(index => new DownloadContentConflictPromptItem(
                    $"第 {index} 部影片",
                    new DownloadContentConflict(
                        DownloadContentSelection.None with { Video = true },
                        new DownloadMediaCapabilities(DownloadMediaOutputModes.VideoOnly),
                        DownloadContentSelection.None with { Video = true },
                        new DownloadQualitySubstitutions(
                            new DownloadQualitySubstitution(
                                80,
                                "1080P 高清",
                                index,
                                $"替代畫質 {index}"),
                            Audio: null))))
                .ToArray());
    }

}
