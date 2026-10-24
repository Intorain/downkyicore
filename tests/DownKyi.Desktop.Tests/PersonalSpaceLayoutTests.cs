using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using DownKyi.Images;
using DownKyi.Presentation;
using DownKyi.Views;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace DownKyi.Desktop.Tests;

public sealed class PersonalSpaceLayoutTests
{
    private static readonly string[] PackageTitles = ["收藏夹", "我的订阅", "稍后再看", "历史记录", "Bilibili 动态"];

    [AvaloniaFact]
    public Task PersonalHeadersContainTheirContentInEveryInteractionState()
    {
        return AvaloniaTestDispatcher.RunAsync(() => WithThemes(() =>
        {
            foreach (var view in new UserControl[]
            {
                new ViewMySpace(), new ViewFriends(), new ViewMyFavorites(),
                new ViewMyBangumiFollow(), new ViewMyToViewVideo(), new ViewMyHistory()
            })
            {
                var window = new Window { Content = view, Width = 800, Height = 550 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    var header = Assert.IsType<Grid>(view.FindControl<Grid>("PersonalPageHeader"));
                    foreach (var button in header.Children.OfType<Button>())
                    {
                        button.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
                        // Populate bound icons without an account or a network request.
                        foreach (var icon in button.GetVisualDescendants().OfType<ShapePath>())
                        {
                            icon.Width = 24;
                            icon.Height = 24;
                            icon.Data = Geometry.Parse("M 16,4 L 8,12 L 16,20");
                        }

                        window.UpdateLayout();
                        AssertContentInsideSurface(button, view);
                        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window);
                        Assert.NotNull(center);
                        window.MouseMove(center.Value);
                        Assert.True(button.IsPointerOver, $"{view.GetType().Name} header must receive pointer input: {center.Value}.");
                        AssertContentInsideSurface(button, view);
                        window.MouseDown(center.Value, MouseButton.Left);
                        AssertContentInsideSurface(button, view);
                        window.MouseUp(center.Value, MouseButton.Left);
                        button.Focus(NavigationMethod.Tab);
                        window.UpdateLayout();
                        AssertContentInsideSurface(button, view);
                    }
                }
                finally
                {
                    window.Close();
                }
            }
        }));
    }

    [AvaloniaFact]
    public Task PersonalSpaceKeepsEveryEntryReachableInAShortWindow()
    {
        return AvaloniaTestDispatcher.RunAsync(() => WithThemes(() =>
        {
            var view = new ViewMySpace();
            var list = Assert.IsType<ListBox>(view.FindControl<ListBox>("SpacePackageList"));
            list.ItemsSource = PackageTitles
                .Select(title => new SpaceItem { Title = title, IsEnabled = true, Image = NormalIcon.Current.FavoriteOutline })
                .ToArray();
            var scroll = Assert.IsType<ScrollViewer>(view.FindControl<ScrollViewer>("PersonalSpaceScroll"));
            scroll.IsVisible = true;
            var window = new Window { Content = view, Width = 800, Height = 550 };
            try
            {
                window.Show();
                window.UpdateLayout();
                scroll.ScrollToEnd();
                window.UpdateLayout();
                Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
                for (var index = 0; index < 5; index++)
                {
                    var item = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(index));
                    var surface = Assert.Single(item.GetVisualDescendants().OfType<Border>(), border => border.Name == "SpaceEntry");
                    var start = surface.TranslatePoint(default, scroll);
                    var end = surface.TranslatePoint(new Point(surface.Bounds.Width, surface.Bounds.Height), scroll);
                    Assert.NotNull(start);
                    Assert.NotNull(end);
                    Assert.InRange(start.Value.X, 0, scroll.Bounds.Width);
                    Assert.InRange(start.Value.Y, 0, scroll.Bounds.Height);
                    Assert.InRange(end.Value.X, 0, scroll.Bounds.Width);
                    Assert.InRange(end.Value.Y, 0, scroll.Bounds.Height);
                    var title = Assert.Single(item.GetVisualDescendants().OfType<TextBlock>());
                    Assert.True(title.Bounds.Width <= surface.Bounds.Width - 32);
                }
            }
            finally
            {
                window.Close();
            }
        }));
    }

    private static void AssertContentInsideSurface(Button button, UserControl view)
    {
        var surface = Assert.Single(button.GetVisualDescendants().OfType<Border>());
        var content = Assert.IsAssignableFrom<Control>(button.Content);
        var start = content.TranslatePoint(default, surface);
        var end = content.TranslatePoint(new Point(content.Bounds.Width, content.Bounds.Height), surface);
        Assert.NotNull(start);
        Assert.NotNull(end);
        Assert.True(start.Value.X >= 8 && surface.Bounds.Width - end.Value.X >= 8,
            $"{view.GetType().Name} must keep horizontal padding around its entire content.");
        Assert.True(start.Value.Y >= 6 && surface.Bounds.Height - end.Value.Y >= 6,
            $"{view.GetType().Name} must keep vertical padding around its entire content.");
        var position = surface.TranslatePoint(default, view);
        Assert.NotNull(position);
        Assert.InRange(position.Value.X, 0, view.Bounds.Width - surface.Bounds.Width);
    }

    private static void WithThemes(Action assert)
    {
        DesktopTestResources.EnsureDownloadProjectionResources();
        var application = DesktopTestResources.EnsureProductThemeResources();
        var originalTheme = application.RequestedThemeVariant;
        var fluent = new FluentTheme();
        application.Styles.Insert(0, fluent);
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                application.RequestedThemeVariant = theme;
                assert();
            }
        }
        finally
        {
            application.RequestedThemeVariant = originalTheme;
            application.Styles.Remove(fluent);
        }
    }
}
