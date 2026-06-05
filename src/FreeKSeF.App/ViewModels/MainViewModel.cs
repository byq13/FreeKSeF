namespace FreeKSeF.App.ViewModels;

/// <summary>Glowny ViewModel - udostepnia ViewModele poszczegolnych zakladek.</summary>
public sealed class MainViewModel
{
    public SprzedazViewModel Sprzedaz { get; } = new();
    public ZakupViewModel Zakup { get; } = new();
    public NewInvoiceViewModel NowaFaktura { get; } = new();
    public ContractorsViewModel Kontrahenci { get; } = new();
    public SettingsViewModel Ustawienia { get; } = new();
}
