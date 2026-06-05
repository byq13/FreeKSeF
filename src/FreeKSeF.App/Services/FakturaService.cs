using System.IO;
using System.Text;
using FreeKSeF.Data.Entities;
using FreeKSeF.Ksef;
using FreeKSeF.Pdf;

namespace FreeKSeF.App.Services;

/// <summary>
/// Wspolne operacje na fakturze: generowanie/zapis PDF, zapis XML, wysylka do KSeF.
/// Uzywane przez listy sprzedazy i zakupu, by nie dublowac logiki.
/// </summary>
public static class FakturaService
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Generuje PDF faktury (z numerem KSeF i informacja o UPO, jesli sa).</summary>
    public static byte[] Pdf(Invoice inv)
    {
        ArgumentNullException.ThrowIfNull(inv);
        return FakturaPdfGenerator.GenerujPdf(inv.Xml, inv.NumerKsef, !string.IsNullOrEmpty(inv.UpoXml));
    }

    /// <summary>Zapisuje PDF do pliku tymczasowego (do podgladu w oknie) i zwraca sciezke.</summary>
    public static string ZapiszPdfDoTemp(Invoice inv)
    {
        var sciezka = Path.Combine(Path.GetTempPath(), $"FreeKSeF_{BezpiecznaNazwa(inv.Numer)}.pdf");
        File.WriteAllBytes(sciezka, Pdf(inv));
        return sciezka;
    }

    /// <summary>Zapisuje PDF pod wskazana sciezka.</summary>
    public static void ZapiszPdf(Invoice inv, string sciezka) => File.WriteAllBytes(sciezka, Pdf(inv));

    /// <summary>Zapisuje XML FA(3) faktury (UTF-8 bez BOM) - m.in. do recznego wgrania w aplikacji gov.pl.</summary>
    public static void ZapiszXml(Invoice inv, string sciezka)
        => File.WriteAllText(sciezka, inv.Xml, Utf8NoBom);

    /// <summary>
    /// Wysyla zapisana fakture (z bufora) do KSeF i aktualizuje jej status/numer KSeF/UPO w bazie.
    /// Loguje sie wczesniej z zapisanych ustawien. Zwraca wynik do pokazania uzytkownikowi.
    /// </summary>
    public static async Task<WynikWysylki> WyslijAsync(int invoiceId, CancellationToken ct = default)
    {
        await AppServices.ZalogujZUstawienAsync(ct);

        using var db = AppServices.Db();
        var inv = db.Invoices.Find(invoiceId)
            ?? throw new KsefException("Nie znaleziono faktury w bazie.");

        var wynik = await AppServices.Ksef.WyslijFakture(Utf8NoBom.GetBytes(inv.Xml), ct);

        if (wynik.Sukces)
        {
            inv.Status = StatusFaktury.Przyjeta;
            inv.NumerKsef = wynik.NumerKsef;
            inv.NumerReferencyjny = wynik.NumerReferencyjny;
            inv.UpoXml = wynik.UpoXml;
            inv.WyslanoUtc = DateTime.UtcNow;
        }
        else
        {
            inv.Status = StatusFaktury.Blad;
            inv.NumerReferencyjny = wynik.NumerReferencyjny;
        }
        db.SaveChanges();

        return wynik;
    }

    public static string BezpiecznaNazwa(string numer)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            numer = numer.Replace(c, '_');
        return string.IsNullOrWhiteSpace(numer) ? "faktura" : numer;
    }
}
