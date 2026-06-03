namespace FreeKSeF.Ksef;

/// <summary>
/// Tymczasowa implementacja bramki KSeF uzywana, dopoki nie zostanie podlaczony
/// oficjalny pakiet KSeF.Client (wymaga tokenu PAT do GitHub Packages MF).
/// Generowanie i walidacja XML FA(3) oraz eksport do pliku dzialaja niezaleznie
/// od tej bramki - blokowana jest jedynie komunikacja sieciowa z KSeF.
/// </summary>
public sealed class PlaceholderKsefGateway : IKsefGateway
{
    private const string Powod =
        "Integracja sieciowa z KSeF nie jest jeszcze skonfigurowana. " +
        "Podlacz pakiet KSeF.Client (GitHub Packages MF, token PAT z read:packages). " +
        "W miedzyczasie mozesz generowac i eksportowac XML FA(3) do recznego wyslania w Aplikacji Podatnika.";

    public Task<bool> ZalogujAsync(KsefPolaczenie polaczenie, CancellationToken ct = default)
        => throw new KsefException(Powod);

    public Task<WynikWysylki> WyslijFakture(byte[] xmlFa3, CancellationToken ct = default)
        => throw new KsefException(Powod);

    public Task<string?> PobierzUpoAsync(string numerReferencyjny, CancellationToken ct = default)
        => throw new KsefException(Powod);

    public Task<IReadOnlyList<FakturaZKsef>> PobierzZakupyAsync(DateTime od, DateTime @do, CancellationToken ct = default)
        => throw new KsefException(Powod);
}
