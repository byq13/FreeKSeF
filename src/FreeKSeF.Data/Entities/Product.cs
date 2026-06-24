using FreeKSeF.Core.Models;

namespace FreeKSeF.Data.Entities;

/// <summary>Pozycja katalogu produktow/uslug (per firma) - do szybkiego wstawiania na fakture.</summary>
public class Product
{
    public int Id { get; set; }

    /// <summary>Firma (wlasciciel) - produkty izolowane per firma.</summary>
    public int CompanyId { get; set; }

    public string Nazwa { get; set; } = string.Empty;
    public string Jednostka { get; set; } = "szt.";
    public decimal CenaNetto { get; set; }
    public StawkaVat Stawka { get; set; } = StawkaVat.Vat23;

    /// <summary>Opcjonalny symbol PKWiU/CN.</summary>
    public string? Pkwiu { get; set; }
}
