using FinCore.Application.Common.Abstractions;
using FinCore.Application.Integration;
using FinCore.Application.Operations;
using FinCore.Infrastructure.Integration;
using FinCore.Application.Reports;
using FinCore.Infrastructure.Reports;
using FinCore.Application.Reconciliation;
using FinCore.Infrastructure.Reconciliation;
using FinCore.Application.Users;
using FinCore.Infrastructure.Auditing;
using FinCore.Infrastructure.Persistence;
using FinCore.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "FinCore";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión ConnectionStrings:{ConnectionStringName}.");

        services.AddScoped<SessionContextInterceptor>();
        services.AddDbContext<FinCoreDbContext>((sp, options) => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3))
            .AddInterceptors(sp.GetRequiredService<SessionContextInterceptor>()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<FinCoreDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuthorizationReader, AuthorizationReader>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IUserAssignments, UserAssignments>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ITransactionQueries, TransactionQueries>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IStatementImporter, StatementImporter>();
        services.AddScoped<IReconciliationEngine, ReconciliationEngine>();
        services.AddScoped<IReconciliationQueries, ReconciliationQueries>();
        services.AddScoped<IReconciliationDetailRepository, ReconciliationDetailRepository>();
        services.AddScoped<IInboundEventRepository, InboundEventRepository>();
        services.AddScoped<IBankMovementStore, BankMovementStore>();
        services.AddScoped<IEtlWatermarkStore, EtlWatermarkStore>();
        services.AddScoped<IMaintenanceProcedures, MaintenanceProcedures>();
        services.AddSingleton<ILegacyBankReader, OracleLegacyBankReader>();
        services.AddScoped<IReportQueries, ReportQueries>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        services.AddHealthChecks()
            .AddDbContextCheck<FinCoreDbContext>("sqlserver", tags: ["ready"],
                customTestQuery: (db, ct) => db.Database.CanConnectAsync(ct));

        return services;
    }
}
