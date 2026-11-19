using System.Text;

namespace FinCore.Application.Reconciliation;

/// <summary>Fila cruda de un extracto, tal como viene en el archivo (texto). La valida stg.usp_PromoteBankMovements.</summary>
public sealed record RawBankMovement(
    int RowNumber,
    string? AccountNumber,
    string? MovementId,
    string? Reference,
    string? Date,
    string? Amount,
    string? Description);

/// <summary>
/// Lee un extracto CSV (spec importacion-extractos):
///   account_number;movement_id;reference;date;amount;description
/// - Separador ';' o ',' (se detecta en el encabezado). Comillas dobles opcionales, "" escapa una comilla.
/// - El encabezado es obligatorio y las columnas se ubican por nombre (el orden puede variar).
/// - NO valida los valores: eso lo hace el procedimiento de promoción, igual que para Oracle/SSIS.
///   Aquí solo se rechaza un archivo que no se puede leer (sin encabezado, columnas faltantes, demasiadas filas).
/// </summary>
public static class CsvStatementParser
{
    public const int MaxRows = 20_000;

    private static readonly string[] Required = ["account_number", "movement_id", "reference", "date", "amount"];

    public sealed record ParseResult(IReadOnlyList<RawBankMovement> Rows, string? Error)
    {
        public bool IsValid => Error is null;
    }

    public static async Task<ParseResult> ParseAsync(Stream content, CancellationToken ct)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var header = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(header))
            return new([], "El archivo está vacío o no tiene encabezado.");

        var separator = header.Count(c => c == ';') >= header.Count(c => c == ',') ? ';' : ',';
        var columns = Split(header, separator).Select(c => c.Trim().ToLowerInvariant()).ToList();
        var missing = Required.Where(r => !columns.Contains(r)).ToList();
        if (missing.Count > 0)
            return new([], $"Faltan columnas en el encabezado: {string.Join(", ", missing)}. " +
                           "Formato: account_number;movement_id;reference;date;amount;description");

        int Index(string name) => columns.IndexOf(name);
        var (iAccount, iId, iRef, iDate, iAmount, iDesc) =
            (Index("account_number"), Index("movement_id"), Index("reference"), Index("date"), Index("amount"), Index("description"));

        var rows = new List<RawBankMovement>();
        var lineNumber = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (rows.Count >= MaxRows)
                return new([], $"El archivo supera el máximo de {MaxRows:N0} movimientos por carga.");

            var f = Split(line, separator);
            string? At(int i) => i >= 0 && i < f.Count ? Clean(f[i]) : null;
            rows.Add(new RawBankMovement(lineNumber, At(iAccount), At(iId), At(iRef), At(iDate), At(iAmount), At(iDesc)));
        }

        return rows.Count == 0 ? new([], "El archivo no tiene movimientos.") : new(rows, null);
    }

    /// <summary>Divide una línea respetando comillas dobles.</summary>
    internal static List<string> Split(string line, char separator)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == separator) { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        fields.Add(current.ToString());
        return fields;
    }

    /// <summary>Recorta y limita a 400 caracteres (el tamaño máximo de las columnas de staging).</summary>
    private static string? Clean(string value)
    {
        var v = value.Trim();
        return v.Length == 0 ? null : v.Length <= 400 ? v : v[..400];
    }
}
