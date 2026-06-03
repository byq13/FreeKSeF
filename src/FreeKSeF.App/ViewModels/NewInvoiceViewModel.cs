using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Core.Fa3;
using FreeKSeF.Core.Models;
using FreeKSeF.Data;
using FreeKSeF.Data.Entities;
using Microsoft.Win32;

namespace FreeKSeF.App.ViewModels;

/// <summary>Formularz wystawiania nowej faktury sprzedazy.</summary>
public sealed class NewInvoiceViewModel : ViewModelBase
{
    private string _numer = string.Empty;
    private DateTime _dataWystawienia = DateTime.Today;
    private DateTime? _dataSprzedazy = DateTime.Today;
    private string _nabywcaNip = string.Empty;
    private string _nabywcaNazwa = string.Empty;
    private string _nabywcaAdresL1 = string.Empty;
    private string _nabywcaAdresL2 = string.Empty;
    private FormaPlatnosci _formaPlatnosci = FormaPlatnosci.Przelew;
    private DateTime? _terminPlatnosci = DateTime.Today.AddDays(14);
    private string _nrRachunku = string.Empty;
    private Contractor? _wybranyKontrahent;

    public NewInvoiceViewModel()
    {
        Pozycje.CollectionChanged += OnPozycjeChanged;
        DodajPozycjeCommand = new RelayCommand(DodajPozycje);
        UsunPozycjeCommand = new RelayCommand(UsunPozycje, () => WybranaPozycja is not null);
        ZapiszCommand = new RelayCommand(() => ZapiszDoBazy(StatusFaktury.Robocza, "Zapisano fakture (robocza)."));
        EksportujXmlCommand = new RelayCommand(EksportujXml);
        WyslijCommand = new RelayCommand(Wyslij);
        Odswiez();
    }

    public ObservableCollection<PozycjaRow> Pozycje { get; } = new();
    public ObservableCollection<Contractor> Kontrahenci { get; } = new();
    public Array Stawki => Enum.GetValues(typeof(StawkaVat));
    public Array FormyPlatnosci => Enum.GetValues(typeof(FormaPlatnosci));

    public string Numer { get => _numer; set => SetField(ref _numer, value); }
    public DateTime DataWystawienia { get => _dataWystawienia; set => SetField(ref _dataWystawienia, value); }
    public DateTime? DataSprzedazy { get => _dataSprzedazy; set => SetField(ref _dataSprzedazy, value); }
    public string NabywcaNip { get => _nabywcaNip; set => SetField(ref _nabywcaNip, value); }
    public string NabywcaNazwa { get => _nabywcaNazwa; set => SetField(ref _nabywcaNazwa, value); }
    public string NabywcaAdresL1 { get => _nabywcaAdresL1; set => SetField(ref _nabywcaAdresL1, value); }
    public string NabywcaAdresL2 { get => _nabywcaAdresL2; set => SetField(ref _nabywcaAdresL2, value); }
    public FormaPlatnosci FormaPlatnosci { get => _formaPlatnosci; set => SetField(ref _formaPlatnosci, value); }
    public DateTime? TerminPlatnosci { get => _terminPlatnosci; set => SetField(ref _terminPlatnosci, value); }
    public string NrRachunku { get => _nrRachunku; set => SetField(ref _nrRachunku, value); }
    private PozycjaRow? _wybranaPozycja;
    public PozycjaRow? WybranaPozycja { get => _wybranaPozycja; set => SetField(ref _wybranaPozycja, value); }

    /// <summary>Wybor kontrahenta ze slownika wypelnia dane nabywcy.</summary>
    public Contractor? WybranyKontrahent
    {
        get => _wybranyKontrahent;
        set
        {
            if (!SetField(ref _wybranyKontrahent, value) || value is null) return;
            NabywcaNip = value.Nip ?? string.Empty;
            NabywcaNazwa = value.Nazwa;
            NabywcaAdresL1 = value.AdresL1;
            NabywcaAdresL2 = value.AdresL2 ?? string.Empty;
        }
    }

