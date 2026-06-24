using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace FreeKSeF.App.Services;

/// <summary>Pojedynczy kurs sredni z NBP.</summary>
public sealed record NbpKurs(string Kod, DateTime Data, decimal Mid);

/// <summary>Pobiera srednie kursy walut (tabela A) z bezplatnego API NBP (api.nbp.pl).</summary>
public static class NbpService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>
    /// Pobiera kursy tabeli A dla zakresu dat (max ~93 dni). Zwraca pusta liste,
    /// gdy w zakresie nie ma notowan (np. same weekendy).
    /// </summary>
    public static async Task<IReadOnlyList<NbpKurs>> PobierzTabeleA(DateTime od, DateTime @do, CancellationToken ct = default)
    {
        var url = $"https://api.nbp.pl/api/exchangerates/tables/A/{od:yyyy-MM-dd}/{@do:yyyy-MM-dd}/?format=json";

        HttpResponseMessage resp;
        try { resp = await Http.GetAsync(url, ct); }
        catch (Exception ex) { throw new InvalidOperationException("Brak polaczenia z NBP: " + ex.Message, ex); }

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return Array.Empty<NbpKurs>(); // brak danych w zakresie

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"NBP zwrocil blad {(int)resp.StatusCode}.");

        var tresc = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(tresc);

        var wynik = new List<NbpKurs>();
        foreach (var tabela in doc.RootElement.EnumerateArray())
        {
            if (!tabela.TryGetProperty("effectiveDate", out var dataEl) ||
                !DateTime.TryParse(dataEl.GetString(), out var data) ||
                !tabela.TryGetProperty("rates", out var rates))
                continue;

            foreach (var r in rates.EnumerateArray())
            {
                var kod = r.GetProperty("code").GetString();
                if (string.IsNullOrEmpty(kod)) continue;
                var mid = r.GetProperty("mid").GetDecimal();
                wynik.Add(new NbpKurs(kod, data.Date, mid));
            }
        }
        return wynik;
    }
}
