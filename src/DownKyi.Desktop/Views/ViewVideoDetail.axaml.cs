using System;
using Avalonia;
using Avalonia.Controls;

namespace DownKyi.Views;

internal partial class ViewVideoDetail : UserControl
{
    public ViewVideoDetail()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var wide = e.NewSize.Width >= 1180 && e.NewSize.Height >= 620;
        DetailContentGrid.ColumnDefinitions[0].Width = wide ? new GridLength(320) : GridLength.Star;
        DetailContentGrid.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
        DetailContentGrid.RowDefinitions[0].Height = wide ? GridLength.Star : new GridLength(e.NewSize.Height < 650 ? Math.Min(156, e.NewSize.Height / 4) : 208);
        DetailContentGrid.RowDefinitions[1].Height = wide ? new GridLength(0) : GridLength.Star;
        Grid.SetColumn(DetailWorkspace, wide ? 1 : 0);
        Grid.SetRow(DetailWorkspace, wide ? 0 : 1);
        DetailWorkspace.Margin = wide ? new Thickness(28, 0, 0, 0) : new Thickness(0, 16, 0, 0);
    }
}
