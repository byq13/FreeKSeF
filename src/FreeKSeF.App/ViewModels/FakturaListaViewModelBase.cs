using System.Collections.ObjectModel;
using System.Windows;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.App.Views;
using FreeKSeF.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace FreeKSeF.App.ViewModels;

/// <summary>Zakres czasu pokazywanych faktur.</summary>
public enum OkresFiltru
{
    BiezacyMiesiac,
    BiezacyKwartal,
    BiezacyRok,
    Wszystko,
}

/// <summary>
/// Wspolna baza list faktur (sprzedaz/zakup): wczytywanie wg kierunku z filtrem okresu
/// (zapamietywanym w bazie), zaznaczanie wiersza, podglad PDF, zapis XML i usuwanie.
/// </summary>
public abstract class FakturaListaViewModelBase : ViewModelBase
{
    private readonly KierunekFaktury _kierunek;
    private FakturaRow? _wybrany;
    private OkresFiltru _okres;

    protected FakturaListaViewModelBase(KierunekFaktury kierunek)
    {
        _kierunek = kierunek;
        _okres = WczytajOkres();

        PodgladCommand = new RelayCommand(Podglad, () => Wybrany is not null);
        ZapiszXmlCommand = new RelayCommand(ZapiszXml, () => Wybrany is not null);
        ZaznaczCommand = new RelayCommand(Zaznacz, () => Wybrany is not null);
        UsunCommand = new RelayCommand(Usun, () => Wybrany is not null);
    }

    public ObservableCollection<FakturaRow> Wiersze { get; } = new();

    public FakturaRow? Wybrany
    {
        get => _wybrany;
        set => SetField(ref _wybrany, value);
    }

    public Array Okresy => Enum.GetValues(typeof(OkresFiltru));

    /// <summary>Wybrany filtr okresu - zmiana zapisuje preferencje i odswieza liste.</summary>
    public OkresFiltru Okres
    {
        get => _okres;
        set
        {
            if (!SetField(ref _okres, value)) return;
            Ustawienia.Zapisz(KluczOkresu, value.ToString());
            Odswiez();
        }
    }

    public RelayCommand PodgladCommand { get; }
    public RelayCommand ZapiszXmlCommand { get; }
    public RelayCommand ZaznaczCommand { get; }
    public RelayCommand UsunCommand { get; }

    public void Odswiez()
    {
        Wiersze.Clear();
        var (od, doDaty) = ZakresDat(_okres);

        using var db = AppServices.Db();
        var q = db.Invoices.AsNoTracking().Where(i => i.Kierunek == _kierunek);
        if (od is { } o) q = q.Where(i => i.DataWystawienia >= o);
        if (doDaty is { } d) q = q.Where(i => i.DataWystawienia <= d);

        foreach (var inv in q.OrderByDescending(i => i.DataWystawienia).ThenByDescending(i => i.Id).ToList())
            Wiersze.Add(new FakturaRow(inv));
    }

    /// <summary>Dodaje swiezo zapisana/pobrana fakture na gore listy.</summary>
    protected void DodajNaGore(Invoice inv)
    {
        var row = new FakturaRow(inv);
        Wiersze.Insert(0, row);
        Wybrany = row;
    }

    private void Podglad()
    {
        if (Wybrany is null) return;
        var okno = new PodgladFakturyWindow(Wybrany.Faktura) { Owner = Application.Current.MainWindow };
        okno.ShowDialog();
    }

    private void Zaznacz()
    {
        if (Wybrany is not null) Wybrany.Zaznaczona = !Wybrany.Zaznaczona;
    }

    private void ZapiszXml()
    {
        if (Wybrany is null) return;
        var f = Wybrany.Faktura;
        var dialog = new SaveFileDialog
        {
            Title = "Zapisz XML faktury (FA(3))",
            Filter = "Plik XML (*.xml)|*.xml",
            FileName = FakturaService.BezpiecznaNazwa(f.Numer) + ".xml",
        };
        if (dialog.ShowDialog() != true) return;
        FakturaService.ZapiszXml(f, dialog.FileName);
        MessageBox.Show("Zapisano XML (gotowy do wgrania w Aplikacji Podatnika KSeF).",
            "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Usun()
    {
        if (Wybrany is null) return;
        var f = Wybrany.Faktura;

        var ostrzezenie = f.Status is StatusFaktury.Przyjeta or StatusFaktury.Zaimportowana
            ? "\n\nFaktura jest zarejestrowana w KSeF - usuniecie z lokalnej bazy NIE anuluje jej w KSeF."
            : string.Empty;

        if (MessageBox.Show($"Usunac fakture {f.Numer} z lokalnej bazy?{ostrzezenie}",
                "Usuwanie faktury", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            != MessageBoxResult.Yes) return;

        using (var db = AppServices.Db())
        {
            var e = db.Invoices.Find(f.Id);
            if (e is not null) { db.Invoices.Remove(e); db.SaveChanges(); }
        }
        Odswiez();
    }

    private static (DateTime? Od, DateTime? Do) ZakresDat(OkresFiltru okres)
    {
        var dzis = DateTime.Today;
        switch (okres)
        {
            case OkresFiltru.BiezacyMiesiac:
                var pm = new DateTime(dzis.Year, dzis.Month, 1);
                return (pm, pm.AddMonths(1).AddDays(-1));
            case OkresFiltru.BiezacyKwartal:
                var startMies = (dzis.Month - 1) / 3 * 3 + 1;
                var pk = new DateTime(dzis.Year, startMies, 1);
                return (pk, pk.AddMonths(3).AddDays(-1));
            case OkresFiltru.BiezacyRok:
                return (new DateTime(dzis.Year, 1, 1), new DateTime(dzis.Year, 12, 31));
            default:
                return (null, null);
        }
    }

    private string KluczOkresu => $"filtr.okres.{_kierunek}";

    private OkresFiltru WczytajOkres()
        => Enum.TryParse<OkresFiltru>(Ustawienia.Pobierz(KluczOkresu), out var o) ? o : OkresFiltru.Wszystko;
}
