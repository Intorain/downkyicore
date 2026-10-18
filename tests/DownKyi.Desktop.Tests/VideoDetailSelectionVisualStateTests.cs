using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using DownKyi.Presentation;
using DownKyi.Views;
using ShapePath = Avalonia.Controls.Shapes.Path;
using ShapeRectangle = Avalonia.Controls.Shapes.Rectangle;

namespace DownKyi.Desktop.Tests;

public sealed class VideoDetailSelectionVisualStateTests
{
    [AvaloniaFact]
    public Task VideoListDoesNotExposePerItemPlaybackSelectors()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            var application = DesktopTestResources.EnsureProductThemeResources();
            var fluentTheme = new FluentTheme();
            var dataGridStyles = new StyleInclude(new Uri("avares://DownKyi.Desktop.Tests/"))
            {
                Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
            };
            application.Styles.Insert(0, fluentTheme);
            application.Styles.Insert(1, dataGridStyles);
            var view = new VideoDetailSelectionView();
            var dataGrid = Assert.IsType<DataGrid>(view.FindControl<DataGrid>("NameVideoPages"));
            dataGrid.AutoGenerateColumns = false;
            dataGrid.ItemsSource = new[] { CreatePage("Download preference fixture") };
            var window = new Window { Content = view, Width = 960, Height = 540 };

