using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;

namespace FinCore.Application.Auth.Logout;

public sealed record LogoutCommand(string? RefreshToken);

/// <summary>Cierra la sesión revocando el refresh token. Siempre termina bien: cerrar sesión dos veces no es un error.</summary>
public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            var token = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(command.RefreshToken), cancellationToken);
            if (token is not null && !token.IsRevoked)
            {
                token.Revoke(clock.GetUtcNow().UtcDateTime);
                auditLog.Record(new AuditEntry(AuditActions.Logout, "User", token.UserId.ToString(), token.UserId,
                    currentUser.UserName, currentUser.CompanyId));
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        return Unit.Value;
    }
}
