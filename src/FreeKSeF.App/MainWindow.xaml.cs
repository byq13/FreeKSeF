using System.Windows;
using System.Windows.Controls;
using FreeKSeF.App.ViewModels;

namespace FreeKSeF.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Odswieza liste/formularz przy wejsciu na zakladke (np. po wystawieniu faktury).</summary>
    private void Zakladki_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged bubbluje tez z wewnetrznych kontrolek (DataGrid/ComboBox) - reagujemy tylko na TabControl.
        if (!ReferenceEquals(e.Source, sender)) return;
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not TabItem tab) return;

        var dc = (tab.Content as FrameworkElement)?.DataContext;
        switch (dc)
        {
            case FakturaListaViewModelBase lista:
                lista.Odswiez();
                break;
            case NewInvoiceViewModel nowa:
                nowa.Odswiez();
                break;
            case ContractorsViewModel kontrahenci:
                kontrahenci.Wczytaj();
                break;
            case ProductsViewModel produkty:
                produkty.Wczytaj();
                break;
            case SettingsViewModel ustawienia:
                ustawienia.Wczytaj();
                break;
        }
    }
}
