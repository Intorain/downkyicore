using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;

namespace DownKyi.Views;

internal partial class ViewIndex : UserControl
{
    public ViewIndex()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // Reflow the existing controls so resizing preserves input, focus and commands.
        var portrait = e.NewSize.Width < e.NewSize.Height * 1.15;
        Classes.Set("portrait", portrait);
        // Narrower typesetting lets taller windows enlarge both lines without distorting glyphs.
        IndexWordmarkLayout.Width = portrait
            ? Math.Clamp(e.NewSize.Width / e.NewSize.Height * 640, 400, 680)
            : double.NaN;
        IndexLayout.ColumnDefinitions[1].Width = new GridLength(portrait ? 1 : 2.8, GridUnitType.Star);
        IndexLayout.ColumnDefinitions[2].Width = portrait ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        IndexLayout.RowDefinitions[1].Height = new GridLength(portrait ? Math.Clamp(e.NewSize.Height * 0.18, 160, 260) : 0);
        Grid.SetColumn(IndexArtwork, portrait ? 1 : 2);
        Grid.SetRow(IndexArtwork, portrait ? 1 : 0);
        IndexArtLayout.ColumnDefinitions[1].Width = new GridLength(portrait ? 180 : 0);
        IndexArtLayout.RowDefinitions[1].Height = new GridLength(portrait ? 0 : 100);
        Grid.SetColumn(IndexArtCaption, portrait ? 1 : 0);
        Grid.SetRow(IndexArtCaption, portrait ? 0 : 1);
        IndexArtCaption.VerticalAlignment = portrait ? VerticalAlignment.Center : VerticalAlignment.Stretch;
    }
}
