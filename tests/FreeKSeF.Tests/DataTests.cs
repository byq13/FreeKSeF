using FreeKSeF.Core.Models;
using FreeKSeF.Data;
using FreeKSeF.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreeKSeF.Tests;

public class DataTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"freeksef_test_{Guid.NewGuid():N}.db");

    private FreeKSeFDbContext NowyKontekst()
    {
        var options = new DbContextOptionsBuilder<FreeKSeFDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var ctx = new FreeKSeFDbContext(options);
        ctx.Database.Migrate();
        return ctx;
    }

    private static FakturaModel Przyklad() => new()
    {
        Numer = "FV 7/06/2026",
        DataWystawienia = new DateTime(2026, 6, 3),
        Sprzedawca = new Strona { Nip = "5260001246", Nazwa = "Moja Firma", Adres = new Adres { AdresL1 = "ul. A 1", AdresL2 = "00-001 Warszawa" } },
        Nabywca = new Strona { Nip = "1132994096", Nazwa = "Klient SA", Adres = new Adres { AdresL1 = "ul. B 2", AdresL2 = "30-001 Kraków" } },
        Pozycje = { new PozycjaFaktury { Nazwa = "Usługa", Jednostka = "usł.", Ilosc = 1, CenaNetto = 1000m, Stawka = StawkaVat.Vat23 } },
    };

    [Fact]
    public void Migracja_tworzy_baze_i_zapis_faktury_dziala()
    {
        using (var ctx = NowyKontekst())
        {
            ctx.Companies.Add(new Company { Nip = "5260001246", Nazwa = "Moja Firma", AdresL1 = "ul. A 1", Srodowisko = Srodowisko.Test });
            ctx.Contractors.Add(new Contractor { Nip = "1132994096", Nazwa = "Klient SA", AdresL1 = "ul. B 2" });
            ctx.Invoices.Add(FakturaMapping.ToEntity(Przyklad()));
            ctx.SaveChanges();
        }

        using (var ctx = NowyKontekst())
        {
            var inv = ctx.Invoices.Include(i => i.Pozycje).Single();
            Assert.Equal("FV 7/06/2026", inv.Numer);
            Assert.Equal(KierunekFaktury.Sprzedaz, inv.Kierunek);
            Assert.Equal(1230m, inv.SumaBrutto);
            Assert.Single(inv.Pozycje);
            Assert.Equal(StawkaVat.Vat23, inv.Pozycje[0].Stawka);
            Assert.Contains("<Faktura", inv.Xml);
            Assert.Single(ctx.Companies);
            Assert.Single(ctx.Contractors);
        }
    }

    [Fact]
    public void Import_zakupu_z_XML_odczytuje_podsumowania()
    {
        // Generujemy XML jak z KSeF, potem importujemy jako zakup.
        var xml = FreeKSeF.Core.Fa3.Fa3Serializer.ToXml(FreeKSeF.Core.Fa3.Fa3Mapper.ToFa3(Przyklad()));

        using var ctx = NowyKontekst();
        var zakup = FakturaMapping.ZakupZXml(xml, numerKsef: "5260001246-20260603-AB12CD-34");
        ctx.Invoices.Add(zakup);
        ctx.SaveChanges();

        var z = ctx.Invoices.Single();
        Assert.Equal(KierunekFaktury.Zakup, z.Kierunek);
        Assert.Equal(StatusFaktury.Zaimportowana, z.Status);
        Assert.Equal("5260001246-20260603-AB12CD-34", z.NumerKsef);
        Assert.Equal(1000m, z.SumaNetto);
        Assert.Equal(230m, z.SumaVat);
        Assert.Equal(1230m, z.SumaBrutto);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
