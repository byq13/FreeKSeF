using FreeKSeF.Core.Fa3;
using FreeKSeF.Core.Models;
using FreeKSeF.Pdf;
using Xunit;

namespace FreeKSeF.Tests;

public class PdfTests
{
    private static string PrzykladowyXml()
    {
        var model = new FakturaModel
        {
            Numer = "FV 3/06/2026",
            DataWystawienia = new DateTime(2026, 6, 5),
            DataSprzedazy = new DateTime(2026, 6, 5),
            Sprzedawca = new Strona { Nip = "5260001246", Nazwa = "Moja Firma", Adres = new Adres { AdresL1 = "ul. Kwiatowa 5", AdresL2 = "00-001 Warszawa" } },
            Nabywca = new Strona { Nip = "1132994096", Nazwa = "Klient SA", Adres = new Adres { AdresL1 = "ul. Polna 1", AdresL2 = "30-001 Kraków" } },
            TerminPlatnosci = new DateTime(2026, 6, 19),
            NrRachunku = "61109010140000071219812874",
            Pozycje =
            {
                new PozycjaFaktury { Nazwa = "Usługa programistyczna", Jednostka = "godz.", Ilosc = 10, CenaNetto = 150m, Stawka = StawkaVat.Vat23 },
                new PozycjaFaktury { Nazwa = "Książka", Jednostka = "szt.", Ilosc = 2, CenaNetto = 40m, Stawka = StawkaVat.Vat5 },
            },
        };
        return Fa3Serializer.ToXml(Fa3Mapper.ToFa3(model));
    }

    [Fact]
    public void GenerujePoprawnyPlikPdf()
    {
        var pdf = FakturaPdfGenerator.GenerujPdf(PrzykladowyXml(), numerKsef: "5260001246-20260605-AB12CD-34", maUpo: true);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 1000, "PDF wydaje sie zbyt maly");

        // Sygnatura pliku PDF.
        var naglowek = System.Text.Encoding.ASCII.GetString(pdf, 0, 5);
        Assert.Equal("%PDF-", naglowek);
    }

    [Fact]
    public void PdfDzialaBezNumeruKsef()
    {
        // Faktura w buforze (jeszcze niewyslana) tez musi sie wygenerowac.
        var pdf = FakturaPdfGenerator.GenerujPdf(PrzykladowyXml());
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }
}
