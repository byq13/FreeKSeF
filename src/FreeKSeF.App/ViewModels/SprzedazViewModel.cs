using System.Windows;
using System.Windows.Input;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Data;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>
/// Lista faktur sprzedazy. Faktury powstaja w buforze (robocze); wysylka do KSeF
/// nastepuje TYLKO na wyrazne polecenie z potwierdzeniem. Mozna tez zaimportowac
/// faktury sprzedazy juz zarejestrowane w KSeF (np. wystawione w innym programie).
/// </summary>
public sealed class SprzedazViewModel : FakturaListaViewModelBase
{
    private const int LimitPobranNaImport = 50;

    private bool _zajety;
    private DateTime _importOd = DateTime.Today.AddMonths(-3);
    private DateTime _importDo = DateTime.Today;
    private string _importStatus = string.Empty;

    public SprzedazViewModel() : base(KierunekFaktury.Sprzedaz)
    {
        WyslijCommand = new RelayCommand(Wyslij, () => !_zajety && Wybrana is not null);
        UsunCommand = new RelayCommand(Usun, () => Wybrana is not null);
        PobierzSprzedazCommand = new RelayCommand(PobierzSprzedaz, () => !_zajety);
        Odswiez();
    }

    public DateTime ImportOd { get => _importOd; set => SetField(ref _importOd, value); }
    public DateTime ImportDo { get => _importDo; set => SetField(ref _importDo, value); }
    public string ImportStatus { get => _importStatus; set => SetField(ref _importStatus, value); }

    public RelayCommand WyslijCommand { get; }
    public RelayCommand UsunCommand { get; }
    public RelayCommand PobierzSprzedazCommand { get; }

    private async void Wyslij()
    {
        if (Wybrana is null) return;

        if (Wybrana.Status == StatusFaktury.Przyjeta)
        {
            MessageBox.Show($"Faktura {Wybrana.Numer} zostala juz przyjeta przez KSeF (numer {Wybrana.NumerKsef}).",
                "Juz wyslana", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var potwierdzenie = MessageBox.Show(
            $"Czy na pewno wyslac fakture {Wybrana.Numer} do KSeF?\n\n" +
            "Operacja jest nieodwracalna - faktura zostanie trwale zarejestrowana w KSeF.",
            "Potwierdzenie wysylki",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (potwierdzenie != MessageBoxResult.Yes) return;

        var id = Wybrana.Id;
        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var wynik = await FakturaService.WyslijAsync(id);
            Odswiez();
            if (wynik.Sukces)
                MessageBox.Show($"Faktura przyjeta przez KSeF.\nNumer KSeF: {wynik.NumerKsef}",
                    "Wyslano", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("KSeF nie potwierdzil przyjecia: " + (wynik.Blad ?? "nieznany blad"),
                    "Blad wysylki", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Ksef.KsefException ex)
        {
            MessageBox.Show(ex.Message, "KSeF niedostepny", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _zajety = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async void PobierzSprzedaz()
    {
        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        int dodane = 0;
        try
        {
            ImportStatus = "Logowanie do KSeF...";
            await AppServices.ZalogujZUstawienAsync();

            HashSet<string> posiadane;
            using (var db = AppServices.Db())
                posiadane = db.Invoices.Where(i => i.NumerKsef != null).Select(i => i.NumerKsef!).ToHashSet();

            ImportStatus = "Sprawdzanie listy faktur w KSeF...";
            var wynik = await AppServices.Ksef.PobierzFakturyAsync(Ksef.StronaRola.Sprzedawca, ImportOd, ImportDo, posiadane, LimitPobranNaImport, f =>
            {
                if (ZapiszPobranaSprzedaz(f)) dodane++;
                ImportStatus = $"Pobieranie faktur sprzedazy: {dodane}...";
                return Task.CompletedTask;
            });

            Odswiez();
            ImportStatus = $"Znaleziono {wynik.Znalezione}, juz posiadane {wynik.JuzPosiadane}, pobrane {wynik.Pobrano}.";
            MessageBox.Show(Podsumowanie(wynik), "Import sprzedazy", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Ksef.KsefException ex)
        {
            Odswiez();
            MessageBox.Show($"{ex.Message}\n\nDo tego momentu dodano nowych faktur: {dodane}.",
                "KSeF niedostepny", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _zajety = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool ZapiszPobranaSprzedaz(Ksef.FakturaZKsef faktura)
    {
        using var db = AppServices.Db();
        if (db.Invoices.Any(i => i.NumerKsef == faktura.NumerKsef))
            return false;

        var inv = FakturaMapping.ZImportu(faktura.Xml, KierunekFaktury.Sprzedaz, faktura.NumerKsef);
        db.Invoices.Add(inv);
        db.SaveChanges();

        Application.Current.Dispatcher.Invoke(() => DodajNaGore(inv));
        return true;
    }

    private void Usun()
    {
        if (Wybrana is null) return;

        var tekst = Wybrana.Status == StatusFaktury.Przyjeta || Wybrana.Status == StatusFaktury.Zaimportowana
            ? $"Faktura {Wybrana.Numer} jest zarejestrowana w KSeF. Usuniecie z lokalnej bazy NIE anuluje jej w KSeF.\n\nUsunac wpis lokalny?"
            : $"Usunac robocza fakture {Wybrana.Numer} z lokalnej bazy?";

        if (MessageBox.Show(tekst, "Usuwanie faktury", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            != MessageBoxResult.Yes) return;

        using (var db = AppServices.Db())
        {
            var e = db.Invoices.Find(Wybrana.Id);
            if (e is not null) { db.Invoices.Remove(e); db.SaveChanges(); }
        }
        Odswiez();
    }

    private static string Podsumowanie(Ksef.WynikImportu w)
    {
        var tekst =
            $"Znaleziono w KSeF: {w.Znalezione}\n" +
            $"Juz posiadane (pominiete, bez pobierania): {w.JuzPosiadane}\n" +
            $"Pobrane teraz: {w.Pobrano}";

        if (w.LimitOsiagniety)
            tekst +=
                $"\n\nOsiagnieto limit pobran na jeden import ({LimitPobranNaImport}).\n" +
                $"Pozostalo do pobrania: {w.PozostaloDoPobrania}.\n" +
                "KSeF pozwala na ok. 64 zapytania na godzine - dokoncz import za jakis czas.";

        return tekst;
    }
}
