using System.Windows;

namespace FreeKSeF.App.Views;

/// <summary>Proste okno pytajace o haslo do tokena KSeF (gdy uzytkownik go zahaslowal).</summary>
public partial class HasloWindow : Window
{
    private HasloWindow(string nazwaFirmy)
    {
        InitializeComponent();
        Opis.Text = $"Token KSeF firmy \"{nazwaFirmy}\" jest chroniony hasłem.\nPodaj hasło, aby go odszyfrować:";
        Loaded += (_, _) => HasloBox.Focus();
    }

    /// <summary>Pyta o haslo. Zwraca null, gdy uzytkownik anulowal.</summary>
    public static string? Zapytaj(string nazwaFirmy)
    {
        var okno = new HasloWindow(nazwaFirmy) { Owner = Application.Current.MainWindow };
        return okno.ShowDialog() == true ? okno.HasloBox.Password : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
