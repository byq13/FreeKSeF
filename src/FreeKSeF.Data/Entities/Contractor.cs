namespace FreeKSeF.Data.Entities;

/// <summary>Kontrahent (nabywca) zapisany do ponownego uzycia.</summary>
public class Contractor
{
    public int Id { get; set; }

    /// <summary>Firma (wlasciciel) - kontrahenci sa izolowani per firma.</summary>
    public int CompanyId { get; set; }

    /// <summary>Identyfikator podatkowy (NIP dla PL, VAT UE lub inny ID dla zagranicy).</summary>
    public string? Nip { get; set; }
    public string Nazwa { get; set; } = string.Empty;

    /// <summary>Kod kraju kontrahenta (ISO 2-literowy), domyslnie PL.</summary>
    public string KodKraju { get; set; } = "PL";
    public string AdresL1 { get; set; } = string.Empty;
    public string? AdresL2 { get; set; }

    public DateTime UtworzonoUtc { get; set; } = DateTime.UtcNow;
}
