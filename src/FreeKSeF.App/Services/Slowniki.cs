namespace FreeKSeF.App.Services;

/// <summary>Proste slowniki do list rozwijanych w UI (waluty, kraje).</summary>
public static class Slowniki
{
    /// <summary>Najczestsze waluty (PLN pierwsze).</summary>
    public static readonly IReadOnlyList<string> Waluty = new[]
    {
        "PLN", "EUR", "USD", "GBP", "CHF", "CZK", "SEK", "NOK", "DKK", "UAH", "HUF", "CAD", "JPY", "AUD",
    };

    /// <summary>Najczestsze kody krajow (PL pierwsze). KodUE/np. EL dla Grecji, XI dla Irlandii Pln.</summary>
    public static readonly IReadOnlyList<string> Kraje = new[]
    {
        "PL", "DE", "GB", "US", "FR", "CZ", "SK", "IT", "ES", "NL", "BE", "AT", "SE", "NO", "DK", "FI",
        "IE", "PT", "EL", "HU", "RO", "BG", "LT", "LV", "EE", "HR", "SI", "LU", "CY", "MT", "XI",
        "CH", "UA", "CA", "CN", "JP", "AU", "NO",
    };
}
