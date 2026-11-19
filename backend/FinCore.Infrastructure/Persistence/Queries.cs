using System.Text.Json;
using FinCore.Application.Operations;
using FinCore.Application.Users;
using FinCore.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Infrastructure.Persistence;

/// <summary>Consultas de lectura de usuarios (sin seguimiento de cambios).</summary>
internal sealed class UserQueries(FinCoreDbContext db) : IUserQueries
{
    public async Task<IReadOnlyList<UserSummaryDto>> ListByCompanyAsync(int companyId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName, u.FullName, u.Email, u.IsActive, u.LockoutEndUtc, u.LastLoginAtUtc })
            .ToListAsync(cancellationToken);

        var ids = users.Select(u => u.Id).ToList();
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                           where ids.Contains(ur.UserId) select new { ur.UserId, r.Code }).ToListAsync(cancellationToken);
        var branches = await (from ub in db.UserBranches join b in db.Branches on ub.BranchId equals b.Id
                              where ids.Contains(ub.UserId) select new { ub.UserId, b.Code }).ToListAsync(cancellationToken);
        var accounts = await db.UserWindowsAccounts.Where(w => ids.Contains(w.UserId))
            .ToDictionaryAsync(w => w.UserId, w => w.WindowsAccount, cancellationToken);

        return users.Select(u => new UserSummaryDto(
                u.Id, u.UserName, u.FullName, u.Email, u.IsActive, u.LockoutEndUtc > nowUtc, u.LastLoginAtUtc,
                roles.Where(r => r.UserId == u.Id).Select(r => r.Code).ToList(),
                branches.Where(b => b.UserId == u.Id).Select(b => b.Code).OrderBy(c => c).ToList(),
                accounts.GetValueOrDefault(u.Id)))
            .ToList();
    }

    public async Task<UserDetailDto?> GetAsync(int companyId, int userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        if (user is null) return null;

        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                           where ur.UserId == userId orderby r.Id select r.Code).ToListAsync(cancellationToken);
        var branchIds = await db.UserBranches.Where(ub => ub.UserId == userId).Select(ub => ub.BranchId).ToListAsync(cancellationToken);
        var account = await db.UserWindowsAccounts.Where(w => w.UserId == userId).Select(w => w.WindowsAccount).SingleOrDefaultAsync(cancellationToken);

        return new UserDetailDto(user.Id, user.UserName, user.FullName, user.Email, user.IsActive, user.IsLockedOut(nowUtc),
            user.LastLoginAtUtc, user.CreatedAtUtc, roles, branchIds, Convert.ToBase64String(user.RowVersion), account);
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var permissions = await (from rp in db.RolePermissions join p in db.Permissions on rp.PermissionId equals p.Id
                                 select new { rp.RoleId, p.Code }).ToListAsync(cancellationToken);
        return roles.Select(r =>
        {
            var codes = permissions.Where(p => p.RoleId == r.Id).Select(p => p.Code).OrderBy(c => c).ToList();
            return new RoleDto(r.Code, r.Name, r.Description, codes, codes.Contains(Permissions.BranchesAll));
        }).ToList();
    }
}

