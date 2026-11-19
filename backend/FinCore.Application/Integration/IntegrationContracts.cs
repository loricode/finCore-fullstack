using FinCore.Application.Reconciliation;
using FinCore.Domain.Integration;
using FinCore.Domain.Operations;

namespace FinCore.Application.Integration;

// ------------------------------------------------------------------ webhooks (adaptador Node.js)

/// <summary>Movimiento bancario en el contrato de FinCore (el adaptador ya tradujo el formato del proveedor).</summary>
public sealed record BankEventMovement(
    string AccountNumber,
    string MovementId,
    string? Reference,
    DateOnly Date,
    decimal Amount,
    string? Description);

public sealed record ReceiveBankEventCommand(
    string Provider,
    string ExternalEventId,
    string EventType,
    DateTime? OccurredAt,
    BankEventMovement? Movement,
    string RawPayload);

public sealed record BankEventResult(long InboundEventId, string Status, long? BankStatementDetailId, bool Duplicate, string? Error);

public interface IInboundEventRepository
{
    Task<InboundEvent?> GetAsync(string provider, string externalEventId, CancellationToken ct);
    Task<IReadOnlyList<InboundEvent>> GetStaleReceivedAsync(DateTime receivedBeforeUtc, int max, CancellationToken ct);
    void Add(InboundEvent inboundEvent);
}

/// <summary>Escribe movimientos que llegan por webhook en el extracto WEBHOOK del día de la cuenta.</summary>
public interface IBankMovementStore
{
    /// <summary>Devuelve el Id del movimiento creado, o null si (cuenta, movimiento) ya existía (llegó por otro canal).</summary>
    Task<long?> AddWebhookMovementAsync(Account account, BankEventMovement movement, CancellationToken ct);
}

// ------------------------------------------------------------------ Oracle (sistema legado)

/// <summary>
/// Fila del sistema legado ya transformada al formato de staging (fecha yyyy-MM-dd y valor con punto decimal),
/// con la misma consulta que usa el paquete SSIS (etl/FinCore.Integration.SSIS/sql/oracle_extract_movements.sql).
/// </summary>
public sealed record LegacyMovement(
    long Sequence,
    string? AccountNumber,
    string? MovementId,
    string? Reference,
    string? BookingDate,
    string? Amount,
    string? Narrative);

public interface ILegacyBankReader
{
    /// <summary>Nombre del origen para la marca de agua y el historial ETL, p. ej. ORACLE:LEGACY_BANK.BANK_MOVEMENTS.</summary>
    string SourceName { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<LegacyMovement>> ReadAfterAsync(long lastSequence, int maxRows, CancellationToken ct);
}

public interface IEtlWatermarkStore
{
    Task<long> GetAsync(string sourceName, CancellationToken ct);
    Task SetAsync(string sourceName, long value, CancellationToken ct);
}

/// <summary>Resultado de una extracción incremental: null en Import si no había datos nuevos.</summary>
public sealed record LegacyImportResult(long PreviousWatermark, long NewWatermark, ImportResultDto? Import);

// ------------------------------------------------------------------ mantenimiento

public interface IMaintenanceProcedures
{
    Task<int> PurgeStagingAsync(int retentionDays, CancellationToken ct);
}
