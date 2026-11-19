using FinCore.Application.Common;
using FinCore.Application.Reports;
using FinCore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

/// <summary>
/// Módulo 5 · Reportes financieros (spec reportes-financieros). Mismos procedimientos rpt.* que los informes SSRS.
/// format=json (por defecto) o csv (exige reports.export, se audita como EXPORT). Período por defecto: mes en curso.
/// </summary>
[Route("api/v1/reports")]
[Authorize(Policy = Permissions.ReportsRead)]
public sealed class ReportsController(ReportHandler reports) : ApiControllerBase
{
    /// <summary>Indicadores financieros del período: totales, estados, cobertura de conciliación, diferencias abiertas y calidad de las cargas.</summary>
    [HttpGet("indicators")]
    [ProducesResponseType<FinancialIndicatorsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Indicators([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var period = reports.ResolvePeriod(from, to);
        return period.IsSuccess ? FromResult(await reports.IndicatorsAsync(period.Value, ct)) : FromResult(period);
    }

    /// <summary>Totales por sucursal autorizada.</summary>
    [HttpGet("branch-summary")]
    [ProducesResponseType<IReadOnlyList<BranchSummaryRow>>(StatusCodes.Status200OK)]
    public Task<IActionResult> BranchSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? format,
        CancellationToken ct) =>
        Run(from, to, format, "resumen-sucursales", p => reports.BranchSummaryAsync(p, ct), ReportColumns.BranchSummary, ct);

    /// <summary>Operaciones por día o por mes y por tipo. granularity = DAY | MONTH.</summary>
    [HttpGet("operations-by-period")]
    [ProducesResponseType<IReadOnlyList<OperationsByPeriodRow>>(StatusCodes.Status200OK)]
    public Task<IActionResult> OperationsByPeriod([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? granularity,
        [FromQuery] int? branchId, [FromQuery] string? format, CancellationToken ct) =>
        Run(from, to, format, "operaciones-por-periodo", p => reports.OperationsByPeriodAsync(p, granularity, branchId, ct),
            ReportColumns.OperationsByPeriod, ct);

    /// <summary>Actividad por usuario (además exige audit.read; lo valida rpt.usp_UserActivity → 403).</summary>
    [HttpGet("user-activity")]
    [ProducesResponseType<IReadOnlyList<UserActivityRow>>(StatusCodes.Status200OK)]
    public Task<IActionResult> UserActivity([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? format,
        CancellationToken ct) =>
        Run(from, to, format, "actividad-usuarios", p => reports.UserActivityAsync(p, ct), ReportColumns.UserActivity, ct);

    private async Task<IActionResult> Run<T>(DateOnly? from, DateOnly? to, string? format, string name,
        Func<ReportPeriod, Task<Result<IReadOnlyList<T>>>> query, IReadOnlyList<(string, Func<T, object?>)> columns, CancellationToken ct)
    {
        var period = reports.ResolvePeriod(from, to);
        if (!period.IsSuccess) return FromResult(period);

        var rows = await query(period.Value);
        if (!rows.IsSuccess || !string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return FromResult(rows);

        var file = await reports.ExportAsync(name, period.Value, rows.Value, columns, ct);
        return FromResult(file, f => File(f.Content, f.ContentType, f.FileName));
    }
}
