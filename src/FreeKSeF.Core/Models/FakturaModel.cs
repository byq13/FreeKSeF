namespace FreeKSeF.Core.Models;

/// <summary>
/// Uproszczony, domenowy model faktury sprzedazy dla uslugowych JDG.
/// Niezalezny od schematu XML - mapowany do FA(3) przez <see cref="Fa3.Fa3Mapper"/>.
/// </summary>
public sealed class FakturaModel
{
    /// <summary>Numer faktury (P_2), np. "FV 1/06/2026".</summary>
    public string Numer { get; set; } = string.Empty;

    /// <summary>Data wystawienia (P_1).</summary>
    public DateTime DataWystawienia { get; set; } = DateTime.Today;

    /// <summary>Data dokonania/zakonczenia dostawy lub uslugi (P_6). Null = pomijane.</summary>
    public DateTime? DataSprzedazy { get; set; }

    /// <summary>Miejsce wystawienia faktury (P_1M). Puste = pomijane (pole opcjonalne).</summary>
    public string? MiejsceWystawienia { get; set; }

    /// <summary>Kod waluty ISO (domyslnie PLN).</summary>
    public string Waluta { get; set; } = "PLN";

    /// <summary>
    /// Kurs waluty do PLN (sredni NBP z dnia roboczego przed wystawieniem). Dla PLN = 1.
    /// Uzywany do przeliczenia kwoty VAT na PLN na fakturach w walucie obcej (dzial VI ustawy).
    /// </summary>
    public decimal Kurs { get; set; } = 1m;

    /// <summary>True, gdy faktura jest w walucie obcej (innej niz PLN).</summary>
    public bool WalutaObca => !string.Equals(Waluta, "PLN", StringComparison.OrdinalIgnoreCase);

    public Strona Sprzedawca { get; set; } = new();
    public Strona Nabywca { get; set; } = new();

    public List<PozycjaFaktury> Pozycje { get; set; } = new();

    // --- Platnosc ---
    public FormaPlatnosci FormaPlatnosci { get; set; } = FormaPlatnosci.Przelew;

    /// <summary>Termin platnosci (null = bez terminu).</summary>
    public DateTime? TerminPlatnosci { get; set; }

    /// <summary>Numer rachunku bankowego do platnosci (opcjonalny).</summary>
    public string? NrRachunku { get; set; }

    /// <summary>Czy oznaczyc fakture jako zaplacona.</summary>
    public bool Zaplacono { get; set; }

    // --- Sumy (wyliczane) ---
    public decimal SumaNetto => Pozycje.Sum(p => p.WartoscNetto);
    public decimal SumaVat => Pozycje.Sum(p => p.KwotaVat);
    public decimal SumaBrutto => Pozycje.Sum(p => p.WartoscBrutto);
}
