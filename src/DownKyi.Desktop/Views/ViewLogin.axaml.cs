using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DownKyi.Views;

internal partial class ViewLogin : UserControl
{
    public ViewLogin()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var stacked = e.NewSize.Width < 1000 && e.NewSize.Height >= 760;
        LoginComposition.ColumnDefinitions[0].Width = GridLength.Star;
        LoginComposition.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : GridLength.Star;
        Grid.SetColumn(LoginScanPanel, stacked ? 0 : 1);
        Grid.SetRow(LoginScanPanel, stacked ? 1 : 0);
        LoginIntro.Margin = stacked ? new Thickness(0, 0, 0, 28) : new Thickness(0, 0, 32, 0);
        LoginIntro.HorizontalAlignment = stacked ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        LoginIntro.Width = stacked ? 264 : double.NaN;
        LoginHeadline.FontSize = e.NewSize.Height < 650 ? 36 : 44;
    }
}
