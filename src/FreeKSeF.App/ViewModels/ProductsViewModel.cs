using System.Collections.ObjectModel;
using System.Windows;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Core.Models;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.ViewModels;

/// <summary>Katalog produktow/uslug (per firma) - CRUD, by nie wpisywac w kolko tych samych pozycji.</summary>
public sealed class ProductsViewModel : ViewModelBase
{
    private Product? _wybrany;
    private int _id;
    private string _nazwa = string.Empty;
    private string _jednostka = "szt.";
    private decimal _cenaNetto;
    private StawkaVat _stawka = StawkaVat.Vat23;
    private string _pkwiu = string.Empty;

    public ProductsViewModel()
    {
        NowyCommand = new RelayCommand(Nowy);
        ZapiszCommand = new RelayCommand(Zapisz);
        UsunCommand = new RelayCommand(Usun, () => _id > 0);
        AppServices.FirmyZmienione += () => { Wczytaj(); Nowy(); };
        Wczytaj();
    }

    public ObservableCollection<Product> Produkty { get; } = new();
    public Array Stawki => Enum.GetValues(typeof(StawkaVat));

    public Product? Wybrany
    {
        get => _wybrany;
        set
        {
            if (!SetField(ref _wybrany, value) || value is null) return;
            _id = value.Id;
            Nazwa = value.Nazwa;
            Jednostka = value.Jednostka;
            CenaNetto = value.CenaNetto;
            Stawka = value.Stawka;
            Pkwiu = value.Pkwiu ?? string.Empty;
        }
    }

    public string Nazwa { get => _nazwa; set => SetField(ref _nazwa, value); }
    public string Jednostka { get => _jednostka; set => SetField(ref _jednostka, value); }
    public decimal CenaNetto { get => _cenaNetto; set => SetField(ref _cenaNetto, value); }
    public StawkaVat Stawka { get => _stawka; set => SetField(ref _stawka, value); }
    public string Pkwiu { get => _pkwiu; set => SetField(ref _pkwiu, value); }

    public RelayCommand NowyCommand { get; }
    public RelayCommand ZapiszCommand { get; }
    public RelayCommand UsunCommand { get; }

    public void Wczytaj()
    {
        var firmaId = AppServices.AktywnaFirmaId;
        Produkty.Clear();
        using var db = AppServices.Db();
        foreach (var p in db.Products.Where(p => p.CompanyId == firmaId).OrderBy(x => x.Nazwa))
            Produkty.Add(p);
    }

    private void Nowy()
    {
        _wybrany = null;
        OnPropertyChanged(nameof(Wybrany));
        _id = 0;
        Nazwa = string.Empty;
        Jednostka = "szt.";
        CenaNetto = 0m;
        Stawka = StawkaVat.Vat23;
        Pkwiu = string.Empty;
    }

    private void Zapisz()
    {
        if (string.IsNullOrWhiteSpace(Nazwa))
        {
            MessageBox.Show("Podaj nazwe produktu/uslugi.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = AppServices.Db();
        var p = _id > 0 ? db.Products.Find(_id) ?? new Product() : new Product();
        p.CompanyId = AppServices.AktywnaFirmaId;
        p.Nazwa = Nazwa.Trim();
        p.Jednostka = string.IsNullOrWhiteSpace(Jednostka) ? "szt." : Jednostka.Trim();
        p.CenaNetto = CenaNetto;
        p.Stawka = Stawka;
        p.Pkwiu = string.IsNullOrWhiteSpace(Pkwiu) ? null : Pkwiu.Trim();

        if (p.Id == 0) db.Products.Add(p);
        db.SaveChanges();
        _id = p.Id;

        Wczytaj();
        Wybrany = Produkty.FirstOrDefault(x => x.Id == _id);
        MessageBox.Show("Zapisano produkt.", "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Usun()
    {
        if (_id == 0) return;
        using (var db = AppServices.Db())
        {
            var p = db.Products.Find(_id);
            if (p is not null) { db.Products.Remove(p); db.SaveChanges(); }
        }
        Wczytaj();
        Nowy();
    }
}
