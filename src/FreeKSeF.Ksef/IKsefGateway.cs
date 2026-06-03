using FreeKSeF.Data.Entities;

namespace FreeKSeF.Ksef;

/// <summary>Parametry polaczenia z KSeF.</summary>
public sealed record KsefPolaczenie(Srodowisko Srodowisko, string NipPodatnika, string Token);

/// <summary>Wynik wyslania faktury do KSeF.</summary>
public sealed record WynikWysylki(
    bool Sukces,
    string? NumerReferencyjny,
    string? NumerKsef,
    string? UpoXml,
    string? Blad);

/// <summary>Faktura zakupu pobrana z KSeF.</summary>
public sealed record FakturaZKsef(string NumerKsef, DateTime DataPrzyjecia, string Xml);

/// <summary>
/// Kontrakt integracji z Krajowym Systemem e-Faktur.
/// Implementacja produkcyjna opiera sie na oficjalnym kliencie KSeF.Client (MF).
/// </summary>
public interface IKsefGateway
{
    /// <summary>Uwierzytelnia tokenem KSeF i otwiera sesje. Zwraca true przy powodzeniu.</summary>
    Task<bool> ZalogujAsync(KsefPolaczenie polaczenie, CancellationToken ct = default);

    /// <summary>Wysyla pojedyncza fakture (XML FA(3), UTF-8) w sesji interaktywnej.</summary>
    Task<WynikWysylki> WyslijFakture(byte[] xmlFa3, CancellationToken ct = default);

    /// <summary>Pobiera UPO dla wczesniej wyslanej faktury wg numeru referencyjnego.</summary>
    Task<string?> PobierzUpoAsync(string numerReferencyjny, CancellationToken ct = default);

    /// <summary>
    /// Pobiera faktury zakupu (gdzie jestesmy nabywca) z dowolnego zakresu dat -
    /// bez ograniczenia 30 dni z Aplikacji Podatnika.
    /// </summary>
    Task<IReadOnlyList<FakturaZKsef>> PobierzZakupyAsync(DateTime od, DateTime @do, CancellationToken ct = default);
}

/// <summary>Wyjatek sygnalizujacy problem komunikacji z KSeF.</summary>
public sealed class KsefException : Exception
{
    public KsefException(string message, Exception? inner = null) : base(message, inner) { }
}
