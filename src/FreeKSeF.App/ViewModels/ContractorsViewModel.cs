using System.Collections.ObjectModel;
using System.Windows;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>Slownik kontrahentow (nabywcow) - proste CRUD.</summary>
public sealed class ContractorsViewModel : ViewModelBase
{
    private Contractor? _wybrany;

    public ContractorsViewModel()
    {
        NowyCommand = new RelayCommand(Nowy);
        ZapiszCommand = new RelayCommand(Zapisz, () => Wybrany is not null);
        UsunCommand = new RelayCommand(Usun, () => Wybrany is { Id: > 0 });
        Wczytaj();
    }

    public ObservableCollection<Contractor> Kontrahenci { get; } = new();

    public Contractor? Wybrany
    {
        get => _wybrany;
        set => SetField(ref _wybrany, value);
    }

    public RelayCommand NowyCommand { get; }
    public RelayCommand ZapiszCommand { get; }
    public RelayCommand UsunCommand { get; }

    public void Wczytaj()
    {
        Kontrahenci.Clear();
        using var db = AppServices.Db();
        foreach (var c in db.Contractors.OrderBy(x => x.Nazwa))
            Kontrahenci.Add(c);
    }

    private void Nowy()
    {
        var c = new Contractor { Nazwa = "Nowy kontrahent" };
        Kontrahenci.Add(c);
        Wybrany = c;
    }

    private void Zapisz()
    {
        if (Wybrany is null) return;
        if (string.IsNullOrWhiteSpace(Wybrany.Nazwa))
        {
            MessageBox.Show("Podaj nazwe kontrahenta.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = AppServices.Db();
        if (Wybrany.Id == 0)
        {
            db.Contractors.Add(Wybrany);
        }
        else
        {
            db.Contractors.Update(Wybrany);
        }
        db.SaveChanges();
        MessageBox.Show("Zapisano kontrahenta.", "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Usun()
    {
        if (Wybrany is not { Id: > 0 }) return;
        using var db = AppServices.Db();
        var e = db.Contractors.Find(Wybrany.Id);
        if (e is not null)
        {
            db.Contractors.Remove(e);
            db.SaveChanges();
        }
        Kontrahenci.Remove(Wybrany);
        Wybrany = null;
    }
}
