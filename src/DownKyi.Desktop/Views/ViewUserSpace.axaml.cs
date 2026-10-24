using Avalonia;
using Avalonia.Controls;

namespace DownKyi.Views;

internal partial class ViewUserSpace : UserControl
{
    public ViewUserSpace()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var compact = e.NewSize.Width < 1100;
        CreatorArtwork.IsVisible = !compact;
        CreatorIdentity.ColumnDefinitions[2].Width = new GridLength(compact ? 0 : 24);
        CreatorIdentity.ColumnDefinitions[3].Width = new GridLength(compact ? 0 : 220);
        CreatorNavigation.ColumnDefinitions[1].Width = compact ? new GridLength(0) : GridLength.Auto;
        Grid.SetColumn(NameTabRightBanners, compact ? 0 : 1);
        Grid.SetRow(NameTabRightBanners, compact ? 1 : 0);
    }
}
