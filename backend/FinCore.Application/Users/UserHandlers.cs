using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Security;
using FluentValidation;

namespace FinCore.Application.Users;

/// <summary>GET /users · Usuarios de la empresa del administrador.</summary>
public sealed class GetUsersHandler(ICurrentUser currentUser, IUserQueries queries, TimeProvider clock)
{
    public async Task<Result<IReadOnlyList<UserSummaryDto>>> HandleAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetSession(out _, out var companyId)) return Guard.NotAuthenticated;
        var list = await queries.ListByCompanyAsync(companyId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        return Result<IReadOnlyList<UserSummaryDto>>.From(list);
    }
}

/// <summary>GET /users/{id} · Detalle para editar (roles, sucursales y versión).</summary>
public sealed class GetUserHandler(ICurrentUser currentUser, IUserQueries queries, TimeProvider clock)
{
    public async Task<Result<UserDetailDto>> HandleAsync(int userId, CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetSession(out _, out var companyId)) return Guard.NotAuthenticated;
        var user = await queries.GetAsync(companyId, userId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        return user is null ? UserErrors.NotFound : user;
    }
}

/// <summary>GET /roles · Catálogo de roles con sus permisos, para el formulario de usuarios.</summary>
public sealed class GetRolesHandler(IUserQueries queries)
{
    public async Task<Result<IReadOnlyList<RoleDto>>> HandleAsync(CancellationToken cancellationToken) =>
        Result<IReadOnlyList<RoleDto>>.From(await queries.ListRolesAsync(cancellationToken));
}

/// <summary>HU-02 · Crear usuario con roles y sucursales. Audita USER_CREATED (sin contraseña).</summary>
public sealed class CreateUserHandler(
    IValidator<CreateUserCommand> validator,
    ICurrentUser currentUser,
    IUserRepository users,
    IUserAssignments assignments,
    UserAssignmentPolicy policy,
    IPasswordHasher passwordHasher,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<int>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        if (!currentUser.TryGetSession(out var adminId, out var companyId)) return Guard.NotAuthenticated;

        var userName = command.UserName.Trim().ToLowerInvariant();
        if (await users.UserNameExistsAsync(userName, cancellationToken))
            return Error.Conflict("user.duplicate_username", $"Ya existe el usuario {userName}.");
        if (await users.EmailExistsAsync(command.Email.Trim(), null, cancellationToken))
            return Error.Conflict("user.duplicate_email", "Ya existe un usuario con ese correo.");

        var resolved = await policy.ResolveAsync(command.Roles, command.BranchIds, companyId, isSelf: false, cancellationToken);
        if (!resolved.IsSuccess) return resolved.Error!;

        var now = clock.GetUtcNow().UtcDateTime;
        var user = User.Create(companyId, userName, command.Email, command.FullName, passwordHasher.Hash(command.Password), now);

        // Todo o nada: el usuario, sus roles, sus sucursales y la auditoría.
        return await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            users.Add(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);   // SQL Server asigna el Id

            await assignments.ReplaceRolesAsync(user, resolved.Value.RoleIds, adminId, cancellationToken);
            await assignments.ReplaceBranchesAsync(user, resolved.Value.BranchIds, adminId, cancellationToken);
            auditLog.Record(new AuditEntry(AuditActions.UserCreated, "User", user.Id.ToString(), adminId, currentUser.UserName, companyId,
                new { user.UserName, user.Email, user.FullName, roles = resolved.Value.RoleCodes, branchIds = resolved.Value.BranchIds }));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<int>.From(user.Id);
        }, cancellationToken);
    }
}

