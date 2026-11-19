using FinCore.Domain.Operations;
using FinCore.Domain.Security;

namespace FinCore.Application.Common.Abstractions;

public interface IUserRepository
{
    /// <summary>Busca sin distinguir mayúsculas (la columna usa intercalación CI).</summary>
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, int? excludingUserId, CancellationToken cancellationToken);
    void Add(User user);

    /// <summary>Fija el RowVersion que el cliente leyó: si cambió en la base, SaveChanges falla por concurrencia (409).</summary>
    void SetExpectedRowVersion(User user, byte[] rowVersion);
}

public sealed record RoleInfo(short Id, string Code);

/// <summary>Roles y sucursales asignados a un usuario (sec.UserRoles, sec.UserBranches).</summary>
public interface IUserAssignments
{
    Task<IReadOnlyList<RoleInfo>> GetRolesByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetPermissionsOfRolesAsync(IReadOnlyCollection<short> roleIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetActiveBranchIdsAsync(int companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetRoleCodesAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetBranchIdsAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Deja exactamente estos roles (solo agrega y quita las diferencias). El usuario ya debe tener Id.</summary>
    Task ReplaceRolesAsync(User user, IReadOnlyCollection<short> roleIds, int assignedByUserId, CancellationToken cancellationToken);

    /// <summary>Deja exactamente estas sucursales (solo agrega y quita las diferencias). El usuario ya debe tener Id.</summary>
    Task ReplaceBranchesAsync(User user, IReadOnlyCollection<int> branchIds, int assignedByUserId, CancellationToken cancellationToken);

    /// <summary>Cuenta de Windows asociada al usuario para Reporting Services (sec.UserWindowsAccounts).</summary>
    Task<string?> GetWindowsAccountAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Usuario que ya tiene asociada esa cuenta (sin distinguir mayúsculas), si existe.</summary>
    Task<int?> FindUserByWindowsAccountAsync(string windowsAccount, CancellationToken cancellationToken);

    /// <summary>Asocia, cambia o (con null) quita la cuenta de Windows del usuario.</summary>
    Task SetWindowsAccountAsync(int userId, string? windowsAccount, int assignedByUserId, CancellationToken cancellationToken);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(int userId, DateTime nowUtc, CancellationToken cancellationToken);
    void Add(RefreshToken token);
}

public sealed record AuthorizedBranch(int BranchId, string Code, string Name, string City);

/// <summary>Consultas de autorización. Reutilizan las funciones de la base (sec.tvf_*) para tener un solo criterio.</summary>
public interface IAuthorizationReader
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetRoleCodesAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuthorizedBranch>> GetAuthorizedBranchesAsync(int userId, CancellationToken cancellationToken);
}

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Account?> GetByBankAccountNumberAsync(int companyId, string bankAccountNumber, CancellationToken cancellationToken);
}

public interface ITransactionRepository
{
    Task<Transaction?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken);
    Task<Transaction?> GetByIdempotencyKeyAsync(int companyId, string key, CancellationToken cancellationToken);
    Task<bool> ReferenceExistsAsync(int companyId, string reference, CancellationToken cancellationToken);
    void Add(Transaction transaction);
    void SetExpectedRowVersion(Transaction transaction, byte[] rowVersion);
}
