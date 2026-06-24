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
}
