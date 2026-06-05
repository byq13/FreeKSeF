using System.Net.Http;
using System.Text.Json;
using FreeKSeF.Core.Models;

namespace FreeKSeF.App.Services;

/// <summary>Dane firmy pobrane z rejestru po NIP.</summary>
public sealed record DaneFirmy(string Nip, string Nazwa, string AdresL1, string AdresL2);

/// <summary>
/// Pobiera dane firmy po NIP z bezplatnego API Ministerstwa Finansow
/// (Wykaz podatnikow VAT / "Biala lista", wl-api.mf.gov.pl) - bez klucza.
/// Zwraca nazwe i adres na podstawie danych z rejestru (REGON/GUS).
/// </summary>
public static class GusService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private const string BazaUrl = "https://wl-api.mf.gov.pl/api/search/nip/";

    /// <summary>
    /// Zwraca dane firmy dla podanego NIP (akceptuje myslniki/spacje/PL) albo null, gdy nie znaleziono.
    /// Rzuca <see cref="InvalidOperationException"/> przy bledzie NIP lub komunikacji.
    /// </summary>
    public static async Task<DaneFirmy?> PobierzAsync(string nipWejscie, CancellationToken ct = default)
    {
        var nip = Nip.Normalizuj(nipWejscie);
        if (!Nip.Waliduj(nip))
            throw new InvalidOperationException("Nieprawidlowy NIP (sprawdz cyfry).");

        var url = $"{BazaUrl}{nip}?date={DateTime.Today:yyyy-MM-dd}";

        HttpResponseMessage resp;
        try
        {
            resp = await Http.GetAsync(url, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Brak polaczenia z rejestrem MF: " + ex.Message, ex);
        }

        var tresc = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Rejestr MF zwrocil blad {(int)resp.StatusCode}. {Komunikat(tresc)}");

        using var doc = JsonDocument.Parse(tresc);
        if (!doc.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("subject", out var subj) ||
            subj.ValueKind == JsonValueKind.Null)
            return null;

        var nazwa = Tekst(subj, "name");
        if (string.IsNullOrWhiteSpace(nazwa)) return null;

        var adres = Tekst(subj, "workingAddress");
        if (string.IsNullOrWhiteSpace(adres)) adres = Tekst(subj, "residenceAddress");
        var (l1, l2) = PodzielAdres(adres);

        return new DaneFirmy(nip, nazwa.Trim(), l1, l2);
    }

    private static string Tekst(JsonElement obj, string nazwa)
        => obj.TryGetProperty(nazwa, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : string.Empty;

    /// <summary>Adres MF jest jednym ciagiem "ulica i numer, kod miasto" - dzielimy po ostatnim przecinku.</summary>
    private static (string L1, string L2) PodzielAdres(string adres)
    {
        adres = adres.Trim();
        if (string.IsNullOrEmpty(adres)) return (string.Empty, string.Empty);
        var i = adres.LastIndexOf(',');
        return i < 0
            ? (adres, string.Empty)
            : (adres[..i].Trim(), adres[(i + 1)..].Trim());
    }

    private static string Komunikat(string tresc)
    {
        try
        {
            using var doc = JsonDocument.Parse(tresc);
            if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                return m.GetString() ?? string.Empty;
        }
        catch { /* nie-JSON */ }
        return string.Empty;
    }
}
