using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using DownKyi.Application.Desktop;
using DownKyi.Images;
using DownKyi.Infrastructure.Settings;
using DownKyi.Platform;
using DownKyi.Presentation;
using DownKyi.ViewModels.UserSpace;
using DownKyi.Views;
using DownKyi.Views.UserSpace;

namespace DownKyi.Desktop.Tests;

public sealed class RemainingUiLayoutTests
{
    private static readonly string[] CreatorTabs = ["投稿", "合集和系列", "收藏夹"];
    private static readonly string[] CreatorStatistics = ["关注数", "粉丝数", "获赞数", "播放数"];
    private static readonly Size[] Viewports =
    [
        new(798, 518), new(960, 1000), new(999, 700), new(1000, 700),
        new(1099, 700), new(1100, 700), new(1179, 619), new(1180, 620),
        new(1440, 900), new(1920, 1080), new(798, 518)
    ];

    [AvaloniaFact]
    public void CreatorCategoryWhitespaceRespondsToHoverAndClick()
    {
        WithThemes(() =>
        {
            var view = new ViewArchive();
            var list = view.FindControl<ListBox>("NamePublicationZones")!;
            var category = new PublicationZone { Name = "动画", Count = 6 };
            list.ItemsSource = new[] { category };
            var window = new Window { Content = view, Width = 800, Height = 550 };
            try
            {
                window.Show();
                Settle(window);
                var item = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
                var point = item.TranslatePoint(new Point(150, 45), window)!.Value;
                window.MouseMove(point);
                Settle(window);
                Assert.True(item.IsPointerOver);
                var theme = Avalonia.Application.Current!.ActualThemeVariant;
                Assert.True(Avalonia.Application.Current.TryGetResource("SpaceHover", theme, out var hover));
                Assert.Contains(item.GetVisualDescendants().OfType<Border>(),
                    border => border.Background is ISolidColorBrush brush && brush.Color == ((ISolidColorBrush)hover!).Color);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.Same(category, list.SelectedItem);
            }
            finally { window.Close(); }
        });
    }

