using FinCore.Domain.Operations;

namespace FinCore.Application.Operations;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record TransactionFilter(
    int? BranchId = null,
    int? AccountId = null,
    string? Status = null,
    string? Type = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record TransactionDto(
    Guid PublicId,
    string Reference,
    DateOnly OperationDate,
    int BranchId,
    string BranchCode,
    string BranchName,
    int AccountId,
    string AccountCode,
    string TypeCode,
    string TypeName,
    string StatusCode,
    string StatusName,
    decimal Amount,
    short Direction,
    decimal SignedAmount,
    string? Description,
    string? CounterpartyName,
    string? CounterpartyAccount,
    string? RejectionReason,
    int CreatedByUserId,
    string CreatedByUserName,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    DateTime? ReconciledAtUtc,
    Guid? OriginalPublicId,
    Guid? ReversalPublicId,
    string RowVersion);

public sealed record TransactionHistoryDto(
    string? FromStatus,
    string ToStatus,
    DateTime ChangedAtUtc,
    string? ChangedBy,
    string? CorrelationId,
    string? Reason);

/// <summary>Acciones que el usuario puede hacer ahora sobre la operación (según estado y permisos).</summary>
public sealed record TransactionActions(bool CanEdit, bool CanProcess, bool CanReject, bool CanReverse);

public sealed record TransactionDetailDto(TransactionDto Transaction, IReadOnlyList<TransactionHistoryDto> History, TransactionActions Actions);

public sealed record TransactionSummaryDto(
    int PendingCount,
    int ProcessedCount,
    int ReconciledCount,
    int RejectedCount,
    int ReversedCount,
    decimal MonthIncome,
    decimal MonthExpense,
    decimal MonthNet,
    DateOnly MonthStart);

public sealed record AccountDto(
    int Id,
    string Code,
    string Name,
    string BankName,
    string BankAccountNumberMasked,
    int BranchId,
    string BranchCode,
    string BranchName,
    decimal Balance);

/// <summary>Consultas de lectura de operaciones. Siempre filtran por las sucursales autorizadas que recibe.</summary>
public interface ITransactionQueries
{
    Task<PagedResult<TransactionDto>> SearchAsync(TransactionFilter filter, IReadOnlyCollection<int> branchIds, CancellationToken ct);
    Task<TransactionDto?> GetAsync(Guid publicId, IReadOnlyCollection<int> branchIds, CancellationToken ct);
    Task<IReadOnlyList<TransactionHistoryDto>> GetHistoryAsync(Guid publicId, CancellationToken ct);
    Task<TransactionSummaryDto> SummaryAsync(IReadOnlyCollection<int> branchIds, DateOnly monthStart, DateOnly today, CancellationToken ct);
    Task<IReadOnlyList<AccountDto>> ListAccountsAsync(IReadOnlyCollection<int> branchIds, DateOnly asOf, CancellationToken ct);
}

public sealed record RegisterTransactionCommand(
    int AccountId,
    string Type,
    string Reference,
    DateOnly OperationDate,
    decimal Amount,
    string? Description,
    string? CounterpartyName,
    string? CounterpartyAccount,
    string? IdempotencyKey);

/// <summary>Created = false cuando la Idempotency-Key ya existía (reintento del cliente).</summary>
public sealed record RegisterTransactionResult(Guid PublicId, bool Created);

public sealed record UpdateTransactionCommand(
    Guid PublicId,
    DateOnly OperationDate,
    decimal Amount,
    string? Description,
    string? CounterpartyName,
    string? CounterpartyAccount,
    string RowVersion);

public sealed record ChangeTransactionStatusCommand(Guid PublicId, string? Reason = null);

public static class TransactionCodes
{
    public static readonly IReadOnlyDictionary<string, TransactionType> RegistrableTypes = new Dictionary<string, TransactionType>
    {
        ["ING"] = TransactionType.Income,
        ["EGR"] = TransactionType.Expense,
        ["TRF"] = TransactionType.Transfer,
        ["PAG"] = TransactionType.Payment,
    };

    public static readonly IReadOnlySet<string> Statuses = new HashSet<string>
        { "PENDING", "PROCESSED", "RECONCILED", "REJECTED", "REVERSED" };

    public static readonly IReadOnlySet<string> AllTypes = new HashSet<string> { "ING", "EGR", "TRF", "PAG", "REV" };
}
