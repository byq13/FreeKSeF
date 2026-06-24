namespace FreeKSeF.Data.Entities;

/// <summary>
/// Faktura zapisana lokalnie - zarowno wystawiona (sprzedaz), jak i zaimportowana (zakup).
/// Pelny XML FA(3) przechowujemy w <see cref="Xml"/>, co pozwala na podglad i ponowne wyslanie.
/// </summary>
public class Invoice
{
    public int Id { get; set; }

    /// <summary>Firma (wlasciciel) - faktury sa izolowane per firma.</summary>
    public int CompanyId { get; set; }

    public KierunekFaktury Kierunek { get; set; }
    public StatusFaktury Status { get; set; }

    public string Numer { get; set; } = string.Empty;

    /// <summary>Numer KSeF nadany po przyjeciu (np. 5260001246-20260603-...). Null gdy niewyslana.</summary>
    public string? NumerKsef { get; set; }

    /// <summary>Numer referencyjny elementu sesji/faktury w KSeF (do sprawdzania statusu).</summary>
    public string? NumerReferencyjny { get; set; }

    public DateTime DataWystawienia { get; set; }
    public DateTime? DataSprzedazy { get; set; }

    // Migawka danych kontrahenta (drugiej strony) - by historia byla niezalezna od edycji slownika.
    public string? KontrahentNip { get; set; }
    public string KontrahentNazwa { get; set; } = string.Empty;

    public string Waluta { get; set; } = "PLN";
    public decimal SumaNetto { get; set; }
    public decimal SumaVat { get; set; }
    public decimal SumaBrutto { get; set; }

    /// <summary>Pelny XML faktury w formacie FA(3).</summary>
    public string Xml { get; set; } = string.Empty;

    /// <summary>UPO (Urzedowe Poswiadczenie Odbioru) w formacie XML, po przyjeciu przez KSeF.</summary>
    public string? UpoXml { get; set; }

    public DateTime UtworzonoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? WyslanoUtc { get; set; }

    public List<InvoiceItem> Pozycje { get; set; } = new();
}
