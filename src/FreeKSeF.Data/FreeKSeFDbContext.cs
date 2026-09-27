using FreeKSeF.Core.Fa3;
using FreeKSeF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreeKSeF.Data;

/// <summary>
/// Kontekst EF Core na bazie SQLite. Przechowuje profil firmy, kontrahentow,
/// faktury (wystawione i zaimportowane) wraz z pozycjami oraz dziennik KSeF.
/// </summary>
public class FreeKSeFDbContext : DbContext
{
    public FreeKSeFDbContext(DbContextOptions<FreeKSeFDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<KsefLog> KsefLogs => Set<KsefLog>();
    public DbSet<Ustawienie> Ustawienia => Set<Ustawienie>();
    public DbSet<KursWaluty> Kursy => Set<KursWaluty>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Kwoty pieniezne: dokladnosc 18,2.
        foreach (var prop in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetPrecision(18);
            prop.SetScale(2);
        }

        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasIndex(i => i.NumerKsef);
            e.Property(i => i.Rodzaj).HasMaxLength(10);
            e.HasIndex(i => new { i.CompanyId, i.Kierunek, i.DataWystawienia });
            e.HasMany(i => i.Pozycje)
                .WithOne(p => p.Invoice!)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Contractor>().HasIndex(c => new { c.CompanyId, c.Nip });

        modelBuilder.Entity<Ustawienie>().HasKey(u => u.Klucz);

        modelBuilder.Entity<KursWaluty>(e =>
        {
            e.HasIndex(k => new { k.Kod, k.Data }).IsUnique();
            e.Property(k => k.Kurs).HasPrecision(18, 6); // kursy NBP maja 4 miejsca; zapas
        });

        modelBuilder.Entity<Product>().HasIndex(p => new { p.CompanyId, p.Nazwa });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UzupelnijRodzaje();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UzupelnijRodzaje();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Rodzaj faktury zawsze wynika z XML - ustawiamy go centralnie, aby kazda sciezka
    /// zapisu (nowa FV, import z KSeF, wczytanie XML, edytor) miala go poprawnie.
    /// </summary>
    private void UzupelnijRodzaje()
    {
        foreach (var e in ChangeTracker.Entries<Invoice>())
        {
            if (e.State == EntityState.Added ||
                (e.State == EntityState.Modified && (e.Property(i => i.Xml).IsModified || e.Entity.Rodzaj is null)))
                e.Entity.Rodzaj = Fa3Rodzaj.ZXml(e.Entity.Xml);
        }
    }

    /// <summary>Jednorazowo uzupelnia rodzaj faktur zapisanych przed dodaniem tej kolumny.</summary>
    public void UzupelnijBrakujaceRodzaje()
    {
        var bez = Invoices.Where(i => i.Rodzaj == null).ToList();
        if (bez.Count == 0) return;
        foreach (var inv in bez)
            inv.Rodzaj = Fa3Rodzaj.ZXml(inv.Xml) ?? string.Empty; // "" = nieznany, nie sprawdzamy ponownie
        base.SaveChanges(true);
    }
}
