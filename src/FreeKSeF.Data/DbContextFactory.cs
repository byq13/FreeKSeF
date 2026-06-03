using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FreeKSeF.Data;

/// <summary>
/// Tworzy kontekst bazy na pliku SQLite. Domyslnie plik w katalogu danych aplikacji
/// uzytkownika (%AppData%\FreeKSeF\freeksef.db na Windows).
/// </summary>
public static class FreeKSeFDb
{
    public static string DomyslnaSciezka()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FreeKSeF");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "freeksef.db");
    }

    /// <summary>Tworzy kontekst dla wskazanej (lub domyslnej) sciezki pliku i stosuje migracje.</summary>
    public static FreeKSeFDbContext Utworz(string? sciezkaPliku = null)
    {
        var path = sciezkaPliku ?? DomyslnaSciezka();
        var options = new DbContextOptionsBuilder<FreeKSeFDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        var ctx = new FreeKSeFDbContext(options);
        ctx.Database.Migrate();
        return ctx;
    }
}

/// <summary>Fabryka uzywana przez narzedzia EF Core (dotnet ef) do generowania migracji.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FreeKSeFDbContext>
{
    public FreeKSeFDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FreeKSeFDbContext>()
            .UseSqlite("Data Source=freeksef_design.db")
            .Options;
        return new FreeKSeFDbContext(options);
    }
}
