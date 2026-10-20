using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace DownKyi.CustomAction;

internal class ScrollIntoViewBehavior : Behavior<DataGrid>
{
    private int _itemsSourceVersion;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.PropertyChanged += OnGridPropertyChanged;
            QueueScrollIntoView();
        }
    }

    protected override void OnDetaching()
    {
        _itemsSourceVersion++;
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.PropertyChanged -= OnGridPropertyChanged;
        }
    }

    private void OnGridPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != DataGrid.ItemsSourceProperty)
        {
            return;
        }

        QueueScrollIntoView();
    }

    private void QueueScrollIntoView()
    {
        var dataGrid = AssociatedObject;
        if (dataGrid == null)
        {
            return;
        }

        var itemsSourceVersion = ++_itemsSourceVersion;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (itemsSourceVersion != _itemsSourceVersion
                    || !ReferenceEquals(dataGrid, AssociatedObject))
                {
                    return;
                }

                var selectedItem = dataGrid.SelectedItem;
                if (selectedItem == null)
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