/// <summary>HU-02 · Editar perfil, roles y sucursales con concurrencia optimista. Audita valores antes y después.</summary>
public sealed class UpdateUserHandler(
    IValidator<UpdateUserCommand> validator,
    ICurrentUser currentUser,
    IUserRepository users,
    IUserAssignments assignments,
    UserAssignmentPolicy policy,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        if (!currentUser.TryGetSession(out var adminId, out var companyId)) return Guard.NotAuthenticated;

        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId) return UserErrors.NotFound;

        var rowVersion = Guard.FromBase64(command.RowVersion);
        if (rowVersion is null) return new Error("validation.rowVersion", "La versión del registro no es válida.", ErrorType.Validation);
        users.SetExpectedRowVersion(user, rowVersion);

        if (await users.EmailExistsAsync(command.Email.Trim(), user.Id, cancellationToken))
            return Error.Conflict("user.duplicate_email", "Ya existe un usuario con ese correo.");

        var resolved = await policy.ResolveAsync(command.Roles, command.BranchIds, companyId, isSelf: user.Id == adminId, cancellationToken);
        if (!resolved.IsSuccess) return resolved.Error!;

        var before = new
        {
            user.FullName, user.Email,
            roles = await assignments.GetRoleCodesAsync(user.Id, cancellationToken),
            branchIds = await assignments.GetBranchIdsAsync(user.Id, cancellationToken),
        };

        user.UpdateProfile(command.FullName, command.Email, clock.GetUtcNow().UtcDateTime);
        await assignments.ReplaceRolesAsync(user, resolved.Value.RoleIds, adminId, cancellationToken);
        await assignments.ReplaceBranchesAsync(user, resolved.Value.BranchIds, adminId, cancellationToken);

        auditLog.Record(new AuditEntry(AuditActions.UserUpdated, "User", user.Id.ToString(), adminId, currentUser.UserName, companyId,
            new { user.FullName, user.Email, roles = resolved.Value.RoleCodes, branchIds = resolved.Value.BranchIds }, before));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>HU-04 · Asignar sucursales a un usuario.</summary>
public sealed class UpdateUserBranchesHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IUserAssignments assignments,
    UserAssignmentPolicy policy,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<Unit>> HandleAsync(UpdateUserBranchesCommand command, CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetSession(out var adminId, out var companyId)) return Guard.NotAuthenticated;
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId) return UserErrors.NotFound;

        var roles = await assignments.GetRoleCodesAsync(user.Id, cancellationToken);
        var resolved = await policy.ResolveAsync(roles, command.BranchIds ?? [], companyId, user.Id == adminId, cancellationToken);
        if (!resolved.IsSuccess) return resolved.Error!;

        var before = await assignments.GetBranchIdsAsync(user.Id, cancellationToken);
        await assignments.ReplaceBranchesAsync(user, resolved.Value.BranchIds, adminId, cancellationToken);
        auditLog.Record(new AuditEntry(AuditActions.UserBranchesChanged, "User", user.Id.ToString(), adminId, currentUser.UserName, companyId,
            new { branchIds = resolved.Value.BranchIds }, new { branchIds = before }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>HU-02 · Activar o desactivar. Desactivar revoca todas las sesiones del usuario.</summary>
public sealed class SetUserActiveHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(int userId, bool active, CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetSession(out var adminId, out var companyId)) return Guard.NotAuthenticated;
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.CompanyId != companyId) return UserErrors.NotFound;

        var now = clock.GetUtcNow().UtcDateTime;
        if (active)
        {
            user.Activate(now);
        }
        else
        {
            user.Deactivate(adminId, now);
            foreach (var token in await refreshTokens.GetActiveByUserAsync(user.Id, now, cancellationToken))
                token.Revoke(now);
        }

        auditLog.Record(new AuditEntry(active ? AuditActions.UserActivated : AuditActions.UserDeactivated, "User",
            user.Id.ToString(), adminId, currentUser.UserName, companyId, new { user.UserName }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// Etapa 6 · Asocia la cuenta de Windows con la que el usuario abre los informes de Reporting Services.
/// SSRS solo le ofrece "Ver como" ese usuario (o todos, si tiene reports.viewas). Audita el antes y el después.
/// </summary>
public sealed class SetUserWindowsAccountHandler(
    IValidator<SetUserWindowsAccountCommand> validator,
    ICurrentUser currentUser,
    IUserRepository users,
    IUserAssignments assignments,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<Unit>> HandleAsync(SetUserWindowsAccountCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        if (!currentUser.TryGetSession(out var adminId, out var companyId)) return Guard.NotAuthenticated;
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId) return UserErrors.NotFound;

        var account = string.IsNullOrWhiteSpace(command.WindowsAccount) ? null : command.WindowsAccount.Trim();
        if (account is not null)
        {
            var owner = await assignments.FindUserByWindowsAccountAsync(account, cancellationToken);
            if (owner is not null && owner != user.Id)
                return Error.Conflict("user.windows_account_taken", $"La cuenta {account} ya está asociada a otro usuario.");
        }

        var before = await assignments.GetWindowsAccountAsync(user.Id, cancellationToken);
        if (string.Equals(before, account, StringComparison.OrdinalIgnoreCase)) return Unit.Value;

        await assignments.SetWindowsAccountAsync(user.Id, account, adminId, cancellationToken);
        auditLog.Record(new AuditEntry(AuditActions.UserWindowsAccountChanged, "User", user.Id.ToString(), adminId, currentUser.UserName,
            companyId, new { windowsAccount = account }, new { windowsAccount = before }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

internal static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound("user.not_found", "El usuario no existe.");
}
