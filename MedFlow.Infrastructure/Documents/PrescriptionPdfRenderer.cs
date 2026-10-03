using System.Globalization;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace MedFlow.Infrastructure.Documents;

/// <summary>
/// One-page prescription PDF built with PDFsharp (MIT). The signature block is a blank line for a
/// handwritten signature; the document is not, and does not claim to be, an electronic signature.
/// </summary>
public class PrescriptionPdfRenderer : IPrescriptionDocumentRenderer
{
    private const double Margin = 54, PageW = 612, PageH = 792; // US Letter, 0.75" margins
    private const double ContentW = PageW - 2 * Margin;
    private static readonly object FontLock = new();

    private static void EnsureFonts()
    {
        lock (FontLock)
            GlobalFontSettings.FontResolver ??= new EmbeddedFontResolver();
    }

    public byte[] Render(PrescriptionDocumentData d) => Render(d, null);

    /// <summary>Overload that also reports every string drawn, so tests can assert on content.</summary>
    internal byte[] Render(PrescriptionDocumentData d, List<string>? drawnText)
    {
        EnsureFonts();
        using var doc = new PdfDocument();
        doc.Info.Title = $"Prescription {d.PrescriptionId}";
        doc.Info.Creator = "MedFlow";
        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(PageW);
        page.Height = XUnit.FromPoint(PageH);
        using var gfx = XGraphics.FromPdfPage(page);
        var c = new Canvas(gfx, drawnText);

        var h1 = new XFont(EmbeddedFontResolver.Family, 20, XFontStyleEx.Bold);
        var h2 = new XFont(EmbeddedFontResolver.Family, 10, XFontStyleEx.Bold);
        var body = new XFont(EmbeddedFontResolver.Family, 11, XFontStyleEx.Regular);
        var bold = new XFont(EmbeddedFontResolver.Family, 11, XFontStyleEx.Bold);
        var small = new XFont(EmbeddedFontResolver.Family, 8, XFontStyleEx.Regular);
        var banner = new XFont(EmbeddedFontResolver.Family, 14, XFontStyleEx.Bold);
        var grey = new XSolidBrush(XColor.FromGrayScale(0.4));
        var pen = new XPen(XColors.Black, 0.8);

        // Reserve room at the bottom for the signature block + footer; long text is truncated above it.
        const double sigTop = PageH - Margin - 130;
        double y = Margin;

        c.DrawString("Prescription", h1, XBrushes.Black, new XPoint(Margin, y + 18));
        c.DrawString($"Rx No. {d.PrescriptionId}", body, XBrushes.Black,
            new XRect(Margin, y + 4, ContentW, 16), XStringFormats.TopRight);
        y += 30;
        c.G.DrawLine(pen, Margin, y, Margin + ContentW, y);
        y += 12;

        var invalid = InvalidReason(d);
        if (invalid != null)
        {
            var r = new XRect(Margin, y, ContentW, 26);
            c.G.DrawRectangle(new XPen(XColors.DarkRed, 1.5), r);
            c.DrawString($"NOT VALID - {invalid}", banner, XBrushes.DarkRed, r, XStringFormats.Center);
            y += 38;
        }

        // Two columns: prescriber and patient
        var colW = (ContentW - 20) / 2;
        var left = new List<(string, string)> { ("Name", d.DoctorName), ("Specialty", d.Specialty) };
        if (!string.IsNullOrWhiteSpace(d.LicenseNumber)) left.Add(("Licence No.", d.LicenseNumber!));
        if (!string.IsNullOrWhiteSpace(d.DoctorPhone)) left.Add(("Phone", d.DoctorPhone!));
        var right = new List<(string, string)>
        {
            ("Name", d.PatientName), ("Date of birth", Date(d.PatientDateOfBirth))
        };
        if (!string.IsNullOrWhiteSpace(d.PatientPhone)) right.Add(("Phone", d.PatientPhone!));
        if (!string.IsNullOrWhiteSpace(d.PatientAddress)) right.Add(("Address", d.PatientAddress!));

        c.DrawString("PRESCRIBER", h2, grey, new XPoint(Margin, y + 9));
        c.DrawString("PATIENT", h2, grey, new XPoint(Margin + colW + 20, y + 9));
        y += 16;
        var yl = Block(c, left, body, bold, Margin, y, colW, sigTop);
        var yr = Block(c, right, body, bold, Margin + colW + 20, y, colW, sigTop);
        y = Math.Max(yl, yr) + 8;
        c.G.DrawLine(pen, Margin, y, Margin + ContentW, y);
        y += 14;

        c.DrawString("MEDICATION", h2, grey, new XPoint(Margin, y + 9));
        y += 18;
        var med = new List<(string, string)>
        {
            ("Drug", d.DrugName), ("Dosage", d.Dosage), ("Frequency", d.Frequency),
            ("Issued", Date(d.IssuedDate)), ("Expires", Date(d.ExpiryDate)),
            ("Refills", d.RefillsRemaining.ToString(CultureInfo.InvariantCulture)),
            ("Status", d.Status.ToString())
        };
        if (!string.IsNullOrWhiteSpace(d.Instructions)) med.Add(("Instructions", d.Instructions!));
        Block(c, med, body, bold, Margin, y, ContentW, sigTop);

        // Signature block
        var sy = sigTop + 50;
        c.G.DrawLine(pen, Margin, sy, Margin + 300, sy);
        c.G.DrawLine(pen, Margin + 340, sy, Margin + ContentW, sy);
        c.DrawString("Prescriber signature", small, grey, new XPoint(Margin, sy + 11));
        c.DrawString(d.DoctorName, body, XBrushes.Black, new XPoint(Margin, sy + 26));
        c.DrawString("Date", small, grey, new XPoint(Margin + 340, sy + 11));
        c.DrawString("Not valid unless signed by the prescriber. Handwritten signature only; this document is not an electronic prescription.",
            small, grey, new XPoint(Margin, PageH - Margin + 6));

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private sealed class Canvas(XGraphics g, List<string>? sink)
    {
        public XGraphics G { get; } = g;
        public void DrawString(string text, XFont font, XBrush brush, XPoint p) { sink?.Add(text); G.DrawString(text, font, brush, p); }
        public void DrawString(string text, XFont font, XBrush brush, XRect r, XStringFormat f) { sink?.Add(text); G.DrawString(text, font, brush, r, f); }
    }

    private static string? InvalidReason(PrescriptionDocumentData d)
    {
        if (d.Status == PrescriptionStatus.Cancelled) return "CANCELLED";
        if (d.Status == PrescriptionStatus.Expired || d.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow)) return "EXPIRED";
        return null;
    }

