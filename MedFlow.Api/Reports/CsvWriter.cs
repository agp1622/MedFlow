using System.Globalization;
using System.Text;

namespace MedFlow.Api.Reports;

/// <summary>
/// Minimal RFC 4180 CSV writer. Text cells that a spreadsheet could read as a formula (leading = + - @,
/// tab or carriage return) are prefixed with an apostrophe so they open as plain text.
/// Numbers and dates are written in a fixed, locale-neutral format and are never prefixed.
/// </summary>
public sealed class CsvWriter
{
    private readonly StringBuilder _sb = new();

    public static string Text(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return Quote(value);
    }

    public static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Quote(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    /// <summary>Writes one row of already-formatted cells (use Text/Number/Date to build them).</summary>
    public CsvWriter Row(params string[] cells)
    {
        _sb.Append(string.Join(',', cells)).Append("\r\n");
        return this;
    }

    /// <summary>UTF-8 with a byte order mark so Excel reads accented characters correctly.</summary>
    public byte[] ToBytes() => new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(_sb.ToString())).ToArray();
}
