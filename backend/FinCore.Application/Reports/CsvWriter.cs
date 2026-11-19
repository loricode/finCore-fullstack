using System.Globalization;
using System.Text;

namespace FinCore.Application.Reports;

/// <summary>
/// CSV para abrir con doble clic en Excel con configuración regional de Colombia:
///   - separador ';' (Excel en es-CO usa ';' porque la coma es el separador decimal),
///   - números con coma decimal y sin separador de miles, fechas yyyy-MM-dd,
///   - UTF-8 con BOM, para que Excel muestre bien las tildes (Montería, Bogotá).
/// Los valores que empiezan por = + - @ se anteponen con ' para evitar inyección de fórmulas (CSV injection).
/// </summary>
public static class CsvWriter
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Write<T>(IReadOnlyList<(string Header, Func<T, object?> Value)> columns, IEnumerable<T> rows)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(';', columns.Select(c => Escape(c.Header))).Append("\r\n");
        foreach (var row in rows)
            sb.AppendJoin(';', columns.Select(c => Escape(Format(c.Value(row))))).Append("\r\n");

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    internal static string Format(object? value) => value switch
    {
        null => "",
        decimal d => d.ToString("0.##", Colombia),
        double d => d.ToString("0.##", Colombia),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        bool b => b ? "Sí" : "No",
        IFormattable f => f.ToString(null, Colombia),
        _ => value.ToString() ?? "",
    };

    internal static string Escape(string value)
    {
        // Un valor negativo es un número, no una fórmula.
        var isNumber = decimal.TryParse(value, NumberStyles.Number, Colombia, out _);
        if (!isNumber && value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
