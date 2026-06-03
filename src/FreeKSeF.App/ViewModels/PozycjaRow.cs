using FreeKSeF.App.Mvvm;
using FreeKSeF.Core.Models;

namespace FreeKSeF.App.ViewModels;

/// <summary>Wiersz pozycji faktury w formularzu - z przeliczaniem kwot na zywo.</summary>
public sealed class PozycjaRow : ViewModelBase
{
    private string _nazwa = string.Empty;
    private string _jednostka = "szt.";
    private decimal _ilosc = 1m;
    private decimal _cenaNetto;
    private StawkaVat _stawka = StawkaVat.Vat23;

    public string Nazwa { get => _nazwa; set => SetField(ref _nazwa, value); }
    public string Jednostka { get => _jednostka; set => SetField(ref _jednostka, value); }

    public decimal Ilosc
    {
        get => _ilosc;
        set { if (SetField(ref _ilosc, value)) PrzeliczKwoty(); }
    }

    public decimal CenaNetto
    {
        get => _cenaNetto;
        set { if (SetField(ref _cenaNetto, value)) PrzeliczKwoty(); }
    }

    public StawkaVat Stawka
    {
        get => _stawka;
        set { if (SetField(ref _stawka, value)) PrzeliczKwoty(); }
    }

    public decimal WartoscNetto => Math.Round(Ilosc * CenaNetto, 2, MidpointRounding.AwayFromZero);
    public decimal KwotaVat => Math.Round(WartoscNetto * Stawka.Procent(), 2, MidpointRounding.AwayFromZero);
    public decimal WartoscBrutto => WartoscNetto + KwotaVat;

    private void PrzeliczKwoty()
    {
        OnPropertyChanged(nameof(WartoscNetto));
        OnPropertyChanged(nameof(KwotaVat));
        OnPropertyChanged(nameof(WartoscBrutto));
    }

    public PozycjaFaktury ToModel() => new()
    {
        Nazwa = Nazwa,
        Jednostka = Jednostka,
        Ilosc = Ilosc,
        CenaNetto = CenaNetto,
        Stawka = Stawka,
    };
}
