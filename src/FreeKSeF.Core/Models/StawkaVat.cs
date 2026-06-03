namespace FreeKSeF.Core.Models;

/// <summary>
/// Stawki VAT obslugiwane w MVP (najczestsze dla uslugowych JDG).
/// Wartosc liczbowa = procent (dla zwolnionej/0 podatek = 0).
/// </summary>
public enum StawkaVat
{
    /// <summary>Stawka podstawowa 23% -> P_13_1 / P_14_1.</summary>
    Vat23,

    /// <summary>Stawka obnizona 8% -> P_13_2 / P_14_2.</summary>
    Vat8,

    /// <summary>Stawka obnizona 5% -> P_13_3 / P_14_3.</summary>
    Vat5,

    /// <summary>Stawka 0% krajowa (bez WDT/eksportu) -> P_13_6_1.</summary>
    Vat0,

    /// <summary>Sprzedaz zwolniona od podatku -> P_13_7.</summary>
    Zwolniona,
}

public static class StawkaVatExtensions
{
    /// <summary>Procent VAT jako ulamek (0.23, 0.08, ...). Dla zw/0 zwraca 0.</summary>
    public static decimal Procent(this StawkaVat s) => s switch
    {
        StawkaVat.Vat23 => 0.23m,
        StawkaVat.Vat8 => 0.08m,
        StawkaVat.Vat5 => 0.05m,
        _ => 0m,
    };

    /// <summary>Etykieta do UI/podgladu.</summary>
    public static string Etykieta(this StawkaVat s) => s switch
    {
        StawkaVat.Vat23 => "23%",
        StawkaVat.Vat8 => "8%",
        StawkaVat.Vat5 => "5%",
        StawkaVat.Vat0 => "0%",
        StawkaVat.Zwolniona => "zw",
        _ => s.ToString(),
    };
}
