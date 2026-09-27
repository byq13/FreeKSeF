using FreeKSeF.App.Mvvm;
using FreeKSeF.Core.Fa3;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>
/// Wiersz listy faktur - opakowuje encje i dodaje stan UI (np. chwilowe zaznaczenie),
/// ktory nie jest zapisywany w bazie.
/// </summary>
public sealed class FakturaRow : ViewModelBase
{
    public FakturaRow(Invoice faktura) => Faktura = faktura;

    public Invoice Faktura { get; }

    /// <summary>Moment wyslania do KSeF w czasie lokalnym (kolumna opcjonalna).</summary>
    public DateTime? Wyslano => Faktura.WyslanoUtc?.ToLocalTime();

    /// <summary>Moment utworzenia/pobrania w czasie lokalnym (kolumna opcjonalna).</summary>
    public DateTime Utworzono => Faktura.UtworzonoUtc.ToLocalTime();

    /// <summary>Krotka etykieta rodzaju faktury, np. "Zaliczkowa".</summary>
    public string RodzajEtykieta => Fa3Rodzaj.Krotka(Faktura.Rodzaj);

    /// <summary>Pelna nazwa rodzaju (tooltip), np. "Faktura korygująca fakturę zaliczkową".</summary>
    public string RodzajOpis => Fa3Rodzaj.Nazwa(Faktura.Rodzaj);

    /// <summary>Kolor tla plakietki rodzaju.</summary>
    public string RodzajTlo => Faktura.Rodzaj switch
    {
        Fa3Rodzaj.Zal => "#FEF3C7",
        Fa3Rodzaj.Roz => "#D1FAE5",
        Fa3Rodzaj.Upr => "#DBEAFE",
        var r when Fa3Rodzaj.JestKorekta(r) => "#FEE2E2",
        _ => "#F3F4F6",
    };

    /// <summary>Kolor tekstu plakietki rodzaju.</summary>
    public string RodzajKolor => Faktura.Rodzaj switch
    {
        Fa3Rodzaj.Zal => "#92400E",
        Fa3Rodzaj.Roz => "#065F46",
        Fa3Rodzaj.Upr => "#1E40AF",
        var r when Fa3Rodzaj.JestKorekta(r) => "#991B1B",
        _ => "#374151",
    };

    private bool _zaznaczona;
    /// <summary>Chwilowe podswietlenie wiersza (tylko w biezacej sesji).</summary>
    public bool Zaznaczona { get => _zaznaczona; set => SetField(ref _zaznaczona, value); }
}
