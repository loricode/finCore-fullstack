using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Common;
using FinCore.Domain.Operations;
using FinCore.Domain.Security;
using FluentValidation;

namespace FinCore.Application.Operations;

/// <summary>HU-08 · Buscar operaciones con filtros y paginación, solo en sucursales autorizadas.</summary>
public sealed class SearchTransactionsHandler(IValidator<TransactionFilter> validator, OperationScope scope, ITransactionQueries queries)
{
    public async Task<Result<PagedResult<TransactionDto>>> HandleAsync(TransactionFilter filter, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(filter, ct);
        var branches = await scope.AuthorizedBranchIdsAsync(ct);
        if (filter.BranchId is { } requested && !branches.Contains(requested))
            return new PagedResult<TransactionDto>([], filter.Page, filter.PageSize, 0);
        return await queries.SearchAsync(filter, branches, ct);
    }
}

/// <summary>Detalle con historial de estados y acciones disponibles para el usuario.</summary>
public sealed class GetTransactionHandler(OperationScope scope, ITransactionQueries queries, ICurrentUser currentUser)
{
    public async Task<Result<TransactionDetailDto>> HandleAsync(Guid publicId, CancellationToken ct)
    {
        var branches = await scope.AuthorizedBranchIdsAsync(ct);
        var dto = await queries.GetAsync(publicId, branches, ct);
        if (dto is null) return OperationScope.NotFound;

        var history = await queries.GetHistoryAsync(publicId, ct);
        return new TransactionDetailDto(dto, history, ActionsFor(dto));
    }

    private TransactionActions ActionsFor(TransactionDto t)
    {
        var pending = t.StatusCode == "PENDING";
        var reversible = t.StatusCode is "PROCESSED" or "RECONCILED" && t.TypeCode != "REV";
        var canProcessPermission = currentUser.HasPermission(Permissions.OperationsProcess);
        var dualControlBlocks = t.Amount >= TransactionRules.DualControlThreshold && t.CreatedByUserId == currentUser.UserId;
        return new TransactionActions(
            CanEdit: pending && currentUser.HasPermission(Permissions.OperationsCreate)
                     && (t.CreatedByUserId == currentUser.UserId || canProcessPermission),
            CanProcess: pending && canProcessPermission && !dualControlBlocks,
            CanReject: pending && currentUser.HasPermission(Permissions.OperationsReject),
            CanReverse: reversible && currentUser.HasPermission(Permissions.OperationsReverse));
    }
}

/// <summary>Indicadores de inicio: operaciones por estado y movimiento del mes en las sucursales autorizadas.</summary>
public sealed class GetTransactionSummaryHandler(OperationScope scope, ITransactionQueries queries, TimeProvider clock)
{
    public async Task<Result<TransactionSummaryDto>> HandleAsync(CancellationToken ct)
    {
        var today = BusinessCalendar.Today(clock.GetUtcNow().UtcDateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        return await queries.SummaryAsync(await scope.AuthorizedBranchIdsAsync(ct), monthStart, today, ct);
    }
}

/// <summary>Cuentas de las sucursales autorizadas con su saldo a hoy (fin.tvf_AccountBalance).</summary>
public sealed class GetAccountsHandler(OperationScope scope, ITransactionQueries queries, TimeProvider clock)
{
    public async Task<Result<IReadOnlyList<AccountDto>>> HandleAsync(CancellationToken ct)
    {
        var today = BusinessCalendar.Today(clock.GetUtcNow().UtcDateTime);
        return Result<IReadOnlyList<AccountDto>>.From(await queries.ListAccountsAsync(await scope.AuthorizedBranchIdsAsync(ct), today, ct));
    }
}
