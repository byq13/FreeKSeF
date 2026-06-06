using System.Globalization;
using System.Windows.Data;
using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.Converters;

/// <summary>Skraca status faktury do jednego znaku (pelny opis trafia do tooltipa).</summary>
public sealed class StatusSkrotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        StatusFaktury.Robocza => "B",        // bufor
        StatusFaktury.Wyslana => "W",        // wyslana, czeka na potwierdzenie
        StatusFaktury.Przyjeta => "✓",       // przyjeta przez KSeF
        StatusFaktury.Blad => "!",           // blad / odrzucona
        StatusFaktury.Zaimportowana => "I",  // zaimportowana z KSeF
        _ => "?",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
