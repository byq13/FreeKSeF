namespace FreeKSeF.Data.Entities;

/// <summary>Kontrahent (nabywca) zapisany do ponownego uzycia.</summary>
public class Contractor
{
    public int Id { get; set; }

    public string? Nip { get; set; }
    public string Nazwa { get; set; } = string.Empty;

    public string KodKraju { get; set; } = "PL";
    public string AdresL1 { get; set; } = string.Empty;
    public string? AdresL2 { get; set; }

    public DateTime UtworzonoUtc { get; set; } = DateTime.UtcNow;
}
