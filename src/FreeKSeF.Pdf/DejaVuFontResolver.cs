using System.Reflection;
using PdfSharp.Fonts;

namespace FreeKSeF.Pdf;

/// <summary>
/// Resolver fontow dla PdfSharp 6 (build "Core" nie korzysta z fontow systemowych).
/// Udostepnia osadzony font DejaVu Sans (regular + bold) z polskimi znakami,
/// dzieki czemu PDF renderuje sie identycznie na Windows i na Linuksie.
/// </summary>
public sealed class DejaVuFontResolver : IFontResolver
{
    public const string Family = "DejaVu Sans";

    private const string Regular = "DejaVuSans";
    private const string Bold = "DejaVuSans-Bold";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new(isBold ? Bold : Regular);

    public byte[] GetFont(string faceName)
    {
        var plik = faceName.Contains("Bold", StringComparison.OrdinalIgnoreCase)
            ? "DejaVuSans-Bold.ttf"
            : "DejaVuSans.ttf";

        var asm = typeof(DejaVuFontResolver).Assembly;
        var nazwa = "FreeKSeF.Pdf.Fonts." + plik;
        using var s = asm.GetManifestResourceStream(nazwa)
            ?? throw new FileNotFoundException($"Brak osadzonego fontu: {nazwa}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
