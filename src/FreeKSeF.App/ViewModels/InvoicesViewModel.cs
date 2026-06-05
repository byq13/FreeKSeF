using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FreeKSeF.App.Mvvm;
using FreeKSeF.App.Services;
using FreeKSeF.Data;
using FreeKSeF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreeKSeF.App.ViewModels;

/// <summary>Lista faktur (wystawionych i zaimportowanych), podglad oraz import zakupow z KSeF.</summary>
public sealed class InvoicesViewModel : ViewModelBase
{
    private Invoice? _wybrana;
    private DateTime _importOd = DateTime.Today.AddMonths(-3);
    private DateTime _importDo = DateTime.Today;
    private bool _pokazSprzedaz = true;
    private bool _pokazZakup = true;
    private bool _zajety;
    private string _importStatus = string.Empty;

    public InvoicesViewModel()
    {
        OdswiezCommand = new RelayCommand(Odswiez);
        PobierzZakupyCommand = new RelayCommand(PobierzZakupy, () => !_zajety);
        Odswiez();
    }

    public ObservableCollection<Invoice> Faktury { get; } = new();

    public Invoice? Wybrana
    {
        get => _wybrana;
        set { if (SetField(ref _wybrana, value)) OnPropertyChanged(nameof(PodgladXml)); }
    }

    public string PodgladXml => Wybrana?.Xml ?? string.Empty;

    public bool PokazSprzedaz { get => _pokazSprzedaz; set { if (SetField(ref _pokazSprzedaz, value)) Odswiez(); } }
    public bool PokazZakup { get => _pokazZakup; set { if (SetField(ref _pokazZakup, value)) Odswiez(); } }

    public DateTime ImportOd { get => _importOd; set => SetField(ref _importOd, value); }
    public DateTime ImportDo { get => _importDo; set => SetField(ref _importDo, value); }
    public string ImportStatus { get => _importStatus; set => SetField(ref _importStatus, value); }

    public RelayCommand OdswiezCommand { get; }
    public RelayCommand PobierzZakupyCommand { get; }

    public void Odswiez()
    {
        Faktury.Clear();
        using var db = AppServices.Db();
        var q = db.Invoices.AsNoTracking().AsQueryable();
        var kierunki = new List<KierunekFaktury>();
        if (PokazSprzedaz) kierunki.Add(KierunekFaktury.Sprzedaz);
        if (PokazZakup) kierunki.Add(KierunekFaktury.Zakup);
        q = q.Where(i => kierunki.Contains(i.Kierunek));

        foreach (var inv in q.OrderByDescending(i => i.DataWystawienia).ThenByDescending(i => i.Id))
            Faktury.Add(inv);
    }

    private async void PobierzZakupy()
    {
        _zajety = true;
        CommandManager.InvalidateRequerySuggested();
        int pobrane = 0;
        int dodane = 0;
        int pominiete = 0;

        try
        {
            ImportStatus = "Logowanie do KSeF...";
            await AppServices.ZalogujZUstawienAsync();
            ImportStatus = "Pobieranie faktur z KSeF...";

            var faktury = await AppServices.Ksef.PobierzZakupyAsync(ImportOd, ImportDo, f =>
            {
                pobrane++;
                if (ZapiszPobranaFakture(f))
                    dodane++;
                else
                    pominiete++;

                ImportStatus = $"Pobrano: {pobrane}, dodano: {dodane}, pominieto duplikaty: {pominiete}.";
                return Task.CompletedTask;
            });

            Odswiez();
            MessageBox.Show($"Pobrano {faktury.Count} faktur(y), dodano nowych: {dodane}.", "Import zakupow",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Ksef.KsefException ex)
        {
            Odswiez();
            MessageBox.Show(
                $"{ex.Message}\n\nDo tego momentu pobrano: {pobrane}, dodano: {dodane}, pominieto duplikaty: {pominiete}.",
                "KSeF niedostepny",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _zajety = false;
            if (pobrane == 0 && string.IsNullOrEmpty(ImportStatus) == false)
                ImportStatus = "Import przerwany bez pobranych faktur.";
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool ZapiszPobranaFakture(Ksef.FakturaZKsef faktura)
    {
        using var db = AppServices.Db();
        if (db.Invoices.Any(i => i.NumerKsef == faktura.NumerKsef))
            return false;

        var inv = FakturaMapping.ZakupZXml(faktura.Xml, faktura.NumerKsef);
        db.Invoices.Add(inv);
        db.SaveChanges();

        if (PokazZakup)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Faktury.Insert(0, inv);
                Wybrana = inv;
            });
        }

        return true;
    }
}
