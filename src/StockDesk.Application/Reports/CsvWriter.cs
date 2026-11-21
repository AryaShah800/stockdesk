using System.Globalization;
using System.Text;

namespace StockDesk.Application.Reports;

/// <summary>
/// Minimal RFC 4180 CSV writer. Fields that start with =, +, - or @ are prefixed with a quote so a product
/// named "=HYPERLINK(...)" is not run as a formula when the file is opened in a spreadsheet.
/// </summary>
public static class CsvWriter
{
    public static string Write(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        AppendLine(sb, header);
        foreach (var row in rows)
            AppendLine(sb, row.Select(Format));
        return sb.ToString();
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static void AppendLine(StringBuilder sb, IEnumerable<string> fields)
    {
        sb.Append(string.Join(',', fields.Select(Escape))).Append("\r\n");
    }

    private static string Escape(string field)
    {
        if (field.Length > 0 && field[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            field = "'" + field;
        return field.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
    }
}
