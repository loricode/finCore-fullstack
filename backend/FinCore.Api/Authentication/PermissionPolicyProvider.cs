using FinCore.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FinCore.Api.Authentication;

/// <summary>
/// Cada código de permiso es una política: [Authorize(Policy = "users.manage")] exige el claim perm=users.manage.
/// Las políticas se construyen a demanda, así un permiso nuevo en sec.Permissions no requiere registrarlo aquí.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.Contains('.'))
            return _fallback.GetPolicyAsync(policyName);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(FinCoreClaims.Permission, policyName)
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
