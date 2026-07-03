using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;
using FreeKSeF.Core.Fa3;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace FreeKSeF.Pdf;

/// <summary>
/// Generuje czytelny PDF faktury na podstawie XML w formacie FA(3).
/// Uzywa MigraDoc + osadzonego fontu DejaVu (polskie znaki, identycznie na Windows i Linux).
/// </summary>
public static class FakturaPdfGenerator
{
    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly object FontLock = new();
    private static bool _fontUstawiony;

    /// <summary>Generuje PDF faktury. <paramref name="numerKsef"/> i <paramref name="maUpo"/> sa opcjonalne (sprzedaz przed wyslaniem).</summary>
    public static byte[] GenerujPdf(string xmlFa3, string? numerKsef = null, bool maUpo = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlFa3);
        UstawFont();

        var fa = Fa3Serializer.FromXml(xmlFa3);
        var doc = BudujDokument(fa, numerKsef, maUpo);

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static void UstawFont()
    {
        if (_fontUstawiony) return;
        lock (FontLock)
        {
            if (_fontUstawiony) return;
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new DejaVuFontResolver();
            _fontUstawiony = true;
        }
    }

    private static Document BudujDokument(Faktura fa, string? numerKsef, bool maUpo)
    {
        var doc = new Document();
        doc.Styles["Normal"]!.Font.Name = DejaVuFontResolver.Family;
        doc.Styles["Normal"]!.Font.Size = 9;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.5);

        NaglowekFaktury(sec, fa, numerKsef);
        Strony(sec, fa);
        TabelaPozycji(sec, fa);
        Podsumowanie(sec, fa);
        Platnosc(sec, fa);
        Stopka(sec, maUpo);

