namespace FinCore.Application.Auth;

/// <summary>
/// Tokens emitidos al iniciar sesión o renovar. La API entrega el access token en el cuerpo
/// y el refresh token en una cookie HttpOnly.
/// </summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

internal static class AuthErrors
{
    // Un solo mensaje para usuario inexistente, inactivo o contraseña incorrecta: no revela qué usuarios existen.
    public static readonly Common.Error InvalidCredentials =
        Common.Error.Unauthorized("auth.invalid_credentials", "Usuario o contraseña incorrectos.");

    public static readonly Common.Error InvalidRefreshToken =
        Common.Error.Unauthorized("auth.invalid_refresh_token", "La sesión expiró. Inicie sesión de nuevo.");

    public static Common.Error LockedOut(DateTime lockoutEndUtc) =>
        Common.Error.Locked("auth.locked_out",
            $"La cuenta está bloqueada por intentos fallidos hasta las {lockoutEndUtc:HH:mm} UTC.");
}
