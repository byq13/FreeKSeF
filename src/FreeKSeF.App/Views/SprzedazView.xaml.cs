using System.Windows.Controls;
using System.Windows.Input;
using FreeKSeF.App.Services;

namespace FreeKSeF.App.Views;

public partial class SprzedazView : UserControl
{
    public SprzedazView()
    {
        InitializeComponent();
    }

    private void Grid_RightClick(object sender, MouseButtonEventArgs e)
        => GridPomocnik.ZaznaczWierszPodKursorem((DataGrid)sender, e.OriginalSource as System.Windows.DependencyObject);

    /// <summary>Dwuklik na wierszu = podglad faktury (PDF).</summary>
    private void Grid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!GridPomocnik.KliknietoWiersz(e.OriginalSource as System.Windows.DependencyObject)) return;
        if (DataContext is ViewModels.FakturaListaViewModelBase vm && vm.PodgladCommand.CanExecute(null))
            vm.PodgladCommand.Execute(null);
    }

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

    /// <summary>Menu wyboru widocznych kolumn listy.</summary>
    private void Kolumny_Click(object sender, System.Windows.RoutedEventArgs e)
        => GridPomocnik.PokazWyborKolumn(Grid, (string)Grid.Tag, (System.Windows.UIElement)sender);
}
