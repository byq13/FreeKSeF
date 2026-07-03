using System.Collections.ObjectModel;
using System.Windows;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Core.Fa3;
using FreeKSeF.Data;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>
/// Edytor zaawansowany faktury: tabela WSZYSTKICH pol FA(3) (nazwa po lewej, wartosc po
/// prawej - tekst, lista dla enumow, ptaszek gdy sa dokladnie 2 opcje). Faktury robocze
/// mozna edytowac i zapisac; wyslane/otrzymane sa tylko do odczytu (viewer XML).
/// </summary>
public sealed class EdytorFakturyViewModel : ViewModelBase
{
    private readonly Invoice _inv;
    private readonly Faktura _fa;
    private string _xml;
    private string _status = string.Empty;

    public EdytorFakturyViewModel(Invoice inv)
    {
        _inv = inv;
        _xml = inv.Xml;
        _fa = Fa3Serializer.FromXml(inv.Xml);
        TylkoOdczyt = !(inv.Kierunek == KierunekFaktury.Sprzedaz && inv.Status == StatusFaktury.Robocza);

        foreach (var pole in Fa3Pola.Wypisz(_fa))
            Pola.Add(new PoleRow(pole, TylkoOdczyt));

        ZapiszCommand = new RelayCommand(Zapisz, () => !TylkoOdczyt);
        WalidujCommand = new RelayCommand(Waliduj, () => !TylkoOdczyt);
        Status = TylkoOdczyt
            ? "Tylko do odczytu (faktura wyslana/otrzymana)."
            : "Faktura robocza - mozna edytowac pola i zapisac.";
    }

    public bool TylkoOdczyt { get; }
    public bool Edytowalne => !TylkoOdczyt;
    public string Tytul => TylkoOdczyt ? $"Podgląd zaawansowany — {_inv.Numer}" : $"Edytor zaawansowany — {_inv.Numer}";
    public ObservableCollection<PoleRow> Pola { get; } = new();
    public string Xml { get => _xml; private set => SetField(ref _xml, value); }
    public string Status { get => _status; set => SetField(ref _status, value); }

    /// <summary>True, gdy zapisano zmiany - lista faktur ma sie odswiezyc.</summary>
    public bool Zapisano { get; private set; }

    public RelayCommand ZapiszCommand { get; }
    public RelayCommand WalidujCommand { get; }

    /// <summary>Wpisuje wartosci z tabeli do modelu i buduje XML. Null = blad formatu ktoregos pola.</summary>
    private string? ZbudujXml()
    {
        foreach (var row in Pola)
        {
            try
            {
                row.Zastosuj();
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "Bledna wartosc", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }
        return Fa3Serializer.ToXml(_fa);
    }

    private void Waliduj()
    {
        if (ZbudujXml() is not { } xml) return;
        Xml = xml;
        var wynik = Fa3Validator.Validate(xml);
        Status = wynik.IsValid
            ? "Walidacja FA(3): OK."
            : "Bledy walidacji FA(3):\n" + string.Join("\n", wynik.Errors.Take(5));
    }

    private void Zapisz()
    {
        if (ZbudujXml() is not { } xml) return;
        Xml = xml;

        var wynik = Fa3Validator.Validate(xml);
        if (!wynik.IsValid)
        {
            var odp = MessageBox.Show(
                "Faktura nie przechodzi walidacji FA(3):\n\n" + string.Join("\n", wynik.Errors.Take(5)) +
                "\n\nZapisac mimo to? (KSeF moze ja odrzucic)",
                "Blad walidacji", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (odp != MessageBoxResult.Yes) return;
        }

        using (var db = AppServices.Db())
        {
            var e = db.Invoices.Find(_inv.Id);
            if (e is null)
            {
                MessageBox.Show("Faktura nie istnieje juz w bazie.", "FreeKSeF",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Odswiezamy pola listowe z nowego XML (numer, daty, kontrahent, sumy).
            var nowy = FakturaMapping.ZImportu(xml, e.CompanyId, e.Kierunek, e.NumerKsef);
            e.Numer = nowy.Numer;
            e.DataWystawienia = nowy.DataWystawienia;
            e.DataSprzedazy = nowy.DataSprzedazy;
            e.KontrahentNip = nowy.KontrahentNip;
            e.KontrahentNazwa = nowy.KontrahentNazwa;
            e.Waluta = nowy.Waluta;
            e.SumaNetto = nowy.SumaNetto;
            e.SumaVat = nowy.SumaVat;
            e.SumaBrutto = nowy.SumaBrutto;
            e.Xml = xml;
            db.SaveChanges();
        }

        Zapisano = true;
        Status = wynik.IsValid ? "Zapisano (walidacja OK)." : "Zapisano MIMO bledow walidacji.";
    }
}

/// <summary>
/// Wiersz tabeli edytora: naglowek sekcji albo pole z edytorem dopasowanym do typu
/// (tekst / lista opcji / ptaszek przy dokladnie 2 opcjach).
/// </summary>
public sealed class PoleRow : ViewModelBase
{
    private readonly PoleFa3 _pole;
    private string _wartosc;

    public PoleRow(PoleFa3 pole, bool tylkoOdczyt)
    {
        _pole = pole;
        _wartosc = pole.Wartosc;
        TylkoOdczyt = tylkoOdczyt;
    }

    public string Etykieta => _pole.Etykieta;
    public bool Naglowek => _pole.Naglowek;
    public Thickness Margines => new(4 + _pole.Wciecie * 18, 0, 8, 0);
    public bool TylkoOdczyt { get; }
    public bool Edytowalne => !TylkoOdczyt;
    public IReadOnlyList<string>? Opcje => _pole.Opcje;

    public bool JestCheck => !Naglowek && Opcje is { Count: 2 };
    public bool JestLista => !Naglowek && Opcje is not null && !JestCheck;
    public bool JestTekst => !Naglowek && Opcje is null;

    public string Wartosc
    {
        get => _wartosc;
        set { if (SetField(ref _wartosc, value)) OnPropertyChanged(nameof(Zaznaczone)); }
    }

    /// <summary>Dla pol z dokladnie 2 opcjami: ptaszek = pierwsza opcja (np. "1" = tak).</summary>
    public bool Zaznaczone
    {
        get => Opcje is { Count: 2 } o && Wartosc == o[0];
        set { if (Opcje is { Count: 2 } o) Wartosc = value ? o[0] : o[1]; }
    }

    /// <summary>Podpowiedz dla ptaszka, np. "1 = zaznaczone, 2 = odznaczone".</summary>
    public string OpisCheck => Opcje is { Count: 2 } o ? $"{o[0]} = zaznaczone, {o[1]} = odznaczone" : string.Empty;

    /// <summary>Wpisuje wartosc z edytora do modelu FA(3).</summary>
    public void Zastosuj() => Fa3Pola.Zastosuj(_pole, Wartosc);
}
