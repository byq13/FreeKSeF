using System.Reflection;
using System.Xml.Serialization;
using FreeKSeF.Core.Fa3.Etd;
using FreeKSeF.Core.Models;

namespace FreeKSeF.Core.Fa3;

/// <summary>
/// Mapuje domenowy <see cref="FakturaModel"/> na obiektowy model FA(3) (<see cref="Faktura"/>),
/// gotowy do serializacji do XML zgodnego ze schematem schemat_FA(3)_v1-0E.xsd.
/// </summary>
public static class Fa3Mapper
{
    public const string KodSystemowy = "FA (3)";
    public const string WersjaSchemy = "1-0E";

    public static Faktura ToFa3(FakturaModel m)
    {
        ArgumentNullException.ThrowIfNull(m);

        var f = new Faktura
        {
            Naglowek = BudujNaglowek(),
            Podmiot1 = BudujSprzedawce(m.Sprzedawca),
            Podmiot2 = BudujNabywce(m.Nabywca),
            Fa = BudujFa(m),
        };
        return f;
    }

    private static TNaglowek BudujNaglowek() => new()
    {
        KodFormularza = new TNaglowekKodFormularza
        {
            Value = TKodFormularza.Fa,
            KodSystemowy = KodSystemowy,
            WersjaSchemy = WersjaSchemy,
        },
        WariantFormularza = TNaglowekWariantFormularza.Item3,
        DataWytworzeniaFa = DateTime.Now,
        SystemInfo = "FreeKSeF",
    };

    private static FakturaPodmiot1 BudujSprzedawce(Strona s) => new()
    {
        DaneIdentyfikacyjne = new TPodmiot1
        {
            Nip = s.Nip,
            Nazwa = s.Nazwa,
        },
        Adres = BudujAdres(s.Adres),
    };

    private static FakturaPodmiot2 BudujNabywce(Strona s)
    {
        var kraj = string.IsNullOrWhiteSpace(s.Adres.KodKraju) ? "PL" : s.Adres.KodKraju.Trim();
        var id = s.Nip?.Trim();
        var dane = new TPodmiot2 { Nazwa = s.Nazwa };

        if (s.BrakNip || string.IsNullOrWhiteSpace(id))
        {
            // Osoba prywatna / brak identyfikatora.
            dane.BrakId = TWybor1.Item1;
            dane.BrakIdSpecified = true;
        }
        else if (kraj.Equals("PL", StringComparison.OrdinalIgnoreCase))
        {
            dane.Nip = Nip.Normalizuj(id);
        }
        else if (KrajeUE.CzyUE(kraj))
        {
            // Kontrahent z UE - VAT UE (kod kraju + numer).
            dane.KodUe = EnumZWartosci<TKodyKrajowUe>(kraj);
            dane.KodUeSpecified = true;
            dane.NrVatUe = id;
        }
        else
        {
            // Kontrahent spoza UE - dowolny identyfikator + kod kraju.
            dane.KodKraju = EnumZWartosci<TKodKraju>(kraj);
            dane.KodKrajuSpecified = true;
            dane.NrId = id;
        }

        return new FakturaPodmiot2
        {
            DaneIdentyfikacyjne = dane,
            Adres = BudujAdres(s.Adres),
            // JST/GV sa wymagane: "2" = nie (nabywca nie jest JST ani czlonkiem grupy VAT).
            Jst = FakturaPodmiot2Jst.Item2,
            Gv = FakturaPodmiot2Gv.Item2,
        };
    }

    private static TAdres BudujAdres(Adres a) => new()
    {
        KodKraju = EnumZWartosci<TKodKraju>(a.KodKraju),
        AdresL1 = a.AdresL1,
        AdresL2 = string.IsNullOrWhiteSpace(a.AdresL2) ? null : a.AdresL2,
    };

    private static FakturaFa BudujFa(FakturaModel m)
    {
        var fa = new FakturaFa
        {
            KodWaluty = EnumZWartosci<TKodWaluty>(m.Waluta),
            P1 = m.DataWystawienia,
            P2 = m.Numer,
            RodzajFaktury = TRodzajFaktury.Vat,
            Adnotacje = BudujAdnotacjeStandardowe(),
            P15 = m.SumaBrutto,
        };

        if (m.DataSprzedazy is { } ds)
        {
            fa.P6 = ds;
            fa.P6Specified = true;
        }

        // Dla waluty obcej VAT (P_14_x) podaje sie w PLN wg kursu; kwoty netto/brutto w walucie.
        var kurs = m.WalutaObca ? (m.Kurs > 0 ? m.Kurs : 1m) : 1m;
        UstawSumyVat(fa, m, kurs);
        DodajWiersze(fa, m, m.WalutaObca ? kurs : null);
        fa.Platnosc = BudujPlatnosc(m);

        return fa;
    }

    /// <summary>Adnotacje dla zwyklej faktury VAT - wszystkie znaczniki "nie dotyczy".</summary>
    private static FakturaFaAdnotacje BudujAdnotacjeStandardowe() => new()
    {
        P16 = TWybor12.Item2,
        P17 = TWybor12.Item2,
        P18 = TWybor12.Item2,
        P18A = TWybor12.Item2,
        Zwolnienie = new FakturaFaAdnotacjeZwolnienie { P19N = TWybor1.Item1, P19NSpecified = true },
        NoweSrodkiTransportu = new FakturaFaAdnotacjeNoweSrodkiTransportu { P22N = TWybor1.Item1, P22NSpecified = true },
        P23 = TWybor12.Item2,
        PMarzy = new FakturaFaAdnotacjePMarzy { PPMarzyN = TWybor1.Item1, PPMarzyNSpecified = true },
    };

