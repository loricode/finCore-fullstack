using System.Text.Json;
using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Application.Operations;
using FinCore.Application.Reconciliation;
using FinCore.Domain.Common;

namespace FinCore.Application.Integration;

/// <summary>
/// HU-20 · Extracción incremental desde el sistema legado (Oracle) hacia staging, con la marca de agua etl.Watermarks.
/// Mismo flujo que el paquete SSIS: abrir ejecución → staging → stg.usp_PromoteBankMovements.
/// </summary>
public sealed class ImportFromLegacyBankHandler(
    ICurrentUser currentUser,
    IRequestContext request,
    ILegacyBankReader reader,
    IEtlWatermarkStore watermarks,
    IStatementImporter importer)
{
    public const int BatchSize = 5_000;

    public async Task<Result<LegacyImportResult>> HandleAsync(CancellationToken ct)
    {
        if (!currentUser.TryGetSession(out _, out var companyId)) return Guard.NotAuthenticated;
        if (!reader.IsConfigured)
            return Error.Conflict("legacy.not_configured", "No hay conexión configurada con el sistema legado.");

        var last = await watermarks.GetAsync(reader.SourceName, ct);
        var movements = await reader.ReadAfterAsync(last, BatchSize, ct);
        if (movements.Count == 0) return new LegacyImportResult(last, last, null);

        // Número de fila = posición en el lote; la secuencia de Oracle queda como identificador del movimiento.
        var rows = movements.Select((m, i) => new RawBankMovement(i + 1, m.AccountNumber, m.MovementId, m.Reference,
            m.BookingDate, m.Amount, m.Narrative)).ToList();

        var import = await importer.ImportAsync(companyId, "ORACLE", "FinCore.Worker · Oracle LEGACY_BANK",
            $"{reader.SourceName} > {last}", rows, currentUser.UserName ?? "fincore-worker", request.CorrelationId, ct);

        var newWatermark = movements.Max(m => m.Sequence);
        await watermarks.SetAsync(reader.SourceName, newWatermark, ct);
        return new LegacyImportResult(last, newWatermark, import);
    }
}

public sealed record ScheduledReconciliationResult(int AccountId, string AccountCode, int? ReconciliationId, string Outcome);

/// <summary>
/// HU-21 · Conciliación programada: concilia cada cuenta activa en los últimos N días hasta ayer (hora de Colombia).
/// Una cuenta con conciliación en curso se omite (lo registra el log) y las demás continúan.
/// </summary>
public sealed class ScheduledReconciliationHandler(
    OperationScope scope,
    ITransactionQueries transactions,
    IReconciliationEngine engine,
    ICurrentUser currentUser,
    IRequestContext request,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<IReadOnlyList<ScheduledReconciliationResult>>> HandleAsync(int daysBack, CancellationToken ct)
    {
        if (!currentUser.TryGetSession(out var userId, out var companyId)) return Guard.NotAuthenticated;

        var yesterday = BusinessCalendar.Today(clock.GetUtcNow().UtcDateTime).AddDays(-1);
        var from = yesterday.AddDays(-(Math.Clamp(daysBack, 1, 93) - 1));
        var accounts = await transactions.ListAccountsAsync(await scope.AuthorizedBranchIdsAsync(ct), yesterday, ct);

        var results = new List<ScheduledReconciliationResult>();
        foreach (var account in accounts)
        {
            try
            {
                var id = await engine.RunAsync(new RunReconciliationCommand(account.Id, from, yesterday), userId, request.CorrelationId, ct);
                results.Add(new ScheduledReconciliationResult(account.Id, account.Code, id, "COMPLETED"));
            }
            catch (ReconciliationAlreadyRunningException)
            {
                results.Add(new ScheduledReconciliationResult(account.Id, account.Code, null, "SKIPPED_RUNNING"));
            }
        }

        auditLog.Record(new AuditEntry(AuditActions.ReconciliationRun, "Reconciliation", null, userId, currentUser.UserName,
            companyId, new { scheduled = true, from, to = yesterday, results = JsonSerializer.Serialize(results) }));
        await unitOfWork.SaveChangesAsync(ct);
        return results;
    }
}

/// <summary>Recupera eventos de webhook que quedaron RECEIVED (la API se cayó a mitad del proceso) y los procesa.</summary>
public sealed class RecoverInboundEventsHandler(IInboundEventRepository events, BankEventProcessor processor, TimeProvider clock)
{
    public async Task<Result<int>> HandleAsync(TimeSpan olderThan, CancellationToken ct)
    {
        var stale = await events.GetStaleReceivedAsync(clock.GetUtcNow().UtcDateTime - olderThan, 100, ct);
        var processed = 0;
        foreach (var e in stale.Where(e => e.CanBeProcessed))
        {
            var payload = JsonSerializer.Deserialize<ReceivedPayload>(e.Payload, JsonSerializerOptions.Web);
            if (payload?.Movement is null) continue;
            await processor.ProcessAsync(e, payload.Movement, ct);
            processed++;
        }
        return processed;
    }

    private sealed record ReceivedPayload(BankEventMovement? Movement);
}

/// <summary>Depuración diaria del staging (stg.usp_PurgeStaging).</summary>
public sealed class PurgeStagingHandler(IMaintenanceProcedures maintenance)
{
    public async Task<Result<int>> HandleAsync(int retentionDays, CancellationToken ct) =>
        await maintenance.PurgeStagingAsync(retentionDays, ct);
}
