using System.Windows;
using FreeKSeF.App.ViewModels;

namespace FreeKSeF.App.Views;

/// <summary>Okno podgladu i edycji lokalnej tabeli kursow walut.</summary>
public partial class KursyWindow : Window
{
    public KursyWindow()
    {
        InitializeComponent();
        DataContext = new KursyViewModel();
    }
}
