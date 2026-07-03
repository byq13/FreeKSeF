using FreeKSeF.Core.Fa3;
using FreeKSeF.Core.Models;

namespace FreeKSeF.Tests;

/// <summary>
/// Testy silnika edytora zaawansowanego (Fa3Pola): wypisywanie wszystkich pol FA(3)
/// przez refleksje i wpisywanie wartosci z powrotem (z para *Specified i enumami).
/// </summary>
public class Fa3PolaTests
{
    private static FakturaModel Przyklad() => new()
    {
        Numer = "FV 1/06/2026",
        DataWystawienia = new DateTime(2026, 6, 3),
        Sprzedawca = new Strona
        {
            Nip = "5260001246",
            Nazwa = "Testowa Firma Usluigowa",
            Adres = new Adres { AdresL1 = "ul. Testowa 1", AdresL2 = "00-001 Warszawa" },
        },
        Nabywca = new Strona
        {
            Nip = "1111111111",
            Nazwa = "Klient Sp. z o.o.",
            Adres = new Adres { AdresL1 = "ul. Kliencka 2", AdresL2 = "00-002 Warszawa" },
        },
        Pozycje = { new PozycjaFaktury { Nazwa = "Usluga informatyczna", Ilosc = 1, CenaNetto = 1800m, Stawka = StawkaVat.Vat23 } },
    };

    [Fact]
    public void Wypisuje_kluczowe_pola_z_wartosciami()
    {
        var pola = Fa3Pola.Wypisz(Fa3Mapper.ToFa3(Przyklad()));

        Assert.Equal("FV 1/06/2026", pola.Single(p => p.Sciezka == "Fa.P_2").Wartosc);
        Assert.Equal("2026-06-03", pola.Single(p => p.Sciezka == "Fa.P_1").Wartosc);
        Assert.Equal("5260001246", pola.Single(p => p.Sciezka == "Podmiot1.DaneIdentyfikacyjne.NIP").Wartosc);
        Assert.Equal("Usluga informatyczna", pola.Single(p => p.Sciezka == "Fa.FaWiersz[1].P_7").Wartosc);

        // Stawka VAT w wierszu to enum - ma liste opcji z wartosciami XML (w tym "23").
        var stawka = pola.Single(p => p.Sciezka == "Fa.FaWiersz[1].P_12");
        Assert.Equal("23", stawka.Wartosc);
        Assert.NotNull(stawka.Opcje);
        Assert.Contains("23", stawka.Opcje!);
        Assert.Contains("zw", stawka.Opcje!);
    }

    [Fact]
    public void Zmiana_pola_trafia_do_XML_i_waliduje_sie()
    {
        var fa = Fa3Mapper.ToFa3(Przyklad());
        var pola = Fa3Pola.Wypisz(fa);

        Fa3Pola.Zastosuj(pola.Single(p => p.Sciezka == "Fa.P_2"), "FV 99/12/2026");
        Fa3Pola.Zastosuj(pola.Single(p => p.Sciezka == "Fa.P_1M"), "Krakow");

        var xml = Fa3Serializer.ToXml(fa);
        Assert.Contains("<P_2>FV 99/12/2026</P_2>", xml);
        Assert.Contains("<P_1M>Krakow</P_1M>", xml);

        var wynik = Fa3Validator.Validate(xml);
        Assert.True(wynik.IsValid, string.Join("\n", wynik.Errors));
    }

    [Fact]
    public void Puste_pole_opcjonalne_czysci_Specified_a_zly_format_rzuca()
    {
        var model = Przyklad();
        model.DataSprzedazy = new DateTime(2026, 6, 3);
        var fa = Fa3Mapper.ToFa3(model);
        var pola = Fa3Pola.Wypisz(fa);

        // Wyczyszczenie opcjonalnej daty P_6 -> znika z XML.
        Fa3Pola.Zastosuj(pola.Single(p => p.Sciezka == "Fa.P_6"), "");
        var xml = Fa3Serializer.ToXml(fa);
        Assert.DoesNotContain("<P_6>", xml);
        Assert.True(Fa3Validator.Validate(xml).IsValid);

        // Bledna data -> czytelny wyjatek ze sciezka pola.
        var ex = Assert.Throws<ArgumentException>(
            () => Fa3Pola.Zastosuj(pola.Single(p => p.Sciezka == "Fa.P_1"), "nie-data"));
        Assert.Contains("Fa.P_1", ex.Message);
    }
}
