using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Security;

namespace FinCore.Application.Auth.Refresh;

public sealed record RefreshSessionCommand(string? RefreshToken);

/// <summary>
/// Renueva la sesión con rotación de refresh token.
/// Si llega un token ya revocado, alguien lo está reutilizando (posible robo): se revocan todos los tokens
/// activos del usuario y se obliga a iniciar sesión de nuevo.
/// </summary>
public sealed class RefreshSessionHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAuthorizationReader authorization,
    ITokenService tokenService,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IRequestContext request,
    TimeProvider clock)
{
    public async Task<Result<AuthTokens>> HandleAsync(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return AuthErrors.InvalidRefreshToken;

        var now = clock.GetUtcNow().UtcDateTime;
        var current = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(command.RefreshToken), cancellationToken);
        if (current is null)
            return AuthErrors.InvalidRefreshToken;

        var user = await users.GetByIdAsync(current.UserId, cancellationToken);

        if (current.IsRevoked)
        {
            foreach (var token in await refreshTokens.GetActiveByUserAsync(current.UserId, now, cancellationToken))
                token.Revoke(now);

            auditLog.Record(new AuditEntry(AuditActions.RefreshTokenReuse, "User", current.UserId.ToString(),
                current.UserId, user?.UserName, user?.CompanyId, new { refreshTokenId = current.Id }));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        if (current.IsExpired(now) || user is null || !user.CanSignIn(now))
        {
            current.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        var replacement = tokenService.GenerateRefreshToken();
        current.Revoke(now, replacement.Hash);
        var replacementEntity = RefreshToken.Issue(user.Id, replacement.Hash, now, tokenService.RefreshTokenLifetime, request.IpAddress);
        refreshTokens.Add(replacementEntity);

        // Los permisos se leen de nuevo: un cambio de roles se refleja a más tardar en la siguiente renovación.
        var permissions = await authorization.GetPermissionsAsync(user.Id, cancellationToken);
        var access = tokenService.CreateAccessToken(new AccessTokenSubject(user.Id, user.CompanyId, user.UserName, permissions));

        auditLog.Record(new AuditEntry(AuditActions.TokenRefreshed, "User", user.Id.ToString(), user.Id, user.UserName, user.CompanyId));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthTokens(access.Token, access.ExpiresAtUtc, replacement.Value, replacementEntity.ExpiresAtUtc);
    }
}
