using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DownKyi.Presentation;
using DownKyi.Views;
using DownKyi.Views.Settings;
using DownKyi.Views.Toolbox;

namespace DownKyi.Desktop.Tests;

public sealed class StudioResponsiveLayoutTests
{
    private static readonly Size[] Viewports =
    [
        new(800, 550), new(960, 1000), new(1049, 700), new(1050, 700),
        new(1440, 900), new(1920, 1080), new(800, 550)
    ];

    private static readonly string[] SettingsTitles = ["基本设置", "网络设置", "视频设置", "弹幕设置", "关于与反馈"];
    private static readonly string[] ToolboxTitles = ["去水印", "提取音视频", "B站小工具"];
    private static readonly string[] Choices = ["第一项", "第二项"];

    [AvaloniaFact]
    public Task EveryWorkspacePageKeepsControlsInsideTheAvailableWidthWhenResized()
    {
        return AvaloniaTestDispatcher.RunAsync(() => WithThemes(() =>
        {
            AssertPage(new ViewToolbox(), new ViewDelogo(), ToolboxTitles);
            AssertPage(new ViewToolbox(), new ViewExtractMedia(), ToolboxTitles);
            AssertPage(new ViewToolbox(), new ViewBiliHelper(), ToolboxTitles);
            AssertPage(new ViewSettings(), new ViewBasic(), SettingsTitles);
            AssertPage(new ViewSettings(), new ViewNetwork(), SettingsTitles);
            AssertPage(new ViewSettings(), new ViewVideo(), SettingsTitles);
            AssertPage(new ViewSettings(), new ViewDanmaku(), SettingsTitles);
            AssertPage(new ViewSettings(), new ViewAbout(), SettingsTitles);
        }));
    }

    [AvaloniaFact]
    public Task ResizingPreservesInputFocusAndNavigationWhileDropdownSelectionRemainsReadable()
    {
        return AvaloniaTestDispatcher.RunAsync(() => WithThemes(() =>
        {
            var shell = new ViewToolbox();
            var child = new ViewBiliHelper();
            var host = shell.FindControl<ContentControl>("StudioContent")!;
            host.Content = child;
            var tabs = shell.FindControl<ListBox>("NameLeftTabHeaders")!;
            tabs.ItemsSource = ToolboxTitles.Select((title, id) => new TabHeader { Id = id, Title = title });
            tabs.SelectedIndex = 2;
            var window = new Window { Content = shell, Width = 1440, Height = 900 };
            try
            {
                window.Show();
                var input = child.FindControl<TextBox>("NameAvid")!;
                input.Text = "170001";
                input.Focus(NavigationMethod.Tab);
                input.CaretIndex = 3;
                foreach (var size in Viewports)
                {
                    window.Width = size.Width;
                    window.Height = size.Height;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.Same(child, host.Content);
                    Assert.Equal("170001", input.Text);
                    Assert.Equal(3, input.CaretIndex);
                    Assert.True(input.IsFocused);
                    Assert.Equal(2, tabs.SelectedIndex);
                }

                var first = (ListBoxItem)tabs.ContainerFromIndex(0)!;
                var point = first.TranslatePoint(new Point(first.Bounds.Width / 2, first.Bounds.Height / 2), window)!.Value;
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.Equal(0, tabs.SelectedIndex);

                var network = new BuiltinDownloaderSettingsView();
                host.Content = network;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var combo = network.FindControl<ComboBox>("NameMaxCurrentDownloads")!;
                combo.ItemsSource = Choices;
                combo.SelectedIndex = 1;
                combo.IsDropDownOpen = true;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var selected = (ComboBoxItem)combo.ContainerFromIndex(1)!;
                var presenter = selected.GetVisualDescendants()
                    .OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                    .Single(control => control.Name == "PART_ContentPresenter");
                var app = Avalonia.Application.Current!;
                Assert.True(app.TryGetResource("SpaceSelection", app.RequestedThemeVariant, out var brush));
                Assert.Equal(((ISolidColorBrush)brush!).Color, ((ISolidColorBrush)presenter.Background!).Color);
                combo.IsDropDownOpen = false;
            }
            finally
            {
                window.Close();
            }
        }));
    }

    private static void AssertPage(UserControl shell, UserControl child, string[] titles)
    {
        shell.FindControl<ContentControl>("StudioContent")!.Content = child;
        var tabs = shell.FindControl<ListBox>("NameLeftTabHeaders")!;
        tabs.ItemsSource = titles.Select((title, id) => new TabHeader { Id = id, Title = title });
        tabs.SelectedIndex = titles.Length - 1;
        var window = new Window { Content = shell, Width = 800, Height = 550 };
        try
        {
            window.Show();
            foreach (var size in Viewports)
            {
                window.Width = size.Width;
                window.Height = size.Height;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var contentBounds = BoundsInWindow(child, window);
                var navigationBounds = BoundsInWindow(tabs, window);
                Assert.True(contentBounds.Width >= 700, $"{child.GetType().Name}: {contentBounds}");
                Assert.True(contentBounds.Bottom <= window.ClientSize.Height);
                if (size.Width < 1050)
                {
                    Assert.True(navigationBounds.Bottom <= contentBounds.Top);
                }
                else
                {
                    Assert.True(navigationBounds.Right <= contentBounds.Left, $"{child.GetType().Name} at {size}: nav {navigationBounds}, content {contentBounds}");
                }

                foreach (var control in child.GetVisualDescendants().OfType<Control>()
                    .Where(control => control.IsEffectivelyVisible && control is TextBox or ComboBox or Button or CheckBox or RadioButton))
                {
                    var bounds = BoundsInWindow(control, window);
                    Assert.True(bounds.Left >= contentBounds.Left - 0.5 && bounds.Right <= contentBounds.Right + 0.5,
                        $"{child.GetType().Name}/{control.Name} at {size}: {bounds} outside {contentBounds}");
                }

                if (child is ViewDelogo delogo)
                {
                    var preview = BoundsInWindow(delogo.FindControl<Border>("NameVideoPreview")!, window);
                    var geometry = BoundsInWindow(delogo.FindControl<Border>("NameLogoGeometryPanel")!, window);
                    Assert.True(preview.Width >= 360);
                    Assert.True(geometry.Top >= preview.Bottom + 16 || geometry.Left >= preview.Right + 16);
                }

                var scroll = child.GetVisualDescendants().OfType<ScrollViewer>().First();
                scroll.ScrollToEnd();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var lastControl = child.GetVisualDescendants().OfType<Control>()
                    .Where(control => control.IsEffectivelyVisible && control is TextBox or ComboBox or Button or CheckBox)
                    .OrderBy(control => BoundsInWindow(control, window).Bottom).Last();
                Assert.True(BoundsInWindow(lastControl, window).Bottom <= contentBounds.Bottom + 0.5);
                scroll.ScrollToHome();
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static Rect BoundsInWindow(Control control, Window window)
    {
        return new Rect(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);
    }

    private static void WithThemes(Action action)
    {
        var app = DesktopTestResources.EnsureProductThemeResources();
        var original = app.RequestedThemeVariant;
        var fluent = new FluentTheme();
        app.Styles.Insert(0, fluent);
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                app.RequestedThemeVariant = theme;
                action();
            }
        }
        finally
        {
            app.RequestedThemeVariant = original;
            app.Styles.Remove(fluent);
        }
    }
}
