using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;

namespace FreeKSeF.Core.Fa3;

/// <summary>
/// Jedno pole faktury w edytorze zaawansowanym: sciezka (np. "Fa.P_2"), etykieta XML,
/// aktualna wartosc jako tekst oraz - dla enumow - lista dozwolonych opcji.
/// Wiersze-naglowki oznaczaja sekcje (Podmiot1, Fa, FaWiersz [1]...).
/// </summary>
public sealed class PoleFa3
{
    public required string Sciezka { get; init; }
    public required string Etykieta { get; init; }
    public int Wciecie { get; init; }
    public bool Naglowek { get; init; }
    public string Wartosc { get; set; } = string.Empty;

    /// <summary>Dozwolone wartosci (enum XML). Pusty wpis "" = brak wartosci (pole opcjonalne).</summary>
    public IReadOnlyList<string>? Opcje { get; init; }

    internal object? Obiekt { get; init; }
    internal PropertyInfo? Prop { get; init; }
}

/// <summary>
/// Generyczny "property grid" nad obiektowym modelem FA(3): wypisuje wszystkie pola
/// (przez refleksje, wg nazw XML) i pozwala wpisac wartosci z powrotem - z obsluga
/// pol opcjonalnych (pary *Specified) i enumow (wartosci z atrybutow XmlEnum).
/// Uzywany przez okno edytora zaawansowanego.
/// </summary>
public static class Fa3Pola
{
    /// <summary>Wypisuje wszystkie pola faktury w kolejnosci deklaracji (jak w XML).</summary>
    public static List<PoleFa3> Wypisz(Faktura fa)
    {
        ArgumentNullException.ThrowIfNull(fa);
        var wynik = new List<PoleFa3>();
        Przejdz(fa, string.Empty, 0, wynik);
        return wynik;
    }

    /// <summary>
    /// Wpisuje wartosc (tekst z edytora) z powrotem do modelu. Pusty tekst = brak wartosci
    /// (dla pol opcjonalnych czysci *Specified / string). Rzuca ArgumentException
    /// z czytelnym komunikatem przy niepoprawnym formacie.
    /// </summary>
    public static void Zastosuj(PoleFa3 pole, string? tekst)
    {
        if (pole.Prop is null || pole.Obiekt is null) return; // naglowek sekcji

        var prop = pole.Prop;
        var obj = pole.Obiekt;
        var spec = SpecifiedDla(obj, prop);
        tekst = tekst?.Trim() ?? string.Empty;

        if (tekst.Length == 0)
        {
            if (spec is not null) { spec.SetValue(obj, false); return; }
            if (prop.PropertyType == typeof(string)) { prop.SetValue(obj, null); return; }
            return; // pole wymagane typu wartosciowego - zostawiamy poprzednia wartosc
        }

        object wartosc;
        try
        {
            wartosc = Parsuj(prop.PropertyType, tekst);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            throw new ArgumentException($"Niepoprawna wartosc \"{tekst}\" w polu {pole.Sciezka}.");
        }

        prop.SetValue(obj, wartosc);
        spec?.SetValue(obj, true);
    }

    // --- Przejscie po modelu ---

    private static void Przejdz(object obj, string sciezka, int wciecie, List<PoleFa3> wynik)
    {
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.Name.EndsWith("Specified", StringComparison.Ordinal)) continue; // obslugiwane parami

            var typ = prop.PropertyType;
            typ = Nullable.GetUnderlyingType(typ) ?? typ;
            var nazwa = NazwaXml(prop);
            var pelna = sciezka.Length == 0 ? nazwa : $"{sciezka}.{nazwa}";

            if (typ == typeof(byte[])) continue;

