using System.Windows;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>Ustawienia firmy (sprzedawcy), srodowisko KSeF i token dostepu.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private int _id;
    private string _nip = string.Empty;
    private string _nazwa = string.Empty;
    private string _adresL1 = string.Empty;
    private string _adresL2 = string.Empty;
    private string _nrRachunku = string.Empty;
    private Srodowisko _srodowisko = Srodowisko.Test;
    private bool _tokenUstawiony;

    public SettingsViewModel()
    {
        ZapiszCommand = new RelayCommand(Zapisz);
        Wczytaj();
    }

    public Array Srodowiska => Enum.GetValues(typeof(Srodowisko));

    public string Nip { get => _nip; set => SetField(ref _nip, value); }
    public string Nazwa { get => _nazwa; set => SetField(ref _nazwa, value); }
    public string AdresL1 { get => _adresL1; set => SetField(ref _adresL1, value); }
    public string AdresL2 { get => _adresL2; set => SetField(ref _adresL2, value); }
    public string NrRachunku { get => _nrRachunku; set => SetField(ref _nrRachunku, value); }
    public Srodowisko Srodowisko { get => _srodowisko; set => SetField(ref _srodowisko, value); }

    /// <summary>Czy token jest juz zapisany (informacja dla UI).</summary>
    public bool TokenUstawiony { get => _tokenUstawiony; set { if (SetField(ref _tokenUstawiony, value)) OnPropertyChanged(nameof(StatusTokenu)); } }

    public string StatusTokenu => TokenUstawiony ? "Token zapisany." : "Brak tokenu.";

    /// <summary>Nowy token wpisany w UI (PasswordBox). Pusty = bez zmiany.</summary>
    public string NowyToken { private get; set; } = string.Empty;

    public RelayCommand ZapiszCommand { get; }

    private void Wczytaj()
    {
        using var db = AppServices.Db();
        var c = db.Companies.OrderBy(x => x.Id).FirstOrDefault();
        if (c is null) return;

        _id = c.Id;
        Nip = c.Nip;
        Nazwa = c.Nazwa;
        AdresL1 = c.AdresL1;
        AdresL2 = c.AdresL2 ?? string.Empty;
        NrRachunku = c.NrRachunku ?? string.Empty;
        Srodowisko = c.Srodowisko;
        TokenUstawiony = !string.IsNullOrEmpty(c.KsefTokenProtected);
    }

    private void Zapisz()
    {
        if (string.IsNullOrWhiteSpace(Nip) || string.IsNullOrWhiteSpace(Nazwa))
        {
            MessageBox.Show("Podaj NIP i nazwe firmy.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = AppServices.Db();
        var c = db.Companies.FirstOrDefault(x => x.Id == _id) ?? new Company();
        c.Nip = Nip.Trim();
        c.Nazwa = Nazwa.Trim();
        c.AdresL1 = AdresL1.Trim();
        c.AdresL2 = string.IsNullOrWhiteSpace(AdresL2) ? null : AdresL2.Trim();
        c.NrRachunku = string.IsNullOrWhiteSpace(NrRachunku) ? null : NrRachunku.Trim();
        c.Srodowisko = Srodowisko;

        if (!string.IsNullOrWhiteSpace(NowyToken))
        {
            c.KsefTokenProtected = SecretProtector.Protect(NowyToken.Trim());
            TokenUstawiony = true;
            NowyToken = string.Empty;
        }

        if (c.Id == 0) db.Companies.Add(c);
        db.SaveChanges();
        _id = c.Id;

        MessageBox.Show("Zapisano ustawienia.", "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