    public decimal SumaNetto => Pozycje.Sum(p => p.WartoscNetto);
    public decimal SumaVat => Pozycje.Sum(p => p.KwotaVat);
    public decimal SumaBrutto => Pozycje.Sum(p => p.WartoscBrutto);

    public RelayCommand DodajPozycjeCommand { get; }
    public RelayCommand UsunPozycjeCommand { get; }
    public RelayCommand ZapiszCommand { get; }
    public RelayCommand EksportujXmlCommand { get; }
    public RelayCommand WyslijCommand { get; }

    /// <summary>Przeladowuje dane pomocnicze (kontrahenci, nr konta, propozycja numeru).</summary>
    public void Odswiez()
    {
        Kontrahenci.Clear();
        using var db = AppServices.Db();
        foreach (var c in db.Contractors.OrderBy(x => x.Nazwa))
            Kontrahenci.Add(c);

        var firma = db.Companies.OrderBy(x => x.Id).FirstOrDefault();
        if (firma is not null && string.IsNullOrWhiteSpace(NrRachunku))
            NrRachunku = firma.NrRachunku ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Numer))
            Numer = ProponowanyNumer(db);

        if (Pozycje.Count == 0)
            DodajPozycje();
    }

    private static string ProponowanyNumer(FreeKSeFDbContext db)
    {
        var teraz = DateTime.Today;
        var wTymMiesiacu = db.Invoices.Count(i =>
            i.Kierunek == KierunekFaktury.Sprzedaz &&
            i.DataWystawienia.Year == teraz.Year &&
            i.DataWystawienia.Month == teraz.Month);
        return $"FV {wTymMiesiacu + 1}/{teraz:MM}/{teraz:yyyy}";
    }

    private void DodajPozycje()
    {
        var row = new PozycjaRow();
        row.PropertyChanged += OnPozycjaPropertyChanged;
        Pozycje.Add(row);
        WybranaPozycja = row;
    }

    private void UsunPozycje()
    {
        if (WybranaPozycja is null) return;
        WybranaPozycja.PropertyChanged -= OnPozycjaPropertyChanged;
        Pozycje.Remove(WybranaPozycja);
        WybranaPozycja = Pozycje.LastOrDefault();
    }

    private void OnPozycjeChanged(object? sender, NotifyCollectionChangedEventArgs e) => PrzeliczSumy();

    private void OnPozycjaPropertyChanged(object? sender, PropertyChangedEventArgs e) => PrzeliczSumy();

    private void PrzeliczSumy()
    {
        OnPropertyChanged(nameof(SumaNetto));
        OnPropertyChanged(nameof(SumaVat));
        OnPropertyChanged(nameof(SumaBrutto));
    }

    private FakturaModel? ZbudujModel()
    {
        using var db = AppServices.Db();
        var firma = db.Companies.OrderBy(x => x.Id).FirstOrDefault();
        if (firma is null)
        {
            MessageBox.Show("Najpierw uzupelnij dane firmy w zakladce Ustawienia.", "Brak danych firmy",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        if (string.IsNullOrWhiteSpace(Numer) || string.IsNullOrWhiteSpace(NabywcaNazwa) || Pozycje.Count == 0)
        {
            MessageBox.Show("Uzupelnij numer, nabywce i co najmniej jedna pozycje.", "Brak danych",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        return new FakturaModel
        {
            Numer = Numer.Trim(),
            DataWystawienia = DataWystawienia,
            DataSprzedazy = DataSprzedazy,
            Sprzedawca = new Strona
            {
                Nip = firma.Nip,
                Nazwa = firma.Nazwa,
                Adres = new Adres { KodKraju = firma.KodKraju, AdresL1 = firma.AdresL1, AdresL2 = firma.AdresL2 },
            },
            Nabywca = new Strona
            {
                Nip = string.IsNullOrWhiteSpace(NabywcaNip) ? null : NabywcaNip.Trim(),
                BrakNip = string.IsNullOrWhiteSpace(NabywcaNip),
                Nazwa = NabywcaNazwa.Trim(),
                Adres = new Adres { AdresL1 = NabywcaAdresL1.Trim(), AdresL2 = string.IsNullOrWhiteSpace(NabywcaAdresL2) ? null : NabywcaAdresL2.Trim() },
            },
            FormaPlatnosci = FormaPlatnosci,
            TerminPlatnosci = TerminPlatnosci,
            NrRachunku = string.IsNullOrWhiteSpace(NrRachunku) ? null : NrRachunku.Trim(),
            Pozycje = Pozycje.Select(p => p.ToModel()).ToList(),
        };
    }

    private Invoice? ZapiszDoBazy(StatusFaktury status, string komunikat)
    {
        var model = ZbudujModel();
        if (model is null) return null;

        // Walidacja XSD przed zapisem - lapie bledy zanim trafia do KSeF.
        var xml = Fa3Serializer.ToXml(Fa3Mapper.ToFa3(model));
        var wynik = Fa3Validator.Validate(xml);
        if (!wynik.IsValid)
        {
            MessageBox.Show("Faktura nie przeszla walidacji FA(3):\n\n" + string.Join("\n", wynik.Errors),
                "Blad walidacji", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        using var db = AppServices.Db();
        var inv = FakturaMapping.ToEntity(model);
        inv.Status = status;
        db.Invoices.Add(inv);
        db.SaveChanges();

        if (!string.IsNullOrEmpty(komunikat))
            MessageBox.Show(komunikat, "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
        return inv;
    }

    private void EksportujXml()
    {
        var model = ZbudujModel();
        if (model is null) return;

        var xml = Fa3Mapper.ToFa3(model);
        var wynik = Fa3Validator.Validate(Fa3Serializer.ToXml(xml));
        if (!wynik.IsValid)
        {
            MessageBox.Show("Faktura nie przeszla walidacji FA(3):\n\n" + string.Join("\n", wynik.Errors),
                "Blad walidacji", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Zapisz XML faktury (FA(3))",
            Filter = "Plik XML (*.xml)|*.xml",
            FileName = BezpiecznaNazwaPliku(model.Numer) + ".xml",
        };
        if (dialog.ShowDialog() != true) return;

        File.WriteAllBytes(dialog.FileName, Fa3Serializer.ToXmlBytes(xml));
        MessageBox.Show("Zapisano XML. Mozesz go recznie wyslac w Aplikacji Podatnika KSeF.",
            "Eksport zakonczony", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void Wyslij()
    {
        var inv = ZapiszDoBazy(StatusFaktury.Robocza, string.Empty);
        if (inv is null) return;

        try
        {
            var wynik = await AppServices.Ksef.WyslijFakture(System.Text.Encoding.UTF8.GetBytes(inv.Xml));
            using var db = AppServices.Db();
            var e = db.Invoices.Find(inv.Id)!;
            if (wynik.Sukces)
            {
                e.Status = StatusFaktury.Przyjeta;
                e.NumerKsef = wynik.NumerKsef;
                e.NumerReferencyjny = wynik.NumerReferencyjny;
                e.UpoXml = wynik.UpoXml;
                e.WyslanoUtc = DateTime.UtcNow;
                db.SaveChanges();
                MessageBox.Show($"Faktura przyjeta przez KSeF.\nNumer KSeF: {wynik.NumerKsef}", "Wyslano",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                e.Status = StatusFaktury.Blad;
                db.SaveChanges();
                MessageBox.Show("KSeF odrzucil fakture: " + wynik.Blad, "Blad wysylki",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Ksef.KsefException ex)
        {
            MessageBox.Show(ex.Message, "KSeF niedostepny", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string BezpiecznaNazwaPliku(string numer)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            numer = numer.Replace(c, '_');
        return numer;
    }
}
