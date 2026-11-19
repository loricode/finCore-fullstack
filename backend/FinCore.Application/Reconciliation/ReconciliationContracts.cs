using FinCore.Application.Operations;
using FinCore.Domain.Reconciliation;

namespace FinCore.Application.Reconciliation;

// ------------------------------------------------------------------ importación (ETL)

public sealed record EtlErrorDto(int RowNumber, string? SourceKey, string ErrorCode, string ErrorMessage);

public sealed record ImportResultDto(
    long EtlExecutionId,
    string Status,
    int RowsRead,
    int RowsLoaded,
    int RowsRejected,
    IReadOnlyList<EtlErrorDto> Errors);

public sealed record EtlExecutionDto(
    long EtlExecutionId,
    string PackageName,
    string SourceSystem,
    string? SourceName,
    string Status,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    int RowsRead,
    int RowsLoaded,
    int RowsRejected,
    string? ErrorCodes,
    string? ErrorMessage,
    string TriggeredBy);

/// <summary>Carga de un extracto: abre la ejecución ETL, llena staging y llama al procedimiento de promoción.</summary>
public interface IStatementImporter
{
    /// <param name="sourceSystem">CSV u ORACLE (etl.ETLExecutions.SourceSystem).</param>
    Task<ImportResultDto> ImportAsync(int companyId, string sourceSystem, string packageName, string sourceName,
        IReadOnlyList<RawBankMovement> rows, string triggeredBy, string correlationId, CancellationToken ct);
}

// ------------------------------------------------------------------ conciliación

public sealed record ReconciliationSummaryDto(
    int ReconciliationId,
    int AccountId,
    string AccountCode,
    int BranchId,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    byte DateToleranceDays,
    string Status,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    string? ExecutedBy,
    string? CorrelationId,
    int InternalCount,
    int BankCount,
    int MatchedCount,
    int DifferenceCount,
    int AmountMismatchCount,
    int DateMismatchCount,
    int MissingInBankCount,
    int MissingInternalCount,
    int DuplicateReferenceCount,
    int PendingReviewCount,
    int OpenDifferenceCount,
    decimal NetDifferenceAmount,
    string? ErrorMessage);

public sealed record ReconciliationDetailDto(
    long Id,
    string ResultTypeCode,
    string ResultTypeName,
    bool RequiresAction,
    Guid? TransactionPublicId,
    string? InternalReference,
    DateOnly? OperationDate,
    string? BankReference,
    DateOnly? MovementDate,
    string? BankDescription,
    decimal? InternalAmount,
    decimal? BankAmount,
    decimal DifferenceAmount,
    string? Notes,
    DateTime? ResolvedAtUtc,
    string? ResolvedBy,
    string? ResolutionNote);

public sealed record ReconciliationWithDetailsDto(ReconciliationSummaryDto Summary, IReadOnlyList<ReconciliationDetailDto> Details);

public sealed record OpenDifferenceDto(
    long ReconciliationDetailId,
    int ReconciliationId,
    int AccountId,
    string AccountCode,
    int BranchId,
    string BranchName,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string ResultTypeCode,
    string ResultTypeName,
    Guid? TransactionPublicId,
    string? InternalReference,
    DateOnly? OperationDate,
    string? BankReference,
    DateOnly? MovementDate,
    decimal? InternalAmount,
    decimal? BankAmount,
    decimal DifferenceAmount,
    string? Notes);

public sealed record RunReconciliationCommand(int AccountId, DateOnly PeriodFrom, DateOnly PeriodTo, byte DateToleranceDays = 1);

public sealed record ResolveDifferenceCommand(long DetailId, string? Note);

/// <summary>Ya hay una conciliación en curso para la cuenta (error 50104 del procedimiento).</summary>
public sealed class ReconciliationAlreadyRunningException(string message) : Exception(message);

/// <summary>
/// Ejecuta rec.usp_RunReconciliation (ADR-003). El 50104 se traduce a <see cref="ReconciliationAlreadyRunningException"/>;
/// los demás errores 5010x llegan como SqlException al manejador global.
/// </summary>
public interface IReconciliationEngine
{
    Task<int> RunAsync(RunReconciliationCommand command, int userId, string correlationId, CancellationToken ct);
}

public interface IReconciliationQueries
{
    Task<PagedResult<ReconciliationSummaryDto>> ListAsync(IReadOnlyCollection<int> branchIds, int? accountId, int page, int pageSize, CancellationToken ct);
    Task<ReconciliationSummaryDto?> GetSummaryAsync(int reconciliationId, IReadOnlyCollection<int> branchIds, CancellationToken ct);
    Task<IReadOnlyList<ReconciliationDetailDto>> GetDetailsAsync(int reconciliationId, CancellationToken ct);
    Task<IReadOnlyList<OpenDifferenceDto>> GetOpenDifferencesAsync(IReadOnlyCollection<int> branchIds, CancellationToken ct);
    Task<PagedResult<EtlExecutionDto>> ListEtlExecutionsAsync(int companyId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<EtlErrorDto>> GetEtlErrorsAsync(int companyId, long etlExecutionId, int max, CancellationToken ct);
}

public interface IReconciliationDetailRepository
{
    /// <summary>El detalle con la sucursal de su cuenta (para validar el alcance).</summary>
    Task<(ReconciliationDetail Detail, int BranchId)?> GetWithBranchAsync(long detailId, CancellationToken ct);
}
