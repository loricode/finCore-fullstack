namespace FinCore.Domain.Common;

/// <summary>
/// Violación de una regla de negocio. La API la traduce a 409 Conflict.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    /// <summary>Código estable para el cliente, p. ej. "user.inactive".</summary>
    public string Code { get; } = code;
}
