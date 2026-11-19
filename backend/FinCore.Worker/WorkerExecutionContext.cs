using FinCore.Application.Common.Abstractions;

namespace FinCore.Worker;

/// <summary>
/// Identidad del Worker para los casos de uso: actúa como la cuenta de servicio (por defecto svc.integration),
/// de modo que la auditoría, el historial de estados (SESSION_CONTEXT) y el alcance por sucursal funcionan igual que en la API.
/// Cada ejecución de un trabajo tiene su propio CorrelationId (AsyncLocal: no se mezcla entre trabajos concurrentes).
/// </summary>
public sealed class WorkerExecutionContext : ICurrentUser, IRequestContext
{
    private static readonly AsyncLocal<string?> Correlation = new();
    private HashSet<string> _permissions = [];

    public bool IsAuthenticated => UserId is not null;
    public int? UserId { get; private set; }
    public int? CompanyId { get; private set; }
    public string? UserName { get; private set; }
    public bool HasPermission(string permission) => _permissions.Contains(permission);

    public string CorrelationId => Correlation.Value ?? "worker-sin-ejecucion";
    public string? IpAddress => null;

    public bool IsInitialized => UserId is not null;

    public void Initialize(int userId, int companyId, string userName, IEnumerable<string> permissions)
    {
        UserId = userId;
        CompanyId = companyId;
        UserName = userName;
        _permissions = [.. permissions];
    }

    /// <summary>Abre el contexto de una ejecución: todo lo que se registre lleva este CorrelationId.</summary>
    public static IDisposable BeginRun(string correlationId)
    {
        var previous = Correlation.Value;
        Correlation.Value = correlationId;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => Correlation.Value = previous;
    }
}
