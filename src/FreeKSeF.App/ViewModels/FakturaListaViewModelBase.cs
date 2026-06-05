using System.Collections.ObjectModel;
using System.Windows;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.App.Views;
using FreeKSeF.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace FreeKSeF.App.ViewModels;

/// <summary>
/// Wspolna baza list faktur (sprzedaz/zakup): wczytywanie wg kierunku, wybor,
/// podglad PDF w oknie oraz szybki zapis XML na dysk.
/// </summary>
public abstract class FakturaListaViewModelBase : ViewModelBase
{
    private readonly KierunekFaktury _kierunek;
    private Invoice? _wybrana;

    protected FakturaListaViewModelBase(KierunekFaktury kierunek)
    {
        _kierunek = kierunek;
        PodgladCommand = new RelayCommand(Podglad, () => Wybrana is not null);
        ZapiszXmlCommand = new RelayCommand(ZapiszXml, () => Wybrana is not null);
    }

    public ObservableCollection<Invoice> Faktury { get; } = new();

    public Invoice? Wybrana
    {
        get => _wybrana;
        set => SetField(ref _wybrana, value);
    }

    public RelayCommand PodgladCommand { get; }
    public RelayCommand ZapiszXmlCommand { get; }

    public void Odswiez()
    {
        Faktury.Clear();
        using var db = AppServices.Db();
        var lista = db.Invoices.AsNoTracking()
            .Where(i => i.Kierunek == _kierunek)
            .OrderByDescending(i => i.DataWystawienia).ThenByDescending(i => i.Id)
            .ToList();
        foreach (var inv in lista)
            Faktury.Add(inv);
    }

    /// <summary>Dodaje swiezo zapisana/pobrana fakture na gore listy.</summary>
    protected void DodajNaGore(Invoice inv)
    {
        Faktury.Insert(0, inv);
        Wybrana = inv;
    }

    private void Podglad()
    {
        if (Wybrana is null) return;
        var okno = new PodgladFakturyWindow(Wybrana) { Owner = Application.Current.MainWindow };
        okno.ShowDialog();
    }

    private void ZapiszXml()
    {
        if (Wybrana is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Zapisz XML faktury (FA(3))",
            Filter = "Plik XML (*.xml)|*.xml",
            FileName = FakturaService.BezpiecznaNazwa(Wybrana.Numer) + ".xml",
        };
        if (dialog.ShowDialog() != true) return;
        FakturaService.ZapiszXml(Wybrana, dialog.FileName);
        MessageBox.Show("Zapisano XML (gotowy do wgrania w Aplikacji Podatnika KSeF).",
            "FreeKSeF", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
