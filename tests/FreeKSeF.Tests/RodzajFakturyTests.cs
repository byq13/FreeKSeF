using FreeKSeF.Core.Fa3;
using FreeKSeF.Data;
using FreeKSeF.Data.Entities;
using FreeKSeF.Pdf;
using Microsoft.EntityFrameworkCore;

namespace FreeKSeF.Tests;

public class RodzajFakturyTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"freeksef_rodzaj_{Guid.NewGuid():N}.db");

    private static string Dane(string plik) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Dane", plik));

    [Theory]
    [InlineData("fa_zaliczkowa.xml", "ZAL")]
    [InlineData("fa_rozliczeniowa.xml", "ROZ")]
    public void Rodzaj_odczytany_z_xml(string plik, string oczekiwany)
        => Assert.Equal(oczekiwany, Fa3Rodzaj.ZXml(Dane(plik)));

    [Fact]
    public void Rodzaj_z_prefiksem_przestrzeni_nazw_i_uszkodzony_xml()
    {
        Assert.Equal("KOR", Fa3Rodzaj.ZXml("<t:Faktura xmlns:t=\"x\"><t:Fa><t:RodzajFaktury>KOR</t:RodzajFaktury></t:Fa></t:Faktura>"));
        Assert.Null(Fa3Rodzaj.ZXml("<Faktura><Fa>"));
        Assert.Null(Fa3Rodzaj.ZXml(""));
    }

    [Fact]
    public void Nazwy_rodzajow_jak_w_wizualizacji_MF()
    {
        Assert.Equal("Faktura zaliczkowa", Fa3Rodzaj.Nazwa("ZAL"));
        Assert.Equal("Faktura rozliczeniowa", Fa3Rodzaj.Nazwa("ROZ"));
        Assert.Equal("Faktura korygująca fakturę zaliczkową", Fa3Rodzaj.Nazwa("KOR_ZAL"));
        Assert.True(Fa3Rodzaj.JestKorekta("KOR_ROZ"));
        Assert.False(Fa3Rodzaj.JestKorekta("ROZ"));
    }

    [Fact]
    public void Zapis_faktury_ustawia_rodzaj_automatycznie_a_backfill_uzupelnia_stare()
    {
        var opcje = new DbContextOptionsBuilder<FreeKSeFDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        int id;
        using (var ctx = new FreeKSeFDbContext(opcje))
        {
            ctx.Database.Migrate();
            var inv = new Invoice { CompanyId = 1, Numer = "FZ 2026/09/0007", Xml = Dane("fa_zaliczkowa.xml") };
            ctx.Invoices.Add(inv);
            ctx.SaveChanges();
            Assert.Equal("ZAL", inv.Rodzaj);
            id = inv.Id;

            // Symulacja faktury zapisanej przed dodaniem kolumny.
            ctx.Database.ExecuteSqlRaw("UPDATE Invoices SET Rodzaj = NULL");
        }
        using (var ctx = new FreeKSeFDbContext(opcje))
        {
            ctx.UzupelnijBrakujaceRodzaje();
            Assert.Equal("ZAL", ctx.Invoices.Single(i => i.Id == id).Rodzaj);
        }
    }

    [Theory]
    [InlineData("fa_zaliczkowa.xml", "5260001246-20260921-A60BED800092-BC")]
    [InlineData("fa_rozliczeniowa.xml", "5260001246-20260922-9ED0ED800090-D9")]
    public void Pdf_dla_faktur_zaliczkowej_i_rozliczeniowej_z_kodem_QR(string plik, string numerKsef)
    {
        var xml = Dane(plik);
        Assert.True(Fa3Validator.Validate(xml).IsValid);

        var pdf = FakturaPdfGenerator.GenerujPdf(xml, numerKsef, maUpo: true, FakturaPdfGenerator.QrProdukcja);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.True(pdf.Length > 20_000, "PDF z kodem QR powinien zawierac obraz");
    }

    [Fact]
    public void Link_weryfikacji_KSeF_w_formacie_MF()
    {
        var link = FakturaPdfGenerator.LinkWeryfikacji(FakturaPdfGenerator.QrTest, "1111111111", new DateTime(2026, 2, 1), "<Faktura/>");
        Assert.Matches(@"^https://qr-test\.ksef\.mf\.gov\.pl/invoice/1111111111/01-02-2026/[A-Za-z0-9_-]{43}$", link);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }
}
