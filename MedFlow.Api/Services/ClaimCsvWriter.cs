using System.Text;
using MedFlow.Core.DTOs;

namespace MedFlow.Api.Services;

/// <summary>CSV form of a claim draft: header, status and disclaimer rows, one row per item and per missing item.</summary>
public static class ClaimCsvWriter
{
    public static string Write(ClaimDraftDto draft)
    {
        var sb = new StringBuilder();
        void Row(params string?[] cells) => sb.Append(string.Join(',', cells.Select(Escape))).Append("\r\n");
        Row("section", "item", "key", "label", "value");
        Row("status", "", "status", "", draft.Status);
        Row("disclaimer", "", "disclaimer", "", draft.Disclaimer);
        foreach (var i in draft.Items) Row("item", i.Item, i.Key, i.Label, i.Value);
        foreach (var m in draft.Missing) Row("missing", m.Item, m.Key, m.Label, "");
        return sb.ToString();
    }

    /// <summary>RFC 4180 quoting; cells starting with a formula character get a leading apostrophe so spreadsheets show text.</summary>
    public static string Escape(string? value)
    {
        var v = value ?? "";
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
        return v.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
}
