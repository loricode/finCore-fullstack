using FinCore.Application.Auth.Login;
using FinCore.Application.Auth.Logout;
using FinCore.Application.Auth.Me;
using FinCore.Application.Auth.Refresh;
using FinCore.Application.Integration;
using FinCore.Application.Operations;
using FinCore.Application.Organization;
using FinCore.Application.Reconciliation;
using FinCore.Application.Reports;
using FinCore.Application.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Application;

public static class DependencyInjection
{
    /// <summary>Registra los casos de uso y validadores. Cada caso de uso es una clase con un método HandleAsync.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // Autenticación
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<GetAuthorizedBranchesHandler>();

        // Usuarios (HU-02, HU-04)
        services.AddScoped<UserAssignmentPolicy>();
        services.AddScoped<GetUsersHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<GetRolesHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<UpdateUserBranchesHandler>();
        services.AddScoped<SetUserWindowsAccountHandler>();
        services.AddScoped<SetUserActiveHandler>();

        // Operaciones (Etapa 2)
        services.AddScoped<OperationScope>();
        services.AddScoped<SearchTransactionsHandler>();
        services.AddScoped<GetTransactionHandler>();
        services.AddScoped<GetTransactionSummaryHandler>();
        services.AddScoped<GetAccountsHandler>();
        services.AddScoped<RegisterTransactionHandler>();
        services.AddScoped<UpdateTransactionHandler>();
        services.AddScoped<ProcessTransactionHandler>();
        services.AddScoped<RejectTransactionHandler>();
        services.AddScoped<ReverseTransactionHandler>();

        // Conciliación (Etapa 3)
        services.AddScoped<ImportStatementHandler>();
        services.AddScoped<ListEtlExecutionsHandler>();
        services.AddScoped<GetEtlErrorsHandler>();
        services.AddScoped<RunReconciliationHandler>();
        services.AddScoped<ListReconciliationsHandler>();
        services.AddScoped<GetReconciliationHandler>();
        services.AddScoped<GetOpenDifferencesHandler>();
        services.AddScoped<ResolveDifferenceHandler>();

        // Integración (Etapa 4): webhooks y Worker
        services.AddScoped<BankEventProcessor>();
        services.AddScoped<ReceiveBankEventHandler>();
        services.AddScoped<ImportFromLegacyBankHandler>();
        services.AddScoped<ScheduledReconciliationHandler>();
        services.AddScoped<RecoverInboundEventsHandler>();
        services.AddScoped<PurgeStagingHandler>();

        // Reportes (Etapa 5)
        services.AddScoped<ReportHandler>();

        return services;
    }
}
