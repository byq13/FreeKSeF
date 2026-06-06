using System.Windows.Controls;
using System.Windows.Input;
using FreeKSeF.App.Services;

namespace FreeKSeF.App.Views;

public partial class ZakupView : UserControl
{
    public ZakupView()
    {
        InitializeComponent();
    }

    private void Grid_RightClick(object sender, MouseButtonEventArgs e)
        => GridPomocnik.ZaznaczWierszPodKursorem((DataGrid)sender, e.OriginalSource as System.Windows.DependencyObject);

    private void Grid_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var g = (DataGrid)sender;
        GridPomocnik.PrzywrocKolumny(g, (string)g.Tag);
    }

    private void Grid_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var g = (DataGrid)sender;
        GridPomocnik.ZapiszKolumny(g, (string)g.Tag);
    }

    private void Grid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        var g = (DataGrid)sender!;
        GridPomocnik.ZapiszKolumny(g, (string)g.Tag);
    }
}
