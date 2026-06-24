namespace FreeKSeF.Core.Models;

/// <summary>Kody krajow Unii Europejskiej (do rozroznienia identyfikatora nabywcy: VAT UE vs NrID).</summary>
public static class KrajeUE
{
    private static readonly HashSet<string> Kody = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "ES", "FI", "FR", "HR",
        "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK",
        // Irlandia Polnocna - kod stosowany do transakcji UE
        "XI",
    };

    /// <summary>Czy podany kod kraju nalezy do UE (na potrzeby VAT UE). PL traktujemy jako krajowy osobno.</summary>
    public static bool CzyUE(string? kodKraju)
        => !string.IsNullOrWhiteSpace(kodKraju) && Kody.Contains(kodKraju.Trim());
}
