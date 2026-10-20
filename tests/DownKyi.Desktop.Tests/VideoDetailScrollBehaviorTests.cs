using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using DownKyi.CustomAction;
using DownKyi.Presentation;

namespace DownKyi.Desktop.Tests;

public sealed class VideoDetailScrollBehaviorTests
{
    [AvaloniaFact]
    public async Task SelectingAnotherVisiblePageAfterItemsSourceReplacementDoesNotReturnToTheInputPage()
    {
        await AvaloniaTestDispatcher.RunAsync(async () =>
        {
            var application = DesktopTestResources.EnsureProductThemeResources();
            var fluentTheme = new FluentTheme();
            var dataGridStyles = new StyleInclude(
                new Uri("avares://DownKyi.Desktop.Tests/"))
            {
                Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
            };
            application.Styles.Insert(0, fluentTheme);
            application.Styles.Insert(1, dataGridStyles);

            var pages = Enumerable.Range(1, 80)
                .Select(order => new VideoPage
                {
                    Cid = order,
                    Order = order,
                    Name = $"Page {order}"
                })
                .ToArray();
            var inputPage = pages[29];
            var laterPage = pages[69];
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = pages,
                SelectionMode = DataGridSelectionMode.Extended
            };
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Binding = new Binding(nameof(VideoPage.Name))
            });
            var behaviors = Interaction.GetBehaviors(dataGrid);
            behaviors.Add(new VideoPageSelectionBehavior());
            behaviors.Add(new ScrollIntoViewBehavior());
            var window = new Window
            {
                Content = dataGrid,
                Width = 480,
                Height = 240
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                inputPage.IsSelected = true;
                await Task.Delay(150, TestContext.Current.CancellationToken).ConfigureAwait(true);
                await Dispatcher.UIThread.InvokeAsync(
                    window.UpdateLayout,
                    DispatcherPriority.Background);

                var verticalScrollBar = Assert.Single(
                    dataGrid.GetVisualDescendants().OfType<ScrollBar>(),
                    scrollBar => scrollBar.Orientation == Orientation.Vertical);
                var inputPageOffset = verticalScrollBar.Value;
                Assert.True(inputPageOffset > 0);

                dataGrid.ItemsSource = pages.ToArray();
                await Dispatcher.UIThread.InvokeAsync(
                    window.UpdateLayout,
                    DispatcherPriority.Background);
                var replacedInputPageOffset = verticalScrollBar.Value;
                Assert.True(replacedInputPageOffset > 0);

                dataGrid.ScrollIntoView(laterPage, null);
                window.UpdateLayout();
                var laterPageOffset = verticalScrollBar.Value;
                Assert.True(laterPageOffset > replacedInputPageOffset);

                laterPage.IsSelected = true;
                await Task.Delay(150, TestContext.Current.CancellationToken).ConfigureAwait(true);
                await Dispatcher.UIThread.InvokeAsync(
                    window.UpdateLayout,
                    DispatcherPriority.Background);

                Assert.Equal(laterPageOffset, verticalScrollBar.Value);
            }
            finally
            {
                window.Close();
                application.Styles.Remove(dataGridStyles);
                application.Styles.Remove(fluentTheme);
            }
        }).ConfigureAwait(true);
    }
}
