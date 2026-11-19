using FinCore.Domain.Organization;
using FinCore.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Infrastructure.Persistence.Configurations;

// Cada configuración refleja el script de database/002_Tables correspondiente.

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users", "sec");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserName).HasMaxLength(50).IsRequired();
        b.Property(x => x.Email).HasMaxLength(150).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens", "sec");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        b.Property(x => x.CreatedByIp).HasMaxLength(45);
        b.Ignore(x => x.IsRevoked);
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("Branches", "sec");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.City).HasMaxLength(80).IsRequired();
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<RoleRecord>
{
    public void Configure(EntityTypeBuilder<RoleRecord> b)
    {
        b.ToTable("Roles", "sec");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Name).HasMaxLength(80);
        b.Property(x => x.Description).HasMaxLength(250);
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<PermissionRecord>
{
    public void Configure(EntityTypeBuilder<PermissionRecord> b)
    {
        b.ToTable("Permissions", "sec");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(50).IsUnicode(false);
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermissionRecord>
{
    public void Configure(EntityTypeBuilder<RolePermissionRecord> b)
    {
        b.ToTable("RolePermissions", "sec");
        b.HasKey(x => new { x.RoleId, x.PermissionId });
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleRecord>
{
    public void Configure(EntityTypeBuilder<UserRoleRecord> b)
    {
        b.ToTable("UserRoles", "sec");
        b.HasKey(x => new { x.UserId, x.RoleId });
    }
}

internal sealed class UserBranchConfiguration : IEntityTypeConfiguration<UserBranchRecord>
{
    public void Configure(EntityTypeBuilder<UserBranchRecord> b)
    {
        b.ToTable("UserBranches", "sec");
        b.HasKey(x => new { x.UserId, x.BranchId });
    }
}

internal sealed class UserWindowsAccountConfiguration : IEntityTypeConfiguration<UserWindowsAccountRecord>
{
    public void Configure(EntityTypeBuilder<UserWindowsAccountRecord> b)
    {
        b.ToTable("UserWindowsAccounts", "sec");
        b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).ValueGeneratedNever();
        b.Property(x => x.WindowsAccount).HasMaxLength(256);
        b.Property(x => x.AssignedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogRecord>
{
    public void Configure(EntityTypeBuilder<AuditLogRecord> b)
    {
        // La tabla tiene un trigger INSTEAD OF UPDATE, DELETE (solo inserción). HasTrigger evita que EF Core
        // use OUTPUT sin INTO, que SQL Server no permite en tablas con triggers.
        b.ToTable("AuditLogs", "audit", t => t.HasTrigger("trg_AuditLogs_AppendOnly"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.EntityName).HasMaxLength(100);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.UserName).HasMaxLength(100);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.Property(x => x.IpAddress).HasMaxLength(45);
    }
}

internal sealed class AuthorizedBranchRowConfiguration : IEntityTypeConfiguration<AuthorizedBranchRow>
{
    public void Configure(EntityTypeBuilder<AuthorizedBranchRow> b)
    {
        // Sin tabla: se materializa desde sec.tvf_AuthorizedBranches con FromSql.
        b.HasNoKey().ToView(null);
    }
}
