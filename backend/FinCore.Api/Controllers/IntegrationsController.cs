using System.Text.Json;
using FinCore.Application.Integration;
using FinCore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

public sealed record BankEventRequest(
    string? Provider,
    string? ExternalEventId,
    string? EventType,
    DateTime? OccurredAt,
    BankEventMovement? Movement);

/// <summary>Módulo 4 · Eventos de proveedores bancarios entregados por el adaptador Node.js (spec integracion-webhooks).</summary>
[Route("api/v1/integrations")]
[Authorize(Policy = Permissions.IntegrationWebhooks)]
public sealed class IntegrationsController : ApiControllerBase
{
    private const int MaxPayloadBytes = 64 * 1024;

    /// <summary>
    /// Recibe un movimiento bancario. 200 procesado, ignorado (ya existía) o duplicado (mismo evento reenviado) ·
    /// 409 mismo id con otro contenido · 422 error de negocio (no reintentar).
    /// </summary>
    [HttpPost("bank-events")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxPayloadBytes)]
    [ProducesResponseType<BankEventResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BankEvent([FromServices] ReceiveBankEventHandler handler, CancellationToken ct)
    {
        // Se lee el cuerpo crudo: su SHA-256 identifica el contenido exacto del evento (idempotencia y conflicto).
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(ct);
        BankEventRequest? body;
        try { body = JsonSerializer.Deserialize<BankEventRequest>(raw, JsonSerializerOptions.Web); }
        catch (JsonException) { body = null; }
        if (body is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["body"] = ["El cuerpo no es un JSON válido."] })
                { Title = "Datos inválidos" });

        var result = await handler.HandleAsync(new ReceiveBankEventCommand(body.Provider ?? "", body.ExternalEventId ?? "",
            body.EventType ?? "", body.OccurredAt, body.Movement, raw), ct);

        return FromResult(result, r => r.Status == "FAILED"
            ? UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Evento no procesable",
                Detail = r.Error,
                Extensions = { ["code"] = "integration.business_error", ["inboundEventId"] = r.InboundEventId },
            })
            : Ok(r));
    }
}
