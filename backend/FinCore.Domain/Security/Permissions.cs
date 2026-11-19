namespace FinCore.Domain.Security;

/// <summary>
/// Códigos de permiso. Deben coincidir con sec.Permissions (database/008_Seed/002_sec_RolesPermissions.sql).
/// La API usa cada código como nombre de política: [Authorize(Policy = Permissions.UsersManage)].
/// </summary>
public static class Permissions
{
    public const string UsersManage = "users.manage";
    public const string BranchesManage = "branches.manage";
    public const string AccountsManage = "accounts.manage";
    public const string BranchesAll = "branches.all";
    public const string OperationsCreate = "operations.create";
    public const string OperationsRead = "operations.read";
    public const string OperationsProcess = "operations.process";
    public const string OperationsReject = "operations.reject";
    public const string OperationsReverse = "operations.reverse";
    public const string StatementsImport = "statements.import";
    public const string ReconciliationRun = "reconciliation.run";
    public const string ReconciliationRead = "reconciliation.read";
    public const string ReconciliationResolve = "reconciliation.resolve";
    public const string ReportsRead = "reports.read";
    public const string ReportsExport = "reports.export";
    public const string ReportsViewAs = "reports.viewas";
    public const string AuditRead = "audit.read";
    public const string IntegrationWebhooks = "integration.webhooks";
}