    private static void UstawSumyVat(FakturaFa fa, FakturaModel m, decimal kurs)
    {
        // Netto w walucie faktury; VAT przeliczony na PLN (kurs=1 dla PLN).
        decimal Netto(StawkaVat s) => m.Pozycje.Where(p => p.Stawka == s).Sum(p => p.WartoscNetto);
        decimal VatPln(StawkaVat s)
        {
            var netto = Netto(s);
            if (netto == 0m) return 0m;
            var nettoPln = Math.Round(netto * kurs, 2, MidpointRounding.AwayFromZero);
            return Math.Round(nettoPln * s.Procent(), 2, MidpointRounding.AwayFromZero);
        }

        void Set(decimal value, Action<decimal> setVal, Action<bool> setSpec)
        {
            if (value != 0m) { setVal(value); setSpec(true); }
        }

        // 23% -> P_13_1 / P_14_1
        Set(Netto(StawkaVat.Vat23), v => fa.P131 = v, b => fa.P131Specified = b);
        Set(VatPln(StawkaVat.Vat23), v => fa.P141 = v, b => fa.P141Specified = b);
        // 8% -> P_13_2 / P_14_2
        Set(Netto(StawkaVat.Vat8), v => fa.P132 = v, b => fa.P132Specified = b);
        Set(VatPln(StawkaVat.Vat8), v => fa.P142 = v, b => fa.P142Specified = b);
        // 5% -> P_13_3 / P_14_3
        Set(Netto(StawkaVat.Vat5), v => fa.P133 = v, b => fa.P133Specified = b);
        Set(VatPln(StawkaVat.Vat5), v => fa.P143 = v, b => fa.P143Specified = b);
        // 0% krajowa -> P_13_6_1 (bez kwoty podatku)
        Set(Netto(StawkaVat.Vat0), v => fa.P1361 = v, b => fa.P1361Specified = b);
        // zwolnione -> P_13_7
        Set(Netto(StawkaVat.Zwolniona), v => fa.P137 = v, b => fa.P137Specified = b);
    }

    private static void DodajWiersze(FakturaFa fa, FakturaModel m, decimal? kurs)
    {
        ulong nr = 1;
        foreach (var p in m.Pozycje)
        {
            var w = new FakturaFaFaWiersz
            {
                NrWierszaFa = nr++,
                P7 = p.Nazwa,
                P8A = p.Jednostka,
                P8B = p.Ilosc,
                P8BSpecified = true,
                P9A = p.CenaNetto,
                P9ASpecified = true,
                P11 = p.WartoscNetto,
                P11Specified = true,
                P12 = MapStawka(p.Stawka),
                P12Specified = true,
            };
            if (kurs is { } k)
            {
                w.KursWaluty = k;
                w.KursWalutySpecified = true;
            }
            fa.FaWiersz.Add(w);
        }
    }

    private static FakturaFaPlatnosc BudujPlatnosc(FakturaModel m)
    {
        var pl = new FakturaFaPlatnosc
        {
            FormaPlatnosci = MapFormaPlatnosci(m.FormaPlatnosci),
            FormaPlatnosciSpecified = true,
        };

        if (m.Zaplacono)
        {
            pl.Zaplacono = TWybor1.Item1;
            pl.ZaplaconoSpecified = true;
        }

        if (m.TerminPlatnosci is { } termin)
        {
            pl.TerminPlatnosci.Add(new FakturaFaPlatnoscTerminPlatnosci
            {
                Termin = termin,
                TerminSpecified = true,
            });
        }

        if (!string.IsNullOrWhiteSpace(m.NrRachunku))
        {
            pl.RachunekBankowy.Add(new TRachunekBankowy { NrRb = m.NrRachunku!.Replace(" ", string.Empty) });
        }

        return pl;
    }

    private static TStawkaPodatku MapStawka(StawkaVat s) => s switch
    {
        StawkaVat.Vat23 => TStawkaPodatku.Item23,
        StawkaVat.Vat8 => TStawkaPodatku.Item8,
        StawkaVat.Vat5 => TStawkaPodatku.Item5,
        StawkaVat.Vat0 => TStawkaPodatku.Item0Kr,
        StawkaVat.Zwolniona => TStawkaPodatku.Zw,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Nieobslugiwana stawka VAT"),
    };

    private static TFormaPlatnosci MapFormaPlatnosci(FormaPlatnosci f) => f switch
    {
        FormaPlatnosci.Gotowka => TFormaPlatnosci.Item1,
        FormaPlatnosci.Karta => TFormaPlatnosci.Item2,
        FormaPlatnosci.Bon => TFormaPlatnosci.Item3,
        FormaPlatnosci.Czek => TFormaPlatnosci.Item4,
        FormaPlatnosci.Kredyt => TFormaPlatnosci.Item5,
        FormaPlatnosci.Przelew => TFormaPlatnosci.Item6,
        FormaPlatnosci.Mobilna => TFormaPlatnosci.Item7,
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, "Nieobslugiwana forma platnosci"),
    };

    /// <summary>
    /// Zwraca wartosc enuma odpowiadajaca wartosci XML (z atrybutu XmlEnum),
    /// np. "PL" -> TKodKraju.Pl, "PLN" -> TKodWaluty.Pln.
    /// </summary>
    private static T EnumZWartosci<T>(string xmlValue) where T : struct, Enum
    {
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attr = field.GetCustomAttribute<XmlEnumAttribute>();
            var name = attr?.Name ?? field.Name;
            if (string.Equals(name, xmlValue, StringComparison.OrdinalIgnoreCase))
                return (T)field.GetValue(null)!;
        }
        throw new ArgumentException($"Brak wartosci '{xmlValue}' w enumie {typeof(T).Name}", nameof(xmlValue));
    }
}
