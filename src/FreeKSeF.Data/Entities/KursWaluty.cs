namespace FreeKSeF.Data.Entities;

/// <summary>
/// Kurs waluty (sredni NBP) na dany dzien. Wlasna tabela zasilana z api.nbp.pl,
/// uzywana do przeliczania VAT na PLN na fakturach w walucie obcej.
/// </summary>
public class KursWaluty
{
    public int Id { get; set; }

    /// <summary>Kod waluty ISO, np. EUR, USD.</summary>
    public string Kod { get; set; } = string.Empty;

    /// <summary>Dzien obowiazywania kursu (data publikacji tabeli NBP).</summary>
    public DateTime Data { get; set; }

    /// <summary>Kurs sredni do PLN.</summary>
    public decimal Kurs { get; set; }

    /// <summary>Tabela NBP (zwykle "A").</summary>
    public string Tabela { get; set; } = "A";
}
