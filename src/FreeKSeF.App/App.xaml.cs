using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace FreeKSeF.App;

public partial class App : Application
{
    public App()
    {
        UstawKulturePolska();
        // Globalna obsluga nieoczekiwanych bledow - zamiast cichego zamkniecia.
        DispatcherUnhandledException += OnUnhandledException;
    }

    /// <summary>Ustawia polskie formaty (data dd.MM.yyyy, kwoty z przecinkiem) w calej aplikacji.</summary>
    private static void UstawKulturePolska()
    {
        var pl = new CultureInfo("pl-PL");
        CultureInfo.DefaultThreadCurrentCulture = pl;
        CultureInfo.DefaultThreadCurrentUICulture = pl;
        Thread.CurrentThread.CurrentCulture = pl;
        Thread.CurrentThread.CurrentUICulture = pl;

        // WPF domyslnie formatuje wiazania wg en-US niezaleznie od kultury watku -
        // nadpisujemy jezyk elementow, by StringFormat uzywal pl-PL.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(pl.IetfLanguageTag)));
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.Message,
            "Wystapil blad",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
