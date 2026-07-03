using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Core.Models;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>
/// Menedzer firm: lista firm + dane (NIP/GUS, adres, rachunek, srodowisko, token KSeF),
/// wybor aktywnej firmy oraz test polaczenia z KSeF. Kazda firma ma wlasne dane i token.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private Company? _wybrana;
    private int _id;
    private string _nazwa = string.Empty;
    private string _nip = string.Empty;
    private string _adresL1 = string.Empty;
    private string _adresL2 = string.Empty;
    private string _nrRachunku = string.Empty;
    private Srodowisko _srodowisko = Srodowisko.Test;
    private string _numerSzablon = "FV {NR}/{MM}/{RRRR}";
    private bool _numerResetRoczny;
    private bool _tokenUstawiony;
    private bool _zajety;

    public SettingsViewModel()
    {
        NowyCommand = new RelayCommand(Nowy);
        ZapiszCommand = new RelayCommand(Zapisz);
        UsunCommand = new RelayCommand(Usun, () => _id > 0);
        UstawAktywnaCommand = new RelayCommand(UstawAktywna, () => _id > 0);
        TestujPolaczenieCommand = new RelayCommand(TestujPolaczenie, () => !_zajety);
        PobierzZGusCommand = new RelayCommand(PobierzZGus, () => !_zajety);
        Wczytaj();
    }

    public ObservableCollection<Company> Firmy { get; } = new();
    public Array Srodowiska => Enum.GetValues(typeof(Srodowisko));

    public Company? Wybrana
    {
        get => _wybrana;
        set
        {
            if (!SetField(ref _wybrana, value) || value is null) return;
            _id = value.Id;
            Nazwa = value.Nazwa;
            Nip = value.Nip;
            AdresL1 = value.AdresL1;
            AdresL2 = value.AdresL2 ?? string.Empty;
            NrRachunku = value.NrRachunku ?? string.Empty;
            Srodowisko = value.Srodowisko;
            NumerSzablon = string.IsNullOrWhiteSpace(value.NumerSzablon) ? "FV {NR}/{MM}/{RRRR}" : value.NumerSzablon;
            NumerResetRoczny = value.NumerResetRoczny;
            TokenUstawiony = !string.IsNullOrEmpty(value.KsefTokenProtected);
            NowyToken = string.Empty;
        }
    }

    public string Nazwa { get => _nazwa; set => SetField(ref _nazwa, value); }
    public string Nip { get => _nip; set => SetField(ref _nip, value); }
    public string AdresL1 { get => _adresL1; set => SetField(ref _adresL1, value); }
    public string AdresL2 { get => _adresL2; set => SetField(ref _adresL2, value); }
    public string NrRachunku { get => _nrRachunku; set => SetField(ref _nrRachunku, value); }
    public Srodowisko Srodowisko { get => _srodowisko; set => SetField(ref _srodowisko, value); }
    public string NumerSzablon { get => _numerSzablon; set => SetField(ref _numerSzablon, value); }
    public bool NumerResetRoczny { get => _numerResetRoczny; set => SetField(ref _numerResetRoczny, value); }

    public bool TokenUstawiony { get => _tokenUstawiony; set { if (SetField(ref _tokenUstawiony, value)) OnPropertyChanged(nameof(StatusTokenu)); } }
    public string StatusTokenu => TokenUstawiony ? "Token zapisany." : "Brak tokenu.";

    /// <summary>Nowy token wpisany w UI (PasswordBox). Pusty = bez zmiany.</summary>
    public string NowyToken { private get; set; } = string.Empty;

    public string AktywnaFirmaNazwa
    {
        get
        {
            var f = Firmy.FirstOrDefault(x => x.Id == AppServices.AktywnaFirmaId);
            return f is null ? "(brak)" : $"{f.Nazwa} ({f.Nip})";
        }
    }

    public RelayCommand NowyCommand { get; }
    public RelayCommand ZapiszCommand { get; }
    public RelayCommand UsunCommand { get; }
    public RelayCommand UstawAktywnaCommand { get; }
    public RelayCommand TestujPolaczenieCommand { get; }
    public RelayCommand PobierzZGusCommand { get; }

    public void Wczytaj()
    {
        Firmy.Clear();
        using var db = AppServices.Db();
        foreach (var c in db.Companies.OrderBy(x => x.Nazwa))
            Firmy.Add(c);
        OnPropertyChanged(nameof(AktywnaFirmaNazwa));
    }

    private void Nowy()
    {
        _wybrana = null;
        OnPropertyChanged(nameof(Wybrana));
        _id = 0;
        Nazwa = Nip = AdresL1 = AdresL2 = NrRachunku = string.Empty;
        Srodowisko = Srodowisko.Test;
        NumerSzablon = "FV {NR}/{MM}/{RRRR}";
        NumerResetRoczny = false;
        TokenUstawiony = false;
        NowyToken = string.Empty;
    }

    private void Zapisz()
    {
        if (string.IsNullOrWhiteSpace(Nip) || string.IsNullOrWhiteSpace(Nazwa))
        {
            MessageBox.Show("Podaj NIP i nazwe firmy.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool pierwszaFirma;
        using (var db = AppServices.Db())
        {
            pierwszaFirma = !db.Companies.Any();
            var c = _id > 0 ? db.Companies.Find(_id) ?? new Company() : new Company();
            c.Nip = Core.Models.Nip.Normalizuj(Nip);
            c.Nazwa = Nazwa.Trim();
            c.AdresL1 = AdresL1.Trim();
            c.AdresL2 = string.IsNullOrWhiteSpace(AdresL2) ? null : AdresL2.Trim();
            c.NrRachunku = string.IsNullOrWhiteSpace(NrRachunku) ? null : NrRachunku.Trim();
            c.Srodowisko = Srodowisko;
            c.NumerSzablon = string.IsNullOrWhiteSpace(NumerSzablon) ? "FV {NR}/{MM}/{RRRR}" : NumerSzablon.Trim();
            c.NumerResetRoczny = NumerResetRoczny;
            if (!string.IsNullOrWhiteSpace(NowyToken))
            {
                c.KsefTokenProtected = SecretProtector.Protect(NowyToken.Trim());
                TokenUstawiony = true;
                NowyToken = string.Empty;
            }

            if (c.Id == 0) db.Companies.Add(c);
            db.SaveChanges();
            _id = c.Id;
        }

        // Pierwsza firma staje sie aktywna; w kazdym razie odswiezamy widoki.
        if (pierwszaFirma) AppServices.UstawAktywnaFirme(_id);
        else AppServices.OdswiezFirmy();

        Wczytaj();
        Wybrana = Firmy.FirstOrDefault(x => x.Id == _id);
        MessageBox.Show("Zapisano firme.", "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Usun()
    {
        if (_id == 0) return;
        if (MessageBox.Show("Usunac firme wraz z jej fakturami i kontrahentami z lokalnej bazy?\n" +
                            "(nie wplywa na KSeF)", "Usuwanie firmy",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        using (var db = AppServices.Db())
        {
            db.Invoices.RemoveRange(db.Invoices.Where(i => i.CompanyId == _id));
            db.Contractors.RemoveRange(db.Contractors.Where(c => c.CompanyId == _id));
            var firma = db.Companies.Find(_id);
            if (firma is not null) db.Companies.Remove(firma);
            db.SaveChanges();
        }

        AppServices.OdswiezFirmy(); // ustali nowa aktywna firme i odswiezy widoki
        Wczytaj();
        Nowy();
    }

    private void UstawAktywna()
    {
        if (_id == 0) return;
        AppServices.UstawAktywnaFirme(_id);
        OnPropertyChanged(nameof(AktywnaFirmaNazwa));
        MessageBox.Show($"Aktywna firma: {Nazwa}.", "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void PobierzZGus()
    {
        if (!Core.Models.Nip.Waliduj(Nip))
        {
            MessageBox.Show("Wpisz poprawny NIP (10 cyfr). Myslniki i spacje sa OK.", "NIP",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var dane = await GusService.PobierzAsync(Nip);
            if (dane is null)
            {
                MessageBox.Show("Nie znaleziono firmy o tym NIP w rejestrze.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Nip = dane.Nip;
            Nazwa = dane.Nazwa;
            AdresL1 = dane.AdresL1;
            AdresL2 = dane.AdresL2;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Pobieranie danych", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _zajety = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async void TestujPolaczenie()
    {
        if (string.IsNullOrWhiteSpace(Nip))
        {
            MessageBox.Show("Podaj NIP firmy.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var token = NowyToken.Trim();
        if (string.IsNullOrWhiteSpace(token) && _id > 0)
        {
            using var db = AppServices.Db();
            token = SecretProtector.Unprotect(db.Companies.Find(_id)?.KsefTokenProtected);
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Brak tokenu KSeF. Wpisz token albo zapisz go w firmie.", "Brak danych",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var ok = await AppServices.Ksef.ZalogujAsync(new Ksef.KsefPolaczenie(Srodowisko, Core.Models.Nip.Normalizuj(Nip), token));
            MessageBox.Show(ok ? "Polaczenie z KSeF dziala." : "Logowanie do KSeF nie powiodlo sie.",
                "Test KSeF", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Ksef.KsefException ex)
        {
            MessageBox.Show(ex.Message, "Test KSeF", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _zajety = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
