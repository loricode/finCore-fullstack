using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;

namespace FinCore.Application.Auth.Me;

public sealed record CurrentUserDto(
    int Id,
    string UserName,
    string FullName,
    string Email,
    int CompanyId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<AuthorizedBranch> Branches);

/// <summary>
/// GET /auth/me · Usuario autenticado con sus roles, permisos y sucursales autorizadas.
/// Angular lo usa para armar el menú; la API sigue validando cada petición.
/// </summary>
public sealed class GetCurrentUserHandler(ICurrentUser currentUser, IUserRepository users, IAuthorizationReader authorization)
{
    public async Task<Result<CurrentUserDto>> HandleAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Error.Unauthorized("auth.not_authenticated", "No hay una sesión activa.");

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            return Error.Unauthorized("auth.not_authenticated", "No hay una sesión activa.");

        var roles = await authorization.GetRoleCodesAsync(userId, cancellationToken);
        var permissions = await authorization.GetPermissionsAsync(userId, cancellationToken);
        var branches = await authorization.GetAuthorizedBranchesAsync(userId, cancellationToken);

        return new CurrentUserDto(user.Id, user.UserName, user.FullName, user.Email, user.CompanyId, roles, permissions, branches);
    }
}
