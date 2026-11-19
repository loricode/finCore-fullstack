namespace FinCore.Application.Common.Abstractions;

/// <summary>Datos de la petición en curso. Lo implementa la API a partir de HttpContext.</summary>
public interface IRequestContext
{
    /// <summary>Id de correlación de la petición (cabecera X-Correlation-Id o generado).</summary>
    string CorrelationId { get; }

    string? IpAddress { get; }
}

/// <summary>Usuario autenticado de la petición en curso (claims del access token).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    int? CompanyId { get; }
    string? UserName { get; }
    bool HasPermission(string permission);
}
