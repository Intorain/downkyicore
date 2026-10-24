using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace DownKyi.Views;

internal partial class ViewPublication : UserControl
{
    public ViewPublication()
    {
        InitializeComponent();
    }

    private bool? _compact;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var compact = e.NewSize.Width < 1000;
        if (_compact == compact)
        {
            return;
        }
        _compact = compact;
        PublicationBody.ColumnDefinitions[0].Width = compact ? GridLength.Star : new GridLength(184);
        PublicationBody.ColumnDefinitions[1].Width = compact ? new GridLength(0) : GridLength.Star;
        Grid.SetRowSpan(NameLeftTabHeaders, compact ? 1 : 2);
        Grid.SetColumn(NameMediaPanel, compact ? 0 : 1);
        Grid.SetRow(NameMediaPanel, compact ? 1 : 0);
        Grid.SetRowSpan(NameMediaPanel, compact ? 1 : 2);
        NameLeftTabHeaders.MaxHeight = compact ? 88 : double.PositiveInfinity;
        NameLeftTabHeaders.Margin = compact ? new Thickness(0, 0, 0, 12) : default;
        NameLeftTabHeaders.ItemsPanel = new FuncTemplate<Panel?>(() => compact ? new WrapPanel() : new StackPanel());
    }
}
