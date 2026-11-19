using FinCore.Application.Operations;
using FinCore.Application.Reconciliation;
using FinCore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

public sealed record RunReconciliationRequest(int AccountId, DateOnly PeriodFrom, DateOnly PeriodTo, byte? DateToleranceDays);

public sealed record ResolveDifferenceRequest(string? Note);

/// <summary>HU-13 · Importación de extractos CSV (spec importacion-extractos).</summary>
[Route("api/v1/bank-statements")]
public sealed class BankStatementsController : ApiControllerBase
{
    /// <summary>
    /// Importa un extracto CSV (multipart, campo "file", máx. 5 MB):
    /// account_number;movement_id;reference;date;amount;description. Responde leídas, cargadas, rechazadas y los errores por fila.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = Permissions.StatementsImport)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportStatementHandler.MaxFileBytes + 64 * 1024)]
    [ProducesResponseType<ImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(IFormFile? file, [FromServices] ImportStatementHandler handler, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = ["Adjunta un archivo CSV."] })
                { Title = "Datos inválidos" });
        if (file.Length > ImportStatementHandler.MaxFileBytes)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = ["El archivo supera 5 MB."] })
                { Title = "Datos inválidos" });

        await using var stream = file.OpenReadStream();
        return FromResult(await handler.HandleAsync(file.FileName, stream, ct));
    }
}

[Route("api/v1/etl-executions")]
[Authorize(Policy = Permissions.ReconciliationRead)]
public sealed class EtlExecutionsController : ApiControllerBase
{
    /// <summary>Historial de cargas (CSV y Oracle) de la empresa, la más reciente primero.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<EtlExecutionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromServices] ListEtlExecutionsHandler handler, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        FromResult(await handler.HandleAsync(page, pageSize, ct));

    /// <summary>Filas rechazadas de una carga, con su código de error.</summary>
    [HttpGet("{id:long}/errors")]
    [ProducesResponseType<IReadOnlyList<EtlErrorDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Errors(long id, [FromServices] GetEtlErrorsHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(id, ct));
}

/// <summary>HU-14 a HU-18 · Conciliación bancaria (spec conciliacion-bancaria).</summary>
[Route("api/v1/reconciliations")]
[Authorize(Policy = Permissions.ReconciliationRead)]
public sealed class ReconciliationsController : ApiControllerBase
{
    /// <summary>
    /// Concilia una cuenta en un período (máx. 93 días, tolerancia 0–5, por defecto 1).
    /// 404 cuenta fuera de alcance · 400 período inválido · 409 ya hay una conciliación en curso.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.ReconciliationRun)]
    [ProducesResponseType<ReconciliationSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Run(RunReconciliationRequest request, [FromServices] RunReconciliationHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RunReconciliationCommand(request.AccountId, request.PeriodFrom, request.PeriodTo,
            request.DateToleranceDays ?? 1), ct);
        return FromResult(result, s => CreatedAtAction(nameof(Get), new { id = s.ReconciliationId }, s));
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<ReconciliationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int? accountId, [FromServices] ListReconciliationsHandler handler,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        FromResult(await handler.HandleAsync(accountId, page, pageSize, ct));

    /// <summary>Resumen y resultados de una conciliación.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ReconciliationWithDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, [FromServices] GetReconciliationHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(id, ct));

    /// <summary>Bandeja: diferencias sin resolver de la última conciliación de cada cuenta autorizada.</summary>
    [HttpGet("open-differences")]
    [ProducesResponseType<IReadOnlyList<OpenDifferenceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> OpenDifferences([FromServices] GetOpenDifferencesHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(ct));
}

[Route("api/v1/reconciliation-details")]
public sealed class ReconciliationDetailsController : ApiControllerBase
{
    /// <summary>Resuelve una diferencia con una nota. 409 si ya estaba resuelta o es un resultado conciliado.</summary>
    [HttpPost("{id:long}/resolve")]
    [Authorize(Policy = Permissions.ReconciliationResolve)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(long id, ResolveDifferenceRequest request, [FromServices] ResolveDifferenceHandler handler,
        CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new ResolveDifferenceCommand(id, request.Note), ct), _ => NoContent());
}
