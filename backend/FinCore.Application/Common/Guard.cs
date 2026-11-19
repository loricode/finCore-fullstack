using FinCore.Application.Common.Abstractions;

namespace FinCore.Application.Common;

/// <summary>Utilidades comunes de los casos de uso.</summary>
internal static class Guard
{
    public static readonly Error NotAuthenticated = Error.Unauthorized("auth.not_authenticated", "No hay una sesión activa.");

    /// <summary>Usuario y empresa de la sesión; si no hay sesión, el caso de uso termina con 401.</summary>
    public static bool TryGetSession(this ICurrentUser user, out int userId, out int companyId)
    {
        userId = user.UserId ?? 0;
        companyId = user.CompanyId ?? 0;
        return user.UserId is not null && user.CompanyId is not null;
    }

    public static byte[]? FromBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return Convert.FromBase64String(value); }
        catch (FormatException) { return null; }
    }
}
