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
/// Podsumowanie importu zakupow - pozwala UI pokazac ile zetonow KSeF zuzyto i ile zostalo do pobrania.
/// </summary>
public sealed record WynikImportuZakupow(
    IReadOnlyList<FakturaZKsef> Pobrane,
    int Znalezione,
    int JuzPosiadane,
    int Pobrano,
    int PozostaloDoPobrania)
{
    /// <summary>Czy nie pobrano wszystkich brakujacych (np. z powodu limitu zetonow).</summary>
    public bool LimitOsiagniety => PozostaloDoPobrania > 0;
}

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
    /// Najpierw pobiera tanie metadane (numery KSeF), a pelne faktury sciaga TYLKO
    /// dla numerow spoza <paramref name="juzPosiadane"/> i tylko do limitu
    /// <paramref name="limitPobran"/> - by nie marnowac limitu 64 zapytan/h KSeF.
    /// </summary>
    /// <param name="juzPosiadane">Numery KSeF faktur, ktore juz mamy lokalnie (pomijane przy pobieraniu).</param>
    /// <param name="limitPobran">Maksymalna liczba pelnych faktur do pobrania w tym wywolaniu (0 = bez limitu).</param>
    Task<WynikImportuZakupow> PobierzZakupyAsync(
        DateTime od,
        DateTime @do,
        ISet<string> juzPosiadane,
        int limitPobran,
        Func<FakturaZKsef, Task>? poPobraniu = null,
        CancellationToken ct = default);
}

/// <summary>Wyjatek sygnalizujacy problem komunikacji z KSeF.</summary>
public sealed class KsefException : Exception
{
    public KsefException(string message, Exception? inner = null) : base(message, inner) { }
}
