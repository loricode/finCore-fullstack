using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Operations;
using FinCore.Domain.Organization;
using FinCore.Domain.Reconciliation;
using FinCore.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core sobre la base FinCore. MAPEA las tablas creadas por database/ (ADR-002):
/// no se usan migraciones ni EnsureCreated. Si una configuración no coincide con la tabla,
/// lo detecta la prueba de integración.
/// </summary>
public sealed class FinCoreDbContext(DbContextOptions<FinCoreDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ReconciliationDetail> ReconciliationDetails => Set<ReconciliationDetail>();

    internal DbSet<RoleRecord> Roles => Set<RoleRecord>();
    internal DbSet<PermissionRecord> Permissions => Set<PermissionRecord>();
    internal DbSet<RolePermissionRecord> RolePermissions => Set<RolePermissionRecord>();
    internal DbSet<UserRoleRecord> UserRoles => Set<UserRoleRecord>();
    internal DbSet<UserBranchRecord> UserBranches => Set<UserBranchRecord>();
    internal DbSet<UserWindowsAccountRecord> UserWindowsAccounts => Set<UserWindowsAccountRecord>();
    internal DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();
    internal DbSet<TransactionViewRow> TransactionView => Set<TransactionViewRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinCoreDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // La base usa DATETIME2(3) para instantes y DECIMAL(18,2) para dinero.
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Transacción explícita dentro de la estrategia de reintentos de SQL Server: si hay un error transitorio,
    /// se repite todo el bloque, nunca una parte.
    /// </summary>
    public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken) =>
        Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await Database.BeginTransactionAsync(ct);
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
}
