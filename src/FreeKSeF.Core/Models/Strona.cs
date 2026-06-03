namespace FreeKSeF.Core.Models;

/// <summary>
/// Strona transakcji (sprzedawca = Podmiot1 lub nabywca = Podmiot2).
/// </summary>
public sealed class Strona
{
    /// <summary>NIP (10 cyfr) bez prefiksu. Dla nabywcy moze byc pusty (osoba prywatna).</summary>
    public string? Nip { get; set; }

    /// <summary>Nazwa / nazwa firmy lub imie i nazwisko.</summary>
    public string Nazwa { get; set; } = string.Empty;

    public Adres Adres { get; set; } = new();

    /// <summary>Czy nabywca jest osoba prywatna bez NIP (ustawia BrakID w FA(3)).</summary>
    public bool BrakNip { get; set; }
}
