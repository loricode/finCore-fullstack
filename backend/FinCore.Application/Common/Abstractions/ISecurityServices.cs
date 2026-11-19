namespace FinCore.Application.Common.Abstractions;

public enum PasswordCheck
{
    Failed,
    Success,
    /// <summary>Correcta, pero el hash usa parámetros antiguos y debe regenerarse.</summary>
    SuccessRehashNeeded,
}

/// <summary>Hash de contraseñas (implementación: PasswordHasher de ASP.NET Core Identity, PBKDF2).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    PasswordCheck Verify(string hash, string password);
}

public sealed record AccessTokenSubject(int UserId, int CompanyId, string UserName, IReadOnlyCollection<string> Permissions);

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Un refresh token recién generado: el valor se entrega al cliente y solo el hash se guarda.</summary>
public sealed record GeneratedRefreshToken(string Value, string Hash);

/// <summary>Emisión de tokens (implementación: JWT firmado con HMAC-SHA256).</summary>
public interface ITokenService
{
    TimeSpan RefreshTokenLifetime { get; }
    AccessToken CreateAccessToken(AccessTokenSubject subject);
    GeneratedRefreshToken GenerateRefreshToken();
    string HashRefreshToken(string value);
}
