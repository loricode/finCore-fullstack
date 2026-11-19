using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;

namespace FinCore.Application.Organization;

/// <summary>
/// GET /branches · Sucursales que el usuario puede consultar (HU-04, RN-09).
/// El alcance lo calcula sec.tvf_AuthorizedBranches, la misma función que usan los reportes.
/// </summary>
public sealed class GetAuthorizedBranchesHandler(ICurrentUser currentUser, IAuthorizationReader authorization)
{
    public async Task<Result<IReadOnlyList<AuthorizedBranch>>> HandleAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Error.Unauthorized("auth.not_authenticated", "No hay una sesión activa.");

        var branches = await authorization.GetAuthorizedBranchesAsync(userId, cancellationToken);
        return Result<IReadOnlyList<AuthorizedBranch>>.From(branches);
    }
}
