using FinCore.Application.Operations;
using FinCore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

public sealed record RegisterTransactionRequest(
    int AccountId,
    string Type,
    string Reference,
    DateOnly OperationDate,
    decimal Amount,
    string? Description,
    string? CounterpartyName,
    string? CounterpartyAccount);

public sealed record UpdateTransactionRequest(
    DateOnly OperationDate,
    decimal Amount,
    string? Description,
    string? CounterpartyName,
    string? CounterpartyAccount,
    string RowVersion);

public sealed record ReasonRequest(string? Reason);

/// <summary>Módulo 2 · Operaciones financieras (spec operaciones-financieras).</summary>
[Route("api/v1/transactions")]
[Authorize(Policy = Permissions.OperationsRead)]
public sealed class TransactionsController : ApiControllerBase
{
    /// <summary>Busca operaciones de las sucursales autorizadas, más recientes primero.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TransactionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] int? branchId, [FromQuery] int? accountId, [FromQuery] string? status,
        [FromQuery] string? type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
        [FromServices] SearchTransactionsHandler handler, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        FromResult(await handler.HandleAsync(new TransactionFilter(branchId, accountId, status, type, from, to, search, page, pageSize), ct));

    /// <summary>Indicadores: pendientes, estados del mes, ingresos y egresos del mes.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<TransactionSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromServices] GetTransactionSummaryHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(ct));

    /// <summary>Detalle con historial de estados y las acciones que el usuario puede hacer.</summary>
    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid publicId, [FromServices] GetTransactionHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(publicId, ct));

    /// <summary>
    /// Registra una operación (queda Pendiente). Cabecera opcional Idempotency-Key: con la misma clave y datos
    /// responde 200 con la original; con otros datos, 422.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.OperationsCreate)]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(RegisterTransactionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromServices] RegisterTransactionHandler handler, [FromServices] GetTransactionHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RegisterTransactionCommand(request.AccountId, request.Type ?? "",
            request.Reference ?? "", request.OperationDate, request.Amount, request.Description, request.CounterpartyName,
            request.CounterpartyAccount, idempotencyKey), ct);
        if (!result.IsSuccess) return FromResult(result);

        var detail = await get.HandleAsync(result.Value.PublicId, ct);
        return result.Value.Created
            ? FromResult(detail, d => CreatedAtAction(nameof(Get), new { publicId = d.Transaction.PublicId }, d))
            : FromResult(detail);
    }

    /// <summary>Edita una operación pendiente (RN-06). Exige el rowVersion leído.</summary>
    [HttpPut("{publicId:guid}")]
    [Authorize(Policy = Permissions.OperationsCreate)]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid publicId, UpdateTransactionRequest request,
        [FromServices] UpdateTransactionHandler handler, [FromServices] GetTransactionHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new UpdateTransactionCommand(publicId, request.OperationDate, request.Amount,
            request.Description, request.CounterpartyName, request.CounterpartyAccount, request.RowVersion ?? ""), ct);
        return result.IsSuccess ? FromResult(await get.HandleAsync(publicId, ct)) : FromResult(result);
    }

    /// <summary>Pendiente → Procesada. Desde $10.000.000 no puede procesarla quien la registró (409).</summary>
    [HttpPost("{publicId:guid}/process")]
    [Authorize(Policy = Permissions.OperationsProcess)]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Process(Guid publicId, [FromServices] ProcessTransactionHandler handler,
        [FromServices] GetTransactionHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ChangeTransactionStatusCommand(publicId), ct);
        return result.IsSuccess ? FromResult(await get.HandleAsync(publicId, ct)) : FromResult(result);
    }

    /// <summary>Pendiente → Rechazada, con motivo.</summary>
    [HttpPost("{publicId:guid}/reject")]
    [Authorize(Policy = Permissions.OperationsReject)]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid publicId, ReasonRequest request, [FromServices] RejectTransactionHandler handler,
        [FromServices] GetTransactionHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ChangeTransactionStatusCommand(publicId, request.Reason), ct);
        return result.IsSuccess ? FromResult(await get.HandleAsync(publicId, ct)) : FromResult(result);
    }

    /// <summary>Revierte una operación procesada o conciliada. Responde 201 con la operación de reversión.</summary>
    [HttpPost("{publicId:guid}/reverse")]
    [Authorize(Policy = Permissions.OperationsReverse)]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reverse(Guid publicId, ReasonRequest request, [FromServices] ReverseTransactionHandler handler,
        [FromServices] GetTransactionHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ChangeTransactionStatusCommand(publicId, request.Reason), ct);
        if (!result.IsSuccess) return FromResult(result);
        return FromResult(await get.HandleAsync(result.Value, ct),
            d => CreatedAtAction(nameof(Get), new { publicId = d.Transaction.PublicId }, d));
    }
}

[Route("api/v1/accounts")]
[Authorize(Policy = Permissions.OperationsRead)]
public sealed class AccountsController : ApiControllerBase
{
    /// <summary>Cuentas de las sucursales autorizadas con su saldo a hoy.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccountDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromServices] GetAccountsHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(ct));
}
