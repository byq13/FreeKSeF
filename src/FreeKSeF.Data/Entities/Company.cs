namespace FreeKSeF.Data.Entities;

/// <summary>
/// Profil firmy uzytkownika (sprzedawca). W aplikacji zwykle jeden rekord.
/// </summary>
public class Company
{
    public int Id { get; set; }

    public string Nip { get; set; } = string.Empty;
    public string Nazwa { get; set; } = string.Empty;

    public string KodKraju { get; set; } = "PL";
    public string AdresL1 { get; set; } = string.Empty;
    public string? AdresL2 { get; set; }

    /// <summary>Domyslny numer rachunku do platnosci.</summary>
    public string? NrRachunku { get; set; }

    public Srodowisko Srodowisko { get; set; } = Srodowisko.Test;

    /// <summary>
    /// Token KSeF w postaci zaszyfrowanej (np. DPAPI na Windows).
    /// Nigdy nie przechowujemy tokenu w czystym tekscie.
    /// </summary>
    public string? KsefTokenProtected { get; set; }

    public DateTime UtworzonoUtc { get; set; } = DateTime.UtcNow;
}
