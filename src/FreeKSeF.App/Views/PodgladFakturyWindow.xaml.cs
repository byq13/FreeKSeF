using System.IO;
using System.Windows;
using FreeKSeF.App.Services;
using FreeKSeF.Data.Entities;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace FreeKSeF.App.Views;

/// <summary>Podglad faktury jako PDF (WebView2) z zapisem PDF/XML i drukiem.</summary>
public partial class PodgladFakturyWindow : Window
{
    private readonly Invoice _faktura;
    private string? _pdfTemp;

    public PodgladFakturyWindow(Invoice faktura)
    {
        _faktura = faktura;
        InitializeComponent();
        Title = $"Podgląd faktury {faktura.Numer}";
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _pdfTemp = FakturaService.ZapiszPdfDoTemp(_faktura);
            // Folder danych WebView2 trzymamy w %TEMP%, by NIE smiecic katalogu obok exe.
            var folderTemp = Path.Combine(Path.GetTempPath(), "FreeKSeF", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: folderTemp);
            await Web.EnsureCoreWebView2Async(env);
            // Ukryj pasek narzedzi przegladarki, zostaw sam podglad PDF.
            Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Web.CoreWebView2.Navigate(new Uri(_pdfTemp).AbsoluteUri);
        }
        catch (Exception ex)
        {
            StatusTekst.Text = "Nie udalo sie wygenerowac podgladu: " + ex.Message;
        }
    }

    private void ZapiszPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Zapisz PDF faktury",
            Filter = "Plik PDF (*.pdf)|*.pdf",
            FileName = FakturaService.BezpiecznaNazwa(_faktura.Numer) + ".pdf",
        };
        if (dialog.ShowDialog() != true) return;
        FakturaService.ZapiszPdf(_faktura, dialog.FileName);
        StatusTekst.Text = "Zapisano PDF.";
    }

    private void ZapiszXml_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Zapisz XML faktury (FA(3))",
            Filter = "Plik XML (*.xml)|*.xml",
            FileName = FakturaService.BezpiecznaNazwa(_faktura.Numer) + ".xml",
        };
        if (dialog.ShowDialog() != true) return;
        FakturaService.ZapiszXml(_faktura, dialog.FileName);
        StatusTekst.Text = "Zapisano XML (gotowy do wgrania w Aplikacji Podatnika).";
    }

    private void Drukuj_Click(object sender, RoutedEventArgs e)
    {
        try { Web.CoreWebView2?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser); }
        catch (Exception ex) { StatusTekst.Text = "Drukowanie niedostepne: " + ex.Message; }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        try { if (_pdfTemp is not null && File.Exists(_pdfTemp)) File.Delete(_pdfTemp); }
        catch { /* plik tymczasowy - ignorujemy */ }
    }
}
