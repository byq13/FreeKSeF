using System.Windows.Controls;
using FreeKSeF.App.ViewModels;

namespace FreeKSeF.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        // PasswordBox nie wspiera bindowania - token przekazujemy do ViewModelu recznie.
        TokenBox.PasswordChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                vm.NowyToken = TokenBox.Password;
        };
        HasloBox.PasswordChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                vm.NoweHaslo = HasloBox.Password;
        };
    }

    /// <summary>Otwiera okno podgladu/edycji tabeli kursow walut.</summary>
    private void KursyWalut_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var okno = new KursyWindow { Owner = System.Windows.Application.Current.MainWindow };
        okno.ShowDialog();
    }
}
