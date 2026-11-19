namespace FinCore.Domain.Integration;

public enum InboundEventStatus
{
    Received,
    Processed,
    Failed,
    Ignored,
}

/// <summary>
/// Evento recibido de un proveedor externo (webhook entregado por el adaptador Node.js). Tabla integration.InboundEvents.
/// (Provider, ExternalEventId) es único: un evento reenviado no se procesa dos veces (spec integracion-webhooks).
/// </summary>
public sealed class InboundEvent
{
    public long Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ExternalEventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public InboundEventStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public long? BankStatementDetailId { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? CorrelationId { get; private set; }

    public const int MaxAttempts = 5;

    private InboundEvent() { } // EF Core

    public static InboundEvent Receive(string provider, string externalEventId, string eventType, string payload,
        string payloadHash, string? correlationId, DateTime nowUtc) => new()
    {
        Provider = provider,
        ExternalEventId = externalEventId,
        EventType = eventType,
        Payload = payload,
        PayloadHash = payloadHash,
        Status = InboundEventStatus.Received,
        ReceivedAtUtc = nowUtc,
        CorrelationId = correlationId,
    };

    /// <summary>Puede (re)procesarse: recibido, o fallido por una causa transitoria y con intentos disponibles.</summary>
    public bool CanBeProcessed => Status == InboundEventStatus.Received && Attempts < MaxAttempts;

    public void StartAttempt() => Attempts++;

    public void MarkProcessed(long bankStatementDetailId, DateTime nowUtc)
    {
        Status = InboundEventStatus.Processed;
        BankStatementDetailId = bankStatementDetailId;
        ProcessedAtUtc = nowUtc;
        LastError = null;
    }

    /// <summary>El movimiento ya existía (llegó antes por otro canal, p. ej. CSV u Oracle): no es un error.</summary>
    public void MarkIgnored(string reason, DateTime nowUtc)
    {
        Status = InboundEventStatus.Ignored;
        LastError = Truncate(reason);
        ProcessedAtUtc = nowUtc;
    }

    /// <summary>Error de negocio (cuenta inexistente, datos inválidos): no se reintenta; queda para revisión.</summary>
    public void MarkFailed(string error, DateTime nowUtc)
    {
        Status = InboundEventStatus.Failed;
        LastError = Truncate(error);
        ProcessedAtUtc = nowUtc;
    }

    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];
}
