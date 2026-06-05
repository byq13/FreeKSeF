using System.Windows;
using System.Windows.Input;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>
/// Lista faktur sprzedazy. Faktury powstaja w buforze (robocze); wysylka do KSeF
/// nastepuje TYLKO na wyrazne polecenie z potwierdzeniem.
/// </summary>
public sealed class SprzedazViewModel : FakturaListaViewModelBase
{
    private bool _zajety;

    public SprzedazViewModel() : base(KierunekFaktury.Sprzedaz)
    {
        WyslijCommand = new RelayCommand(Wyslij, () => !_zajety && Wybrana is not null);
        UsunCommand = new RelayCommand(Usun, () => Wybrana is not null);
        Odswiez();
    }

    public RelayCommand WyslijCommand { get; }
    public RelayCommand UsunCommand { get; }

    private async void Wyslij()
    {
        if (Wybrana is null) return;

        if (Wybrana.Status == StatusFaktury.Przyjeta)
        {
            MessageBox.Show($"Faktura {Wybrana.Numer} zostala juz przyjeta przez KSeF (numer {Wybrana.NumerKsef}).",
                "Juz wyslana", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var potwierdzenie = MessageBox.Show(
            $"Czy na pewno wyslac fakture {Wybrana.Numer} do KSeF?\n\n" +
            "Operacja jest nieodwracalna - faktura zostanie trwale zarejestrowana w KSeF.",
            "Potwierdzenie wysylki",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (potwierdzenie != MessageBoxResult.Yes) return;

        var id = Wybrana.Id;
        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var wynik = await FakturaService.WyslijAsync(id);
            Odswiez();
            if (wynik.Sukces)
                MessageBox.Show($"Faktura przyjeta przez KSeF.\nNumer KSeF: {wynik.NumerKsef}",
                    "Wyslano", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("KSeF nie potwierdzil przyjecia: " + (wynik.Blad ?? "nieznany blad"),
                    "Blad wysylki", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Ksef.KsefException ex)
        {
            MessageBox.Show(ex.Message, "KSeF niedostepny", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _zajety = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void Usun()
    {
        if (Wybrana is null) return;

        var tekst = Wybrana.Status == StatusFaktury.Przyjeta
            ? $"Faktura {Wybrana.Numer} jest zarejestrowana w KSeF. Usuniecie z lokalnej bazy NIE anuluje jej w KSeF.\n\nUsunac wpis lokalny?"
            : $"Usunac robocza fakture {Wybrana.Numer} z lokalnej bazy?";

        if (MessageBox.Show(tekst, "Usuwanie faktury", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            != MessageBoxResult.Yes) return;

        using (var db = AppServices.Db())
        {
            var e = db.Invoices.Find(Wybrana.Id);
            if (e is not null) { db.Invoices.Remove(e); db.SaveChanges(); }
        }
        Odswiez();
    }
}
