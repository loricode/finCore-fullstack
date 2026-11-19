using System.ComponentModel.DataAnnotations;

namespace FinCore.Infrastructure.Security;

/// <summary>Sección "Jwt" de la configuración. La clave se entrega por variable de entorno (Jwt__SigningKey), nunca en el repositorio.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = "fincore-api";
    [Required] public string Audience { get; init; } = "fincore-web";

    /// <summary>Clave HMAC-SHA256: al menos 32 caracteres.</summary>
    [Required, MinLength(32)] public string SigningKey { get; init; } = string.Empty;

    [Range(1, 60)] public int AccessTokenMinutes { get; init; } = 15;
    [Range(1, 30)] public int RefreshTokenDays { get; init; } = 7;
}
