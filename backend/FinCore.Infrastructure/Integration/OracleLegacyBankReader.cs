using FinCore.Application.Integration;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace FinCore.Infrastructure.Integration;

/// <summary>
/// Lee movimientos del sistema legado en Oracle (esquema LEGACY_BANK) con la consulta compartida con SSIS,
/// incrustada desde etl/FinCore.Integration.SSIS/sql/oracle_extract_movements.sql.
/// Si no hay cadena de conexión "LegacyBank", el lector queda desactivado (la API no la necesita).
/// </summary>
internal sealed class OracleLegacyBankReader(IConfiguration configuration) : ILegacyBankReader
{
    private static readonly Lazy<string> ExtractQuery = new(LoadQuery);
    private readonly string? _connectionString = configuration.GetConnectionString("LegacyBank");

    public string SourceName => "ORACLE:LEGACY_BANK.BANK_MOVEMENTS";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task<IReadOnlyList<LegacyMovement>> ReadAfterAsync(long lastSequence, int maxRows, CancellationToken ct)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new OracleCommand(ExtractQuery.Value, connection) { BindByName = true, CommandTimeout = 120 };
        command.Parameters.Add("last_sequence", OracleDbType.Int64).Value = lastSequence;
        command.Parameters.Add("max_rows", OracleDbType.Int32).Value = maxRows;

        var rows = new List<LegacyMovement>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
            rows.Add(new LegacyMovement(Convert.ToInt64(reader.GetValue(0)), Text(1), Text(2), Text(3), Text(4), Text(5), Text(6)));
        }
        return rows;
    }

    private static string LoadQuery()
    {
        using var stream = typeof(OracleLegacyBankReader).Assembly.GetManifestResourceStream("oracle_extract_movements.sql")
            ?? throw new InvalidOperationException("No se encontró la consulta de extracción incrustada.");
        using var reader = new StreamReader(stream);
        // ODP.NET no acepta el ';' final ni comentarios antes del SELECT en algunas versiones: se envía solo la sentencia.
        var sql = reader.ReadToEnd();
        var lines = sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal));
        return string.Join('\n', lines).Trim().TrimEnd(';');
    }
}
