using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using FreeKSeF.Core.Fa3;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using QRCoder;

namespace FreeKSeF.Pdf;

/// <summary>
/// Generuje PDF faktury na podstawie XML FA(3) w ukladzie wzorowanym na wizualizacji
/// Ministerstwa Finansow (ksef-pdf-generator): naglowek z rodzajem faktury i numerem KSeF,
/// sekcje stron, szczegoly, pozycje, podsumowanie stawek, adnotacje, platnosc, warunki
/// transakcji, rejestry, stopka oraz kod QR weryfikacji w KSeF.
/// Sekcje bez danych sa pomijane. Uzywa MigraDoc + osadzonego fontu DejaVu.
/// </summary>
public static class FakturaPdfGenerator
{
    /// <summary>Adresy weryfikacji QR (KOD I) dla srodowisk KSeF.</summary>
    public const string QrTest = "https://qr-test.ksef.mf.gov.pl";
    public const string QrDemo = "https://qr-demo.ksef.mf.gov.pl";
    public const string QrProdukcja = "https://qr.ksef.mf.gov.pl";

    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly object FontLock = new();
    private static bool _fontUstawiony;

    private static readonly Color Czerwony = new(220, 38, 38);
    private static readonly Color KolorLinii = new(200, 200, 200);
    private static readonly Color TloNaglowka = new(243, 244, 246);
    private static readonly Color Szary = new(90, 90, 90);
    private static readonly Color Link = new(29, 78, 216);

    /// <summary>Szerokosc obszaru tresci A4 przy marginesach 1,5 cm.</summary>
    private const double SzerokoscCm = 18.0;

    /// <summary>
    /// Generuje PDF faktury. <paramref name="numerKsef"/> i <paramref name="maUpo"/> sa opcjonalne
    /// (sprzedaz przed wyslaniem). Kod QR weryfikacji jest dodawany, gdy znany jest numer KSeF
    /// i adres srodowiska <paramref name="qrBazaUrl"/> (np. <see cref="QrProdukcja"/>).
    /// </summary>
    public static byte[] GenerujPdf(string xmlFa3, string? numerKsef = null, bool maUpo = false, string? qrBazaUrl = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlFa3);
        UstawFont();

