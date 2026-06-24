using System.Windows;
using System.Windows.Input;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Data;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>Lista faktur zakupu + import z KSeF (z ochrona limitu 64 zapytan/h).</summary>
public sealed class ZakupViewModel : FakturaListaViewModelBase
{
    // KSeF limituje do ~64 zapytan/godzine - ograniczamy liczbe pelnych faktur na jeden import.
    private const int LimitPobranNaImport = 50;

    private string _importStatus = string.Empty;
    private bool _zajety;

    public ZakupViewModel() : base(KierunekFaktury.Zakup)
    {
        PobierzZakupyCommand = new RelayCommand(PobierzZakupy, () => !_zajety);
        Odswiez();
    }

    public string ImportStatus { get => _importStatus; set => SetField(ref _importStatus, value); }

    public RelayCommand PobierzZakupyCommand { get; }

    private async void PobierzZakupy()
    {
        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        int dodane = 0;

        try
        {
            ImportStatus = "Logowanie do KSeF...";
            await AppServices.ZalogujZUstawienAsync();

            // Numery KSeF, ktore juz mamy - NIE pobieramy ich ponownie (oszczednosc zetonow).
            var firmaId = AppServices.AktywnaFirmaId;
            HashSet<string> posiadane;
            using (var db = AppServices.Db())
                posiadane = db.Invoices.Where(i => i.CompanyId == firmaId && i.NumerKsef != null)
                    .Select(i => i.NumerKsef!).ToHashSet();

            ImportStatus = "Sprawdzanie listy faktur w KSeF...";
            var (od, doDaty) = ZakresImportu();
            var wynik = await AppServices.Ksef.PobierzFakturyAsync(Ksef.StronaRola.Nabywca, od, doDaty, posiadane, LimitPobranNaImport, f =>
            {
                if (ZapiszPobranaFakture(f)) dodane++;
                ImportStatus = $"Pobieranie nowych faktur: {dodane}...";
                return Task.CompletedTask;
            });

            Odswiez();
            ImportStatus = $"Znaleziono {wynik.Znalezione}, juz posiadane {wynik.JuzPosiadane}, pobrane {wynik.Pobrano}.";
            MessageBox.Show(Podsumowanie(wynik), "Import zakupow", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private bool ZapiszPobranaFakture(Ksef.FakturaZKsef faktura)
    {
        var firmaId = AppServices.AktywnaFirmaId;
        using var db = AppServices.Db();
        if (db.Invoices.Any(i => i.CompanyId == firmaId && i.NumerKsef == faktura.NumerKsef))
            return false;

        var inv = FakturaMapping.ZakupZXml(faktura.Xml, firmaId, faktura.NumerKsef);
        db.Invoices.Add(inv);
        db.SaveChanges();

        Application.Current.Dispatcher.Invoke(() => DodajNaGore(inv));
        return true;
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
                "KSeF pozwala na ok. 64 zapytania na godzine - dokoncz import za jakis czas " +
                "(juz pobrane faktury nie beda pobierane ponownie).";

        return tekst;
    }
}
