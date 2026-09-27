using System.Globalization;
using System.Text;

namespace FieldOps.Application.Common;

/// <summary>RFC 4180 CSV that opens cleanly in Excel (UTF-8 with a BOM).</summary>
public static class Csv
{
    public static byte[] Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var text = new StringBuilder();
        text.AppendJoin(',', headers.Select(Text)).Append("\r\n");
        foreach (var row in rows) text.AppendJoin(',', row.Select(Cell)).Append("\r\n");
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Cell(object? value) => value switch
    {
        null => "",
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        int or long => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Text(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""),
    };

    /// <summary>Quotes when needed and defuses text a spreadsheet would run as a formula.</summary>
    private static string Text(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
