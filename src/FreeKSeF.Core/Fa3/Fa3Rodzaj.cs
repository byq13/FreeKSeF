using System.Xml;

namespace FreeKSeF.Core.Fa3;

/// <summary>
/// Rodzaj faktury FA(3) (pole RodzajFaktury): kod z XML i polskie nazwy,
/// jak w wizualizacji Ministerstwa Finansow (ksef-pdf-generator).
/// </summary>
public static class Fa3Rodzaj
{
    public const string Vat = "VAT";
    public const string Kor = "KOR";
    public const string Zal = "ZAL";
    public const string Roz = "ROZ";
    public const string Upr = "UPR";
    public const string KorZal = "KOR_ZAL";
    public const string KorRoz = "KOR_ROZ";

    /// <summary>Pelna nazwa do naglowka PDF, np. "Faktura zaliczkowa".</summary>
    public static string Nazwa(string? kod) => kod switch
    {
        Vat => "Faktura podstawowa",
        Kor => "Faktura korygująca",
        Zal => "Faktura zaliczkowa",
        Roz => "Faktura rozliczeniowa",
        Upr => "Faktura uproszczona",
        KorZal => "Faktura korygująca fakturę zaliczkową",
        KorRoz => "Faktura korygująca fakturę rozliczeniową",
        _ => "Faktura",
    };

    /// <summary>Krotka etykieta na liste faktur, np. "Zaliczkowa".</summary>
    public static string Krotka(string? kod) => kod switch
    {
        Vat => "Podstawowa",
        Kor => "Korekta",
        Zal => "Zaliczkowa",
        Roz => "Rozliczeniowa",
        Upr => "Uproszczona",
        KorZal => "Korekta zal.",
        KorRoz => "Korekta roz.",
        _ => "—",
    };

    /// <summary>True dla wszystkich rodzajow korekt (KOR, KOR_ZAL, KOR_ROZ).</summary>
    public static bool JestKorekta(string? kod) => kod is Kor or KorZal or KorRoz;

    /// <summary>Kod rodzaju z modelu faktury (np. TRodzajFaktury.KorZal -> "KOR_ZAL").</summary>
    public static string Kod(TRodzajFaktury r) => r switch
    {
        TRodzajFaktury.Vat => Vat,
        TRodzajFaktury.Kor => Kor,
        TRodzajFaktury.Zal => Zal,
        TRodzajFaktury.Roz => Roz,
        TRodzajFaktury.Upr => Upr,
        TRodzajFaktury.KorZal => KorZal,
        TRodzajFaktury.KorRoz => KorRoz,
        _ => Vat,
    };

    /// <summary>
    /// Szybko odczytuje kod rodzaju z surowego XML (bez pelnej deserializacji,
    /// niezaleznie od prefiksu przestrzeni nazw). Null, gdy XML jest pusty/uszkodzony.
    /// </summary>
    public static string? ZXml(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;
        try
        {
            using var r = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true,
            });
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.LocalName == "RodzajFaktury")
                    return r.ReadElementContentAsString().Trim();
        }
        catch (XmlException)
        {
            // Uszkodzony XML - rodzaj nieznany.
        }
        return null;
    }
}
