using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Core.Models;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>Slownik kontrahentow (nabywcow) - CRUD + pobieranie danych po NIP z rejestru.</summary>
public sealed class ContractorsViewModel : ViewModelBase
{
    private Contractor? _wybrany;
    private int _id;
    private string _nazwa = string.Empty;
    private string _nip = string.Empty;
    private string _adresL1 = string.Empty;
    private string _adresL2 = string.Empty;
    private string _kodKraju = "PL";
    private bool _zajety;

    public ContractorsViewModel()
    {
        NowyCommand = new RelayCommand(Nowy);
        ZapiszCommand = new RelayCommand(Zapisz);
        UsunCommand = new RelayCommand(Usun, () => _id > 0);
        PobierzZGusCommand = new RelayCommand(PobierzZGus, () => !_zajety);
        AppServices.FirmyZmienione += () => { Wczytaj(); Nowy(); };
        Wczytaj();
    }

    public ObservableCollection<Contractor> Kontrahenci { get; } = new();

    /// <summary>Zaznaczenie na liscie wczytuje dane do formularza.</summary>
    public Contractor? Wybrany
    {
        get => _wybrany;
        set
        {
            if (!SetField(ref _wybrany, value) || value is null) return;
            _id = value.Id;
            Nazwa = value.Nazwa;
            Nip = value.Nip ?? string.Empty;
            KodKraju = string.IsNullOrWhiteSpace(value.KodKraju) ? "PL" : value.KodKraju;
            AdresL1 = value.AdresL1;
            AdresL2 = value.AdresL2 ?? string.Empty;
        }
    }

    public string Nazwa { get => _nazwa; set => SetField(ref _nazwa, value); }
    public string Nip { get => _nip; set => SetField(ref _nip, value); }
    public string KodKraju { get => _kodKraju; set => SetField(ref _kodKraju, value); }
    public string AdresL1 { get => _adresL1; set => SetField(ref _adresL1, value); }
    public string AdresL2 { get => _adresL2; set => SetField(ref _adresL2, value); }
    public IReadOnlyList<string> Kraje => Slowniki.Kraje;

    public RelayCommand NowyCommand { get; }
    public RelayCommand ZapiszCommand { get; }
    public RelayCommand UsunCommand { get; }
    public RelayCommand PobierzZGusCommand { get; }

    public void Wczytaj()
    {
        var firmaId = AppServices.AktywnaFirmaId;
        Kontrahenci.Clear();
        using var db = AppServices.Db();
        foreach (var c in db.Contractors.Where(c => c.CompanyId == firmaId).OrderBy(x => x.Nazwa))
            Kontrahenci.Add(c);
    }

    private void Nowy()
    {
        _wybrany = null;
        OnPropertyChanged(nameof(Wybrany));
        _id = 0;
        Nazwa = Nip = AdresL1 = AdresL2 = string.Empty;
        KodKraju = "PL";
    }

    private void Zapisz()
    {
        if (string.IsNullOrWhiteSpace(Nazwa))
        {
            MessageBox.Show("Podaj nazwe kontrahenta.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = AppServices.Db();
        var c = _id > 0 ? db.Contractors.Find(_id) ?? new Contractor() : new Contractor();
        c.CompanyId = AppServices.AktywnaFirmaId;
        c.Nazwa = Nazwa.Trim();
        c.KodKraju = string.IsNullOrWhiteSpace(KodKraju) ? "PL" : KodKraju.Trim();
        c.Nip = string.IsNullOrWhiteSpace(Nip) ? null : Core.Models.Nip.Normalizuj(Nip);
        c.AdresL1 = AdresL1.Trim();
        c.AdresL2 = string.IsNullOrWhiteSpace(AdresL2) ? null : AdresL2.Trim();

        if (c.Id == 0) db.Contractors.Add(c);
        db.SaveChanges();
        _id = c.Id;

        Wczytaj();
        Wybrany = Kontrahenci.FirstOrDefault(x => x.Id == _id);
        MessageBox.Show("Zapisano kontrahenta.", "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Usun()
    {
        if (_id == 0) return;
        using (var db = AppServices.Db())
        {
            var e = db.Contractors.Find(_id);
            if (e is not null) { db.Contractors.Remove(e); db.SaveChanges(); }
        }
        Wczytaj();
        Nowy();
    }

    private async void PobierzZGus()
    {
        if (!Core.Models.Nip.Waliduj(Nip))
        {
            MessageBox.Show("Wpisz poprawny NIP (10 cyfr). Myslniki i spacje sa OK.", "NIP",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var dane = await GusService.PobierzAsync(Nip);
            if (dane is null)
            {
                MessageBox.Show("Nie znaleziono firmy o tym NIP w rejestrze.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Nip = dane.Nip;
            Nazwa = dane.Nazwa;
            AdresL1 = dane.AdresL1;
            AdresL2 = dane.AdresL2;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Pobieranie danych", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _zajety = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
