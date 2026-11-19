using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinCore.Application.Common.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinCore.Infrastructure.Security;

/// <summary>Nombres de claims propios del access token.</summary>
public static class FinCoreClaims
{
    public const string CompanyId = "cid";
    public const string Permission = "perm";
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken CreateAccessToken(AccessTokenSubject subject)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, subject.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(FinCoreClaims.CompanyId, subject.CompanyId.ToString()),
        };
        claims.AddRange(subject.Permissions.Select(p => new Claim(FinCoreClaims.Permission, p)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now, expires, credentials);
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public GeneratedRefreshToken GenerateRefreshToken()
    {
        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new GeneratedRefreshToken(value, HashRefreshToken(value));
    }

    /// <summary>SHA-256 en hexadecimal (64 caracteres, sec.RefreshTokens.TokenHash).</summary>
    public string HashRefreshToken(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
