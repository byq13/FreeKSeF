using FreeKSeF.App.Mvvm;
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

    private bool _zaznaczona;
    /// <summary>Chwilowe podswietlenie wiersza (tylko w biezacej sesji).</summary>
    public bool Zaznaczona { get => _zaznaczona; set => SetField(ref _zaznaczona, value); }
}
