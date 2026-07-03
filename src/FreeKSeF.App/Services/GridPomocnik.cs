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

    /// <summary>True, gdy zrodlo zdarzenia lezy w wierszu danych (a nie np. w naglowku kolumny).</summary>
    public static bool KliknietoWiersz(DependencyObject? zrodlo)
        => ZnajdzRodzica<DataGridRow>(zrodlo) is not null;

    /// <summary>Przywraca zapamietany uklad kolumn (po naglowku). Bledny/stary wpis jest ignorowany.</summary>
    public static void PrzywrocKolumny(DataGrid grid, string klucz)
    {
        var zapis = Ustawienia.Pobierz(klucz);
        if (string.IsNullOrWhiteSpace(zapis)) return;

        try
        {
            var mapa = new Dictionary<string, (int Index, double Width, bool? Widoczna)>();
            foreach (var wpis in zapis.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var cz = wpis.Split('|');
                if (cz.Length is not (3 or 4)) continue;
                var naglowek = cz[0];
                if (!int.TryParse(cz[1], out var idx) ||
                    !double.TryParse(cz[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
                    continue;
                // Starszy zapis (3 czesci) nie znal widocznosci - zostawiamy domyslna z XAML.
                bool? widoczna = cz.Length == 4 ? cz[3] != "0" : null;
                mapa[naglowek] = (idx, w, widoczna);
            }

            foreach (var kol in grid.Columns)
            {
                if (kol.Header is not string h || !mapa.TryGetValue(h, out var u)) continue;
                if (u.Width > 20) kol.Width = new DataGridLength(u.Width);
                if (u.Widoczna is { } v) kol.Visibility = v ? Visibility.Visible : Visibility.Collapsed;
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

    /// <summary>Zapisuje aktualny uklad kolumn (kolejnosc + szerokosc + widocznosc).</summary>
    public static void ZapiszKolumny(DataGrid grid, string klucz)
    {
        var sb = new StringBuilder();
        foreach (var kol in grid.Columns)
        {
            if (kol.Header is not string h) continue;
            var w = kol.ActualWidth > 0 ? kol.ActualWidth : kol.Width.DisplayValue;
            sb.Append(h).Append('|').Append(kol.DisplayIndex).Append('|')
              .Append(w.ToString("0", CultureInfo.InvariantCulture)).Append('|')
              .Append(kol.Visibility == Visibility.Visible ? '1' : '0').Append(';');
        }
        Ustawienia.Zapisz(klucz, sb.ToString());
    }

    /// <summary>
    /// Pokazuje menu z lista kolumn (checkbox = widoczna). Zmiana od razu
    /// przelacza widocznosc i zapisuje preferencje uzytkownika.
    /// </summary>
    public static void PokazWyborKolumn(DataGrid grid, string klucz, UIElement przy)
    {
        var menu = new ContextMenu { PlacementTarget = przy };
        foreach (var kol in grid.Columns)
        {
            if (kol.Header is not string h) continue;
            var pozycja = new MenuItem
            {
                Header = h,
                IsCheckable = true,
                IsChecked = kol.Visibility == Visibility.Visible,
                StaysOpenOnClick = true,
            };
            var kolumna = kol;
            pozycja.Click += (_, _) =>
            {
                var pokaz = pozycja.IsChecked;
                // Nie pozwol ukryc ostatniej widocznej kolumny.
                if (!pokaz && grid.Columns.Count(c => c.Visibility == Visibility.Visible) <= 1)
                {
                    pozycja.IsChecked = true;
                    return;
                }
                kolumna.Visibility = pokaz ? Visibility.Visible : Visibility.Collapsed;
                ZapiszKolumny(grid, klucz);
            };
            menu.Items.Add(pozycja);
        }
        menu.IsOpen = true;
    }

    private static T? ZnajdzRodzica<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T)
            d = VisualTreeHelper.GetParent(d);
        return d as T;
    }
}