        var fa = Fa3Serializer.FromXml(xmlFa3);
        var doc = BudujDokument(fa, xmlFa3, numerKsef, maUpo, qrBazaUrl);

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    /// <summary>
    /// Link weryfikacyjny KSeF (KOD I): {baza}/invoice/{NIP sprzedawcy}/{DD-MM-RRRR}/{SHA-256 pliku w Base64URL}.
    /// </summary>
    public static string LinkWeryfikacji(string qrBazaUrl, string nipSprzedawcy, DateTime dataWystawienia, string xmlFa3)
    {
        var hash = SHA256.HashData(Utf8NoBom.GetBytes(xmlFa3));
        var b64Url = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{qrBazaUrl.TrimEnd('/')}/invoice/{nipSprzedawcy}/{dataWystawienia:dd-MM-yyyy}/{b64Url}";
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

    private static Document BudujDokument(Faktura fa, string xml, string? numerKsef, bool maUpo, string? qrBazaUrl)
    {
        var doc = new Document();
        doc.Info.Title = $"Faktura {fa.Fa.P2}";
        var normal = doc.Styles["Normal"]!;
        normal.Font.Name = DejaVuFontResolver.Family;
        normal.Font.Size = 7.5;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        Stronicowanie(sec);

        Naglowek(sec, fa, numerKsef);
        Strony(sec, fa);
        Szczegoly(sec, fa);
        Pozycje(sec, fa);
        Kwota(sec, fa);
        PodsumowanieStawek(sec, fa);
        Adnotacje(sec, fa);
        Rozliczenie(sec, fa);
        DodatkoweInformacje(sec, fa);
        Platnosc(sec, fa);
        WarunkiTransakcji(sec, fa);
        Rejestry(sec, fa);
        KodQr(sec, fa, xml, numerKsef, qrBazaUrl);
        Wytworzona(sec, fa, maUpo);

        return doc;
    }

    // ---------------------------------------------------------------- sekcje

    private static void Stronicowanie(Section sec)
    {
        var p = sec.Footers.Primary.AddParagraph();
        p.Format.Alignment = ParagraphAlignment.Right;
        p.Format.Font.Size = 7;
        p.AddPageField();
        p.AddText(" z ");
        p.AddNumPagesField();
    }

    private static void Naglowek(Section sec, Faktura fa, string? numerKsef)
    {
        var t = sec.AddTable();
        t.AddColumn(Unit.FromCentimeter(8.2));
        t.AddColumn(Unit.FromCentimeter(SzerokoscCm - 8.2));
        var r = t.AddRow();

        var logo = r.Cells[0].AddParagraph();
        logo.Format.Font.Size = 15;
        logo.AddText("Krajowy System ");
        logo.AddFormattedText("e", TextFormat.Bold).Color = Czerwony;
        logo.AddFormattedText("-Faktur", TextFormat.Bold);

        var c = r.Cells[1];
        c.Format.Alignment = ParagraphAlignment.Right;
        c.AddParagraph("Numer Faktury:").Format.Font.Size = 9;
        var nr = c.AddParagraph(fa.Fa.P2 ?? string.Empty);
        nr.Format.Font.Size = 16;
        nr.Format.Font.Bold = true;
        c.AddParagraph(Fa3Rodzaj.Nazwa(Fa3Rodzaj.Kod(fa.Fa.RodzajFaktury))).Format.Font.Size = 9;

        if (!string.IsNullOrWhiteSpace(numerKsef))
        {
            Pole(c.AddParagraph(), "Numer KSeF", numerKsef, 8);
            if (DataZNumeruKsef(numerKsef) is { } d)
                Pole(c.AddParagraph(), "Data nadania numeru KSeF", Data(d), 8);
        }
        else
        {
            var p = c.AddParagraph("Faktura nie została jeszcze przesłana do KSeF");
            p.Format.Font.Size = 8;
            p.Format.Font.Color = Szary;
        }
    }

    private static void Strony(Section sec, Faktura fa)
    {
        Separator(sec);
        var t = sec.AddTable();
        t.AddColumn(Unit.FromCentimeter(SzerokoscCm / 2));
        t.AddColumn(Unit.FromCentimeter(SzerokoscCm / 2));
        var r = t.AddRow();

        // --- Sprzedawca
        var s = r.Cells[0];
        TytulSekcji(s.AddParagraph("Sprzedawca"));
        var p1 = fa.Podmiot1;
        var ps = s.AddParagraph();
        Linia(ps, "Numer EORI", p1.NrEori);
        if (p1.PrefiksPodatnikaSpecified) Linia(ps, "Prefiks VAT", Xml(p1.PrefiksPodatnika));
        Linia(ps, "NIP", p1.DaneIdentyfikacyjne?.Nip);
        Linia(ps, "Nazwa", p1.DaneIdentyfikacyjne?.Nazwa);
        BlokAdresu(s, "Adres", p1.Adres);
        BlokAdresu(s, "Adres do korespondencji", p1.AdresKoresp);
        BlokKontaktu(s, p1.DaneKontaktowe?.Select(k => ((string?)k.Email, (string?)k.Telefon)));
        if (p1.StatusInfoPodatnikaSpecified)
            Linia(s.AddParagraph(), "Status podatnika", StatusPodatnika(Xml(p1.StatusInfoPodatnika)));

        // --- Nabywca
        var n = r.Cells[1];
        TytulSekcji(n.AddParagraph("Nabywca"));
        var p2 = fa.Podmiot2;
        var pn = n.AddParagraph();
        Linia(pn, "Numer EORI", p2.NrEori);
        IdentyfikatorPodmiotu(pn, p2.DaneIdentyfikacyjne?.Nip, null,
            p2.DaneIdentyfikacyjne?.KodUeSpecified == true ? Xml(p2.DaneIdentyfikacyjne.KodUe) : null, p2.DaneIdentyfikacyjne?.NrVatUe,
            p2.DaneIdentyfikacyjne?.KodKrajuSpecified == true ? Xml(p2.DaneIdentyfikacyjne.KodKraju) : null, p2.DaneIdentyfikacyjne?.NrId,
            p2.DaneIdentyfikacyjne?.BrakIdSpecified == true);
        Linia(pn, "Nazwa", p2.DaneIdentyfikacyjne?.Nazwa);
        BlokAdresu(n, "Adres", p2.Adres);
        BlokAdresu(n, "Adres do korespondencji", p2.AdresKoresp);
        BlokKontaktu(n, p2.DaneKontaktowe?.Select(k => ((string?)k.Email, (string?)k.Telefon)), p2.NrKlienta);
        var jg = n.AddParagraph();
        jg.Format.SpaceBefore = Unit.FromMillimeter(2);
        Linia(jg, "ID nabywcy", p2.IdNabywcy);
        Linia(jg, "Faktura dotyczy jednostki podrzędnej JST", Xml(p2.Jst) == "1" ? "TAK" : "NIE");
        Linia(jg, "Faktura dotyczy członka grupy GV", Xml(p2.Gv) == "1" ? "TAK" : "NIE");

        // --- Podmioty trzecie i upowazniony
        if (fa.Podmiot3 is { Count: > 0 })
        {
            TytulSekcjiGlowny(sec, "Podmiot trzeci");
            var t3 = sec.AddTable();
            t3.AddColumn(Unit.FromCentimeter(SzerokoscCm / 2));
            t3.AddColumn(Unit.FromCentimeter(SzerokoscCm / 2));
            for (var i = 0; i < fa.Podmiot3.Count; i++)
            {
                var pod = fa.Podmiot3[i];
                if (i % 2 == 0) t3.AddRow();
                var cell = t3.Rows[t3.Rows.Count - 1].Cells[i % 2];
                var p = cell.AddParagraph();
                p.Format.SpaceAfter = Unit.FromMillimeter(2);
                Linia(p, "Rola", pod.RolaSpecified ? RolaPodmiotu3(Xml(pod.Rola)) : pod.OpisRoli);
                if (pod.UdzialSpecified) Linia(p, "Udział", pod.Udzial.ToString("0.######", Pl) + "%");
                Linia(p, "Numer EORI", pod.NrEori);
                var d3 = pod.DaneIdentyfikacyjne;
                IdentyfikatorPodmiotu(p, d3?.Nip, d3?.IdWew,
                    d3?.KodUeSpecified == true ? Xml(d3.KodUe) : null, d3?.NrVatUe,
                    d3?.KodKrajuSpecified == true ? Xml(d3.KodKraju) : null, d3?.NrId,
                    d3?.BrakIdSpecified == true);
                Linia(p, "Nazwa", d3?.Nazwa);
                Linia(p, "Numer klienta", pod.NrKlienta);
                Linia(p, "ID nabywcy", pod.IdNabywcy);
                BlokAdresu(cell, "Adres", pod.Adres);
            }
        }

        if (fa.PodmiotUpowazniony is { } pu)
        {
            TytulSekcjiGlowny(sec, "Podmiot upoważniony");
            var p = sec.AddParagraph();
            Linia(p, "Numer EORI", pu.NrEori);
            Linia(p, "NIP", pu.DaneIdentyfikacyjne?.Nip);
            Linia(p, "Nazwa", pu.DaneIdentyfikacyjne?.Nazwa);
            Linia(p, "Rola", RolaUpowaznionego(Xml(pu.RolaPu)));
            BlokAdresu(sec, "Adres", pu.Adres);
        }
    }

    private static void Szczegoly(Section sec, Faktura fa)
    {
        var f = fa.Fa;
        var rodzaj = Fa3Rodzaj.Kod(f.RodzajFaktury);
        var zaliczkowa = rodzaj is Fa3Rodzaj.Zal or Fa3Rodzaj.KorZal;

        Separator(sec);
        TytulSekcjiGlowny(sec, "Szczegóły");

        var t = sec.AddTable();
        t.AddColumn(Unit.FromCentimeter(SzerokoscCm / 2));
        t.AddColumn(Unit.FromCentimeter(SzerokoscCm / 2));
        var r = t.AddRow();

        var l = r.Cells[0].AddParagraph();
        Linia(l, "Data wystawienia, z zastrzeżeniem art. 106na ust. 1 ustawy", Data(f.P1));
        Linia(l, "Miejsce wystawienia", f.P1M);
        Linia(l, "Kod waluty", Xml(f.KodWaluty));
        if (f.KursWalutyZSpecified) Linia(l, "Kurs waluty (zaliczka)", f.KursWalutyZ.ToString("0.######", Pl));
        if (Xml(f.Fp) == "1" && f.FpSpecified) Linia(l, "Faktura, o której mowa w art. 109 ust. 3d ustawy", "TAK");
        if (Xml(f.Tp) == "1" && f.TpSpecified) Linia(l, "Istniejące powiązania między nabywcą a dokonującym dostawy", "TAK");

        var pr = r.Cells[1].AddParagraph();
        if (f.P6Specified)
            Linia(pr, zaliczkowa
                ? "Data otrzymania zapłaty"
                : "Data dokonania lub zakończenia dostawy towarów lub wykonania usługi", Data(f.P6));
        if (f.OkresFa is { } okres)
            Linia(pr, "Okres, którego dotyczy faktura", $"{Data(okres.P6Od)} – {Data(okres.P6Do)}");

        // Faktury zaliczkowe rozliczane ta faktura.
        if (f.FakturaZaliczkowa is { Count: > 0 })
        {
            sec.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(1);
            Tabela(sec, new[] { "Numery wcześniejszych faktur zaliczkowych" }, new[] { 7.0 },
                f.FakturaZaliczkowa.Select(z => new[] { z.NrKSeFFaZaliczkowej ?? z.NrFaZaliczkowej ?? string.Empty }));
        }

        // Zaliczki czesciowe (faktura dokumentujaca kilka zaliczek).
        if (f.ZaliczkaCzesciowa is { Count: > 0 })
        {
            sec.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(1);
            Tabela(sec, new[] { "Data otrzymania zaliczki", "Kwota zaliczki", "Kurs waluty" }, new[] { 4.0, 4.0, 3.0 },
                f.ZaliczkaCzesciowa.Select(z => new[]
                {
                    Data(z.P6Z), Kw(z.P15Z), z.KursWalutyZwSpecified ? z.KursWalutyZw.ToString("0.######", Pl) : string.Empty,
                }), wyrownajOd: 1);
        }

        // Korekta.
        if (Fa3Rodzaj.JestKorekta(rodzaj))
        {
            var pk = sec.AddParagraph();
            pk.Format.SpaceBefore = Unit.FromMillimeter(2);
            Linia(pk, "Przyczyna korekty", f.PrzyczynaKorekty);
            if (f.TypKorektySpecified) Linia(pk, "Typ skutku korekty", TypKorekty(Xml(f.TypKorekty)));
            Linia(pk, "Okres, którego dotyczy korekta", f.OkresFaKorygowanej);
            Linia(pk, "Poprawny numer faktury korygowanej", f.NrFaKorygowany);
            if (f.P15ZkSpecified) Linia(pk, "Kwota zapłaty przed korektą", Kw(f.P15Zk));
            if (f.KursWalutyZkSpecified) Linia(pk, "Kurs waluty przed korektą", f.KursWalutyZk.ToString("0.######", Pl));

            if (f.DaneFaKorygowanej is { Count: > 0 })
            {
                sec.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(1);
                Tabela(sec, new[] { "Data wystawienia faktury korygowanej", "Numer faktury korygowanej", "Numer KSeF faktury korygowanej" },
                    new[] { 4.5, 5.0, 8.5 },
                    f.DaneFaKorygowanej.Select(k => new[]
                    {
                        Data(k.DataWystFaKorygowanej), k.NrFaKorygowanej ?? string.Empty,
                        k.NrKSeFFaKorygowanej ?? (k.NrKSeFnSpecified ? "faktura spoza KSeF" : string.Empty),
                    }));
            }

            if (f.Podmiot1K is { } p1k)
            {
                var pp = sec.AddParagraph();
                pp.Format.SpaceBefore = Unit.FromMillimeter(2);
                pp.AddFormattedText("Dane sprzedawcy z faktury korygowanej: ", TextFormat.Bold);
                pp.AddText($"{p1k.DaneIdentyfikacyjne?.Nazwa}, NIP {p1k.DaneIdentyfikacyjne?.Nip}");
            }
            if (f.Podmiot2K is { Count: > 0 })
                foreach (var p2k in f.Podmiot2K)
                {
                    var pp = sec.AddParagraph();
                    pp.AddFormattedText("Dane nabywcy z faktury korygowanej: ", TextFormat.Bold);
                    pp.AddText($"{p2k.DaneIdentyfikacyjne?.Nazwa}{(string.IsNullOrEmpty(p2k.DaneIdentyfikacyjne?.Nip) ? "" : ", NIP " + p2k.DaneIdentyfikacyjne.Nip)}");
                }
        }

        if (f.Wz is { Count: > 0 })
        {
            var pw = sec.AddParagraph();
            pw.Format.SpaceBefore = Unit.FromMillimeter(2);
            Linia(pw, "Numery dokumentów magazynowych WZ", string.Join(", ", f.Wz));
        }
    }

    private static void Pozycje(Section sec, Faktura fa)
    {
        var f = fa.Fa;
        var waluta = Xml(f.KodWaluty);

        if (f.FaWiersz is { Count: > 0 })
        {
            Separator(sec);
            TytulSekcjiGlowny(sec, "Pozycje");
            sec.AddParagraph($"Faktura wystawiona w walucie {waluta}").Format.SpaceAfter = Unit.FromMillimeter(1.5);
            TabelaWierszy(sec, f.FaWiersz.Select(w => new Wiersz(
                w.NrWierszaFa, w.P7, w.P8A,
                w.P8BSpecified ? w.P8B : null,
                w.P9ASpecified ? w.P9A : null, w.P9BSpecified ? w.P9B : null,
                w.P10Specified ? w.P10 : null,
                w.P12Specified ? StawkaTekst(Xml(w.P12)) : null,
                w.P11Specified ? w.P11 : null, w.P11ASpecified ? w.P11A : null,
                w.P11VatSpecified ? w.P11Vat : null,
                w.KursWalutySpecified ? w.KursWaluty : null,
                w.UuId, w.StanPrzedSpecified,
                w.Indeks, w.Gtin, w.PkWiU, w.Cn, w.Pkob,
                w.GtuSpecified ? Xml(w.Gtu) : null,
                w.ProceduraSpecified ? Xml(w.Procedura) : null)).ToList());
        }

        // Faktura zaliczkowa: zamowienie, ktorego dotyczy zaliczka.
        if (f.Zamowienie is { } z)
        {
            Separator(sec);
            TytulSekcjiGlowny(sec, "Zamówienie");
            var p = sec.AddParagraph($"Faktura wystawiona w walucie {waluta}");
            p.AddLineBreak();
            p.AddText($"Wartość zamówienia lub umowy z uwzględnieniem kwoty podatku: {Kw(z.WartoscZamowienia)}");
            p.Format.SpaceAfter = Unit.FromMillimeter(1.5);
            TabelaWierszy(sec, z.ZamowienieWiersz.Select(w => new Wiersz(
                w.NrWierszaZam, w.P7Z, w.P8Az,
                w.P8BzSpecified ? w.P8Bz : null,
                w.P9AzSpecified ? w.P9Az : null, null, null,
                w.P12ZSpecified ? StawkaTekst(Xml(w.P12Z)) : null,
                w.P11NettoZSpecified ? w.P11NettoZ : null, null,
                w.P11VatZSpecified ? w.P11VatZ : null, null,
                w.UuIdz, w.StanPrzedZSpecified,
                w.IndeksZ, w.Gtinz, w.PkWiUz, w.Cnz, w.Pkobz,
                w.GtuzSpecified ? Xml(w.Gtuz) : null,
                w.ProceduraZSpecified ? Xml(w.ProceduraZ) : null)).ToList(), vatJakoKwotaPodatku: true);
        }
    }

    private sealed record Wiersz(
        ulong Lp, string? Nazwa, string? Miara, decimal? Ilosc,
        decimal? CenaNetto, decimal? CenaBrutto, decimal? Rabat, string? Stawka,
        decimal? WartoscNetto, decimal? WartoscBrutto, decimal? Vat, decimal? Kurs,
        string? UuId, bool StanPrzed,
        string? Indeks, string? Gtin, string? PkWiU, string? Cn, string? Pkob, string? Gtu, string? Procedura);

    /// <summary>
    /// Tabela pozycji jak w wizualizacji MF: kolumny opcjonalne pokazywane tylko,
    /// gdy wystepuja w ktorejkolwiek pozycji; kody (indeks, GTIN, GTU...) w osobnej tabeli.
    /// </summary>
    private static void TabelaWierszy(Section sec, List<Wiersz> w, bool vatJakoKwotaPodatku = false)
    {
        var kol = new List<(string Naglowek, double Szer, Func<Wiersz, string> Wart, bool Liczba)>
        {
            ("Lp.", 0.8, x => x.Lp.ToString(Pl) + (x.StanPrzed ? "*" : ""), false),
            ("Nazwa towaru lub usługi", 0, x => x.Nazwa ?? string.Empty, false),
        };
        if (w.Any(x => x.CenaNetto is not null)) kol.Add(("Cena jedn. netto", 1.6, x => Kw(x.CenaNetto), true));
        if (w.Any(x => x.CenaBrutto is not null)) kol.Add(("Cena jedn. brutto", 1.6, x => Kw(x.CenaBrutto), true));
        if (w.Any(x => x.Ilosc is not null)) kol.Add(("Ilość", 1.3, x => x.Ilosc?.ToString("#,##0.00####", Pl) ?? string.Empty, true));
        if (w.Any(x => !string.IsNullOrEmpty(x.Miara))) kol.Add(("Miara", 1.1, x => x.Miara ?? string.Empty, false));
        if (w.Any(x => x.Rabat is not null)) kol.Add(("Rabat", 1.4, x => Kw(x.Rabat), true));
        if (w.Any(x => x.Stawka is not null)) kol.Add(("Stawka podatku", 1.55, x => x.Stawka ?? string.Empty, false));
        if (w.Any(x => x.WartoscNetto is not null)) kol.Add(("Wartość sprzedaży netto", 1.9, x => Kw(x.WartoscNetto), true));
        if (w.Any(x => x.WartoscBrutto is not null)) kol.Add(("Wartość sprzedaży brutto", 1.9, x => Kw(x.WartoscBrutto), true));
        if (w.Any(x => x.Vat is not null)) kol.Add((vatJakoKwotaPodatku ? "Kwota podatku" : "Wartość sprzedaży vat", 1.7, x => Kw(x.Vat), true));
        if (w.Any(x => x.Kurs is not null)) kol.Add(("Kurs waluty", 1.4, x => x.Kurs?.ToString("0.######", Pl) ?? string.Empty, true));
        if (w.Any(x => !string.IsNullOrEmpty(x.UuId))) kol.Add(("UU_ID", 1.5, x => x.UuId ?? string.Empty, false));

        var stale = kol.Sum(k => k.Szer);
        var nazwa = Math.Max(3.5, SzerokoscCm - stale);
        var t = NowaTabela(sec);
        foreach (var k in kol) t.AddColumn(Unit.FromCentimeter(k.Szer > 0 ? k.Szer : nazwa));
        WierszNaglowka(t, kol.Select(k => k.Naglowek).ToArray());
        foreach (var x in w)
        {
            var r = t.AddRow();
            for (var i = 0; i < kol.Count; i++)
            {
                var szerKol = kol[i].Szer > 0 ? kol[i].Szer : nazwa;
                var p = r.Cells[i].AddParagraph(Lamliwy(kol[i].Wart(x), szerKol));
                if (kol[i].Liczba) p.Format.Alignment = ParagraphAlignment.Right;
            }
            if (x.StanPrzed) r.Shading.Color = new Color(250, 250, 250);
        }
        if (w.Any(x => x.StanPrzed))
        {
            var p = sec.AddParagraph("* pozycja w stanie przed korektą");
            p.Format.Font.Size = 6.5;
            p.Format.Font.Color = Szary;
        }

        // Kody towarow - osobna tabela, tylko niepuste kolumny.
        var kody = new List<(string Naglowek, Func<Wiersz, string?> Wart)>
        {
            ("Indeks", x => x.Indeks), ("GTIN", x => x.Gtin), ("PKWiU", x => x.PkWiU), ("CN", x => x.Cn),
            ("PKOB", x => x.Pkob), ("GTU", x => x.Gtu), ("Procedura", x => x.Procedura),
        }.Where(k => w.Any(x => !string.IsNullOrWhiteSpace(k.Wart(x)))).ToList();
        if (kody.Count == 0) return;

        sec.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(2);
        var naglowki = new[] { "Lp." }.Concat(kody.Select(k => k.Naglowek)).ToArray();
        var szer = new[] { 0.8 }.Concat(kody.Select(_ => 3.4)).ToArray();
        Tabela(sec, naglowki, szer, w.Select(x => new[] { x.Lp.ToString(Pl) }.Concat(kody.Select(k => k.Wart(x) ?? string.Empty)).ToArray()));
    }

    private static void Kwota(Section sec, Faktura fa)
    {
        var f = fa.Fa;
        var tekst = Fa3Rodzaj.Kod(f.RodzajFaktury) switch
        {
            Fa3Rodzaj.Zal => "Kwota zapłaty (zaliczki) dokumentowana fakturą",
            Fa3Rodzaj.Roz => "Kwota pozostała do zapłaty",
            Fa3Rodzaj.Kor or Fa3Rodzaj.KorZal or Fa3Rodzaj.KorRoz => "Kwota korekty należności ogółem",
            _ => "Kwota należności ogółem",
        };
        var p = sec.AddParagraph();
        p.Format.Alignment = ParagraphAlignment.Right;
        p.Format.SpaceBefore = Unit.FromMillimeter(4);
        p.Format.SpaceAfter = Unit.FromMillimeter(2);
        p.Format.Font.Size = 11;
        p.AddText($"{tekst}: ");
        p.AddFormattedText($"{Kw(f.P15)} {Xml(f.KodWaluty)}", TextFormat.Bold);
    }

    private static void PodsumowanieStawek(Section sec, Faktura fa)
    {
        var f = fa.Fa;
        var obca = Xml(f.KodWaluty) != "PLN";
        var wiersze = new List<(string Stawka, decimal Netto, decimal? Vat, decimal? VatPln)>();

        void Dodaj(string stawka, bool ns, decimal n, bool vs, decimal v, bool ws, decimal w)
        {
            if (!ns && !vs) return;
            wiersze.Add((stawka, ns ? n : 0m, vs ? v : null, ws ? w : null));
        }

        Dodaj("23% lub 22%", f.P131Specified, f.P131, f.P141Specified, f.P141, f.P141WSpecified, f.P141W);
        Dodaj("8% lub 7%", f.P132Specified, f.P132, f.P142Specified, f.P142, f.P142WSpecified, f.P142W);
        Dodaj("5%", f.P133Specified, f.P133, f.P143Specified, f.P143, f.P143WSpecified, f.P143W);
        Dodaj("4% lub 3% (ryczałt taxi)", f.P134Specified, f.P134, f.P144Specified, f.P144, f.P144WSpecified, f.P144W);
        Dodaj("procedura szczególna OSS", f.P135Specified, f.P135, f.P145Specified, f.P145, false, 0);
        Dodaj("0% – dostawa w kraju", f.P1361Specified, f.P1361, false, 0, false, 0);
        Dodaj("0% – WDT", f.P1362Specified, f.P1362, false, 0, false, 0);
        Dodaj("0% – eksport", f.P1363Specified, f.P1363, false, 0, false, 0);
        Dodaj("zwolnione od podatku", f.P137Specified, f.P137, false, 0, false, 0);
        Dodaj("np z wyłączeniem art. 100 ust. 1 pkt 4", f.P138Specified, f.P138, false, 0, false, 0);
        Dodaj("np – art. 100 ust. 1 pkt 4", f.P139Specified, f.P139, false, 0, false, 0);
        Dodaj("odwrotne obciążenie", f.P1310Specified, f.P1310, false, 0, false, 0);
        Dodaj("marża", f.P1311Specified, f.P1311, false, 0, false, 0);
        if (wiersze.Count == 0) return;

        TytulSekcjiGlowny(sec, "Podsumowanie stawek podatku");
        var nag = new List<string> { "Lp.", "Stawka podatku", "Kwota netto", "Kwota podatku", "Kwota brutto" };
        var szer = new List<double> { 0.8, 4.6, 3.1, 3.1, 3.1 };
        if (obca) { nag.Add("Kwota podatku PLN"); szer.Add(3.3); }
        else szer[1] += 3.3;

        Tabela(sec, nag.ToArray(), szer.ToArray(), wiersze.Select((x, i) =>
        {
            var r = new List<string>
            {
                (i + 1).ToString(Pl), x.Stawka, Kw(x.Netto), Kw(x.Vat), Kw(x.Netto + (x.Vat ?? 0m)),
            };
            if (nag.Count == 6) r.Add(Kw(x.VatPln));
            return r.ToArray();
        }), wyrownajOd: 2);
    }

    private static void Adnotacje(Section sec, Faktura fa)
    {
        var a = fa.Fa.Adnotacje;
        if (a is null) return;
        var pozycje = new List<(string, string)>();

        if (Xml(a.P16) == "1") pozycje.Add(("Metoda kasowa", "TAK"));
        if (Xml(a.P17) == "1") pozycje.Add(("Samofakturowanie", "TAK"));
        if (Xml(a.P18) == "1") pozycje.Add(("Odwrotne obciążenie", "TAK"));
        if (Xml(a.P18A) == "1") pozycje.Add(("Mechanizm podzielonej płatności", "TAK"));
        if (a.Zwolnienie is { P19Specified: true } z)
            pozycje.Add(("Dostawa towarów lub świadczenie usług zwolnionych od podatku",
                z.P19A ?? z.P19B ?? z.P19C ?? "TAK"));
        if (a.NoweSrodkiTransportu is { P22Specified: true })
            pozycje.Add(("Wewnątrzwspólnotowa dostawa nowych środków transportu", "TAK"));
        if (Xml(a.P23) == "1") pozycje.Add(("Procedura uproszczona – faktura wystawiana przez drugiego w kolejności podatnika", "TAK"));
        if (a.PMarzy is { } m && !m.PPMarzyNSpecified)
        {
            var rodzaj = m.PPMarzy2Specified ? "biura podróży"
                : m.PPMarzy31Specified ? "towary używane"
                : m.PPMarzy32Specified ? "dzieła sztuki"
                : m.PPMarzy33Specified ? "przedmioty kolekcjonerskie i antyki"
                : null;
            if (m.PPMarzySpecified || rodzaj is not null)
                pozycje.Add(("Procedura marży", rodzaj ?? "TAK"));
        }
        if (pozycje.Count == 0) return;

        TytulSekcjiGlowny(sec, "Adnotacje");
        var p = sec.AddParagraph();
        foreach (var (k, v) in pozycje) Linia(p, k, v);
    }

    private static void Rozliczenie(Section sec, Faktura fa)
    {
        var r = fa.Fa.Rozliczenie;
        if (r is null) return;

        TytulSekcjiGlowny(sec, "Rozliczenie");
        if (r.Obciazenia is { Count: > 0 })
            Tabela(sec, new[] { "Obciążenia – powód", "Kwota" }, new[] { 10.0, 3.0 },
                r.Obciazenia.Select(o => new[] { o.Powod ?? string.Empty, Kw(o.Kwota) }), wyrownajOd: 1);
        if (r.Odliczenia is { Count: > 0 })
            Tabela(sec, new[] { "Odliczenia – powód", "Kwota" }, new[] { 10.0, 3.0 },
                r.Odliczenia.Select(o => new[] { o.Powod ?? string.Empty, Kw(o.Kwota) }), wyrownajOd: 1);
        var p = sec.AddParagraph();
        p.Format.SpaceBefore = Unit.FromMillimeter(1);
        if (r.SumaObciazenSpecified) Linia(p, "Suma obciążeń", Kw(r.SumaObciazen));
        if (r.SumaOdliczenSpecified) Linia(p, "Suma odliczeń", Kw(r.SumaOdliczen));
        if (r.DoZaplatySpecified) Linia(p, "Do zapłaty", Kw(r.DoZaplaty));
        if (r.DoRozliczeniaSpecified) Linia(p, "Do rozliczenia", Kw(r.DoRozliczenia));
    }

    private static void DodatkoweInformacje(Section sec, Faktura fa)
    {
        var opis = fa.Fa.DodatkowyOpis;
        var zwrot = fa.Fa.ZwrotAkcyzySpecified;
        if (opis is not { Count: > 0 } && !zwrot) return;

        Separator(sec);
        TytulSekcjiGlowny(sec, "Dodatkowe informacje");
        if (zwrot)
            Linia(sec.AddParagraph(), "Zwrot akcyzy (art. 21 ust. 3 ustawy o akcyzie)", "TAK");
        if (opis is { Count: > 0 })
        {
            TytulPodsekcji(sec, "Dodatkowy opis");
            var dlugi = opis.Any(o => o.NrWierszaSpecified);
            var nag = dlugi ? new[] { "Lp.", "Nr wiersza", "Rodzaj informacji", "Treść informacji" } : new[] { "Lp.", "Rodzaj informacji", "Treść informacji" };
            var szer = dlugi ? new[] { 0.8, 1.6, 6.0, 9.6 } : new[] { 0.8, 7.0, 10.2 };
            Tabela(sec, nag, szer, opis.Select((o, i) => dlugi
                ? new[] { (i + 1).ToString(Pl), o.NrWierszaSpecified ? o.NrWiersza.ToString(Pl) : "", o.Klucz ?? "", o.Wartosc ?? "" }
                : new[] { (i + 1).ToString(Pl), o.Klucz ?? "", o.Wartosc ?? "" }));
        }
    }

    private static void Platnosc(Section sec, Faktura fa)
    {
        var pl = fa.Fa.Platnosc;
        if (pl is null) return;

        Separator(sec);
        TytulSekcjiGlowny(sec, "Płatność");
        var p = sec.AddParagraph();
        if (pl.ZaplaconoSpecified) Linia(p, "Informacja o płatności", "Zapłacono");
        else if (pl.ZnacznikZaplatyCzesciowejSpecified)
            Linia(p, "Informacja o płatności", Xml(pl.ZnacznikZaplatyCzesciowej) == "1" ? "Zapłata częściowa" : "Zapłacono w całości (w częściach)");
        if (pl.DataZaplatySpecified) Linia(p, "Data zapłaty", Data(pl.DataZaplaty));
        if (pl.FormaPlatnosciSpecified) Linia(p, "Forma płatności", FormaTekst(Xml(pl.FormaPlatnosci)));
        else if (pl.PlatnoscInnaSpecified) Linia(p, "Forma płatności", "Płatność inna");
        Linia(p, "Opis płatności innej", pl.OpisPlatnosci);
        Linia(p, "Link do płatności", pl.LinkDoPlatnosci);
        Linia(p, "Identyfikator płatności KSeF", pl.IpkSeF);
        if (pl.Skonto is { } sk)
        {
            Linia(p, "Warunki skonta", sk.WarunkiSkonta);
            Linia(p, "Wysokość skonta", sk.WysokoscSkonta);
        }

        if (pl.TerminPlatnosci is { Count: > 0 })
        {
            sec.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(1);
            string OpisTerminu(FakturaFaPlatnoscTerminPlatnosci t)
                => t.TerminOpis is { } o ? $"{o.Ilosc} {o.Jednostka} {o.ZdarzeniePoczatkowe}".Trim() : string.Empty;
            if (pl.TerminPlatnosci.Any(t => OpisTerminu(t).Length > 0))
                Tabela(sec, new[] { "Termin płatności", "Opis terminu" }, new[] { 4.0, 8.0 },
                    pl.TerminPlatnosci.Select(t => new[] { t.TerminSpecified ? Data(t.Termin) : string.Empty, OpisTerminu(t) }));
            else
                Tabela(sec, new[] { "Termin płatności" }, new[] { 4.0 },
                    pl.TerminPlatnosci.Select(t => new[] { t.TerminSpecified ? Data(t.Termin) : string.Empty }));
        }

        if (pl.ZaplataCzesciowa is { Count: > 0 })
        {
            sec.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(1);
            Tabela(sec, new[] { "Data zapłaty częściowej", "Kwota zapłaty częściowej", "Forma płatności" }, new[] { 4.0, 4.0, 6.0 },
                pl.ZaplataCzesciowa.Select(z => new[]
                {
                    Data(z.DataZaplatyCzesciowej), Kw(z.KwotaZaplatyCzesciowej),
                    z.FormaPlatnosciSpecified ? FormaTekst(Xml(z.FormaPlatnosci)) : z.OpisPlatnosci ?? string.Empty,
                }));
        }

        Rachunki(sec, "Numer rachunku bankowego", pl.RachunekBankowy);
        Rachunki(sec, "Numer rachunku bankowego faktora", pl.RachunekBankowyFaktora);
    }

    private static void Rachunki(Section sec, string tytul, IList<TRachunekBankowy>? rachunki)
    {
        if (rachunki is not { Count: > 0 }) return;
        foreach (var rb in rachunki)
        {
            TytulPodsekcji(sec, tytul);
            var t = NowaTabela(sec);
            t.AddColumn(Unit.FromCentimeter(3.6));
            t.AddColumn(Unit.FromCentimeter(6.4));
            void W(string k, string? v)
            {
                var r = t.AddRow();
                r.Cells[0].Shading.Color = TloNaglowka;
                r.Cells[0].AddParagraph(k).Format.Font.Bold = true;
                r.Cells[1].AddParagraph(v ?? string.Empty);
            }
            W("Pełny numer rachunku", FormatRachunku(rb.NrRb));
            W("Kod SWIFT", rb.Swift);
            W("Rachunek własny banku", rb.RachunekWlasnyBankuSpecified ? RachunekWlasny(Xml(rb.RachunekWlasnyBanku)) : null);
            W("Nazwa banku", rb.NazwaBanku);
            W("Opis rachunku", rb.OpisRachunku);
        }
    }

    private static void WarunkiTransakcji(Section sec, Faktura fa)
    {
        var w = fa.Fa.WarunkiTransakcji;
        if (w is null) return;

        Separator(sec);
        TytulSekcjiGlowny(sec, "Warunki transakcji");

        var t = sec.AddTable();
        t.AddColumn(Unit.FromCentimeter(SzerokoscCm / 2));
        t.AddColumn(Unit.FromCentimeter(SzerokoscCm / 2));
        var r = t.AddRow();

        if (w.Umowy is { Count: > 0 })
        {
            r.Cells[0].AddParagraph("Umowa").Format.SpaceAfter = Unit.FromMillimeter(1);
            TabelaWKomorce(r.Cells[0], new[] { "Data umowy", "Numer umowy" }, new[] { 4.2, 4.3 },
                w.Umowy.Select(u => new[] { u.DataUmowySpecified ? Data(u.DataUmowy) : "", u.NrUmowy ?? "" }));
        }
        if (w.Zamowienia is { Count: > 0 })
        {
            r.Cells[1].AddParagraph("Zamówienie").Format.SpaceAfter = Unit.FromMillimeter(1);
            TabelaWKomorce(r.Cells[1], new[] { "Data zamówienia", "Numer zamówienia" }, new[] { 4.5, 4.5 },
                w.Zamowienia.Select(z => new[] { z.DataZamowieniaSpecified ? Data(z.DataZamowienia) : "", z.NrZamowienia ?? "" }));
        }

        var p = sec.AddParagraph();
        p.Format.SpaceBefore = Unit.FromMillimeter(2);
        if (w.NrPartiiTowaru is { Count: > 0 }) Linia(p, "Numery partii towaru", string.Join(", ", w.NrPartiiTowaru));
        Linia(p, "Warunki dostawy towarów", w.WarunkiDostawy);
        if (w.KursUmownySpecified)
            Linia(p, "Kurs umowny", w.KursUmowny.ToString("0.######", Pl) + (w.WalutaUmownaSpecified ? " " + Xml(w.WalutaUmowna) : ""));
        if (w.PodmiotPosredniczacySpecified) Linia(p, "Dostawa dokonana przez podmiot pośredniczący (art. 22 ust. 2d ustawy)", "TAK");
        if (w.Transport is { Count: > 0 }) Linia(p, "Transport", $"{w.Transport.Count} pozycja(e) – szczegóły w pliku XML");
    }

    private static void Rejestry(Section sec, Faktura fa)
    {
        var s = fa.Stopka;
        if (s is null) return;
        var rejestry = s.Rejestry?.Where(r => r is not null).ToList() ?? new();
        var info = s.Informacje?.Where(i => !string.IsNullOrWhiteSpace(i.StopkaFaktury)).ToList() ?? new();
        if (rejestry.Count == 0 && info.Count == 0) return;

        Separator(sec);
        if (rejestry.Count > 0)
        {
            TytulSekcjiGlowny(sec, "Rejestry");
            Tabela(sec, new[] { "Pełna nazwa", "KRS", "REGON", "BDO" }, new[] { 6.0, 4.0, 4.0, 4.0 },
                rejestry.Select(r => new[] { r.PelnaNazwa ?? "", r.Krs ?? "", r.Regon ?? "", r.Bdo ?? "" }));
        }
        if (info.Count > 0)
        {
            TytulSekcjiGlowny(sec, "Pozostałe informacje");
            Tabela(sec, new[] { "Stopka faktury" }, new[] { SzerokoscCm }, info.Select(i => new[] { i.StopkaFaktury }));
        }
    }

    private static void KodQr(Section sec, Faktura fa, string xml, string? numerKsef, string? qrBazaUrl)
    {
        if (string.IsNullOrWhiteSpace(numerKsef) || string.IsNullOrWhiteSpace(qrBazaUrl)) return;
        var nip = fa.Podmiot1.DaneIdentyfikacyjne?.Nip;
        if (string.IsNullOrWhiteSpace(nip)) return;

        var link = LinkWeryfikacji(qrBazaUrl, nip, fa.Fa.P1, xml);
        using var gen = new QRCodeGenerator();
        using var dane = gen.CreateQrCode(link, QRCodeGenerator.ECCLevel.M);
        // PNG RGBA - PDFsharp nie czyta 1-bitowego PNG z paleta (domyslny wynik QRCoder).
        var png = new PngByteQRCode(dane).GetGraphic(8, new byte[] { 0, 0, 0, 255 }, new byte[] { 255, 255, 255, 255 });

        Separator(sec);
        var tyt = TytulSekcjiGlowny(sec, "Sprawdź, czy Twoja faktura znajduje się w KSeF!");
        tyt.Format.KeepWithNext = true;

        var t = sec.AddTable();
        t.KeepTogether = true;
        t.AddColumn(Unit.FromCentimeter(5.5));
        t.AddColumn(Unit.FromCentimeter(SzerokoscCm - 5.5));
        var r = t.AddRow();
        var img = r.Cells[0].AddImage("base64:" + Convert.ToBase64String(png));
        img.Width = Unit.FromCentimeter(4.5);
        img.LockAspectRatio = true;
        r.Cells[0].AddParagraph(numerKsef).Format.Font.Size = 6.5;

        var opis = r.Cells[1].AddParagraph("Nie możesz zeskanować kodu z obrazka? Kliknij w link weryfikacyjny i przejdź do weryfikacji faktury!");
        opis.Format.SpaceAfter = Unit.FromMillimeter(2);
        var pl = r.Cells[1].AddParagraph();
        var h = pl.AddHyperlink(link, HyperlinkType.Web);
        var ft = h.AddFormattedText(link);
        ft.Color = Link;
    }

    private static void Wytworzona(Section sec, Faktura fa, bool maUpo)
    {
        var p = sec.AddParagraph();
        p.Format.SpaceBefore = Unit.FromMillimeter(6);
        p.Format.Font.Size = 7;
        var system = fa.Naglowek?.SystemInfo;
        p.AddFormattedText("Wytworzona w: ", TextFormat.Bold);
        p.AddText(string.IsNullOrWhiteSpace(system) || system.Contains("FreeKSeF", StringComparison.OrdinalIgnoreCase)
            ? "FreeKSeF"
            : $"{system} (wizualizacja: FreeKSeF)");
        if (maUpo)
        {
            p.AddLineBreak();
            p.AddText("Faktura przyjęta w KSeF – dostępne Urzędowe Poświadczenie Odbioru (UPO).");
        }
    }

    // ------------------------------------------------------------ elementy

    private static void Separator(Section sec)
    {
        var p = sec.AddParagraph();
        p.Format.SpaceBefore = Unit.FromMillimeter(3);
        p.Format.SpaceAfter = Unit.FromMillimeter(1);
        p.Format.Font.Size = 1;
        p.Format.Borders.Bottom.Width = 0.75;
        p.Format.Borders.Bottom.Color = KolorLinii;
    }

    private static Paragraph TytulSekcjiGlowny(Section sec, string tekst)
    {
        var p = sec.AddParagraph(tekst);
        p.Format.Font.Size = 11;
        p.Format.SpaceBefore = Unit.FromMillimeter(2);
        p.Format.SpaceAfter = Unit.FromMillimeter(2);
        p.Format.KeepWithNext = true;
        return p;
    }

    private static void TytulSekcji(Paragraph p)
    {
        p.Format.Font.Size = 10;
        p.Format.SpaceAfter = Unit.FromMillimeter(2);
    }

    private static void TytulPodsekcji(Section sec, string tekst)
    {
        var p = sec.AddParagraph(tekst);
        p.Format.Font.Size = 8.5;
        p.Format.SpaceBefore = Unit.FromMillimeter(2.5);
        p.Format.SpaceAfter = Unit.FromMillimeter(1.2);
        p.Format.KeepWithNext = true;
    }

    /// <summary>Dopisuje linie "Etykieta: wartosc" (pogrubiona etykieta); pusta wartosc jest pomijana.</summary>
    private static void Linia(Paragraph p, string etykieta, string? wartosc)
    {
        if (string.IsNullOrWhiteSpace(wartosc)) return;
        if (p.Elements.Count > 0) p.AddLineBreak();
        p.AddFormattedText(etykieta + ": ", TextFormat.Bold);
        p.AddText(wartosc);
    }

    private static void Pole(Paragraph p, string etykieta, string wartosc, double rozmiar)
    {
        p.Format.Font.Size = rozmiar;
        p.AddFormattedText(etykieta + ": ", TextFormat.Bold);
        p.AddText(wartosc);
    }

    private static void IdentyfikatorPodmiotu(Paragraph p, string? nip, string? idWew, string? kodUe, string? nrVatUe,
        string? kodKraju, string? nrId, bool brakId)
    {
        Linia(p, "NIP", nip);
        Linia(p, "Identyfikator wewnętrzny", idWew);
        if (!string.IsNullOrEmpty(nrVatUe)) Linia(p, "Numer VAT-UE", $"{kodUe}{nrVatUe}");
        if (!string.IsNullOrEmpty(nrId)) Linia(p, "Identyfikator podatkowy", string.IsNullOrEmpty(kodKraju) ? nrId : $"{kodKraju} {nrId}");
        if (brakId) Linia(p, "Identyfikator podatkowy", "brak");
    }

    private static void BlokAdresu(Section sec, string tytul, TAdres? a) => BlokAdresu(sec.AddParagraph(), tytul, a);

    private static void BlokAdresu(Cell cell, string tytul, TAdres? a) => BlokAdresu(cell.AddParagraph(), tytul, a);

    private static void BlokAdresu(Paragraph p, string tytul, TAdres? a)
    {
        if (a is null || (string.IsNullOrWhiteSpace(a.AdresL1) && string.IsNullOrWhiteSpace(a.AdresL2))) return;
        p.Format.SpaceBefore = Unit.FromMillimeter(2.5);
        p.AddFormattedText(tytul, TextFormat.Bold);
        if (!string.IsNullOrWhiteSpace(a.AdresL1)) { p.AddLineBreak(); p.AddText(a.AdresL1); }
        if (!string.IsNullOrWhiteSpace(a.AdresL2)) { p.AddLineBreak(); p.AddText(a.AdresL2); }
        p.AddLineBreak();
        p.AddText(NazwaKraju(Xml(a.KodKraju)));
        if (!string.IsNullOrWhiteSpace(a.Gln)) { p.AddLineBreak(); p.AddText("GLN: " + a.Gln); }
    }

    private static void BlokKontaktu(Cell cell, IEnumerable<(string? Email, string? Telefon)>? kontakty, string? nrKlienta = null)
    {
        var lista = kontakty?.Where(k => !string.IsNullOrWhiteSpace(k.Email) || !string.IsNullOrWhiteSpace(k.Telefon)).ToList() ?? new();
        if (lista.Count == 0 && string.IsNullOrWhiteSpace(nrKlienta)) return;
        var p = cell.AddParagraph();
        p.Format.SpaceBefore = Unit.FromMillimeter(2.5);
        p.AddFormattedText("Dane kontaktowe", TextFormat.Bold);
        foreach (var (email, tel) in lista)
        {
            if (!string.IsNullOrWhiteSpace(email)) { p.AddLineBreak(); p.AddFormattedText("E-mail: ", TextFormat.Bold); p.AddText(email); }
            if (!string.IsNullOrWhiteSpace(tel)) { p.AddLineBreak(); p.AddFormattedText("Tel.: ", TextFormat.Bold); p.AddText(tel); }
        }
        if (!string.IsNullOrWhiteSpace(nrKlienta)) { p.AddLineBreak(); p.AddFormattedText("Numer klienta: ", TextFormat.Bold); p.AddText(nrKlienta); }
    }

    private static Table NowaTabela(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.5;
        t.Borders.Color = KolorLinii;
        t.LeftPadding = Unit.FromMillimeter(1.2);
        t.RightPadding = Unit.FromMillimeter(1.2);
        t.TopPadding = Unit.FromMillimeter(0.8);
        t.BottomPadding = Unit.FromMillimeter(0.8);
        return t;
    }

    private static void WierszNaglowka(Table t, string[] naglowki)
    {
        var hr = t.AddRow();
        hr.HeadingFormat = true;
        hr.Shading.Color = TloNaglowka;
        hr.Format.Font.Bold = true;
        for (var i = 0; i < naglowki.Length; i++)
            hr.Cells[i].AddParagraph(naglowki[i]);
    }

    /// <summary>Prosta tabela z naglowkiem; kolumny od <paramref name="wyrownajOd"/> wyrownane do prawej (kwoty).</summary>
    private static void Tabela(Section sec, string[] naglowki, double[] szerokosci, IEnumerable<string[]> wiersze, int wyrownajOd = int.MaxValue)
    {
        var t = NowaTabela(sec);
        foreach (var s in szerokosci) t.AddColumn(Unit.FromCentimeter(s));
        WierszNaglowka(t, naglowki);
        foreach (var w in wiersze)
        {
            var r = t.AddRow();
            for (var i = 0; i < naglowki.Length && i < w.Length; i++)
            {
                var p = r.Cells[i].AddParagraph(Lamliwy(w[i] ?? string.Empty, szerokosci[i]));
                if (i >= wyrownajOd) p.Format.Alignment = ParagraphAlignment.Right;
            }
        }
    }

    /// <summary>Tabela w komorce innej tabeli (np. zamowienia w prawej kolumnie).</summary>
    private static void TabelaWKomorce(Cell cell, string[] naglowki, double[] szerokosci, IEnumerable<string[]> wiersze)
    {
        var tf = cell.Elements.AddTable();
        tf.Borders.Width = 0.5;
        tf.Borders.Color = KolorLinii;
        tf.LeftPadding = Unit.FromMillimeter(1.2);
        tf.TopPadding = Unit.FromMillimeter(0.8);
        tf.BottomPadding = Unit.FromMillimeter(0.8);
        foreach (var s in szerokosci) tf.AddColumn(Unit.FromCentimeter(s));
        WierszNaglowka(tf, naglowki);
        foreach (var w in wiersze)
        {
            var r = tf.AddRow();
            for (var i = 0; i < w.Length; i++) r.Cells[i].AddParagraph(w[i]);
        }
    }

    // ------------------------------------------------------------ formaty

    /// <summary>
    /// MigraDoc lamie wiersze tylko na spacjach - dlugie ciagi (np. "9VDC;SPDT;15A;10A/30VDC")
    /// wychodzilyby poza komorke. Dodajemy spacje po ; , / oraz dzielimy slowa dluzsze niz miesci kolumna.
    /// </summary>
    private static string Lamliwy(string tekst, double szerokoscCm)
    {
        if (string.IsNullOrEmpty(tekst)) return tekst;
        var maks = Math.Max(4, (int)(szerokoscCm * 6.0) - 1); // ok. 6 znakow/cm przy foncie 7,5 pt
        var sb = new StringBuilder(tekst.Length + 16);
        var dl = 0;
        for (var i = 0; i < tekst.Length; i++)
        {
            var c = tekst[i];
            if (char.IsWhiteSpace(c)) { sb.Append(c); dl = 0; continue; }
            if (dl >= maks) { sb.Append(' '); dl = 0; }
            sb.Append(c);
            dl++;
            var nast = i + 1 < tekst.Length ? tekst[i + 1] : ' ';
            if (c is ';' or ',' or '/' && !char.IsWhiteSpace(nast) && !char.IsDigit(nast) && dl > 6)
            {
                sb.Append(' ');
                dl = 0;
            }
        }
        return sb.ToString();
    }

    private static string Kw(decimal? w) => w?.ToString("N2", Pl) ?? string.Empty;

    private static string Data(DateTime d) => d.ToString("dd.MM.yyyy", Pl);

    /// <summary>Data nadania numeru KSeF - drugi segment numeru (RRRRMMDD), np. 5260001246-20260605-...</summary>
    private static DateTime? DataZNumeruKsef(string numer)
    {
        var cz = numer.Split('-');
        return cz.Length > 1 && DateTime.TryParseExact(cz[1], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;
    }

    private static string FormatRachunku(string? nr)
    {
        if (string.IsNullOrWhiteSpace(nr)) return string.Empty;
        var n = nr.Replace(" ", string.Empty);
        // Polski NRB (26 cyfr) lub IBAN PL: grupowanie jak w wizualizacji MF.
        if (n.Length == 26 && n.All(char.IsDigit)) n = "PL" + n;
        if (n.Length == 28 && n.StartsWith("PL", StringComparison.Ordinal))
            return n[..4] + " " + string.Join(" ", Enumerable.Range(0, 6).Select(i => n.Substring(4 + i * 4, 4)));
        return nr;
    }

    private static string StawkaTekst(string v) => int.TryParse(v, out _) ? v + "%" : v switch
    {
        "0 KR" => "0% (kraj)",
        "0 WDT" => "0% (WDT)",
        "0 EX" => "0% (eksport)",
        "oo" => "odwrotne obciążenie",
        "np I" => "np",
        "np II" => "np (art. 100)",
        _ => v,
    };

    private static string FormaTekst(string kod) => kod switch
    {
        "1" => "Gotówka",
        "2" => "Karta",
        "3" => "Bon",
        "4" => "Czek",
        "5" => "Kredyt",
        "6" => "Przelew",
        "7" => "Mobilna",
        var x => x,
    };

    private static string TypKorekty(string kod) => kod switch
    {
        "1" => "Korekta skutkująca w dacie ujęcia faktury pierwotnej",
        "2" => "Korekta skutkująca w dacie wystawienia faktury korygującej",
        "3" => "Korekta skutkująca w dacie innej, w tym gdy dla różnych pozycji faktury korygującej daty te są różne",
        var x => x,
    };

    private static string RachunekWlasny(string kod) => kod switch
    {
        "1" => "Rachunek banku/SKOK do rozliczeń z tytułu nabywanych wierzytelności",
        "2" => "Rachunek banku/SKOK do pobrania należności i przekazania jej dostawcy",
        "3" => "Rachunek prowadzony w ramach gospodarki własnej banku/SKOK",
        var x => x,
    };

    private static string StatusPodatnika(string kod) => kod switch
    {
        "1" => "w stanie likwidacji",
        "2" => "w trakcie postępowania restrukturyzacyjnego",
        "3" => "w stanie upadłości",
        "4" => "przedsiębiorstwo w spadku",
        var x => x,
    };

    private static string RolaPodmiotu3(string kod) => kod switch
    {
        "1" => "Faktor",
        "2" => "Odbiorca",
        "3" => "Podmiot pierwotny",
        "4" => "Dodatkowy nabywca",
        "5" => "Wystawca faktury",
        "6" => "Dokonujący płatności",
        "7" => "JST – wystawca",
        "8" => "JST – odbiorca",
        "9" => "Członek grupy VAT – wystawca",
        "10" => "Członek grupy VAT – odbiorca",
        "11" => "Pracownik",
        var x => x,
    };

    private static string RolaUpowaznionego(string kod) => kod switch
    {
        "1" => "Organ egzekucyjny",
        "2" => "Komornik sądowy",
        "3" => "Przedstawiciel podatkowy",
        var x => x,
    };

    private static string NazwaKraju(string kod) => kod switch
    {
        "PL" => "Polska",
        "DE" => "Niemcy",
        "CZ" => "Czechy",
        "SK" => "Słowacja",
        "LT" => "Litwa",
        "UA" => "Ukraina",
        "GB" => "Wielka Brytania",
        "US" => "Stany Zjednoczone",
        "FR" => "Francja",
        "IT" => "Włochy",
        "ES" => "Hiszpania",
        "NL" => "Holandia",
        "AT" => "Austria",
        "BE" => "Belgia",
        "SE" => "Szwecja",
        "DK" => "Dania",
        "IE" => "Irlandia",
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
