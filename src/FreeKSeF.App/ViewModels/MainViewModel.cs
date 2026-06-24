using System.Collections.ObjectModel;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>Glowny ViewModel - zakladki + przelacznik aktywnej firmy u gory.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private Company? _aktywnaFirma;
    private bool _aktualizacja;

    public MainViewModel()
    {
        AppServices.FirmyZmienione += ZaladujFirmy;
        ZaladujFirmy();
    }

    public SprzedazViewModel Sprzedaz { get; } = new();
    public ZakupViewModel Zakup { get; } = new();
    public NewInvoiceViewModel NowaFaktura { get; } = new();
    public ContractorsViewModel Kontrahenci { get; } = new();
    public ProductsViewModel Produkty { get; } = new();
    public SettingsViewModel Ustawienia { get; } = new();

    public ObservableCollection<Company> Firmy { get; } = new();

    /// <summary>Aktywna firma wybierana w gornym pasku.</summary>
    public Company? AktywnaFirma
    {
        get => _aktywnaFirma;
        set
        {
            if (!SetField(ref _aktywnaFirma, value)) return;
            if (_aktualizacja || value is null) return;
            AppServices.UstawAktywnaFirme(value.Id);
        }
    }

    private void ZaladujFirmy()
    {
        _aktualizacja = true;
        Firmy.Clear();
        using (var db = AppServices.Db())
            foreach (var c in db.Companies.OrderBy(x => x.Nazwa))
                Firmy.Add(c);

        AktywnaFirma = Firmy.FirstOrDefault(x => x.Id == AppServices.AktywnaFirmaId);
        _aktualizacja = false;
    }
}
