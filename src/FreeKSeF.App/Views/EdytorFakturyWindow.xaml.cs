using System.Windows;
using FreeKSeF.App.ViewModels;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.Views;

/// <summary>
/// Okno edytora zaawansowanego / viewera XML: tabela wszystkich pol FA(3).
/// Robocze faktury sprzedazy sa edytowalne, pozostale tylko do odczytu.
/// </summary>
public partial class EdytorFakturyWindow : Window
{
    private readonly EdytorFakturyViewModel _vm;

    public EdytorFakturyWindow(Invoice faktura)
    {
        InitializeComponent();
        _vm = new EdytorFakturyViewModel(faktura);
        DataContext = _vm;
    }

    /// <summary>True, gdy zapisano zmiany - wolajacy powinien odswiezyc liste.</summary>
    public bool Zapisano => _vm.Zapisano;
}
