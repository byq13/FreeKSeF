using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreeKSeF.App.ViewModels;

/// <summary>
/// Podglad i edycja lokalnej tabeli kursow walut (zasilanej z NBP).
/// Kurs w wierszu mozna poprawic recznie; mozna tez dodac wlasne notowanie i usuwac wpisy.
/// </summary>
public sealed class KursyViewModel : ViewModelBase
{
    public const string WszystkieWaluty = "(wszystkie)";

    private readonly List<int> _doUsuniecia = new();
    private string _filtrWaluta = WszystkieWaluty;
    private KursWaluty? _wybrany;
    private string _nowyKod = "EUR";
    private DateTime _nowaData = DateTime.Today;
    private string _nowyKurs = string.Empty;
    private string _status = string.Empty;
    private bool _zajety;

    public KursyViewModel()
    {
        PobierzCommand = new RelayCommand(Pobierz, () => !_zajety);
        DodajCommand = new RelayCommand(Dodaj);
        UsunCommand = new RelayCommand(Usun, () => Wybrany is not null);
        ZapiszCommand = new RelayCommand(Zapisz);
        Odswiez();
    }

    public ObservableCollection<KursWaluty> Wiersze { get; } = new();

    /// <summary>Waluty do filtra i do dodawania wpisu (bez PLN - kurs PLN zawsze 1).</summary>
    public IReadOnlyList<string> Waluty { get; } = Slowniki.Waluty.Where(w => w != "PLN").ToList();

    public IReadOnlyList<string> FiltrWaluty { get; } =
        new[] { WszystkieWaluty }.Concat(Slowniki.Waluty.Where(w => w != "PLN")).ToList();

    public string FiltrWaluta
    {
        get => _filtrWaluta;
        set { if (SetField(ref _filtrWaluta, value)) Odswiez(); }
    }

    public KursWaluty? Wybrany { get => _wybrany; set => SetField(ref _wybrany, value); }

    public string NowyKod { get => _nowyKod; set => SetField(ref _nowyKod, value); }
    public DateTime NowaData { get => _nowaData; set => SetField(ref _nowaData, value); }
    public string NowyKurs { get => _nowyKurs; set => SetField(ref _nowyKurs, value); }

    public string Status { get => _status; set => SetField(ref _status, value); }

    public RelayCommand PobierzCommand { get; }
    public RelayCommand DodajCommand { get; }
    public RelayCommand UsunCommand { get; }
    public RelayCommand ZapiszCommand { get; }

    public void Odswiez()
    {
        Wiersze.Clear();
        _doUsuniecia.Clear();
        using var db = AppServices.Db();
        var q = db.Kursy.AsNoTracking();
        if (FiltrWaluta != WszystkieWaluty)
            q = q.Where(k => k.Kod == FiltrWaluta);
        foreach (var k in q.OrderByDescending(k => k.Data).ThenBy(k => k.Kod).Take(2000).ToList())
            Wiersze.Add(k);
        Status = $"Wpisow: {Wiersze.Count}" + (Wiersze.Count == 2000 ? " (pokazano 2000 najnowszych)" : string.Empty);
    }

    private async void Pobierz()
    {
        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            Status = "Pobieranie kursow z NBP...";
            var dodane = await KursyService.ImportujAsync(DateTime.Today.AddDays(-90), DateTime.Today);
            Odswiez();
            Status = $"Pobrano z NBP (tabela A, ~90 dni). Nowych notowan: {dodane}. Wpisow: {Wiersze.Count}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kursy walut", MessageBoxButton.OK, MessageBoxImage.Warning);
            Status = string.Empty;
        }
        finally
        {
            _zajety = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void Dodaj()
    {
        if (!decimal.TryParse(NowyKurs.Replace(',', '.'),
                System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture,
                out var kurs) || kurs <= 0)
        {
            MessageBox.Show("Podaj poprawny kurs (np. 4,3215).", "Kursy walut",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (Wiersze.Any(w => w.Kod == NowyKod && w.Data == NowaData.Date))
        {
            MessageBox.Show($"Wpis {NowyKod} z dnia {NowaData:yyyy-MM-dd} juz istnieje - popraw kurs w tabeli.",
                "Kursy walut", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var nowy = new KursWaluty { Kod = NowyKod, Data = NowaData.Date, Kurs = kurs, Tabela = "wlasny" };
        Wiersze.Insert(0, nowy);
        Wybrany = nowy;
        NowyKurs = string.Empty;
        Status = "Dodano wpis - kliknij Zapisz zmiany.";
    }

    private void Usun()
    {
        if (Wybrany is not { } k) return;
        if (k.Id > 0) _doUsuniecia.Add(k.Id);
        Wiersze.Remove(k);
        Status = "Usunieto wpis - kliknij Zapisz zmiany.";
    }

    /// <summary>Zapisuje recznie poprawione kursy, nowe wpisy i usuniecia.</summary>
    private void Zapisz()
    {
        using var db = AppServices.Db();

        foreach (var id in _doUsuniecia)
        {
            var e = db.Kursy.Find(id);
            if (e is not null) db.Kursy.Remove(e);
        }

        var zmienione = 0;
        foreach (var w in Wiersze)
        {
            if (w.Kurs <= 0) continue; // nie zapisujemy bezsensownych kursow
            if (w.Id == 0)
            {
                db.Kursy.Add(w);
                zmienione++;
            }
            else
            {
                var e = db.Kursy.Find(w.Id);
                if (e is not null && (e.Kurs != w.Kurs || e.Tabela != w.Tabela))
                {
                    e.Kurs = w.Kurs;
                    e.Tabela = w.Tabela;
                    zmienione++;
                }
            }
        }

        var usuniete = _doUsuniecia.Count;
        db.SaveChanges();
        _doUsuniecia.Clear();
        Odswiez();
        Status = $"Zapisano. Zmienione/dodane: {zmienione}, usuniete: {usuniete}.";
    }
}
