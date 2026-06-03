using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace FreeKSeF.Core.Fa3;

/// <summary>Wynik walidacji XML wzgledem schematu FA(3).</summary>
public sealed record Fa3ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static readonly Fa3ValidationResult Ok = new(true, Array.Empty<string>());
}

/// <summary>
/// Waliduje XML faktury wzgledem oficjalnych schematow XSD FA(3),
/// osadzonych w assembly (offline, bez polaczenia z crd.gov.pl).
/// </summary>
public static class Fa3Validator
{
    private const string MainSchema = "schemat_FA3_v1-0E.xsd";
    private static readonly Lazy<XmlSchemaSet> Schemas = new(BuildSchemaSet);

    public static Fa3ValidationResult Validate(string xml)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return Validate(stream);
    }

    public static Fa3ValidationResult Validate(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var stream = new MemoryStream(xml);
        return Validate(stream);
    }

    public static Fa3ValidationResult Validate(Stream xml)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
        settings.Schemas.Add(Schemas.Value);
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, e) =>
        {
            var pos = e.Exception is { } ex ? $" (linia {ex.LineNumber}, poz. {ex.LinePosition})" : string.Empty;
            errors.Add($"{e.Severity}: {e.Message}{pos}");
        };

        using var reader = XmlReader.Create(xml, settings);
        while (reader.Read()) { }

        return errors.Count == 0 ? Fa3ValidationResult.Ok : new Fa3ValidationResult(false, errors);
    }

    private static XmlSchemaSet BuildSchemaSet()
    {
        var resolver = new EmbeddedXsdResolver();
        var set = new XmlSchemaSet { XmlResolver = resolver };
        using var main = resolver.OpenByFileName(MainSchema);
        var schema = XmlSchema.Read(main, null)
                     ?? throw new InvalidOperationException("Nie udalo sie wczytac schematu FA(3).");
        set.Add(schema);
        set.Compile();
        return set;
    }

    /// <summary>Resolver zwracajacy schematy XSD osadzone w assembly po nazwie pliku.</summary>
    private sealed class EmbeddedXsdResolver : XmlResolver
    {
        private const string Prefix = "FreeKSeF.Core.Schemas.";
        private static readonly Assembly Asm = typeof(Fa3Validator).Assembly;

        public Stream OpenByFileName(string fileName)
            => Asm.GetManifestResourceStream(Prefix + fileName)
               ?? throw new FileNotFoundException($"Brak osadzonego schematu: {fileName}");

        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            var fileName = Path.GetFileName(absoluteUri.AbsolutePath);
            return OpenByFileName(fileName);
        }

        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
            => new("file:///" + Path.GetFileName(relativeUri ?? string.Empty));
    }
}
