using System.Reflection;
using PdfSharp.Fonts;

namespace MedFlow.Infrastructure.Documents;

/// <summary>
/// PDFsharp (core build) cannot read system fonts on Linux, so DejaVu Sans (Bitstream Vera licence,
/// see Fonts/LICENSE-DejaVu.txt) is embedded in the assembly and served from here.
/// </summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string Family = "DejaVu Sans";

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "DejaVuSans-Bold" : "DejaVuSans");

    public byte[]? GetFont(string faceName)
    {
        using var s = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream($"MedFlow.Fonts.{faceName}.ttf")
            ?? throw new InvalidOperationException($"Embedded font {faceName} not found.");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
