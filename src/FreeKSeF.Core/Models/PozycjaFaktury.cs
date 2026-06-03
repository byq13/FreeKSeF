namespace FreeKSeF.Core.Models;

/// <summary>
/// Pozycja faktury (wiersz). Dla uslug zwykle ilosc=1, jednostka="usl." lub "szt.".
/// </summary>
public sealed class PozycjaFaktury
{
    /// <summary>Nazwa towaru/uslugi (P_7). FA(3) dopuszcza do 512 znakow.</summary>
    public string Nazwa { get; set; } = string.Empty;

    /// <summary>Jednostka miary (P_8A), np. "szt.", "usl.", "godz.".</summary>
    public string Jednostka { get; set; } = "szt.";

    /// <summary>Ilosc (P_8B).</summary>
    public decimal Ilosc { get; set; } = 1m;

    /// <summary>Cena jednostkowa netto (P_9A).</summary>
    public decimal CenaNetto { get; set; }

    public StawkaVat Stawka { get; set; } = StawkaVat.Vat23;

    /// <summary>Wartosc netto pozycji (P_11) = round(Ilosc * CenaNetto, 2).</summary>
    public decimal WartoscNetto => Math.Round(Ilosc * CenaNetto, 2, MidpointRounding.AwayFromZero);

    /// <summary>Kwota VAT pozycji = round(WartoscNetto * procent, 2).</summary>
    public decimal KwotaVat => Math.Round(WartoscNetto * Stawka.Procent(), 2, MidpointRounding.AwayFromZero);

    /// <summary>Wartosc brutto pozycji.</summary>
    public decimal WartoscBrutto => WartoscNetto + KwotaVat;
}