        return doc;
    }

    private static void NaglowekFaktury(Section sec, Faktura fa, string? numerKsef)
    {
        var tytul = sec.AddParagraph($"Faktura {fa.Fa.P2}");
        tytul.Format.Font.Size = 16;
        tytul.Format.Font.Bold = true;
        tytul.Format.SpaceAfter = Unit.FromMillimeter(2);

        var info = sec.AddParagraph();
        if (!string.IsNullOrWhiteSpace(fa.Fa.P1M))
            info.AddText($"Miejsce wystawienia: {fa.Fa.P1M}    ");
        info.AddText($"Data wystawienia: {fa.Fa.P1:yyyy-MM-dd}");
        if (fa.Fa.P6Specified)
            info.AddText($"    Data sprzedazy: {fa.Fa.P6:yyyy-MM-dd}");
        info.AddText($"    Waluta: {Xml(fa.Fa.KodWaluty)}");
        if (!string.IsNullOrWhiteSpace(numerKsef))
        {
            info.AddLineBreak();
            var nr = info.AddFormattedText($"Numer KSeF: {numerKsef}");
            nr.Bold = true;
        }
        info.Format.SpaceAfter = Unit.FromMillimeter(4);
    }

    private static void Strony(Section sec, Faktura fa)
    {
        var t = sec.AddTable();
        t.AddColumn(Unit.FromCentimeter(8.5));
        t.AddColumn(Unit.FromCentimeter(8.5));
        var r = t.AddRow();

        BlokStrony(r.Cells[0], "Sprzedawca",
            fa.Podmiot1.DaneIdentyfikacyjne.Nazwa,
            fa.Podmiot1.DaneIdentyfikacyjne.Nip,
            AdresTekst(fa.Podmiot1.Adres));

        BlokStrony(r.Cells[1], "Nabywca",
            fa.Podmiot2.DaneIdentyfikacyjne.Nazwa,
            fa.Podmiot2.DaneIdentyfikacyjne.Nip,
            AdresTekst(fa.Podmiot2.Adres));

        sec.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(3);
    }

    private static void BlokStrony(Cell cell, string tytul, string nazwa, string? nip, string adres)
    {
        var t = cell.AddParagraph(tytul);
        t.Format.Font.Bold = true;
        t.Format.Font.Size = 8;
        t.Format.Font.Color = Colors.Gray;

        cell.AddParagraph(nazwa).Format.Font.Bold = true;
        if (!string.IsNullOrWhiteSpace(nip))
            cell.AddParagraph($"NIP: {nip}");
        foreach (var linia in adres.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            cell.AddParagraph(linia);
    }

    private static void TabelaPozycji(Section sec, Faktura fa)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.25;
        t.Borders.Color = Colors.LightGray;

        double[] szer = { 1, 6.6, 1.3, 1.4, 2, 1.3, 2, 1.8, 2 };
        foreach (var s in szer) t.AddColumn(Unit.FromCentimeter(s));

        string[] naglowki = { "Lp", "Nazwa", "J.m.", "Ilosc", "Cena netto", "VAT", "Netto", "Kwota VAT", "Brutto" };
        var hr = t.AddRow();
        hr.Shading.Color = Colors.WhiteSmoke;
        hr.Format.Font.Bold = true;
        for (var i = 0; i < naglowki.Length; i++)
        {
            hr.Cells[i].AddParagraph(naglowki[i]);
            if (i >= 3) hr.Cells[i].Format.Alignment = ParagraphAlignment.Right;
        }

        foreach (var w in fa.Fa.FaWiersz)
        {
            var netto = w.P11;
            var stawka = StawkaProcent(w.P12);
            var vat = Math.Round(netto * stawka, 2, MidpointRounding.AwayFromZero);
            var brutto = netto + vat;

            var r = t.AddRow();
            r.Cells[0].AddParagraph(w.NrWierszaFa.ToString(Pl));
            r.Cells[1].AddParagraph(w.P7 ?? string.Empty);
            r.Cells[2].AddParagraph(w.P8A ?? string.Empty);
            Kwota(r.Cells[3], w.P8B);
            Kwota(r.Cells[4], w.P9A);
            r.Cells[5].AddParagraph(StawkaTekst(w.P12));
            r.Cells[5].Format.Alignment = ParagraphAlignment.Right;
            Kwota(r.Cells[6], netto);
            Kwota(r.Cells[7], vat);
            Kwota(r.Cells[8], brutto);
        }

        sec.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(2);
    }

    private static void Podsumowanie(Section sec, Faktura fa)
    {
        var netto = fa.Fa.FaWiersz.Sum(w => w.P11);
        var brutto = fa.Fa.P15;
        var vat = brutto - netto;

        var p = sec.AddParagraph();
        p.Format.Alignment = ParagraphAlignment.Right;
        p.AddText($"Razem netto: {Pieniadz(netto)}");
        p.AddLineBreak();
        p.AddText($"VAT: {Pieniadz(vat)}");
        p.AddLineBreak();
        var razem = p.AddFormattedText($"Do zaplaty: {Pieniadz(brutto)} {Xml(fa.Fa.KodWaluty)}");
        razem.Bold = true;
        razem.Size = 12;
        p.Format.SpaceAfter = Unit.FromMillimeter(4);
    }

    private static void Platnosc(Section sec, Faktura fa)
    {
        var pl = fa.Fa.Platnosc;
        if (pl is null) return;

        var p = sec.AddParagraph();
        p.Format.Font.Size = 8;
        if (pl.FormaPlatnosciSpecified)
            p.AddText($"Forma platnosci: {FormaTekst(pl.FormaPlatnosci)}");
        var termin = pl.TerminPlatnosci?.FirstOrDefault();
        if (termin is { TerminSpecified: true })
            p.AddText($"    Termin: {termin.Termin:yyyy-MM-dd}");
        var rachunek = pl.RachunekBankowy?.FirstOrDefault();
        if (rachunek is not null && !string.IsNullOrWhiteSpace(rachunek.NrRb))
        {
            p.AddLineBreak();
            p.AddText($"Rachunek: {rachunek.NrRb}");
        }
    }

    private static void Stopka(Section sec, bool maUpo)
    {
        var p = sec.AddParagraph();
        p.Format.SpaceBefore = Unit.FromMillimeter(8);
        p.Format.Font.Size = 7;
        p.Format.Font.Color = Colors.Gray;
        p.AddText(maUpo
            ? "Faktura przyjeta w KSeF (dostepne UPO). Wygenerowano przez FreeKSeF."
            : "Wygenerowano przez FreeKSeF.");
    }

    // --- pomocnicze ---

    private static void Kwota(Cell cell, decimal wartosc)
    {
        var p = cell.AddParagraph(wartosc.ToString("N2", Pl));
        p.Format.Alignment = ParagraphAlignment.Right;
    }

    private static string Pieniadz(decimal w) => w.ToString("N2", Pl);

    private static string AdresTekst(TAdres? a)
    {
        if (a is null) return string.Empty;
        var linie = new List<string>();
        if (!string.IsNullOrWhiteSpace(a.AdresL1)) linie.Add(a.AdresL1);
        if (!string.IsNullOrWhiteSpace(a.AdresL2)) linie.Add(a.AdresL2);
        return string.Join('\n', linie);
    }

    /// <summary>Procent stawki jako ulamek (do wyliczenia VAT); dla zw/0/oo/np = 0.</summary>
    private static decimal StawkaProcent(TStawkaPodatku s)
        => decimal.TryParse(Xml(s), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p / 100m : 0m;

    /// <summary>Tekst stawki do kolumny: "23%", "8%", "zw", "0 KR" itp.</summary>
    private static string StawkaTekst(TStawkaPodatku s)
    {
        var v = Xml(s);
        return int.TryParse(v, out _) ? v + "%" : v;
    }

    private static string FormaTekst(TFormaPlatnosci f) => Xml(f) switch
    {
        "1" => "gotowka",
        "2" => "karta",
        "3" => "bon",
        "4" => "czek",
        "5" => "kredyt",
        "6" => "przelew",
        "7" => "platnosc mobilna",
        var x => x,
    };

    /// <summary>Zwraca wartosc XML (atrybut XmlEnum) skladnika enuma, np. TKodWaluty.Pln -> "PLN".</summary>
    private static string Xml<T>(T value) where T : Enum
    {
        var field = typeof(T).GetField(value.ToString());
        var attr = field?.GetCustomAttribute<XmlEnumAttribute>();
        return attr?.Name ?? value.ToString();
    }
}
