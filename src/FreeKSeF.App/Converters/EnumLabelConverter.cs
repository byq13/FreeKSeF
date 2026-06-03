using System.Globalization;
using System.Windows.Data;
using FreeKSeF.Core.Models;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.Converters;

/// <summary>Zamienia wartosci enum na czytelne etykiety w UI.</summary>
public sealed class EnumLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        StawkaVat s => s.Etykieta(),
        FormaPlatnosci f => Forma(f),
        Srodowisko e => e switch { Srodowisko.Test => "Testowe", Srodowisko.Demo => "Demo", Srodowisko.Produkcja => "Produkcyjne", _ => e.ToString() },
        KierunekFaktury k => k == KierunekFaktury.Sprzedaz ? "Sprzedaz" : "Zakup",
        StatusFaktury st => Status(st),
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static string Forma(FormaPlatnosci f) => f switch
    {
        FormaPlatnosci.Gotowka => "Gotowka",
        FormaPlatnosci.Karta => "Karta",
        FormaPlatnosci.Bon => "Bon",
        FormaPlatnosci.Czek => "Czek",
        FormaPlatnosci.Kredyt => "Kredyt",
        FormaPlatnosci.Przelew => "Przelew",
        FormaPlatnosci.Mobilna => "Platnosc mobilna",
        _ => f.ToString(),
    };

    private static string Status(StatusFaktury s) => s switch
    {
        StatusFaktury.Robocza => "Robocza",
        StatusFaktury.Wyslana => "Wyslana",
        StatusFaktury.Przyjeta => "Przyjeta (KSeF)",
        StatusFaktury.Blad => "Blad",
        StatusFaktury.Zaimportowana => "Zaimportowana",
        _ => s.ToString(),
    };
}
