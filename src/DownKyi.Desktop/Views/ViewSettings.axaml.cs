using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DownKyi.Views;

internal partial class ViewSettings : UserControl
{
    public ViewSettings()
    {
        InitializeComponent();
    }
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var compact = e.NewSize.Width < 1050;
        Classes.Set("compact", compact);
        StudioLayout.ColumnDefinitions[0].Width = new GridLength(compact ? 0 : 208);
        Grid.SetColumn(StudioRail, compact ? 1 : 0);
        Grid.SetRowSpan(StudioRail, compact ? 1 : 2);
        Grid.SetRow(StudioContent, compact ? 1 : 0);
        Grid.SetRowSpan(StudioContent, compact ? 1 : 2);
        StudioContent.Margin = new Thickness(0, compact ? 20 : 0, 0, 0);
    }
}
