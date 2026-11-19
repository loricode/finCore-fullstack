using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Security;

namespace FinCore.Tests.Application;

// Dobles de prueba en memoria: permiten probar los casos de uso sin base de datos.

internal sealed class FakeClock(DateTime utcNow) : TimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
}

internal sealed class FakeUsers : IUserRepository
{
    public List<User> Items { get; } = [];
    public Task<User?> GetByUserNameAsync(string userName, CancellationToken ct) =>
        Task.FromResult(Items.SingleOrDefault(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)));
    public Task<User?> GetByIdAsync(int id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(u => u.Id == id));
    public Task<bool> UserNameExistsAsync(string userName, CancellationToken ct) =>
        Task.FromResult(Items.Any(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)));
    public Task<bool> EmailExistsAsync(string email, int? excludingUserId, CancellationToken ct) =>
        Task.FromResult(Items.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) && u.Id != excludingUserId));
    public void Add(User user) => Items.Add(user);
    public void SetExpectedRowVersion(User user, byte[] rowVersion) { }
}

internal sealed class FakeRefreshTokens : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(Items.SingleOrDefault(t => t.TokenHash == tokenHash));
    public Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(int userId, DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(t => t.UserId == userId && t.IsActive(nowUtc)).ToList());
    public void Add(RefreshToken token) => Items.Add(token);
}

internal sealed class FakeAuthorization : IAuthorizationReader
{
    public Task<IReadOnlyList<string>> GetPermissionsAsync(int userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(["operations.read"]);
    public Task<IReadOnlyList<string>> GetRoleCodesAsync(int userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(["OPERATOR"]);
    public Task<IReadOnlyList<AuthorizedBranch>> GetAuthorizedBranchesAsync(int userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AuthorizedBranch>>([]);
}

/// <summary>"Hash" reversible para pruebas: hash = "h:" + contraseña.</summary>
internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => "h:" + password;
    public PasswordCheck Verify(string hash, string password) => hash == "h:" + password ? PasswordCheck.Success : PasswordCheck.Failed;
}

internal sealed class FakeTokens : ITokenService
{
    private int _counter;
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(7);
    public AccessToken CreateAccessToken(AccessTokenSubject subject) => new($"access-{subject.UserId}", DateTime.UtcNow.AddMinutes(15));
    public GeneratedRefreshToken GenerateRefreshToken()
    {
        var value = $"refresh-{++_counter}";
        return new GeneratedRefreshToken(value, HashRefreshToken(value));
    }
    public string HashRefreshToken(string value) => "sha:" + value;
}

internal sealed class FakeAudit : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];
    public void Record(AuditEntry entry) => Entries.Add(entry);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }
    public Task SaveChangesAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct) => work();
}

internal sealed class FakeAssignments : IUserAssignments
{
    private static readonly Dictionary<string, (short Id, string[] Permissions)> Roles = new()
    {
        ["ADMIN"] = (1, ["users.manage", "branches.all"]),
        ["SUPERVISOR"] = (2, ["branches.all", "operations.process"]),
        ["OPERATOR"] = (3, ["operations.create"]),
    };

    public Task<IReadOnlyList<RoleInfo>> GetRolesByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RoleInfo>>(codes.Where(Roles.ContainsKey).Select(c => new RoleInfo(Roles[c].Id, c)).ToList());
    public Task<IReadOnlyList<string>> GetPermissionsOfRolesAsync(IReadOnlyCollection<short> roleIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(Roles.Values.Where(r => roleIds.Contains(r.Id)).SelectMany(r => r.Permissions).Distinct().ToList());
    public Task<IReadOnlyList<int>> GetActiveBranchIdsAsync(int companyId, CancellationToken ct) => Task.FromResult<IReadOnlyList<int>>([1, 2]);
    public Task<IReadOnlyList<string>> GetRoleCodesAsync(int userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<IReadOnlyList<int>> GetBranchIdsAsync(int userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<int>>([]);
    public Task ReplaceRolesAsync(User user, IReadOnlyCollection<short> roleIds, int by, CancellationToken ct) => Task.CompletedTask;
    public Task ReplaceBranchesAsync(User user, IReadOnlyCollection<int> branchIds, int by, CancellationToken ct) => Task.CompletedTask;
    public Task<string?> GetWindowsAccountAsync(int userId, CancellationToken ct) => Task.FromResult<string?>(null);
    public Task<int?> FindUserByWindowsAccountAsync(string windowsAccount, CancellationToken ct) => Task.FromResult<int?>(null);
    public Task SetWindowsAccountAsync(int userId, string? windowsAccount, int by, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class FakeRequest : IRequestContext, ICurrentUser
{
    public string CorrelationId => "test-correlation";
    public string? IpAddress => "10.0.0.1";
    public bool IsAuthenticated => false;
    public int? UserId => null;
    public int? CompanyId => null;
    public string? UserName => null;
    public bool HasPermission(string permission) => false;
}

/// <summary>Asigna el Id que en producción genera SQL Server (IDENTITY).</summary>
internal static class TestUsers
{
    public static User Create(int id, string userName, string password, DateTime now)
    {
        var user = User.Create(1, userName, $"{userName}@test", userName, "h:" + password, now);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }
}