/// <summary>Consultas de operaciones sobre la vista fin.vw_Transactions y las funciones de la base.</summary>
internal sealed class TransactionQueries(FinCoreDbContext db) : ITransactionQueries
{
    public async Task<PagedResult<TransactionDto>> SearchAsync(TransactionFilter filter, IReadOnlyCollection<int> branchIds, CancellationToken ct)
    {
        var query = db.TransactionView.AsNoTracking().Where(t => branchIds.Contains(t.BranchId));

        if (filter.BranchId is { } branchId) query = query.Where(t => t.BranchId == branchId);
        if (filter.AccountId is { } accountId) query = query.Where(t => t.AccountId == accountId);
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(t => t.StatusCode == filter.Status.ToUpper());
        if (!string.IsNullOrWhiteSpace(filter.Type)) query = query.Where(t => t.TransactionTypeCode == filter.Type.ToUpper());
        if (filter.From is { } from) query = query.Where(t => t.OperationDate >= from);
        if (filter.To is { } to) query = query.Where(t => t.OperationDate <= to);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(t => t.Reference.Contains(s) || (t.Description != null && t.Description.Contains(s))
                                     || (t.CounterpartyName != null && t.CounterpartyName.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(t => t.OperationDate).ThenByDescending(t => t.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<TransactionDto>(rows.Select(ToDto).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<TransactionDto?> GetAsync(Guid publicId, IReadOnlyCollection<int> branchIds, CancellationToken ct)
    {
        var row = await db.TransactionView.AsNoTracking()
            .SingleOrDefaultAsync(t => t.PublicId == publicId && branchIds.Contains(t.BranchId), ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<IReadOnlyList<TransactionHistoryDto>> GetHistoryAsync(Guid publicId, CancellationToken ct) =>
        (await db.Set<HistoryRow>().FromSql($"""
            SELECT fs.Name AS FromStatus, ts.Name AS ToStatus, h.ChangedAtUtc, u.UserName AS ChangedBy, h.CorrelationId, h.Reason
            FROM fin.TransactionStatusHistory h
            JOIN fin.Transactions t       ON t.Id = h.TransactionId
            JOIN fin.TransactionStatus ts ON ts.Id = h.ToStatusId
            LEFT JOIN fin.TransactionStatus fs ON fs.Id = h.FromStatusId
            LEFT JOIN sec.Users u         ON u.Id = h.ChangedByUserId
            WHERE t.PublicId = {publicId}
            """).ToListAsync(ct))
        .OrderBy(h => h.ChangedAtUtc)
        .Select(h => new TransactionHistoryDto(h.FromStatus, h.ToStatus, h.ChangedAtUtc, h.ChangedBy, h.CorrelationId, h.Reason))
        .ToList();

    public async Task<TransactionSummaryDto> SummaryAsync(IReadOnlyCollection<int> branchIds, DateOnly monthStart, DateOnly today, CancellationToken ct)
    {
        var scoped = db.TransactionView.AsNoTracking().Where(t => branchIds.Contains(t.BranchId));

        // Las pendientes cuentan sin importar la fecha (son trabajo por hacer); el resto, en el mes.
        var pending = await scoped.CountAsync(t => t.StatusId == 1, ct);
        var month = await scoped
            .Where(t => t.OperationDate >= monthStart && t.OperationDate <= today)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Processed = g.Count(t => t.StatusId == 2),
                Reconciled = g.Count(t => t.StatusId == 3),
                Rejected = g.Count(t => t.StatusId == 4),
                Reversed = g.Count(t => t.StatusId == 5),
                Income = g.Sum(t => t.IsEffective && t.SignedAmount > 0 ? t.SignedAmount : 0),
                Expense = g.Sum(t => t.IsEffective && t.SignedAmount < 0 ? -t.SignedAmount : 0),
            })
            .SingleOrDefaultAsync(ct);

        return new TransactionSummaryDto(pending, month?.Processed ?? 0, month?.Reconciled ?? 0, month?.Rejected ?? 0,
            month?.Reversed ?? 0, month?.Income ?? 0, month?.Expense ?? 0, (month?.Income ?? 0) - (month?.Expense ?? 0), monthStart);
    }

    public async Task<IReadOnlyList<AccountDto>> ListAccountsAsync(IReadOnlyCollection<int> branchIds, DateOnly asOf, CancellationToken ct)
    {
        // Las sucursales viajan como un solo parámetro JSON (OPENJSON): consulta parametrizada sin concatenar SQL.
        var branchesJson = JsonSerializer.Serialize(branchIds);
        var rows = await db.Set<AccountRow>().FromSql($"""
            SELECT a.Id, a.Code, a.Name, a.BankName, a.BankAccountNumber, a.BranchId,
                   b.Code AS BranchCode, b.Name AS BranchName, bal.Balance
            FROM fin.Accounts a
            JOIN sec.Branches b ON b.Id = a.BranchId
            CROSS APPLY fin.tvf_AccountBalance(a.Id, {asOf}) bal
            WHERE a.IsActive = 1 AND a.BranchId IN (SELECT CAST(value AS INT) FROM OPENJSON({branchesJson}))
            """).ToListAsync(ct);

        return rows.OrderBy(r => r.BranchName).ThenBy(r => r.Code)
            .Select(r => new AccountDto(r.Id, r.Code, r.Name, r.BankName, Mask(r.BankAccountNumber), r.BranchId, r.BranchCode, r.BranchName, r.Balance))
            .ToList();
    }

    /// <summary>Número de cuenta enmascarado: la interfaz no necesita el número completo.</summary>
    private static string Mask(string number) => number.Length <= 4 ? number : "•••• " + number[^4..];

    private static TransactionDto ToDto(TransactionViewRow t) => new(
        t.PublicId, t.Reference, t.OperationDate, t.BranchId, t.BranchCode, t.BranchName, t.AccountId, t.AccountCode,
        t.TransactionTypeCode, t.TransactionTypeName, t.StatusCode, t.StatusName, t.Amount, t.Direction, t.SignedAmount,
        t.Description, t.CounterpartyName, t.CounterpartyAccount, t.RejectionReason, t.CreatedByUserId, t.CreatedByUserName,
        t.CreatedAtUtc, t.ProcessedAtUtc, t.ReconciledAtUtc, t.OriginalPublicId, t.ReversalPublicId,
        Convert.ToBase64String(t.RowVersion));
}
