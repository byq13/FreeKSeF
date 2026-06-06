using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FreeKSeF.App.Services;

/// <summary>
/// Pomocnicze operacje na DataGrid: zaznaczanie wiersza pod kursorem (dla menu PPM)
/// oraz zapamietywanie ukladu kolumn (kolejnosc + szerokosc) w ustawieniach.
/// </summary>
public static class GridPomocnik
{
    /// <summary>Zaznacza wiersz, w ktory kliknieto - aby menu kontekstowe dotyczylo wlasciwej faktury.</summary>
    public static void ZaznaczWierszPodKursorem(DataGrid grid, DependencyObject? zrodlo)
    {
        var row = ZnajdzRodzica<DataGridRow>(zrodlo);
        if (row is not null)
        {
            grid.SelectedItem = row.Item;
            row.Focus();
        }
    }

    /// <summary>Przywraca zapamietany uklad kolumn (po naglowku). Bledny/stary wpis jest ignorowany.</summary>
    public static void PrzywrocKolumny(DataGrid grid, string klucz)
    {
        var zapis = Ustawienia.Pobierz(klucz);
        if (string.IsNullOrWhiteSpace(zapis)) return;

        try
        {
            var mapa = new Dictionary<string, (int Index, double Width)>();
            foreach (var wpis in zapis.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var cz = wpis.Split('|');
                if (cz.Length != 3) continue;
                var naglowek = cz[0];
                if (int.TryParse(cz[1], out var idx) &&
                    double.TryParse(cz[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
                    mapa[naglowek] = (idx, w);
            }

            foreach (var kol in grid.Columns)
            {
                if (kol.Header is not string h || !mapa.TryGetValue(h, out var u)) continue;
                if (u.Width > 20) kol.Width = new DataGridLength(u.Width);
            }
            // Kolejnosc ustawiamy wg zapamietanych indeksow.
            foreach (var kol in grid.Columns)
                if (kol.Header is string h && mapa.TryGetValue(h, out var u) && u.Index >= 0 && u.Index < grid.Columns.Count)
                    kol.DisplayIndex = u.Index;
        }
        catch
        {
            // Uszkodzony wpis ukladu nie moze blokowac aplikacji.
        }
    }

    /// <summary>Zapisuje aktualny uklad kolumn (kolejnosc + szerokosc).</summary>
    public static void ZapiszKolumny(DataGrid grid, string klucz)
    {
        var sb = new StringBuilder();
        foreach (var kol in grid.Columns)
        {
            if (kol.Header is not string h) continue;
            var w = kol.ActualWidth > 0 ? kol.ActualWidth : kol.Width.DisplayValue;
            sb.Append(h).Append('|').Append(kol.DisplayIndex).Append('|')
              .Append(w.ToString("0", CultureInfo.InvariantCulture)).Append(';');
        }
        Ustawienia.Zapisz(klucz, sb.ToString());
    }

    private static T? ZnajdzRodzica<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T)
            d = VisualTreeHelper.GetParent(d);
        return d as T;
    }
}