    private static string Date(DateOnly d) => d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>Draws label/value rows, wrapping values; stops (with an ellipsis) at <paramref name="limit"/>. Returns the new y.</summary>
    private static double Block(Canvas c, List<(string label, string value)> rows, XFont body, XFont bold,
        double x, double y, double width, double limit)
    {
        const double labelW = 95, lineH = 14;
        var valueW = width - labelW;
        foreach (var (label, value) in rows)
        {
            var lines = Wrap(c, value, body, valueW);
            c.DrawString(label + ":", bold, XBrushes.Black, new XPoint(x, y + 11));
            for (var i = 0; i < lines.Count; i++)
            {
                if (y + lineH > limit - lineH)
                {
                    c.DrawString("...", body, XBrushes.Black, new XPoint(x + labelW, y + 11));
                    return y + lineH;
                }
                c.DrawString(lines[i], body, XBrushes.Black, new XPoint(x + labelW, y + 11));
                y += lineH;
            }
            y += 3;
        }
        return y;
    }

    private static List<string> Wrap(Canvas c, string text, XFont font, double width)
    {
        var result = new List<string>();
        foreach (var para in text.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var word in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var piece in BreakLong(c, word, font, width))
                {
                    var candidate = line.Length == 0 ? piece : line + " " + piece;
                    if (line.Length > 0 && c.G.MeasureString(candidate, font).Width > width)
                    {
                        result.Add(line);
                        line = piece;
                    }
                    else line = candidate;
                }
            }
            result.Add(line);
        }
        return result;
    }

    // A single word wider than the column (e.g. no spaces) is split by characters.
    private static IEnumerable<string> BreakLong(Canvas c, string word, XFont font, double width)
    {
        if (c.G.MeasureString(word, font).Width <= width) { yield return word; yield break; }
        var cur = "";
        foreach (var ch in word)
        {
            if (cur.Length > 0 && c.G.MeasureString(cur + ch, font).Width > width) { yield return cur; cur = ""; }
            cur += ch;
        }
        if (cur.Length > 0) yield return cur;
    }
}
