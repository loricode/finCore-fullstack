using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Common;
using FinCore.Domain.Security;

namespace FinCore.Application.Reports;

/// <summary>
/// HU-23 a HU-26 · Reportes financieros en JSON o CSV.
/// Exportar exige reports.export y se audita como EXPORT (qué reporte, qué período, cuántas filas).
/// </summary>
public sealed class ReportHandler(
    ICurrentUser currentUser,
    IReportQueries queries,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const int MaxPeriodDays = 366;

    /// <summary>Período por defecto: del 1 del mes en curso a hoy (hora de Colombia).</summary>
    public Result<ReportPeriod> ResolvePeriod(DateOnly? from, DateOnly? to)
    {
        var today = BusinessCalendar.Today(clock.GetUtcNow().UtcDateTime);
        var period = new ReportPeriod(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
        if (period.From > period.To)
            return new Error("validation.to", "La fecha final debe ser mayor o igual a la inicial.", ErrorType.Validation);
        if (period.To.DayNumber - period.From.DayNumber > MaxPeriodDays)
            return new Error("validation.to", "El período de un reporte no puede superar un año.", ErrorType.Validation);
        return period;
    }

    public async Task<Result<IReadOnlyList<BranchSummaryRow>>> BranchSummaryAsync(ReportPeriod period, CancellationToken ct) =>
        currentUser.UserId is { } id ? Result<IReadOnlyList<BranchSummaryRow>>.From(await queries.BranchSummaryAsync(id, period, ct)) : Guard.NotAuthenticated;

    public async Task<Result<IReadOnlyList<OperationsByPeriodRow>>> OperationsByPeriodAsync(ReportPeriod period, string? granularity,
        int? branchId, CancellationToken ct)
    {
        var g = (granularity ?? "DAY").ToUpperInvariant();
        if (g is not ("DAY" or "MONTH"))
            return new Error("validation.granularity", "La granularidad debe ser DAY o MONTH.", ErrorType.Validation);
        return currentUser.UserId is { } id
            ? Result<IReadOnlyList<OperationsByPeriodRow>>.From(await queries.OperationsByPeriodAsync(id, period, g, branchId, ct))
            : Guard.NotAuthenticated;
    }

    public async Task<Result<IReadOnlyList<UserActivityRow>>> UserActivityAsync(ReportPeriod period, CancellationToken ct) =>
        currentUser.UserId is { } id ? Result<IReadOnlyList<UserActivityRow>>.From(await queries.UserActivityAsync(id, period, ct)) : Guard.NotAuthenticated;

    public async Task<Result<FinancialIndicatorsDto>> IndicatorsAsync(ReportPeriod period, CancellationToken ct) =>
        currentUser.UserId is { } id ? await queries.IndicatorsAsync(id, period, ct) : Guard.NotAuthenticated;

    /// <summary>Convierte un reporte a CSV y audita la exportación.</summary>
    public async Task<Result<ExportedFile>> ExportAsync<T>(string reportName, ReportPeriod period, IReadOnlyList<T> rows,
        IReadOnlyList<(string Header, Func<T, object?> Value)> columns, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.ReportsExport))
            return Error.Forbidden("reports.export_forbidden", "No tienes permiso para exportar reportes.");

        var content = CsvWriter.Write(columns, rows);
        var fileName = $"fincore-{reportName}-{period.From:yyyy-MM-dd}_{period.To:yyyy-MM-dd}.csv";

        auditLog.Record(new AuditEntry("EXPORT", "Report", reportName, currentUser.UserId, currentUser.UserName, currentUser.CompanyId,
            new { report = reportName, period.From, period.To, rows = rows.Count, format = "csv" }));
        await unitOfWork.SaveChangesAsync(ct);
        return new ExportedFile(fileName, "text/csv; charset=utf-8", content);
    }
}

/// <summary>Columnas de cada reporte en el CSV (encabezados en español, en el orden del informe SSRS).</summary>
public static class ReportColumns
{
    public static readonly IReadOnlyList<(string, Func<BranchSummaryRow, object?>)> BranchSummary =
    [
        ("Código", r => r.BranchCode), ("Sucursal", r => r.BranchName), ("Ciudad", r => r.City),
        ("Operaciones", r => r.TotalOperations), ("Ingresos", r => r.TotalIncome), ("Egresos", r => r.TotalExpense),
        ("Neto", r => r.NetAmount), ("Pendientes", r => r.PendingCount), ("Procesadas", r => r.ProcessedCount),
        ("Conciliadas", r => r.ReconciledCount), ("Rechazadas", r => r.RejectedCount), ("Revertidas", r => r.ReversedCount),
    ];

    public static readonly IReadOnlyList<(string, Func<OperationsByPeriodRow, object?>)> OperationsByPeriod =
    [
        ("Período", r => r.PeriodStart), ("Tipo", r => r.TransactionTypeName), ("Operaciones", r => r.OperationCount),
        ("Efectivas", r => r.EffectiveCount), ("Valor efectivo", r => r.EffectiveAmount),
    ];

    public static readonly IReadOnlyList<(string, Func<UserActivityRow, object?>)> UserActivity =
    [
        ("Usuario", r => r.UserName), ("Nombre", r => r.FullName), ("Activo", r => r.IsActive),
        ("Operaciones registradas", r => r.OperationsCreated), ("Valor registrado", r => r.AmountCreated),
        ("Cambios de estado", r => r.StatusChanges), ("Acciones auditadas", r => r.AuditActions),
        ("Último ingreso (UTC)", r => r.LastLoginAtUtc), ("Última acción (UTC)", r => r.LastActionAtUtc),
    ];
}
