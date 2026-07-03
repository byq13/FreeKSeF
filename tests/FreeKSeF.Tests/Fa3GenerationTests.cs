using FreeKSeF.Core.Fa3;
using FreeKSeF.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace FreeKSeF.Tests;

public class Fa3GenerationTests
{
    private readonly ITestOutputHelper _output;

    public Fa3GenerationTests(ITestOutputHelper output) => _output = output;

    private static FakturaModel PrzykladowaFaktura() => new()
    {
        Numer = "FV 1/06/2026",
        DataWystawienia = new DateTime(2026, 6, 3),
        DataSprzedazy = new DateTime(2026, 5, 31),
        Waluta = "PLN",
        Sprzedawca = new Strona
        {
            Nip = "5260001246",
            Nazwa = "Jan Kowalski Usługi IT",
            Adres = new Adres { KodKraju = "PL", AdresL1 = "ul. Kwiatowa 5/2", AdresL2 = "00-001 Warszawa" },
        },
        Nabywca = new Strona
        {
            Nip = "1132994096",
            Nazwa = "ACME Sp. z o.o.",
            Adres = new Adres { KodKraju = "PL", AdresL1 = "ul. Polna 1", AdresL2 = "30-001 Kraków" },
        },
        FormaPlatnosci = FormaPlatnosci.Przelew,
        TerminPlatnosci = new DateTime(2026, 6, 17),
        NrRachunku = "61 1090 1014 0000 0712 1981 2874",
        Pozycje =
        {
            new PozycjaFaktury { Nazwa = "Usługa programistyczna", Jednostka = "godz.", Ilosc = 10, CenaNetto = 150m, Stawka = StawkaVat.Vat23 },
            new PozycjaFaktury { Nazwa = "Konsultacja", Jednostka = "usł.", Ilosc = 1, CenaNetto = 300m, Stawka = StawkaVat.Vat23 },
        },
    };

    [Fact]
    public void Wygenerowany_XML_jest_zgodny_ze_schematem_FA3()
    {
        var model = PrzykladowaFaktura();
        var fa = Fa3Mapper.ToFa3(model);
        var xml = Fa3Serializer.ToXml(fa);

        _output.WriteLine(xml);

        var wynik = Fa3Validator.Validate(xml);
        Assert.True(wynik.IsValid, "Walidacja XSD nie powiodla sie:\n" + string.Join("\n", wynik.Errors));

        // JST/GV sa wymagane i dla zwyklej firmy musza miec wartosc "2" (nie),
        // a nie domyslna "1" (ktora oznaczalaby JST / czlonka grupy VAT).
        Assert.Contains("<JST>2</JST>", xml);
        Assert.Contains("<GV>2</GV>", xml);
    }

    [Fact]
    public void Miejsce_wystawienia_trafia_do_P_1M_i_waliduje_sie()
    {
        var model = PrzykladowaFaktura();
        model.MiejsceWystawienia = "Warszawa";

        var xml = Fa3Serializer.ToXml(Fa3Mapper.ToFa3(model));
        var wynik = Fa3Validator.Validate(xml);

        Assert.True(wynik.IsValid, "Walidacja XSD nie powiodla sie:\n" + string.Join("\n", wynik.Errors));
        Assert.Contains("<P_1M>Warszawa</P_1M>", xml);
    }

    [Fact]
    public void Sumy_VAT_sa_poprawnie_wyliczone()
    {
        var model = PrzykladowaFaktura();
        Assert.Equal(1800m, model.SumaNetto);
        Assert.Equal(414m, model.SumaVat);
        Assert.Equal(2214m, model.SumaBrutto);
    }

    [Fact]
    public void Roundtrip_serializacji_zachowuje_numer_i_sprzedawce()
    {
        var fa = Fa3Mapper.ToFa3(PrzykladowaFaktura());
        var xml = Fa3Serializer.ToXml(fa);
        var odczyt = Fa3Serializer.FromXml(xml);

        Assert.Equal("FV 1/06/2026", odczyt.Fa.P2);
        Assert.Equal("5260001246", odczyt.Podmiot1.DaneIdentyfikacyjne.Nip);
        Assert.Equal(2214m, odczyt.Fa.P15);
    }

    [Fact]
    public void Stawki_obnizone_i_zwolnione_walidują_się()
    {
        var model = PrzykladowaFaktura();
        model.Pozycje.Add(new PozycjaFaktury { Nazwa = "Książka", Jednostka = "szt.", Ilosc = 2, CenaNetto = 40m, Stawka = StawkaVat.Vat5 });
        model.Pozycje.Add(new PozycjaFaktury { Nazwa = "Usługa zwolniona", Jednostka = "usł.", Ilosc = 1, CenaNetto = 500m, Stawka = StawkaVat.Zwolniona });

        var xml = Fa3Serializer.ToXml(Fa3Mapper.ToFa3(model));
        var wynik = Fa3Validator.Validate(xml);
        Assert.True(wynik.IsValid, string.Join("\n", wynik.Errors));
    }

    [Fact]
    public void Nabywca_z_UE_waliduje_sie()
    {
        var model = PrzykladowaFaktura();
        model.Nabywca = new Strona
        {
            Nip = "811569869",
            Nazwa = "Muster GmbH",
            Adres = new Adres { KodKraju = "DE", AdresL1 = "Hauptstr. 1", AdresL2 = "10115 Berlin" },
        };

        var xml = Fa3Serializer.ToXml(Fa3Mapper.ToFa3(model));
        Assert.True(Fa3Validator.Validate(xml).IsValid);
        Assert.Contains("<KodUE>DE</KodUE>", xml);
        Assert.Contains("<NrVatUE>811569869</NrVatUE>", xml);
    }

    [Fact]
    public void Nabywca_spoza_UE_waliduje_sie()
    {
        var model = PrzykladowaFaktura();
        model.Nabywca = new Strona
        {
            Nip = "12-3456789",
            Nazwa = "Acme Inc.",
            Adres = new Adres { KodKraju = "US", AdresL1 = "1 Main St", AdresL2 = "New York, NY 10001" },
        };

        var xml = Fa3Serializer.ToXml(Fa3Mapper.ToFa3(model));
        Assert.True(Fa3Validator.Validate(xml).IsValid);
        Assert.Contains("<KodKraju>US</KodKraju>", xml);
        Assert.Contains("<NrID>12-3456789</NrID>", xml);
    }

    [Fact]
    public void Faktura_w_EUR_ma_VAT_w_PLN_i_waliduje_sie()
    {
        var model = PrzykladowaFaktura(); // 1800 netto, 23%
        model.Waluta = "EUR";
        model.Kurs = 4.30m;

        var fa = Fa3Mapper.ToFa3(model);
        var xml = Fa3Serializer.ToXml(fa);
        Assert.True(Fa3Validator.Validate(xml).IsValid);

        var odczyt = Fa3Serializer.FromXml(xml);
        Assert.Equal(1800m, odczyt.Fa.P131);                 // netto w EUR
        Assert.Equal(1780.20m, odczyt.Fa.P141);              // VAT w PLN: round(1800*4.30,2)*0.23
        Assert.Contains("<KodWaluty>EUR</KodWaluty>", xml);
    }
}
