using FreeKSeF.Data;
using FreeKSeF.Ksef;

namespace FreeKSeF.App.Services;

/// <summary>
/// Punkt dostepu do wspoldzielonych zaleznosci aplikacji: bazy danych i bramki KSeF.
/// Kontekst bazy tworzymy krotkozyciowo na operacje (zalecane dla aplikacji desktop).
/// </summary>
public static class AppServices
{
    /// <summary>Tworzy nowy kontekst bazy (plik w %AppData%\FreeKSeF) i stosuje migracje.</summary>
    public static FreeKSeFDbContext Db() => FreeKSeFDb.Utworz();

    /// <summary>
    /// Bramka KSeF. Na razie placeholder (rzuca czytelny komunikat) - po podlaczeniu
    /// pakietu KSeF.Client wystarczy podmienic implementacje w tym jednym miejscu.
    /// </summary>
    public static IKsefGateway Ksef { get; set; } = new PlaceholderKsefGateway();
}
