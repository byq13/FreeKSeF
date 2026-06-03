using FreeKSeF.Core.Fa3;
using FreeKSeF.Core.Models;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.Data;

/// <summary>
/// Konwersja miedzy domenowym <see cref="FakturaModel"/> a encja bazodanowa <see cref="Invoice"/>.
/// Przy zapisie generuje rowniez XML FA(3) (przechowywany w encji).
/// </summary>
public static class FakturaMapping
{
    /// <summary>Buduje encje faktury sprzedazy z modelu domenowego (z wygenerowanym XML FA(3)).</summary>
    public static Invoice ToEntity(FakturaModel m, KierunekFaktury kierunek = KierunekFaktury.Sprzedaz)
    {
        ArgumentNullException.ThrowIfNull(m);

        var xml = Fa3Serializer.ToXml(Fa3Mapper.ToFa3(m));

        var inv = new Invoice
        {
            Kierunek = kierunek,
            Status = StatusFaktury.Robocza,
            Numer = m.Numer,
            DataWystawienia = m.DataWystawienia,
            DataSprzedazy = m.DataSprzedazy,
            KontrahentNip = m.Nabywca.Nip,
            KontrahentNazwa = m.Nabywca.Nazwa,
            Waluta = m.Waluta,
            SumaNetto = m.SumaNetto,
            SumaVat = m.SumaVat,
            SumaBrutto = m.SumaBrutto,
            Xml = xml,
        };

        var lp = 1;
        foreach (var p in m.Pozycje)
        {
            inv.Pozycje.Add(new InvoiceItem
            {
                Lp = lp++,
                Nazwa = p.Nazwa,
                Jednostka = p.Jednostka,
                Ilosc = p.Ilosc,
                CenaNetto = p.CenaNetto,
                Stawka = p.Stawka,
                WartoscNetto = p.WartoscNetto,
                KwotaVat = p.KwotaVat,
            });
        }

        return inv;
    }

    /// <summary>
    /// Buduje encje faktury zakupu z surowego XML pobranego z KSeF.
    /// Dane podsumowujace odczytywane sa z modelu FA(3).
    /// </summary>
    public static Invoice ZakupZXml(string xml, string? numerKsef = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);
        var fa = Fa3Serializer.FromXml(xml);

        decimal netto = Sum(fa.Fa.P131Specified, fa.Fa.P131)
                        + Sum(fa.Fa.P132Specified, fa.Fa.P132)
                        + Sum(fa.Fa.P133Specified, fa.Fa.P133)
                        + Sum(fa.Fa.P1361Specified, fa.Fa.P1361)
                        + Sum(fa.Fa.P137Specified, fa.Fa.P137);
        decimal vat = Sum(fa.Fa.P141Specified, fa.Fa.P141)
                      + Sum(fa.Fa.P142Specified, fa.Fa.P142)
                      + Sum(fa.Fa.P143Specified, fa.Fa.P143);

        return new Invoice
        {
            Kierunek = KierunekFaktury.Zakup,
            Status = StatusFaktury.Zaimportowana,
            Numer = fa.Fa.P2,
            NumerKsef = numerKsef,
            DataWystawienia = fa.Fa.P1,
            DataSprzedazy = fa.Fa.P6Specified ? fa.Fa.P6 : null,
            KontrahentNip = fa.Podmiot1.DaneIdentyfikacyjne.Nip,
            KontrahentNazwa = fa.Podmiot1.DaneIdentyfikacyjne.Nazwa,
            Waluta = fa.Fa.KodWaluty.ToString(),
            SumaNetto = netto,
            SumaVat = vat,
            SumaBrutto = fa.Fa.P15,
            Xml = xml,
        };
    }

    private static decimal Sum(bool specified, decimal value) => specified ? value : 0m;
}
