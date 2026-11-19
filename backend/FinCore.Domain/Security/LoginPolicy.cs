namespace FinCore.Domain.Security;

/// <summary>Reglas de inicio de sesión (HU-01, spec seguridad-acceso).</summary>
public static class LoginPolicy
{
    /// <summary>Intentos fallidos consecutivos que bloquean la cuenta.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Duración del bloqueo.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}
