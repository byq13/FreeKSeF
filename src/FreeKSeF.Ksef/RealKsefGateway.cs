using FreeKSeF.Core.Fa3;
using FreeKSeF.Data.Entities;
using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Core.Models.Sessions.OnlineSession;
using KSeF.Client.DI;
using Microsoft.Extensions.DependencyInjection;

namespace FreeKSeF.Ksef;

/// <summary>
/// Realna integracja z KSeF oparta o oficjalny klient Ministerstwa Finansow (KSeF.Client).
/// Logowanie tokenem KSeF, sesja interaktywna (online), szyfrowanie AES-256 + wysylka,
/// pobieranie UPO oraz import faktur zakupu po zakresie dat (bez limitu 30 dni).
/// </summary>
public sealed class RealKsefGateway : IKsefGateway
{
    private const int OpoznienieLimituKsefMs = 6_000;

    private IKSeFClient? _client;
    private ICryptographyService? _crypto;
    private string? _accessToken;
    private string? _nip;

    public async Task<bool> ZalogujAsync(KsefPolaczenie polaczenie, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(polaczenie);
        try
        {
            var services = new ServiceCollection();
            services.AddKSeFClient(o => o.BaseUrl = UrlSrodowiska(polaczenie.Srodowisko));
            services.AddCryptographyClient(CryptographyServiceWarmupMode.Blocking);
            var provider = services.BuildServiceProvider();

            _client = provider.GetRequiredService<IKSeFClient>();
            _crypto = provider.GetRequiredService<ICryptographyService>();
            await _crypto.WarmupAsync(ct);

            var auth = provider.GetRequiredService<IAuthCoordinator>();
            _nip = polaczenie.NipPodatnika;

            var wynik = await auth.AuthKsefTokenAsync(
                AuthenticationTokenContextIdentifierType.Nip,
                polaczenie.NipPodatnika,
                polaczenie.Token,
                _crypto,
                EncryptionMethodEnum.Rsa,
                authorizationPolicy: null,
                cancellationToken: ct);

            _accessToken = wynik.AccessToken?.Token;
            return !string.IsNullOrEmpty(_accessToken);
        }
        catch (Exception ex) when (ex is not KsefException)
        {
            throw new KsefException("Logowanie do KSeF nie powiodlo sie: " + ex.Message, ex);
        }
    }

    public async Task<WynikWysylki> WyslijFakture(byte[] xmlFa3, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(xmlFa3);
        var (client, crypto, token) = Wymagaj();

        try
        {
            // 1) Klucz/IV sesji + szyfrowanie faktury AES-256.
            var enc = crypto.GetEncryptionData();
            var encrypted = crypto.EncryptBytesWithAES256(xmlFa3, enc.CipherKey, enc.CipherIv);
            var metaPlain = crypto.GetMetaData(xmlFa3);
            var metaEnc = crypto.GetMetaData(encrypted);

            // 2) Otwarcie sesji online dla FA(3).
            var open = new OpenOnlineSessionRequest
            {
                FormCode = new FormCode
                {
                    SystemCode = Fa3Mapper.KodSystemowy, // "FA (3)"
                    SchemaVersion = Fa3Mapper.WersjaSchemy, // "1-0E"
                    Value = "FA",
                },
                Encryption = enc.EncryptionInfo,
            };
            var sesja = await client.OpenOnlineSessionAsync(open, token, null, ct);
            var sesjaRef = sesja.ReferenceNumber;

            // 3) Wyslanie zaszyfrowanej faktury.
            var send = new SendInvoiceRequest
            {
                InvoiceHash = metaPlain.HashSHA,
                InvoiceSize = metaPlain.FileSize,
                EncryptedInvoiceHash = metaEnc.HashSHA,
                EncryptedInvoiceSize = metaEnc.FileSize,
                EncryptedInvoiceContent = Convert.ToBase64String(encrypted),
            };
            var wyslana = await client.SendOnlineSessionInvoiceAsync(send, sesjaRef, token, ct);
            var fakturaRef = wyslana.ReferenceNumber;

            // 4) Zamkniecie sesji (uruchamia wygenerowanie UPO).
            await client.CloseOnlineSessionAsync(sesjaRef, token, ct);

            // 5) Oczekiwanie na nadanie numeru KSeF i pobranie UPO.
            string? numerKsef = null;
            string? upo = null;
            for (var i = 0; i < 20; i++)
            {
                var inv = await client.GetSessionInvoiceAsync(sesjaRef, fakturaRef, token, ct);
                if (!string.IsNullOrEmpty(inv.KsefNumber))
                {
                    numerKsef = inv.KsefNumber;
                    upo = await PobierzUpoBezpiecznie(client, sesjaRef, fakturaRef, token, ct);
                    break;
                }
                await Task.Delay(1500, ct);
            }

            return new WynikWysylki(
                Sukces: numerKsef is not null,
                NumerReferencyjny: fakturaRef,
                NumerKsef: numerKsef,
                UpoXml: upo,
                Blad: numerKsef is null ? "KSeF nie nadal numeru w oczekiwanym czasie - sprawdz status pozniej." : null);
        }
        catch (Exception ex) when (ex is not KsefException)
        {
            throw new KsefException("Wysylka faktury do KSeF nie powiodla sie: " + ex.Message, ex);
        }
    }

