namespace FreeKSeF.Core.Models;

/// <summary>
/// Adres w formacie FA(3): kod kraju + dwie linie adresowe.
/// AdresL1 = ulica i numer, AdresL2 = kod pocztowy i miejscowosc.
/// </summary>
public sealed class Adres
{
    /// <summary>Dwuliterowy kod kraju ISO (domyslnie PL).</summary>
    public string KodKraju { get; set; } = "PL";

    /// <summary>Pierwsza linia adresu, np. "ul. Kwiatowa 5/2". Wymagana.</summary>
    public string AdresL1 { get; set; } = string.Empty;

    /// <summary>Druga linia adresu, np. "00-001 Warszawa". Opcjonalna.</summary>
    public string? AdresL2 { get; set; }
}
