namespace FreeKSeF.App.ViewModels;

/// <summary>Glowny ViewModel - udostepnia ViewModele poszczegolnych zakladek.</summary>
public sealed class MainViewModel
{
    public SettingsViewModel Ustawienia { get; } = new();
    public ContractorsViewModel Kontrahenci { get; } = new();
    public NewInvoiceViewModel NowaFaktura { get; } = new();
    public InvoicesViewModel Faktury { get; } = new();
}
