using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Security;
using FluentValidation;

namespace FinCore.Application.Auth.Login;

/// <summary>
/// HU-01 · Iniciar sesión.
/// Verifica credenciales, aplica el bloqueo por intentos fallidos, emite access y refresh token y audita
/// cada intento (LOGIN, LOGIN_FAILED, LOGIN_LOCKED). Los intentos fallidos también se guardan:
/// el contador de bloqueo no puede perderse por devolver un error.
/// </summary>
public sealed class LoginHandler(
    IValidator<LoginCommand> validator,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAuthorizationReader authorization,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IRequestContext request,
    TimeProvider clock)
{
    // Hash de una contraseña aleatoria, calculado una sola vez. Cuando el usuario no existe se verifica contra él
    // para que la respuesta tarde lo mismo que con un usuario real y no permita descubrir usuarios por tiempo.
    private static string? _dummyHash;

    public async Task<Result<AuthTokens>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;

        var user = await users.GetByUserNameAsync(command.UserName.Trim(), cancellationToken);
        if (user is null)
        {
            _dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString("N"));
            passwordHasher.Verify(_dummyHash, command.Password);
            return await FailAsync(null, command.UserName, "unknown_user", cancellationToken);
        }

        if (!user.IsActive)
            return await FailAsync(user, user.UserName, "inactive_user", cancellationToken);

        if (user.IsLockedOut(now))
        {
            Audit(AuditActions.LoginLocked, user, new { reason = "locked_out", lockoutEndUtc = user.LockoutEndUtc });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthErrors.LockedOut(user.LockoutEndUtc!.Value);
        }

        var check = passwordHasher.Verify(user.PasswordHash, command.Password);
        if (check == PasswordCheck.Failed)
        {
            var lockedNow = user.RegisterFailedLogin(now);
            if (lockedNow)
            {
                Audit(AuditActions.LoginLocked, user, new { reason = "too_many_attempts", lockoutEndUtc = user.LockoutEndUtc });
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return AuthErrors.LockedOut(user.LockoutEndUtc!.Value);
            }
            return await FailAsync(user, user.UserName, "invalid_password", cancellationToken);
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
            user.UpgradePasswordHash(passwordHasher.Hash(command.Password), now);

        user.RegisterSuccessfulLogin(now);

        var permissions = await authorization.GetPermissionsAsync(user.Id, cancellationToken);
        var access = tokenService.CreateAccessToken(new AccessTokenSubject(user.Id, user.CompanyId, user.UserName, permissions));
        var refresh = tokenService.GenerateRefreshToken();
        var refreshEntity = RefreshToken.Issue(user.Id, refresh.Hash, now, tokenService.RefreshTokenLifetime, request.IpAddress);
        refreshTokens.Add(refreshEntity);

        Audit(AuditActions.Login, user, null);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthTokens(access.Token, access.ExpiresAtUtc, refresh.Value, refreshEntity.ExpiresAtUtc);
    }

    private async Task<Result<AuthTokens>> FailAsync(User? user, string attemptedUserName, string reason, CancellationToken ct)
    {
        auditLog.Record(new AuditEntry(AuditActions.LoginFailed, "User", user?.Id.ToString(), user?.Id,
            Truncate(attemptedUserName, 100), user?.CompanyId, new { reason }));
        await unitOfWork.SaveChangesAsync(ct);
        return AuthErrors.InvalidCredentials;
    }

    private void Audit(string action, User user, object? details) =>
        auditLog.Record(new AuditEntry(action, "User", user.Id.ToString(), user.Id, user.UserName, user.CompanyId, details));

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
