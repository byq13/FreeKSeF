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

    // Odszyfrowane tokeny per firma - zeby o haslo pytac raz na sesje, nie przy kazdej operacji.
    private static readonly Dictionary<int, string> _tokenCache = new();

    /// <summary>Czysci zapamietany token firmy (po zmianie tokena/hasla w Ustawieniach).</summary>
    public static void WyczyscTokenCache(int firmaId) => _tokenCache.Remove(firmaId);

    /// <summary>
    /// Zwraca odszyfrowany token KSeF firmy. Gdy token jest chroniony haslem - pyta o nie
    /// (do skutku albo anulowania). Stary zapis DPAPI jest po odczycie migrowany na format
    /// przenosny. Null = brak tokena, zle haslo/anulowano lub token z innego komputera (DPAPI).
    /// </summary>
    public static string? PobierzToken(Company firma)
    {
        if (string.IsNullOrEmpty(firma.KsefTokenProtected)) return null;
        if (_tokenCache.TryGetValue(firma.Id, out var zapamietany)) return zapamietany;

        string? token;
        if (SecretProtector.ChronionyHaslem(firma.KsefTokenProtected))
        {
            while (true)
            {
                var haslo = Views.HasloWindow.Zapytaj(firma.Nazwa);
                if (haslo is null) return null; // anulowano
                token = SecretProtector.Unprotect(firma.KsefTokenProtected, haslo);
                if (token is not null) break;
                System.Windows.MessageBox.Show("Nieprawidlowe haslo.", "Token KSeF",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        else
        {
            token = SecretProtector.Unprotect(firma.KsefTokenProtected);

            // Migracja starego zapisu DPAPI na format przenosny (dziala tylko na komputerze,
            // na ktorym token zapisano - dlatego robimy to przy pierwszej okazji).
            if (token is not null && SecretProtector.StaryFormatDpapi(firma.KsefTokenProtected))
            {
                using var db = Db();
                if (db.Companies.Find(firma.Id) is { } f)
                {
                    f.KsefTokenProtected = SecretProtector.Protect(token);
                    db.SaveChanges();
                }
            }
        }

        if (token is not null) _tokenCache[firma.Id] = token;
        return token;
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

            if (string.IsNullOrEmpty(firma.KsefTokenProtected))
                throw new KsefException($"Firma {firma.Nazwa} nie ma tokenu KSeF. Uzupelnij go w Ustawieniach.");

            var t = PobierzToken(firma);
            if (string.IsNullOrWhiteSpace(t))
                throw new KsefException(SecretProtector.ChronionyHaslem(firma.KsefTokenProtected)
                    ? "Nie odszyfrowano tokena (nie podano hasla)."
                    : $"Nie udalo sie odczytac tokena firmy {firma.Nazwa}. Token zapisany na innym komputerze " +
                      "(stary format) - wpisz go ponownie w Ustawieniach.");
            nip = firma.Nip;
            token = t;
            srodowisko = firma.Srodowisko;
        }

        var ok = await Ksef.ZalogujAsync(new KsefPolaczenie(srodowisko, nip, token), ct);
        if (!ok)
            throw new KsefException("Logowanie do KSeF nie powiodlo sie (sprawdz token i srodowisko).");
    }
}