            if (typ.IsEnum)
            {
                if (prop.CanWrite) wynik.Add(LiscEnum(obj, prop, typ, pelna, nazwa, wciecie));
            }
            else if (typ.IsPrimitive || typ == typeof(string) || typ == typeof(decimal) || typ == typeof(DateTime))
            {
                if (prop.CanWrite) wynik.Add(Lisc(obj, prop, pelna, nazwa, wciecie));
            }
            else if (typeof(IEnumerable).IsAssignableFrom(typ) && typ != typeof(string))
            {
                if (prop.GetValue(obj) is not IEnumerable kolekcja) continue;
                var i = 1;
                foreach (var element in kolekcja)
                {
                    if (element is null or string) continue; // kolekcje tekstow (np. WZ) pomijamy
                    var p = $"{pelna}[{i}]";
                    wynik.Add(new PoleFa3 { Sciezka = p, Etykieta = $"{nazwa} [{i}]", Wciecie = wciecie, Naglowek = true });
                    Przejdz(element, p, wciecie + 1, wynik);
                    i++;
                }
            }
            else if (typ.IsClass)
            {
                if (prop.GetValue(obj) is not { } zagniezdzony) continue; // pusta sekcja opcjonalna
                wynik.Add(new PoleFa3 { Sciezka = pelna, Etykieta = nazwa, Wciecie = wciecie, Naglowek = true });
                Przejdz(zagniezdzony, pelna, wciecie + 1, wynik);
            }
        }
    }

    private static PoleFa3 Lisc(object obj, PropertyInfo prop, string sciezka, string nazwa, int wciecie)
    {
        var spec = SpecifiedDla(obj, prop);
        var brak = spec is not null && spec.GetValue(obj) is false;
        return new PoleFa3
        {
            Sciezka = sciezka,
            Etykieta = nazwa,
            Wciecie = wciecie,
            Wartosc = brak ? string.Empty : Formatuj(prop, prop.GetValue(obj)),
            Obiekt = obj,
            Prop = prop,
        };
    }

    private static PoleFa3 LiscEnum(object obj, PropertyInfo prop, Type typEnum, string sciezka, string nazwa, int wciecie)
    {
        var spec = SpecifiedDla(obj, prop);
        var opcje = new List<string>();
        if (spec is not null) opcje.Add(string.Empty); // pole opcjonalne - mozna wyczyscic
        opcje.AddRange(WartosciEnum(typEnum));

        var brak = spec is not null && spec.GetValue(obj) is false;
        return new PoleFa3
        {
            Sciezka = sciezka,
            Etykieta = nazwa,
            Wciecie = wciecie,
            Wartosc = brak ? string.Empty : WartoscEnumNaXml(prop.GetValue(obj)!),
            Opcje = opcje,
            Obiekt = obj,
            Prop = prop,
        };
    }

    // --- Format / parsowanie ---

    private static string Formatuj(PropertyInfo prop, object? wartosc) => wartosc switch
    {
        null => string.Empty,
        DateTime d => JestDataBezCzasu(prop) || d.TimeOfDay == TimeSpan.Zero
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : d.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => wartosc.ToString() ?? string.Empty,
    };

    private static object Parsuj(Type typ, string tekst)
    {
        typ = Nullable.GetUnderlyingType(typ) ?? typ;
        if (typ == typeof(string)) return tekst;
        if (typ.IsEnum)
        {
            foreach (var field in typ.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var xml = field.GetCustomAttribute<XmlEnumAttribute>()?.Name ?? field.Name;
                if (string.Equals(xml, tekst, StringComparison.OrdinalIgnoreCase))
                    return field.GetValue(null)!;
            }
            throw new ArgumentException($"Brak opcji {tekst}.");
        }
        if (typ == typeof(DateTime))
            return DateTime.Parse(tekst, CultureInfo.InvariantCulture, DateTimeStyles.None);
        if (typ == typeof(decimal))
            return decimal.Parse(tekst.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture);
        if (typ == typeof(bool))
            return tekst is "1" or "true" or "tak";
        return Convert.ChangeType(tekst, typ, CultureInfo.InvariantCulture);
    }

    // --- Pomocnicze ---

    /// <summary>True dla pol XML typu "date" (bez czesci czasowej).</summary>
    private static bool JestDataBezCzasu(PropertyInfo prop)
        => prop.GetCustomAttribute<XmlElementAttribute>()?.DataType == "date";

    private static PropertyInfo? SpecifiedDla(object obj, PropertyInfo prop)
    {
        var spec = obj.GetType().GetProperty(prop.Name + "Specified");
        return spec is not null && spec.PropertyType == typeof(bool) && spec.CanWrite ? spec : null;
    }

    private static string NazwaXml(PropertyInfo prop)
    {
        var el = prop.GetCustomAttribute<XmlElementAttribute>();
        if (!string.IsNullOrEmpty(el?.ElementName)) return el.ElementName;
        var at = prop.GetCustomAttribute<XmlAttributeAttribute>();
        if (!string.IsNullOrEmpty(at?.AttributeName)) return at.AttributeName;
        return prop.Name;
    }

    private static IEnumerable<string> WartosciEnum(Type typEnum)
        => typEnum.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetCustomAttribute<XmlEnumAttribute>()?.Name ?? f.Name);

    private static string WartoscEnumNaXml(object wartosc)
    {
        var field = wartosc.GetType().GetField(wartosc.ToString()!);
        return field?.GetCustomAttribute<XmlEnumAttribute>()?.Name ?? wartosc.ToString()!;
    }
}
