namespace FreeKSeF.Data.Entities;

/// <summary>Wpis dziennika operacji KSeF (autoryzacja, wysylka, import) - do diagnostyki.</summary>
public class KsefLog
{
    public int Id { get; set; }

    public DateTime CzasUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Nazwa operacji, np. "Auth", "Wysylka", "Import", "UPO".</summary>
    public string Operacja { get; set; } = string.Empty;

    /// <summary>Poziom: Info / Warn / Error.</summary>
    public string Poziom { get; set; } = "Info";

    public string Komunikat { get; set; } = string.Empty;
}
