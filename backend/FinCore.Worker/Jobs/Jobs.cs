using FinCore.Application.Integration;
using FinCore.Worker.Scheduling;

namespace FinCore.Worker.Jobs;

/// <summary>HU-20 · Extrae los movimientos nuevos del sistema legado (Oracle) cada N segundos.</summary>
public sealed class LegacyBankImportJob(IServiceScopeFactory scopes, WorkerStartup startup, IConfiguration configuration,
    ILogger<LegacyBankImportJob> logger) : ScheduledJob(scopes, startup, configuration, logger)
{
    protected override string Name => "LegacyBankImport";

    protected override async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var result = await services.GetRequiredService<ImportFromLegacyBankHandler>().HandleAsync(ct);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Extracción Oracle omitida: {Reason}", result.Error!.Message);
            return;
        }

        var r = result.Value;
        if (r.Import is null)
            logger.LogInformation("Oracle sin movimientos nuevos (marca de agua {Watermark})", r.PreviousWatermark);
        else
            logger.LogInformation(
                "Oracle: ejecución ETL {EtlExecutionId} {Status} · leídos {Read} · cargados {Loaded} · rechazados {Rejected} · marca {From} → {To}",
                r.Import.EtlExecutionId, r.Import.Status, r.Import.RowsRead, r.Import.RowsLoaded, r.Import.RowsRejected,
                r.PreviousWatermark, r.NewWatermark);
    }
}

/// <summary>HU-21 · Conciliación nocturna de todas las cuentas activas (últimos N días hasta ayer).</summary>
public sealed class NightlyReconciliationJob(IServiceScopeFactory scopes, WorkerStartup startup, IConfiguration configuration,
    ILogger<NightlyReconciliationJob> logger) : ScheduledJob(scopes, startup, configuration, logger)
{
    private readonly int _daysBack = configuration.GetValue("Worker:Jobs:NightlyReconciliation:DaysBack", 1);

    protected override string Name => "NightlyReconciliation";

    protected override async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var result = await services.GetRequiredService<ScheduledReconciliationHandler>().HandleAsync(_daysBack, ct);
        if (!result.IsSuccess) { logger.LogWarning("Conciliación nocturna omitida: {Reason}", result.Error!.Message); return; }

        foreach (var r in result.Value)
            logger.LogInformation("Conciliación {AccountCode}: {Outcome} (#{ReconciliationId})", r.AccountCode, r.Outcome, r.ReconciliationId);
    }
}

/// <summary>Procesa eventos de webhook que quedaron recibidos sin procesar (la API cayó a mitad del proceso).</summary>
public sealed class InboundEventRecoveryJob(IServiceScopeFactory scopes, WorkerStartup startup, IConfiguration configuration,
    ILogger<InboundEventRecoveryJob> logger) : ScheduledJob(scopes, startup, configuration, logger)
{
    protected override string Name => "InboundEventRecovery";

    protected override async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var result = await services.GetRequiredService<RecoverInboundEventsHandler>().HandleAsync(TimeSpan.FromMinutes(2), ct);
        if (result.IsSuccess && result.Value > 0)
            logger.LogInformation("Eventos de webhook recuperados: {Count}", result.Value);
    }
}

/// <summary>HU-22 · Depuración diaria del staging (ejecuciones terminadas hace más de N días).</summary>
public sealed class StagingPurgeJob(IServiceScopeFactory scopes, WorkerStartup startup, IConfiguration configuration,
    ILogger<StagingPurgeJob> logger) : ScheduledJob(scopes, startup, configuration, logger)
{
    private readonly int _retentionDays = configuration.GetValue("Worker:Jobs:StagingPurge:RetentionDays", 30);

    protected override string Name => "StagingPurge";

    protected override async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var result = await services.GetRequiredService<PurgeStagingHandler>().HandleAsync(_retentionDays, ct);
        logger.LogInformation("Staging depurado: {Rows} filas de más de {Days} días", result.Value, _retentionDays);
    }
}
