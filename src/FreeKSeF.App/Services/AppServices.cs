using FreeKSeF.Data;
using FreeKSeF.Data.Entities;
using FreeKSeF.Ksef;

namespace FreeKSeF.App.Services;

/// <summary>
/// Punkt dostepu do wspoldzielonych zaleznosci aplikacji: bazy danych, aktywnej firmy i bramki KSeF.
/// Kontekst bazy tworzymy krotkozyciowo na operacje (zalecane dla aplikacji desktop).
/// </summary>
public static class AppServices
{
    private const string KluczAktywnejFirmy = "aktywnaFirma";
    private static int? _aktywnaFirmaId;

    /// <summary>Tworzy nowy kontekst bazy (plik obok exe) i stosuje migracje.</summary>
    public static FreeKSeFDbContext Db() => FreeKSeFDb.Utworz();

    /// <summary>Bramka KSeF oparta o oficjalny klient Ministerstwa Finansow.</summary>
    public static IKsefGateway Ksef { get; set; } = new RealKsefGateway();

    /// <summary>Zglaszane, gdy zmieni sie lista firm lub aktywna firma - widoki maja sie odswiezyc.</summary>
    public static event Action? FirmyZmienione;

    /// <summary>Id aktywnej firmy (0 = brak firm). Zapamietywane w ustawieniach.</summary>
    public static int AktywnaFirmaId
    {
        get
        {
            _aktywnaFirmaId ??= WyznaczAktywna();
            return _aktywnaFirmaId ?? 0;
        }
    }

    /// <summary>Ustawia aktywna firme i powiadamia widoki.</summary>
    public static void UstawAktywnaFirme(int id)
    {
        _aktywnaFirmaId = id;
        Ustawienia.Zapisz(KluczAktywnejFirmy, id.ToString());
        FirmyZmienione?.Invoke();
    }

    /// <summary>Wymusza ponowne ustalenie aktywnej firmy (np. po dodaniu/usunieciu firmy) i powiadamia.</summary>
    public static void OdswiezFirmy()
    {
        _aktywnaFirmaId = null;
        FirmyZmienione?.Invoke();
    }

    public static Company? AktywnaFirma()
    {
        var id = AktywnaFirmaId;
        if (id == 0) return null;
        using var db = Db();
        return db.Companies.Find(id);
    }

    private static int? WyznaczAktywna()
    {
        using var db = Db();
        if (int.TryParse(Ustawienia.Pobierz(KluczAktywnejFirmy), out var zapisana) &&
            db.Companies.Any(c => c.Id == zapisana))
            return zapisana;
        return db.Companies.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefault();
    }

    /// <summary>
    /// Loguje do KSeF na podstawie ustawien AKTYWNEJ firmy (token + srodowisko + NIP).
    /// Rzuca <see cref="KsefException"/> z czytelnym komunikatem, gdy brak danych.
    /// </summary>
    public static async Task ZalogujZUstawienAsync(CancellationToken ct = default)
    {
        string nip, token;
        Srodowisko srodowisko;
        using (var db = Db())
        {
            var firma = (AktywnaFirmaId != 0 ? db.Companies.Find(AktywnaFirmaId) : null)
                ?? throw new KsefException("Najpierw dodaj firme w zakladce Ustawienia.");

            var t = SecretProtector.Unprotect(firma.KsefTokenProtected);
            if (string.IsNullOrWhiteSpace(t))
                throw new KsefException($"Firma {firma.Nazwa} nie ma tokenu KSeF. Uzupelnij go w Ustawieniach.");
            nip = firma.Nip;
            token = t;
            srodowisko = firma.Srodowisko;
        }

        var ok = await Ksef.ZalogujAsync(new KsefPolaczenie(srodowisko, nip, token), ct);
        if (!ok)
            throw new KsefException("Logowanie do KSeF nie powiodlo sie (sprawdz token i srodowisko).");
    }
}
