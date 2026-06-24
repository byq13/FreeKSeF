using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.App.Views;
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
    private bool _zajetyGus;

    public NewInvoiceViewModel()
    {
        Pozycje.CollectionChanged += OnPozycjeChanged;
        DodajPozycjeCommand = new RelayCommand(DodajPozycje);
        UsunPozycjeCommand = new RelayCommand(UsunPozycje, () => WybranaPozycja is not null);
        ZapiszCommand = new RelayCommand(Zapisz);
        ZapiszIPodgladCommand = new RelayCommand(ZapiszIPodglad);
        EksportujXmlCommand = new RelayCommand(EksportujXml);
        PobierzZGusCommand = new RelayCommand(PobierzZGus, () => !_zajetyGus);
        AppServices.FirmyZmienione += Odswiez;
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
    public RelayCommand ZapiszIPodgladCommand { get; }
    public RelayCommand EksportujXmlCommand { get; }
    public RelayCommand PobierzZGusCommand { get; }

    /// <summary>Przeladowuje dane pomocnicze (kontrahenci, nr konta, propozycja numeru).</summary>
    public void Odswiez()
    {
        var firmaId = AppServices.AktywnaFirmaId;
        Kontrahenci.Clear();
        using var db = AppServices.Db();
        foreach (var c in db.Contractors.Where(c => c.CompanyId == firmaId).OrderBy(x => x.Nazwa))
            Kontrahenci.Add(c);

        var firma = firmaId != 0 ? db.Companies.Find(firmaId) : null;
        if (firma is not null && string.IsNullOrWhiteSpace(NrRachunku))
            NrRachunku = firma.NrRachunku ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Numer))
            Numer = ProponowanyNumer(db, firmaId);

        if (Pozycje.Count == 0)
            DodajPozycje();
    }

    private static string ProponowanyNumer(FreeKSeFDbContext db, int firmaId)
    {
        var teraz = DateTime.Today;
        var wTymMiesiacu = db.Invoices.Count(i =>
            i.CompanyId == firmaId &&
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
        var firma = AppServices.AktywnaFirmaId != 0 ? db.Companies.Find(AppServices.AktywnaFirmaId) : null;
        if (firma is null)
        {
            MessageBox.Show("Najpierw dodaj i wybierz firme w zakladce Ustawienia.", "Brak firmy",
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

        var firmaId = AppServices.AktywnaFirmaId;
        using var db = AppServices.Db();
        var inv = FakturaMapping.ToEntity(model, firmaId);
        inv.Status = status;
        db.Invoices.Add(inv);

        DopiszKontrahenta(db, firmaId, model.Nabywca);
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

    // Faktura trafia do bufora (robocza). Wysylka do KSeF odbywa sie osobno,
    // z zakladki Sprzedaz, po wyraznym potwierdzeniu uzytkownika.
    private void Zapisz()
    {
        if (ZapiszDoBazy(StatusFaktury.Robocza, "Zapisano fakture do bufora (robocza).") is not null)
            ResetujFormularz();
    }

    private void ZapiszIPodglad()
    {
        var inv = ZapiszDoBazy(StatusFaktury.Robocza, komunikat: string.Empty);
        if (inv is null) return;

        var okno = new PodgladFakturyWindow(inv) { Owner = Application.Current.MainWindow };
        okno.ShowDialog();
        ResetujFormularz();
    }

    /// <summary>Czysci formularz po zapisaniu, aby od razu wystawic kolejna fakture.</summary>
    private void ResetujFormularz()
    {
        Numer = string.Empty;
        DataWystawienia = DateTime.Today;
        DataSprzedazy = DateTime.Today;
        WybranyKontrahent = null;
        NabywcaNip = NabywcaNazwa = NabywcaAdresL1 = NabywcaAdresL2 = string.Empty;
        TerminPlatnosci = DateTime.Today.AddDays(14);

        foreach (var p in Pozycje.ToList())
            p.PropertyChanged -= OnPozycjaPropertyChanged;
        Pozycje.Clear();

        Odswiez(); // proponuje nowy numer i odswieza slownik kontrahentow
    }

    private async void PobierzZGus()
    {
        if (!Core.Models.Nip.Waliduj(NabywcaNip))
        {
            MessageBox.Show("Wpisz poprawny NIP nabywcy (10 cyfr). Myslniki i spacje sa OK.", "NIP",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _zajetyGus = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var dane = await GusService.PobierzAsync(NabywcaNip);
            if (dane is null)
            {
                MessageBox.Show("Nie znaleziono firmy o tym NIP w rejestrze.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            NabywcaNip = dane.Nip;
            NabywcaNazwa = dane.Nazwa;
            NabywcaAdresL1 = dane.AdresL1;
            NabywcaAdresL2 = dane.AdresL2;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Pobieranie danych", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _zajetyGus = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Dodaje nabywce do slownika kontrahentow firmy, jesli ma NIP i jeszcze go nie ma.</summary>
    private static void DopiszKontrahenta(FreeKSeFDbContext db, int firmaId, Strona nabywca)
    {
        var nip = Core.Models.Nip.Normalizuj(nabywca.Nip);
        if (string.IsNullOrEmpty(nip)) return;                               // osoba prywatna - pomijamy
        if (db.Contractors.Any(c => c.CompanyId == firmaId && c.Nip == nip)) return; // juz znany

        db.Contractors.Add(new Contractor
        {
            CompanyId = firmaId,
            Nip = nip,
            Nazwa = nabywca.Nazwa,
            AdresL1 = nabywca.Adres.AdresL1,
            AdresL2 = nabywca.Adres.AdresL2,
        });
    }

    private static string BezpiecznaNazwaPliku(string numer)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            numer = numer.Replace(c, '_');
        return numer;
    }
}
