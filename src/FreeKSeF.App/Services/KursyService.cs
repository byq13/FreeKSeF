using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.Services;

/// <summary>
/// Wlasna tabela kursow walut zasilana z NBP. Do faktury bierzemy kurs z ostatniego
/// dnia roboczego PRZED data wystawienia (kurs dnia poprzedniego).
/// </summary>
public static class KursyService
{
    /// <summary>Importuje kursy tabeli A z NBP do lokalnej tabeli (pomija juz istniejace). Zwraca liczbe dodanych.</summary>
    public static async Task<int> ImportujAsync(DateTime od, DateTime @do, CancellationToken ct = default)
    {
        var kursy = await NbpService.PobierzTabeleA(od, @do, ct);
        if (kursy.Count == 0) return 0;

        using var db = AppServices.Db();
        var dodane = 0;
        foreach (var k in kursy)
        {
            var d = k.Data.Date;
            if (db.Kursy.Any(x => x.Kod == k.Kod && x.Data == d)) continue;
            db.Kursy.Add(new KursWaluty { Kod = k.Kod, Data = d, Kurs = k.Mid, Tabela = "A" });
            dodane++;
        }
        db.SaveChanges();
        return dodane;
    }

    /// <summary>Kurs z lokalnej tabeli z ostatniego dnia PRZED podana data (lub null, gdy brak).</summary>
    public static decimal? Kurs(string waluta, DateTime data)
    {
        if (string.Equals(waluta, "PLN", StringComparison.OrdinalIgnoreCase)) return 1m;
        var d = data.Date;
        using var db = AppServices.Db();
        return db.Kursy
            .Where(x => x.Kod == waluta && x.Data < d)
            .OrderByDescending(x => x.Data)
            .Select(x => (decimal?)x.Kurs)
            .FirstOrDefault();
    }

    /// <summary>
    /// Kurs do faktury: jak <see cref="Kurs"/>, a gdy brak w bazie - dociaga z NBP
    /// (ostatnie ~14 dni przed data) i probuje ponownie. Null gdy nadal nieznany.
    /// </summary>
    public static async Task<decimal?> KursDoFakturyAsync(string waluta, DateTime dataWystawienia, CancellationToken ct = default)
    {
        if (string.Equals(waluta, "PLN", StringComparison.OrdinalIgnoreCase)) return 1m;

        var k = Kurs(waluta, dataWystawienia);
        if (k is not null) return k;

        try { await ImportujAsync(dataWystawienia.AddDays(-14), dataWystawienia, ct); }
        catch { /* brak sieci - zwrocimy null, user wpisze recznie */ }

        return Kurs(waluta, dataWystawienia);
    }
}
