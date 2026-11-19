namespace FinCore.Domain.Security;

/// <summary>
/// Token de refresco. Tabla sec.RefreshTokens. Solo se guarda el SHA-256 del token, nunca el token.
/// Cada uso lo rota: el token usado se revoca y apunta a su reemplazo (<see cref="ReplacedByTokenHash"/>).
/// </summary>
public sealed class RefreshToken
{
    public long Id { get; private set; }
    public int UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? CreatedByIp { get; private set; }

    private RefreshToken() { } // EF Core

    public static RefreshToken Issue(int userId, string tokenHash, DateTime nowUtc, TimeSpan lifetime, string? ipAddress) => new()
    {
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAtUtc = nowUtc,
        ExpiresAtUtc = nowUtc.Add(lifetime),
        CreatedByIp = ipAddress,
    };

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc <= nowUtc;

    public bool IsActive(DateTime nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    /// <summary>Revoca el token. Revocar uno ya revocado no cambia nada (idempotente).</summary>
    public void Revoke(DateTime nowUtc, string? replacedByTokenHash = null)
    {
        if (IsRevoked) return;
        RevokedAtUtc = nowUtc;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
