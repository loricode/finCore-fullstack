using FinCore.Application.Common.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FinCore.Infrastructure.Security;

/// <summary>
/// Adaptador sobre PasswordHasher de ASP.NET Core Identity (formato v3: PBKDF2-SHA512, 100.000 iteraciones).
/// Es el mismo formato de los usuarios de demostración de database/009_Demo.
/// </summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly object HashOwner = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(HashOwner, password);

    public PasswordCheck Verify(string hash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(HashOwner, hash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
                _ => PasswordCheck.Failed,
            };
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed; // hash corrupto o de otro formato
        }
    }
}
