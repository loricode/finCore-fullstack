using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Security;

namespace FinCore.Application.Users;

/// <summary>
/// Reglas de asignación de roles y sucursales (HU-02, HU-04):
///  - Los roles deben existir.
///  - Las sucursales deben ser sucursales activas de la empresa del administrador.
///  - Un usuario sin el permiso branches.all necesita al menos una sucursal (si no, no vería nada).
///  - Un administrador no puede quitarse a sí mismo el permiso users.manage (no puede dejarse sin acceso).
/// </summary>
public sealed class UserAssignmentPolicy(IUserAssignments assignments)
{
    public sealed record Resolved(IReadOnlyList<short> RoleIds, IReadOnlyList<string> RoleCodes, IReadOnlyList<int> BranchIds);

    public async Task<Result<Resolved>> ResolveAsync(
        IReadOnlyList<string> roleCodes, IReadOnlyList<int> branchIds, int companyId,
        bool isSelf, CancellationToken ct)
    {
        var codes = roleCodes.Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        var roles = await assignments.GetRolesByCodesAsync(codes, ct);
        var unknown = codes.Except(roles.Select(r => r.Code)).ToList();
        if (unknown.Count > 0)
            return Validation("roles", $"Roles inexistentes: {string.Join(", ", unknown)}.");

        var requested = branchIds.Distinct().ToList();
        var companyBranches = await assignments.GetActiveBranchIdsAsync(companyId, ct);
        if (requested.Except(companyBranches).Any())
            return Validation("branchIds", "Alguna sucursal no existe, está inactiva o es de otra empresa.");

        var permissions = await assignments.GetPermissionsOfRolesAsync(roles.Select(r => r.Id).ToList(), ct);
        if (!permissions.Contains(Permissions.BranchesAll) && requested.Count == 0)
            return Validation("branchIds", "Asigna al menos una sucursal: los roles elegidos no dan acceso a todas.");

        if (isSelf && !permissions.Contains(Permissions.UsersManage))
            return Error.Conflict("user.cannot_remove_own_admin", "No puedes quitarte el permiso de administrar usuarios.");

        return new Resolved(roles.Select(r => r.Id).ToList(), roles.Select(r => r.Code).ToList(), requested);
    }

    private static Error Validation(string field, string message) => new($"validation.{field}", message, ErrorType.Validation);
}
