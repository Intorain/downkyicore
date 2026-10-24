using Avalonia;
using Avalonia.Controls;

namespace DownKyi.Views;

internal partial class VideoDetailSummaryView : UserControl
{
    public VideoDetailSummaryView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var aside = e.NewSize.Width < 500;
        SummaryLayout.ColumnDefinitions[0].Width = new GridLength(aside ? 0 : e.NewSize.Width < 900 ? 200 : 260);
        var rows = aside ? 3 : 2;
        if (SummaryLayout.RowDefinitions.Count != rows)
        {
            SummaryLayout.RowDefinitions.Clear();
            for (var row = 0; row < rows; row++)
            {
                SummaryLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
        }
        Grid.SetColumn(SummaryCover, aside ? 1 : 0);
        Grid.SetRowSpan(SummaryCover, aside ? 1 : 2);
        SummaryCover.Height = aside ? 180 : e.NewSize.Width < 900 ? 112.5 : 146.25;
        Grid.SetRow(SummaryMetadata, aside ? 1 : 0);
        SummaryMetadata.Margin = aside ? new Thickness(0, 20, 0, 0) : new Thickness(20, 0, 0, 0);
        SummaryTitle.FontSize = aside ? 28 : 24;
        Grid.SetRow(SummaryDescription, aside ? 2 : 1);
        SummaryDescription.Margin = aside ? new Thickness(0, 16, 0, 0) : new Thickness(20, 16, 0, 0);
    }
}