            try
            {
                window.Show();
                window.UpdateLayout();

                Assert.Equal(4, dataGrid.Columns.Count);
                Assert.Empty(dataGrid.GetVisualDescendants().OfType<ComboBox>());
            }
            finally
            {
                window.Close();
                application.Styles.Remove(dataGridStyles);
                application.Styles.Remove(fluentTheme);
            }
        });
    }

    [AvaloniaFact]
    public Task ToolbarSearchKeepsOneRoundedFocusOutlineAcrossThemes()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            var application = DesktopTestResources.EnsureProductThemeResources();
            var originalTheme = application.RequestedThemeVariant;
            var fluentTheme = new FluentTheme();
            application.Styles.Insert(0, fluentTheme);
            var view = new VideoDetailToolbarView();
            var window = new Window
            {
                Content = view,
                Width = 960,
                Height = 80
            };

            try
            {
                window.Show();
                var input = Assert.Single(view.GetVisualDescendants().OfType<TextBox>());
                var shell = Assert.Single(input.GetVisualAncestors().OfType<Border>());
                Assert.True(input.Focus());

                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    application.RequestedThemeVariant = theme;
                    window.UpdateLayout();

                    var focusBorder = Assert.Single(
                        input.GetVisualDescendants().OfType<Border>(),
                        border => border.Name == "PART_BorderElement");
                    Assert.True(shell.IsKeyboardFocusWithin);
                    Assert.Equal(default, shell.BorderThickness);
                    Assert.Equal(new CornerRadius(16), focusBorder.CornerRadius);
                    Assert.Equal(new Thickness(1), focusBorder.BorderThickness);
                    Assert.Equal(
                        ResourceColor(application, "BrushControlStrokeFocus", theme),
                        SolidColor(focusBorder.BorderBrush));
                }
            }
            finally
            {
                application.RequestedThemeVariant = originalTheme;
                window.Close();
                application.Styles.Remove(fluentTheme);
            }
        });
    }

    [AvaloniaFact]
    public Task SelectionAndActionsUseNeutralVisibleSurfacesWithoutClipping()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            var application = DesktopTestResources.EnsureProductThemeResources();
            var originalTheme = application.RequestedThemeVariant;
            var fluentTheme = new FluentTheme();
            var dataGridStyles = new StyleInclude(
                new Uri("avares://DownKyi.Desktop.Tests/"))
            {
                Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
            };
            application.Styles.Insert(0, fluentTheme);
            application.Styles.Insert(1, dataGridStyles);

            var page = CreatePage("Selected fixture");
            var selection = new VideoDetailSelectionView();
            var dataGrid = Assert.IsType<DataGrid>(
                selection.FindControl<DataGrid>("NameVideoPages"));
            dataGrid.AutoGenerateColumns = false;
            dataGrid.ItemsSource = new[] { page };
            dataGrid.SelectedItem = page;
            var actions = new VideoDetailActionsView();
            var host = new Grid();
            host.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            host.RowDefinitions.Add(new RowDefinition(new GridLength(52)));
            Grid.SetRow(selection, 0);
            Grid.SetRow(actions, 1);
            host.Children.Add(selection);
            host.Children.Add(actions);
            var window = new Window
            {
                Content = host,
                Width = 960,
                Height = 540
            };

            try
            {
                window.Show();
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    application.RequestedThemeVariant = theme;
                    window.UpdateLayout();

                    var row = Assert.Single(dataGrid.GetVisualDescendants().OfType<DataGridRow>());
                    var selectionFill = Assert.Single(
                        row.GetVisualDescendants().OfType<ShapeRectangle>(),
                        rectangle => rectangle.Name == "BackgroundRectangle");
                    Assert.True(row.IsSelected);
                    Assert.True(selectionFill.IsVisible);
                    Assert.Equal(
                        ResourceColor(application, "BrushSelectionFill", theme),
                        SolidColor(selectionFill.Fill));
                    Assert.NotEqual(
                        ResourceColor(application, "BrushPrimary", theme),
                        SolidColor(selectionFill.Fill));
                    Assert.Equal(
                        ResourceColor(application, "BrushSurfaceSunken", theme),
                        SolidColor(dataGrid.Background));
                    Assert.Equal(
                        ResourceColor(application, "BrushBorderSubtle", theme),
                        SolidColor(dataGrid.BorderBrush));

                    var actionSurface = Assert.IsType<Border>(actions.Content);
                    Assert.Equal(
                        ResourceColor(application, "BrushSurfaceRaised", theme),
                        SolidColor(actionSurface.Background));
                    var actionButtons = actions
                        .GetVisualDescendants()
                        .OfType<Button>()
                        .Where(button => button.MinWidth >= 88)
                        .ToArray();
                    Assert.Equal(2, actionButtons.Length);
                    Assert.All(
                        actionButtons,
                        button => Assert.Equal(
                            ResourceColor(application, "BrushTextPrimary", theme),
                            SolidColor(button.Foreground)));
                    Assert.All(actionButtons, button => Assert.True(button.Bounds.Height >= 36));
                    Assert.All(
                        actionButtons,
                        button => Assert.True(
                            button.TranslatePoint(
                                new Point(0, button.Bounds.Height),
                                actionSurface)!.Value.Y <= actionSurface.Bounds.Height));
                }
            }
            finally
            {
                application.RequestedThemeVariant = originalTheme;
                window.Close();
                application.Styles.Remove(dataGridStyles);
                application.Styles.Remove(fluentTheme);
            }
        });
    }

    [AvaloniaFact]
    public Task SectionBackButtonsKeepTheirPillAndIconInsideTheHeader()
    {
        return AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureProductThemeResources();
            var fluentTheme = new FluentTheme();
            Avalonia.Application.Current!.Styles.Insert(0, fluentTheme);
            var window = new Window { Width = 360, Height = 180 };

            try
            {
                foreach (var page in new Control[] { new ViewSettings(), new ViewToolbox() })
                {
                    window.Content = page;
                    window.Show();
                    window.UpdateLayout();

                    var back = Assert.Single(page.GetVisualDescendants().OfType<Button>());
                    var chrome = Assert.Single(back.GetVisualDescendants().OfType<Border>());
                    var icon = Assert.Single(back.GetVisualDescendants().OfType<ShapePath>());
                    Assert.True(back.Bounds.Height >= 36);
                    Assert.Equal(back.Bounds.Height, chrome.Bounds.Height);
                    Assert.True(icon.Bounds.Height <= chrome.Bounds.Height);
                    Assert.True(icon.Bounds.Width <= chrome.Bounds.Width);
                }
            }
            finally
            {
                window.Close();
                Avalonia.Application.Current!.Styles.Remove(fluentTheme);
            }
        });
    }

    private static VideoPage CreatePage(string name)
    {
        return new VideoPage
        {
            Order = 1,
            Name = name,
            Duration = "01:00"
        };
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
