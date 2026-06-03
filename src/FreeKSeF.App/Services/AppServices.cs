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

    /// <summary>Bramka KSeF oparta o oficjalny klient Ministerstwa Finansow.</summary>
    public static IKsefGateway Ksef { get; set; } = new RealKsefGateway();

    /// <summary>
    /// Loguje do KSeF na podstawie zapisanych ustawien firmy (token + srodowisko + NIP).
    /// Rzuca <see cref="KsefException"/> z czytelnym komunikatem, gdy brak danych.
    /// </summary>
    public static async Task ZalogujZUstawienAsync(CancellationToken ct = default)
    {
        string nip, env_token;
        Data.Entities.Srodowisko srodowisko;
        using (var db = Db())
        {
            var firma = db.Companies.OrderBy(x => x.Id).FirstOrDefault()
                ?? throw new KsefException("Uzupelnij dane firmy w zakladce Ustawienia.");
            var token = SecretProtector.Unprotect(firma.KsefTokenProtected);
            if (string.IsNullOrWhiteSpace(token))
                throw new KsefException("Brak tokenu KSeF. Wpisz token w zakladce Ustawienia.");
            nip = firma.Nip;
            env_token = token;
            srodowisko = firma.Srodowisko;
        }

        var ok = await Ksef.ZalogujAsync(new KsefPolaczenie(srodowisko, nip, env_token), ct);
        if (!ok)
            throw new KsefException("Logowanie do KSeF nie powiodlo sie (sprawdz token i srodowisko).");
    }
}
