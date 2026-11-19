using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Operations;

namespace FinCore.Application.Operations;

/// <summary>
/// Alcance por sucursal (RN-09) para operaciones: qué sucursales ve el usuario y carga de una operación
/// solo si pertenece a una de ellas. Fuera del alcance responde 404 (no revela que existe).
/// </summary>
public sealed class OperationScope(ICurrentUser currentUser, IAuthorizationReader authorization, ITransactionRepository transactions)
{
    public static readonly Error NotFound = Error.NotFound("transaction.not_found", "La operación no existe.");

    public async Task<IReadOnlyList<int>> AuthorizedBranchIdsAsync(CancellationToken ct) =>
        currentUser.UserId is { } userId
            ? (await authorization.GetAuthorizedBranchesAsync(userId, ct)).Select(b => b.BranchId).ToList()
            : [];

    public async Task<Result<Transaction>> LoadAsync(Guid publicId, CancellationToken ct)
    {
        var transaction = await transactions.GetByPublicIdAsync(publicId, ct);
        if (transaction is null || transaction.CompanyId != currentUser.CompanyId)
            return NotFound;

        var branches = await AuthorizedBranchIdsAsync(ct);
        return branches.Contains(transaction.BranchId) ? transaction : NotFound;
    }
}
