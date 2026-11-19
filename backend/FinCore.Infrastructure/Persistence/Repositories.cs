using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Operations;
using FinCore.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Infrastructure.Persistence;

internal sealed class UserRepository(FinCoreDbContext db) : IUserRepository
{
    public Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.UserName == userName, cancellationToken);

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.UserName == userName, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, int? excludingUserId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == email && (excludingUserId == null || u.Id != excludingUserId), cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    public void SetExpectedRowVersion(User user, byte[] rowVersion) =>
        db.Entry(user).Property(u => u.RowVersion).OriginalValue = rowVersion;
}

internal sealed class UserAssignments(FinCoreDbContext db) : IUserAssignments
{
    public async Task<IReadOnlyList<RoleInfo>> GetRolesByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct) =>
        await db.Roles.Where(r => codes.Contains(r.Code)).Select(r => new RoleInfo(r.Id, r.Code)).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetPermissionsOfRolesAsync(IReadOnlyCollection<short> roleIds, CancellationToken ct) =>
        await (from rp in db.RolePermissions
               join p in db.Permissions on rp.PermissionId equals p.Id
               where roleIds.Contains(rp.RoleId)
               select p.Code).Distinct().ToListAsync(ct);

    public async Task<IReadOnlyList<int>> GetActiveBranchIdsAsync(int companyId, CancellationToken ct) =>
        await db.Branches.Where(b => b.CompanyId == companyId && b.IsActive).Select(b => b.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetRoleCodesAsync(int userId, CancellationToken ct) =>
        await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
               where ur.UserId == userId orderby r.Id select r.Code).ToListAsync(ct);

    public async Task<IReadOnlyList<int>> GetBranchIdsAsync(int userId, CancellationToken ct) =>
        await db.UserBranches.Where(ub => ub.UserId == userId).OrderBy(ub => ub.BranchId).Select(ub => ub.BranchId).ToListAsync(ct);

    public async Task ReplaceRolesAsync(User user, IReadOnlyCollection<short> roleIds, int assignedByUserId, CancellationToken ct)
    {
        var current = await db.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync(ct);
        db.UserRoles.RemoveRange(current.Where(c => !roleIds.Contains(c.RoleId)));
        foreach (var roleId in roleIds.Except(current.Select(c => c.RoleId)))
            db.UserRoles.Add(new UserRoleRecord { UserId = user.Id, RoleId = roleId, AssignedByUserId = assignedByUserId });
    }

    public async Task ReplaceBranchesAsync(User user, IReadOnlyCollection<int> branchIds, int assignedByUserId, CancellationToken ct)
    {
        var current = await db.UserBranches.Where(ub => ub.UserId == user.Id).ToListAsync(ct);
        db.UserBranches.RemoveRange(current.Where(c => !branchIds.Contains(c.BranchId)));
        foreach (var branchId in branchIds.Except(current.Select(c => c.BranchId)))
            db.UserBranches.Add(new UserBranchRecord
            {
                UserId = user.Id, BranchId = branchId, CompanyId = user.CompanyId, AssignedByUserId = assignedByUserId,
            });
    }

    public Task<string?> GetWindowsAccountAsync(int userId, CancellationToken ct) =>
        db.UserWindowsAccounts.Where(w => w.UserId == userId).Select(w => w.WindowsAccount).SingleOrDefaultAsync(ct);

    // La intercalación de la base no distingue mayúsculas: la comparación la hace SQL Server.
    public Task<int?> FindUserByWindowsAccountAsync(string windowsAccount, CancellationToken ct) =>
        db.UserWindowsAccounts.Where(w => w.WindowsAccount == windowsAccount).Select(w => (int?)w.UserId).SingleOrDefaultAsync(ct);

    public async Task SetWindowsAccountAsync(int userId, string? windowsAccount, int assignedByUserId, CancellationToken ct)
    {
        var current = await db.UserWindowsAccounts.SingleOrDefaultAsync(w => w.UserId == userId, ct);
        if (windowsAccount is null)
        {
            if (current is not null) db.UserWindowsAccounts.Remove(current);
            return;
        }

        if (current is null)
        {
            db.UserWindowsAccounts.Add(new UserWindowsAccountRecord { UserId = userId, WindowsAccount = windowsAccount, AssignedByUserId = assignedByUserId });
            return;
        }

        current.WindowsAccount = windowsAccount;
        current.AssignedByUserId = assignedByUserId;
        current.AssignedAtUtc = DateTime.UtcNow;
    }
}

internal sealed class RefreshTokenRepository(FinCoreDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(int userId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > nowUtc)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}

internal sealed class AuthorizationReader(FinCoreDbContext db) : IAuthorizationReader
{
    public async Task<IReadOnlyList<string>> GetPermissionsAsync(int userId, CancellationToken cancellationToken) =>
        await (from ur in db.UserRoles
               join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
               join p in db.Permissions on rp.PermissionId equals p.Id
               where ur.UserId == userId
               select p.Code)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetRoleCodesAsync(int userId, CancellationToken cancellationToken) =>
        await (from ur in db.UserRoles
               join r in db.Roles on ur.RoleId equals r.Id
               where ur.UserId == userId
               orderby r.Id
               select r.Code)
            .ToListAsync(cancellationToken);

    /// <summary>Usa sec.tvf_AuthorizedBranches: el mismo criterio de alcance que aplican los reportes rpt.*.</summary>
    public async Task<IReadOnlyList<AuthorizedBranch>> GetAuthorizedBranchesAsync(int userId, CancellationToken cancellationToken) =>
        await db.Set<AuthorizedBranchRow>()
            .FromSql($"SELECT BranchId, CompanyId, Code, Name, City FROM sec.tvf_AuthorizedBranches({userId})")
            .OrderBy(b => b.Name)
            .Select(b => new AuthorizedBranch(b.BranchId, b.Code, b.Name, b.City))
            .ToListAsync(cancellationToken);
}

internal sealed class AccountRepository(FinCoreDbContext db) : IAccountRepository
{
    public Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Account?> GetByBankAccountNumberAsync(int companyId, string bankAccountNumber, CancellationToken cancellationToken) =>
        db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.CompanyId == companyId && a.BankAccountNumber == bankAccountNumber, cancellationToken);
}

internal sealed class TransactionRepository(FinCoreDbContext db) : ITransactionRepository
{
    public Task<Transaction?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken) =>
        db.Transactions.SingleOrDefaultAsync(t => t.PublicId == publicId, cancellationToken);

    public Task<Transaction?> GetByIdempotencyKeyAsync(int companyId, string key, CancellationToken cancellationToken) =>
        db.Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.CompanyId == companyId && t.IdempotencyKey == key, cancellationToken);

    public Task<bool> ReferenceExistsAsync(int companyId, string reference, CancellationToken cancellationToken) =>
        db.Transactions.AnyAsync(t => t.CompanyId == companyId && t.Reference == reference, cancellationToken);

    public void Add(Transaction transaction) => db.Transactions.Add(transaction);

    public void SetExpectedRowVersion(Transaction transaction, byte[] rowVersion) =>
        db.Entry(transaction).Property(t => t.RowVersion).OriginalValue = rowVersion;
}
