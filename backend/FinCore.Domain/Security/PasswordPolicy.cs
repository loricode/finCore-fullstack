namespace FinCore.Domain.Security;

/// <summary>Política de contraseñas (spec seguridad-acceso): 10+ caracteres con mayúscula, minúscula, número y símbolo.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MaxLength = 128;

    public static bool IsSatisfiedBy(string? password) =>
        password is { Length: >= MinLength and <= MaxLength }
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(c => !char.IsLetterOrDigit(c));

    public const string Description =
        "La contraseña debe tener al menos 10 caracteres, con mayúscula, minúscula, número y símbolo.";
}
