using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using DownKyi.Core.BiliApi.BiliUtils;
using DownKyi.Domain.Downloads;
using DownKyi.Models;
using DownKyi.ViewModels.DownloadManager;
using DownKyi.Views.DownloadManager;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace DownKyi.Desktop.Tests;

public sealed class DownloadTaskRowLayoutTests
{
    private static readonly ThemeVariant[] Themes =
    [
        ThemeVariant.Light,
        ThemeVariant.Dark
    ];

    [AvaloniaFact]
    public Task TaskRowSeparatesIdentityProgressMetricsAndActionsAcrossThemes()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            var application = DesktopTestResources.EnsureProductThemeResources();
            var originalTheme = application.RequestedThemeVariant;
            var fluentTheme = new FluentTheme();
            application.Styles.Insert(0, fluentTheme);

            try
            {
                foreach (var theme in Themes)
                {
                    application.RequestedThemeVariant = theme;
                    AssertTaskRow(application, theme);
                }
            }
            finally
            {
                application.RequestedThemeVariant = originalTheme;
                application.Styles.Remove(fluentTheme);
            }
        });
    }

    [AvaloniaFact]
    public Task FailedTaskOffersDetailsInPlaceOfTheSpeedMetric()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();
            var application = DesktopTestResources.EnsureProductThemeResources();
            var fluentTheme = new FluentTheme();
            application.Styles.Insert(0, fluentTheme);
            var item = new DownloadingItem
            {
                Failure = new DownloadFailure("download.transfer.http-403", "下载失败", true),
                Downloading = new Downloading
                {
                    DownloadStatus = DownloadStatus.DownloadFailed,
                    DownloadContent = string.Empty,
                    DownloadStatusTitle = "下载失败",
                    SpeedDisplay = "2 MB/s"
                }
            };
            var view = new ViewDownloading();
            var list = Assert.IsType<ListBox>(view.FindControl<ListBox>("DownloadingList"));
            list.ItemsSource = new[] { item };
            var window = new Window { Content = view, Width = 800, Height = 500 };

            try
            {
                window.Show();
                window.UpdateLayout();
                var container = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
                var row = Named<Border>(container, "NameTaskRow");
                var detailsButton = Named<Button>(container, "NameTaskFailureDetailsButton");
                var detailsPanel = Named<Border>(container, "NameTaskFailureDetailsPanel");
                var status = Named<TextBlock>(container, "NameTaskStatus");

                Assert.Equal("下载失败", status.Text);
                Assert.True(detailsButton.IsVisible);
                Assert.Equal("查看原因", detailsButton.Content);
                Assert.False(Named<TextBlock>(container, "NameTaskSpeed").IsVisible);
                Assert.False(detailsPanel.IsVisible);
                Assert.Equal(86, row.Bounds.Height);

                // Empty content must not replace the status binding with a captured string.
                item.DownloadContent = "视频";
                item.DownloadStatusTitle = "准备重试";
                window.UpdateLayout();
                Assert.Equal("准备重试", status.Text);

                item.IsFailureDetailsExpanded = true;
                window.UpdateLayout();

                Assert.True(detailsPanel.IsVisible);
                Assert.True(row.Bounds.Height > 86);
                Assert.Equal("收起原因", detailsButton.Content);
                Assert.Contains(
                    "download.transfer.http-403",
                    Named<TextBlock>(detailsPanel, "NameTaskFailureDetailsText").Text,
                    StringComparison.Ordinal);

                item.IsFailureDetailsExpanded = false;
                window.UpdateLayout();

                Assert.False(detailsPanel.IsVisible);
                Assert.Equal(86, row.Bounds.Height);
            }
            finally
            {
                window.Close();
                application.Styles.Remove(fluentTheme);
            }
        });
    }

    private static void AssertTaskRow(
        Avalonia.Application application,
        ThemeVariant theme)
    {
        var item = new DownloadingItem
        {
            Downloading = new Downloading
            {
                DownloadContent = "下载视频",
                DownloadStatusTitle = "暂停中……",
                DownloadStatus = DownloadStatus.Pause,
                Progress = 31,
                DownloadingFileSize = "285.92 MB / 4.55 GB",
                SpeedDisplay = "2.62 MB/s"
            }
        };
        item.DownloadBase.Order = 1;
        item.DownloadBase.MainTitle = "示例影片：较长标题用于检查下载任务的可读性";
        item.DownloadBase.Name = "第一集";
        item.DownloadBase.Duration = "12:48";
        item.DownloadBase.Resolution = new Quality { Name = "1080P" };
        item.DownloadBase.VideoCodecName = "AVC";
        item.DownloadBase.AudioCodec = new Quality { Name = "AAC" };

        var view = new ViewDownloading();
        var content = Assert.IsType<Grid>(
            view.FindControl<Grid>("NameDownloadingContent"));
        var list = Assert.IsType<ListBox>(
            view.FindControl<ListBox>("DownloadingList"));
        content.IsVisible = true;
        list.ItemsSource = new[] { item };

        var host = new ContentControl
        {
            Content = view,
            Width = 800,
            Height = 500
        };
        var window = new Window
        {
            Content = host,
            Width = 800,
            Height = 500
        };

        try
        {
            window.Show();
            window.UpdateLayout();

            var container = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
            var row = Named<Border>(container, "NameTaskRow");
            var iconFrame = Named<Border>(container, "NameZoneIconFrame");
            var title = Named<TextBlock>(container, "NameTaskTitle");
            var metadata = Named<TextBlock>(container, "NameTaskMetadata");
            var status = Named<TextBlock>(container, "NameTaskStatus");
            var percentage = Named<TextBlock>(container, "NameTaskProgressPercentage");
            var progress = Named<ProgressBar>(container, "NameTaskProgress");
            var size = Named<TextBlock>(container, "NameTaskSize");
            var speed = Named<TextBlock>(container, "NameTaskSpeed");
            var failureDetails = Named<Button>(container, "NameTaskFailureDetailsButton");
            var failurePanel = Named<Border>(container, "NameTaskFailureDetailsPanel");
            var toggle = Named<Button>(container, "NameTaskToggleButton");
            var delete = Named<Button>(container, "NameTaskDeleteButton");

            Assert.Equal(86, row.Bounds.Height);
            Assert.Equal(40, iconFrame.Bounds.Width);
            Assert.Equal(40, iconFrame.Bounds.Height);
            Assert.Equal(6, progress.Bounds.Height);
            Assert.Equal("31%", percentage.Text);
            Assert.Equal("暂停中……", item.DownloadStatusTitle);
            Assert.Equal("285.92 MB / 4.55 GB", size.Text);
            Assert.Equal("2.62 MB/s", speed.Text);
            Assert.False(failureDetails.IsVisible);
            Assert.False(failurePanel.IsVisible);

            Assert.True(LeftInView(title, view) < LeftInView(progress, view));
            Assert.True(RightInView(progress, view) < LeftInView(toggle, view));
            Assert.True(TopInView(metadata, view) > TopInView(title, view));
            Assert.True(TopInView(progress, view) > TopInView(status, view));
            Assert.True(TopInView(size, view) > TopInView(progress, view));
            Assert.True(TopInView(speed, view) > TopInView(progress, view));

            Assert.Equal(36, toggle.Bounds.Width);
            Assert.Equal(36, toggle.Bounds.Height);
            Assert.Equal(36, delete.Bounds.Width);
            Assert.Equal(36, delete.Bounds.Height);
            Assert.Equal(
                ResourceColor(application, "SpaceInk", theme),
                SolidColor(toggle.Foreground));
            Assert.NotEqual(
                ResourceColor(application, "SpaceAccent", theme),
                SolidColor(toggle.Foreground));
            Assert.Equal(
                ResourceColor(application, "DownloadWarning", theme),
                SolidColor(delete.Foreground));
            Assert.Equal(
                ResourceColor(application, "DownloadWarning", theme),
                SolidColor(Named<ShapePath>(delete, null).Fill));
            Assert.Equal(
                ResourceColor(application, "SpaceHover", theme),
                SolidColor(iconFrame.Background));

            var track = Named<Border>(progress, "PART_Track");
            var indicator = Named<Border>(progress, "PART_Indicator");
            Assert.Equal(0, track.CornerRadius.TopLeft);
            Assert.Equal(
                ResourceColor(application, "SpaceRule", theme),
                SolidColor(track.Background));
            Assert.Equal(
                ResourceColor(application, "SpaceAccent", theme),
                SolidColor(indicator.Background));

            foreach (var button in new[] { toggle, delete })
            {
                var chrome = Assert.Single(
                    button.GetVisualDescendants().OfType<Border>());
                Assert.Equal(
                    ResourceColor(application, "SpacePaper", theme),
                    SolidColor(chrome.Background));
            }

            var hoverPoint = container.TranslatePoint(new Point(8, 8), window)
                ?? throw new InvalidOperationException("Could not translate task-row hover point.");
            window.MouseMove(hoverPoint);
            window.UpdateLayout();
            Assert.True(container.IsPointerOver);
            Assert.Equal(
                ResourceColor(application, "SpaceHover", theme),
                SolidColor(row.Background));
        }
        finally
        {
            window.Close();
        }
    }

    private static T Named<T>(Control root, string? name)
        where T : Control
    {
        var controls = root.GetVisualDescendants().OfType<T>();
        return name is null
            ? Assert.Single(controls)
            : Assert.Single(controls, control => control.Name == name);
    }

    private static double LeftInView(Control control, ViewDownloading view)
    {
        return control.TranslatePoint(new Point(0, 0), view)!.Value.X;
    }

    private static double RightInView(Control control, ViewDownloading view)
    {
        return LeftInView(control, view) + control.Bounds.Width;
    }

    private static double TopInView(Control control, ViewDownloading view)
    {
        return control.TranslatePoint(new Point(0, 0), view)!.Value.Y;
    }

    private static Color ResourceColor(
        Avalonia.Application application,
        string key,
        ThemeVariant theme)
    {
        Assert.True(application.TryGetResource(key, theme, out var value));
        return SolidColor(value);
    }

    private static Color SolidColor(object? value)
    {
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }
}
