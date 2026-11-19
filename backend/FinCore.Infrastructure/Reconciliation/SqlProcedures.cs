using System.Data;
using FinCore.Application.Reconciliation;
using FinCore.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Infrastructure.Reconciliation;

/// <summary>
/// Carga de extractos con los mismos procedimientos que usa SSIS (spec importacion-extractos):
///   1. etl.usp_StartExecution            abre la ejecución
///   2. SqlBulkCopy a stg.BankMovements   filas crudas (texto), en un solo viaje a la base
///   3. stg.usp_PromoteBankMovements      valida, rechaza y carga (su propia transacción)
/// Si falla el paso 2, la ejecución queda FAILED (etl.usp_FailExecution) y el error se propaga.
/// </summary>
internal sealed class StatementImporter(FinCoreDbContext db) : IStatementImporter
{
    public async Task<ImportResultDto> ImportAsync(int companyId, string sourceSystem, string packageName, string sourceName,
        IReadOnlyList<RawBankMovement> rows, string triggeredBy, string correlationId, CancellationToken ct)
    {
        var connection = await OpenAsync(db, ct);

        long executionId;
        await using (var start = Procedure(connection, "etl.usp_StartExecution"))
        {
            start.Parameters.AddWithValue("@CompanyId", companyId);
            start.Parameters.AddWithValue("@PackageName", packageName);
            start.Parameters.AddWithValue("@SourceSystem", sourceSystem);
            start.Parameters.AddWithValue("@SourceName", sourceName);
            start.Parameters.AddWithValue("@TriggeredBy", triggeredBy);
            start.Parameters.AddWithValue("@CorrelationId", correlationId);
            var output = start.Parameters.Add("@EtlExecutionId", SqlDbType.BigInt);
            output.Direction = ParameterDirection.Output;
            await start.ExecuteNonQueryAsync(ct);
            executionId = (long)output.Value;
        }

        try
        {
            using var bulk = new SqlBulkCopy(connection) { DestinationTableName = "stg.BankMovements", BulkCopyTimeout = 120 };
            var table = ToStagingTable(executionId, rows);
            foreach (DataColumn column in table.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            await bulk.WriteToServerAsync(table, ct);
        }
        catch (Exception ex)
        {
            await using var fail = Procedure(connection, "etl.usp_FailExecution");
            fail.Parameters.AddWithValue("@EtlExecutionId", executionId);
            fail.Parameters.AddWithValue("@ErrorMessage", $"Falló la carga a staging: {ex.Message}");
            await fail.ExecuteNonQueryAsync(CancellationToken.None);
            throw;
        }

        await using (var promote = Procedure(connection, "stg.usp_PromoteBankMovements"))
        {
            promote.Parameters.AddWithValue("@EtlExecutionId", executionId);
            await promote.ExecuteNonQueryAsync(ct);
        }

        var summary = await db.Set<EtlExecutionRow>().AsNoTracking().SingleAsync(e => e.EtlExecutionId == executionId, ct);
        var errors = await new ReconciliationQueries(db).GetEtlErrorsAsync(companyId, executionId, 200, ct);
        return new ImportResultDto(executionId, summary.Status, summary.RowsRead, summary.RowsLoaded, summary.RowsRejected, errors);
    }

    /// <summary>Las columnas de staging tienen tamaños fijos: se recorta para que la carga no falle por longitud.</summary>
    private static DataTable ToStagingTable(long executionId, IReadOnlyList<RawBankMovement> rows)
    {
        var table = new DataTable();
        table.Columns.Add("EtlExecutionId", typeof(long));
        table.Columns.Add("RowNumber", typeof(int));
        foreach (var name in new[] { "AccountNumber", "ExternalMovementId", "BankReference", "MovementDate", "Amount", "Description" })
            table.Columns.Add(name, typeof(string));

        static object Cut(string? value, int max) => value is null ? DBNull.Value : value.Length <= max ? value : value[..max];

        foreach (var r in rows)
            table.Rows.Add(executionId, r.RowNumber, Cut(r.AccountNumber, 60), Cut(r.MovementId, 100), Cut(r.Reference, 100),
                Cut(r.Date, 30), Cut(r.Amount, 40), Cut(r.Description, 400));
        return table;
    }

    internal static async Task<SqlConnection> OpenAsync(FinCoreDbContext db, CancellationToken ct)
    {
        // La conexión del DbContext: al abrirse, el interceptor fija UserId y CorrelationId en SESSION_CONTEXT.
        await db.Database.OpenConnectionAsync(ct);
        return (SqlConnection)db.Database.GetDbConnection();
    }

    internal static SqlCommand Procedure(SqlConnection connection, string name) =>
        new(name, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 };
}

/// <summary>Invoca rec.usp_RunReconciliation, sin transacción externa (así la cabecera FAILED sobrevive a un error).</summary>
internal sealed class ReconciliationEngine(FinCoreDbContext db) : IReconciliationEngine
{
    public async Task<int> RunAsync(RunReconciliationCommand command, int userId, string correlationId, CancellationToken ct)
    {
        var connection = await StatementImporter.OpenAsync(db, ct);
        await using var run = StatementImporter.Procedure(connection, "rec.usp_RunReconciliation");
        run.Parameters.AddWithValue("@AccountId", command.AccountId);
        run.Parameters.Add("@PeriodFrom", SqlDbType.Date).Value = command.PeriodFrom.ToDateTime(TimeOnly.MinValue);
        run.Parameters.Add("@PeriodTo", SqlDbType.Date).Value = command.PeriodTo.ToDateTime(TimeOnly.MinValue);
        run.Parameters.Add("@DateToleranceDays", SqlDbType.TinyInt).Value = command.DateToleranceDays;
        run.Parameters.AddWithValue("@ExecutedByUserId", userId);
        run.Parameters.AddWithValue("@ExecutedBy", "api");
        run.Parameters.AddWithValue("@CorrelationId", correlationId);
        var output = run.Parameters.Add("@ReconciliationId", SqlDbType.Int);
        output.Direction = ParameterDirection.Output;

        try
        {
            await run.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (ex.Number == 50104)
        {
            throw new ReconciliationAlreadyRunningException(ex.Message);
        }
        return (int)output.Value;
    }
}

/// <summary>Procedimientos de mantenimiento que ejecuta el Worker.</summary>
internal sealed class MaintenanceProcedures(FinCoreDbContext db) : FinCore.Application.Integration.IMaintenanceProcedures
{
    public async Task<int> PurgeStagingAsync(int retentionDays, CancellationToken ct)
    {
        var connection = await StatementImporter.OpenAsync(db, ct);
        await using var purge = StatementImporter.Procedure(connection, "stg.usp_PurgeStaging");
        purge.Parameters.AddWithValue("@RetentionDays", retentionDays);
        return Convert.ToInt32(await purge.ExecuteScalarAsync(ct));
    }
}

internal sealed class ReconciliationDetailRepository(FinCoreDbContext db) : IReconciliationDetailRepository
{
    public async Task<(Domain.Reconciliation.ReconciliationDetail Detail, int BranchId)?> GetWithBranchAsync(long detailId, CancellationToken ct)
    {
        var detail = await db.ReconciliationDetails.SingleOrDefaultAsync(d => d.Id == detailId, ct);
        if (detail is null) return null;

        var branchId = await db.Database.SqlQuery<int>($"""
            SELECT a.BranchId AS Value
            FROM rec.Reconciliations r JOIN fin.Accounts a ON a.Id = r.AccountId
            WHERE r.Id = {detail.ReconciliationId}
            """).SingleAsync(ct);
        return (detail, branchId);
    }
}
