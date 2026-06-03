using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace FreeKSeF.Core.Fa3;

/// <summary>
/// Serializacja/deserializacja modelu FA(3) (<see cref="Faktura"/>) do/z XML.
/// XML jest w UTF-8 bez BOM, z domyslna przestrzenia nazw FA(3).
/// </summary>
public static class Fa3Serializer
{
    public const string Namespace = "http://crd.gov.pl/wzor/2025/06/25/13775/";

    private static readonly XmlSerializer Serializer = new(typeof(Faktura));

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Serializuje fakture do bajtow XML (UTF-8 bez BOM) - gotowe do wyslania/zapisu.</summary>
    public static byte[] ToXmlBytes(Faktura faktura)
    {
        ArgumentNullException.ThrowIfNull(faktura);

        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = Utf8NoBom,
            Indent = true,
            IndentChars = "  ",
        };
        using (var writer = XmlWriter.Create(ms, settings))
        {
            // Tylko domyslna przestrzen nazw FA(3); bez xsi/xsd.
            var ns = new XmlSerializerNamespaces();
            ns.Add(string.Empty, Namespace);
            Serializer.Serialize(writer, faktura, ns);
        }
        return ms.ToArray();
    }

    /// <summary>Serializuje fakture do tekstu XML.</summary>
    public static string ToXml(Faktura faktura) => Utf8NoBom.GetString(ToXmlBytes(faktura));

    /// <summary>Deserializuje XML faktury (np. pobrany z KSeF) do modelu FA(3).</summary>
    public static Faktura FromXml(string xml)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);
        using var reader = new StringReader(xml);
        return (Faktura)Serializer.Deserialize(reader)!;
    }

    /// <summary>Deserializuje XML faktury z bajtow.</summary>
    public static Faktura FromXmlBytes(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var ms = new MemoryStream(xml);
        return (Faktura)Serializer.Deserialize(ms)!;
    }
}
