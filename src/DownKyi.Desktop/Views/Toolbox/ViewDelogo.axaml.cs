using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DownKyi.Views.Toolbox;

internal partial class ViewDelogo : UserControl
{
    public ViewDelogo()
    {
        InitializeComponent();
    }
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var stacked = e.NewSize.Width < 820;
        DelogoWorkspace.ColumnDefinitions[1].Width = new GridLength(stacked ? 0 : 312);
        Grid.SetColumn(NameLogoGeometryPanel, stacked ? 0 : 1);
        Grid.SetRow(NameLogoGeometryPanel, stacked ? 1 : 0);
        NameLogoGeometryPanel.Margin = new Thickness(stacked ? 0 : 24, stacked ? 20 : 0, 0, 0);
        NameLogoGeometryPanel.Padding = new Thickness(stacked ? 0 : 20, stacked ? 20 : 0, 0, 0);
        NameLogoGeometryPanel.BorderThickness = stacked ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);
    }
}