    [AvaloniaFact]
    public void LibraryResizingPreservesSelectionAndInputWhileActionsStayClickable()
    {
        WithThemes(() =>
        {
            using var navigation = new AvaloniaNavigationService(
                route => throw new InvalidOperationException($"Unexpected navigation: {route}"), action => action());
            using var settings = new SettingsStore(Path.Combine(Path.GetTempPath(), $"downkyi-ui-{Guid.NewGuid():N}.json"));
            var publication = new ViewPublication();
            var series = new ViewSeasonsSeriesDetail();
            var favorites = new ViewPublicFavorites();
            var publicationItems = publication.FindControl<ListBox>("NameMedias")!;
            publicationItems.ItemsSource = Enumerable.Range(1, 30).Select(i => new PublicationMedia(navigation, AppRoute.Publication)
            {
                Title = $"第 {i} 集 · 长影片名称与比例、字体及版式设计的练习",
                Duration = "24:30",
                PlayNumber = "12.8万",
                CreateTime = "2026-10-23"
            }).ToArray();
            var tabs = publication.FindControl<ListBox>("NameLeftTabHeaders")!;
            tabs.ItemsSource = Enumerable.Range(1, 12).Select(i => new TabHeader { Id = i, Title = $"分类 {i}", SubTitle = "24" }).ToArray();
            var seriesItems = series.FindControl<ListBox>("NameMedias")!;
            seriesItems.ItemsSource = Enumerable.Range(1, 30).Select(i => new ChannelMedia(navigation, AppRoute.SeasonsSeries)
            {
                Title = $"第 {i} 集 · 很长的合集标题与原始资料",
                Duration = "24:30"
            }).ToArray();
            var favoritesItems = favorites.FindControl<ListBox>("NameFavoritesMedias")!;
            favoritesItems.ItemsSource = Enumerable.Range(1, 30).Select(i => new FavoritesMedia(navigation, AppRoute.PublicFavorites, settings)
            {
                Order = i,
                Title = $"第 {i} 集 · 很长的收藏内容名称与原始资料",
                UpName = "示例创作者",
                Duration = "24:30"
            }).ToArray();
            favorites.FindControl<Grid>("PublicFavoritesBody")!.IsVisible = true;

            foreach (var (view, list, footerName) in new (Control, ListBox, string)[]
            {
                (publication, publicationItems, "LibraryToolbar"),
                (series, seriesItems, "LibraryToolbar"),
                (favorites, favoritesItems, "PublicFavoritesFooter")
            })
            {
                var arrow = view.FindControl<Avalonia.Controls.Shapes.Path>("BackArrowPath")!;
                arrow.Data = Geometry.Parse(NavigationIcon.CreateArrowBack().Data!);
                var window = new Window { Content = view, Width = 1440, Height = 900 };
                try
                {
                    window.Show();
                    Settle(window);
                    list.SelectedIndex = 1;
                    var selected = list.SelectedItem;
                    var search = view == publication ? view.GetVisualDescendants().OfType<TextBox>().First() : null;
                    if (search is not null)
                    {
                        search.Text = "keep this search";
                        Assert.True(search.Focus());
                    }
                    var footer = view.FindControl<Control>(footerName)!;
                    foreach (var size in Viewports)
                    {
                        window.Width = size.Width;
                        window.Height = size.Height;
                        Settle(window);
                        Assert.Equal(size, view.Bounds.Size);
                        Assert.Same(selected, list.SelectedItem);
                        Assert.True(list.Bounds.Height >= 100);
                        AssertFits(list, view);
                        AssertFits(footer, view);
                        Assert.All(footer.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible), button => AssertFits(button, view));
                        if (search is not null)
                        {
                            Assert.Equal("keep this search", search.Text);
                            Assert.True(search.IsFocused);
                            AssertFits(search, view);
                        }
                        var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
                    }
                    var clicked = false;
                    var action = footer.GetVisualDescendants().OfType<Button>().Last();
                    action.Command = new RelayCommand(() => clicked = true);
                    var point = action.TranslatePoint(new Point(action.Bounds.Width / 2, action.Bounds.Height / 2), window)!.Value;
                    window.MouseDown(point, MouseButton.Left);
                    window.MouseUp(point, MouseButton.Left);
                    Assert.True(clicked);
                    var back = view.FindControl<Button>("LibraryBackButton")!;
                    Assert.InRange(arrow.TranslatePoint(default, back)!.Value.X, 0, 16);
                }
                finally { window.Close(); }
            }
        });
    }

    [AvaloniaFact]
    public void CreatorTabsAndAllChildPagesRemainReachableAcrossBreakpoints()
    {
        WithThemes(() =>
        {
            var view = new ViewUserSpace();
            var host = view.FindControl<ContentControl>("CreatorContent")!;
            view.FindControl<Grid>("CreatorIdentity")!.IsVisible = true;
            view.FindControl<Grid>("CreatorNavigation")!.IsVisible = true;
            host.IsVisible = true;
            var tabs = view.FindControl<ListBox>("NameTabLeftBanners")!;
            tabs.ItemsSource = CreatorTabs.Select((t, i) => new TabLeftBanner { Id = i, Title = t, Icon = NormalIcon.Current.Favorite }).ToArray();
            view.FindControl<ListBox>("NameTabRightBanners")!.ItemsSource = CreatorStatistics.Select(t => new TabRightBanner { Label = t, Count = "1.2万", IsEnabled = true }).ToArray();
            var archive = new ViewArchive();
            archive.FindControl<ListBox>("NamePublicationZones")!.ItemsSource = Enumerable.Range(1, 100).Select(i => new PublicationZone { Name = $"投稿分类 {i}", Count = 120 }).ToArray();
            var folders = new ViewFavorites();
            folders.FindControl<ListBox>("NameFavorites")!.ItemsSource = Enumerable.Range(1, 100).Select(i => new FavoriteFolder(i, "", NormalIcon.Current.Favorite, "很长的收藏夹名称与不同的收藏内容", 120, "2026-10-23")).ToArray();
            var series = new ViewUserSpaceSeasonsSeries();
            series.FindControl<ListBox>("NameSeasonsSeries")!.ItemsSource = Enumerable.Range(1, 100).Select(i => new SeasonsSeries { Id = i, Name = "很长的合集和系列名称与不同内容", Count = 120, TypeImage = NormalIcon.Current.Favorite }).ToArray();
            var window = new Window { Content = view, Width = 1440, Height = 900 };
            try
            {
                window.Show();
                foreach (var child in new Control[] { archive, folders, series })
                {
                    host.Content = child;
                    tabs.SelectedIndex = 1;
                    foreach (var size in Viewports)
                    {
                        window.Width = size.Width;
                        window.Height = size.Height;
                        Settle(window);
                        Assert.Same(child, host.Content);
                        Assert.Equal(1, tabs.SelectedIndex);
                        AssertFits(host, view);
                        Assert.True(host.Bounds.Height >= 100);
                        var scroll = child.GetVisualDescendants().OfType<ScrollViewer>().First();
                        Assert.True(scroll.Viewport.Height > 0);
                        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
                        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
                        scroll.ScrollToEnd();
                        Settle(window);
                        Assert.True(scroll.Offset.Y > 0);
                    }
                }
            }
            finally { window.Close(); }
        });
    }

    [AvaloniaFact]
    public void ContextMenuUsesTheStudioPaletteAndStillExecutesItsAction()
    {
        WithThemes(() =>
        {
            var clicked = false;
            var menu = new ContextMenu();
            var item = new MenuItem { Header = "复制封面链接", Command = new RelayCommand(() => clicked = true) };
            menu.Items.Add(item);
            var target = new Button { Content = "封面", ContextMenu = menu };
            var window = new Window { Content = target, Width = 800, Height = 550 };
            try
            {
                window.Show();
                menu.Open(target);
                Settle(window);
                Assert.True(menu.IsOpen);
                Assert.Equal(default, menu.CornerRadius);
                var theme = Avalonia.Application.Current!.RequestedThemeVariant!;
                Assert.True(Avalonia.Application.Current.TryGetResource("SpacePaper", theme, out var paper));
                Assert.Equal(((ISolidColorBrush)paper!).Color, ((ISolidColorBrush)menu.Background!).Color);
                item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.True(clicked);
            }
            finally { menu.Close(); window.Close(); }
        });
    }

    private static void WithThemes(Action action)
    {
        var application = DesktopTestResources.EnsureProductThemeResources();
        DesktopTestResources.EnsureDownloadProjectionResources();
        var original = application.RequestedThemeVariant;
        var fluent = new FluentTheme();
        var chrome = new StyleInclude(new Uri("avares://DownKyi.Desktop.Tests/")) { Source = new Uri("avares://DownKyi.Desktop/Themes/Styles/StudioChrome.axaml") };
        application.Styles.Insert(0, fluent);
        application.Styles.Add(chrome);
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                application.RequestedThemeVariant = theme;
                action();
            }
        }
        finally
        {
            application.RequestedThemeVariant = original;
            application.Styles.Remove(chrome);
            application.Styles.Remove(fluent);
        }
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static void AssertFits(Control control, Control view)
    {
        var origin = control.TranslatePoint(default, view)!.Value;
        Assert.InRange(origin.X, 0, view.Bounds.Width - control.Bounds.Width + 1);
        Assert.InRange(origin.Y, 0, view.Bounds.Height - control.Bounds.Height + 1);
    }
}
