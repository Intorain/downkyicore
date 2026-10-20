using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace DownKyi.CustomAction;

internal class ScrollIntoViewBehavior : Behavior<DataGrid>
{
    private int _itemsSourceVersion;
    private bool _shouldScrollIntoView;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            _shouldScrollIntoView = true;
            AssociatedObject.SelectionChanged += OnSelectionChanged;
            AssociatedObject.PropertyChanged += OnGridPropertyChanged;
        }
    }

    protected override void OnDetaching()
    {
        _itemsSourceVersion++;
        _shouldScrollIntoView = false;
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
            AssociatedObject.PropertyChanged -= OnGridPropertyChanged;
        }
    }

    private void OnGridPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != DataGrid.ItemsSourceProperty)
        {
            return;
        }

        _itemsSourceVersion++;
        _shouldScrollIntoView = true;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var dataGrid = AssociatedObject;
        var selectedItem = dataGrid?.SelectedItem;
        if (!_shouldScrollIntoView || dataGrid == null || selectedItem == null)
        {
            return;
        }

        _shouldScrollIntoView = false;
        var itemsSourceVersion = _itemsSourceVersion;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (itemsSourceVersion != _itemsSourceVersion
                    || !ReferenceEquals(dataGrid, AssociatedObject))
                {
                    return;
                }

                try
                {
                    dataGrid.ScrollIntoView(selectedItem, null);
                }
                catch (InvalidOperationException)
                {
                    return;
                }
                catch (ArgumentException)
                {
                    return;
                }
            },
            DispatcherPriority.Background);
    }
}
