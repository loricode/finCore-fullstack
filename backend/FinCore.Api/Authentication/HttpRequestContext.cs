using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinCore.Api.Middlewares;
using FinCore.Application.Common.Abstractions;
using FinCore.Infrastructure.Security;

namespace FinCore.Api.Authentication;

/// <summary>Implementa <see cref="ICurrentUser"/> y <see cref="IRequestContext"/> a partir de HttpContext.</summary>
public sealed class HttpRequestContext(IHttpContextAccessor accessor) : ICurrentUser, IRequestContext
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int? UserId => ReadInt(JwtRegisteredClaimNames.Sub);

    public int? CompanyId => ReadInt(FinCoreClaims.CompanyId);

    public string? UserName => IsAuthenticated ? Principal!.FindFirstValue(JwtRegisteredClaimNames.UniqueName) : null;

    public bool HasPermission(string permission) => Principal?.HasClaim(FinCoreClaims.Permission, permission) == true;

    public string CorrelationId =>
        accessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string ?? "no-http-context";

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private int? ReadInt(string claimType) =>
        IsAuthenticated && int.TryParse(Principal!.FindFirstValue(claimType), out var value) ? value : null;
}
