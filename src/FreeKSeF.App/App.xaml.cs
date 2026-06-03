using System.Windows;
using System.Windows.Threading;

namespace FreeKSeF.App;

public partial class App : Application
{
    public App()
    {
        // Globalna obsluga nieoczekiwanych bledow - zamiast cichego zamkniecia.
        DispatcherUnhandledException += OnUnhandledException;
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