    public Task<string?> PobierzUpoAsync(string numerReferencyjny, CancellationToken ct = default)
        // UPO pobieramy w ramach sesji w WyslijFakture; samodzielne pobranie wymaga kontekstu sesji.
        => Task.FromResult<string?>(null);

    public async Task<WynikImportuZakupow> PobierzZakupyAsync(
        DateTime od,
        DateTime @do,
        ISet<string> juzPosiadane,
        int limitPobran,
        Func<FakturaZKsef, Task>? poPobraniu = null,
        CancellationToken ct = default)
    {
        var (client, _, token) = Wymagaj();
        juzPosiadane ??= new HashSet<string>();
        var pobrane = new List<FakturaZKsef>();

        try
        {
            // 1) Tanie metadane - same numery KSeF (bez pobierania pelnych faktur).
            var metadane = await PobierzMetadaneAsync(client, token, od, @do, ct);
            var znalezione = metadane.Count;

            // 2) Pomijamy te, ktore juz mamy lokalnie - tego nie pobieramy ponownie.
            var brakujace = metadane.Where(m => !juzPosiadane.Contains(m.Numer)).ToList();
            var juz = znalezione - brakujace.Count;

            // 3) Pobieramy pelne faktury TYLKO dla brakujacych i tylko do limitu zetonow.
            var doPobrania = limitPobran > 0 ? Math.Min(brakujace.Count, limitPobran) : brakujace.Count;
            for (var i = 0; i < doPobrania; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(OpoznienieLimituKsefMs, ct);

                var m = brakujace[i];
                var xml = await client.GetInvoiceAsync(m.Numer, token, ct);
                var faktura = new FakturaZKsef(m.Numer, m.Data, xml);
                pobrane.Add(faktura);
                if (poPobraniu is not null)
                    await poPobraniu(faktura);
            }

            var pozostalo = brakujace.Count - pobrane.Count;
            return new WynikImportuZakupow(pobrane, znalezione, juz, pobrane.Count, pozostalo);
        }
        catch (Exception ex) when (ex is not KsefException)
        {
            throw new KsefException("Pobieranie faktur zakupu z KSeF nie powiodlo sie: " + ex.Message, ex);
        }
    }

    /// <summary>Pobiera same metadane faktur zakupu (numer KSeF + data) z zakresu dat.</summary>
    private static async Task<List<(string Numer, DateTime Data)>> PobierzMetadaneAsync(
        IKSeFClient client, string token, DateTime od, DateTime @do, CancellationToken ct)
    {
        var wynik = new List<(string, DateTime)>();
        var filtry = new InvoiceQueryFilters
        {
            SubjectType = InvoiceSubjectType.Subject2, // jestesmy nabywca
            DateRange = new DateRange
            {
                DateType = DateType.Issue,
                From = PoczatekDniaUtc(od),
                To = KoniecDniaUtc(@do),
            },
        };

        int offset = 0;
        const int rozmiarStrony = 100;
        while (true)
        {
            var strona = await client.QueryInvoiceMetadataAsync(filtry, token, offset, rozmiarStrony, SortOrder.Desc, ct);
            var faktury = strona.Invoices;
            if (faktury is null || faktury.Count == 0) break;

            foreach (var meta in faktury)
                if (!string.IsNullOrEmpty(meta.KsefNumber))
                    wynik.Add((meta.KsefNumber, meta.AcquisitionDate.UtcDateTime));

            if (!strona.HasMore) break;
            await Task.Delay(OpoznienieLimituKsefMs, ct);
            offset += rozmiarStrony;
        }

        return wynik;
    }

    private static async Task<string?> PobierzUpoBezpiecznie(IKSeFClient client, string sesjaRef, string fakturaRef, string token, CancellationToken ct)
    {
        try { return await client.GetSessionInvoiceUpoByReferenceNumberAsync(sesjaRef, fakturaRef, token, ct); }
        catch { return null; }
    }

    private static DateTimeOffset PoczatekDniaUtc(DateTime data)
        => new(data.Year, data.Month, data.Day, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset KoniecDniaUtc(DateTime data)
        => PoczatekDniaUtc(data).AddDays(1).AddTicks(-1);

    private (IKSeFClient client, ICryptographyService crypto, string token) Wymagaj()
    {
        if (_client is null || _crypto is null || string.IsNullOrEmpty(_accessToken))
            throw new KsefException("Najpierw zaloguj sie do KSeF (Ustawienia: token + srodowisko).");
        return (_client, _crypto, _accessToken!);
    }

    private static string UrlSrodowiska(Srodowisko s) => s switch
    {
        Srodowisko.Test => KsefEnvironmentsUris.TEST,
        Srodowisko.Demo => KsefEnvironmentsUris.DEMO,
        Srodowisko.Produkcja => KsefEnvironmentsUris.PROD,
        _ => KsefEnvironmentsUris.TEST,
    };
}
