using FreeKSeF.Core.Models;

namespace FreeKSeF.Data.Entities;

/// <summary>Pozycja faktury zapisana w bazie.</summary>
public class InvoiceItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int Lp { get; set; }

    public string Nazwa { get; set; } = string.Empty;
    public string Jednostka { get; set; } = "szt.";
    public decimal Ilosc { get; set; } = 1m;
    public decimal CenaNetto { get; set; }
    public StawkaVat Stawka { get; set; } = StawkaVat.Vat23;

    public decimal WartoscNetto { get; set; }
    public decimal KwotaVat { get; set; }
}
