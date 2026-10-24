using Avalonia;
using Avalonia.Controls;

namespace DownKyi.Views;

internal partial class ViewPublicFavorites : UserControl
{
    public ViewPublicFavorites()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var compact = e.NewSize.Width < 1180 || e.NewSize.Height < 620;
        PublicFavoritesBody.ColumnDefinitions[0].Width = compact ? GridLength.Star : new GridLength(296);
        PublicFavoritesBody.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 24);
        PublicFavoritesBody.ColumnDefinitions[2].Width = compact ? new GridLength(0) : GridLength.Star;
        if (PublicFavoritesBody.RowDefinitions.Count != 3)
        {
            PublicFavoritesBody.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        PublicFavoritesBody.RowDefinitions[0].Height = compact ? new GridLength(e.NewSize.Height < 700 ? 136 : 200) : GridLength.Star;
        PublicFavoritesBody.RowDefinitions[1].Height = compact ? GridLength.Star : new GridLength(0);
        Grid.SetRow(PublicFavoritesFooter, 2);
        Grid.SetColumn(NameFavoritesMedias, compact ? 0 : 2);
        Grid.SetRow(NameFavoritesMedias, compact ? 1 : 0);
        Grid.SetColumn(PublicMediaEmptyState, compact ? 0 : 2);
        Grid.SetRow(PublicMediaEmptyState, compact ? 1 : 0);
        Grid.SetColumn(PublicMediaLoading, compact ? 0 : 2);
        Grid.SetRow(PublicMediaLoading, compact ? 1 : 0);
        FavoritesSummary.Margin = compact ? new Thickness(0, 0, 0, 12) : default;
        FavoritesSummaryLayout.ColumnDefinitions[0].Width = new GridLength(compact ? 176 : 0);
        Grid.SetColumn(FavoritesCover, compact ? 0 : 1);
        Grid.SetRow(FavoritesMetadata, compact ? 0 : 1);
        FavoritesMetadata.Margin = compact ? new Thickness(16, 0, 0, 0) : default;
    }
}
