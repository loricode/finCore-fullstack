using FinCore.Application.Auth.Login;
using FinCore.Application.Auth.Refresh;
using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Security;
using FluentValidation;

namespace FinCore.Tests.Application;

public sealed class LoginHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    private readonly FakeUsers _users = new();
    private readonly FakeRefreshTokens _tokens = new();
    private readonly FakeAudit _audit = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeClock _clock = new(Now);

    private LoginHandler Handler() => new(new LoginCommandValidator(), _users, _tokens, new FakeAuthorization(),
        new FakeHasher(), new FakeTokens(), _audit, _uow, new FakeRequest(), _clock);

    [Fact]
    public async Task Credenciales_correctas_emiten_tokens_guardan_el_refresh_y_auditan_LOGIN()
    {
        _users.Items.Add(TestUsers.Create(7, "admin", "Secreta*1", Now));

        var result = await Handler().HandleAsync(new LoginCommand("ADMIN", "Secreta*1"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-7", result.Value.AccessToken);
        var stored = Assert.Single(_tokens.Items);
        Assert.Equal("sha:" + result.Value.RefreshToken, stored.TokenHash);   // se guarda el hash, no el token
        Assert.Equal("10.0.0.1", stored.CreatedByIp);
        Assert.Equal(AuditActions.Login, Assert.Single(_audit.Entries).Action);
        Assert.Equal(1, _uow.Saves);
    }

    [Fact]
    public async Task Usuario_inexistente_y_contrasena_incorrecta_responden_igual()
    {
        _users.Items.Add(TestUsers.Create(7, "admin", "Secreta*1", Now));

        var unknown = await Handler().HandleAsync(new LoginCommand("nadie", "x"), default);
        var wrong = await Handler().HandleAsync(new LoginCommand("admin", "x"), default);

        Assert.Equal(ErrorType.Unauthorized, unknown.Error!.Type);
        Assert.Equal(unknown.Error, wrong.Error);
        Assert.All(_audit.Entries, e => Assert.Equal(AuditActions.LoginFailed, e.Action));
        Assert.Equal(2, _uow.Saves); // los intentos fallidos también se guardan
    }

    [Fact]
    public async Task Cinco_fallos_bloquean_la_cuenta_y_luego_ni_la_contrasena_correcta_entra()
    {
        var user = TestUsers.Create(7, "admin", "Secreta*1", Now);
        _users.Items.Add(user);

        Result<FinCore.Application.Auth.AuthTokens> last = default;
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++)
            last = await Handler().HandleAsync(new LoginCommand("admin", "mala"), default);

        Assert.Equal(ErrorType.Locked, last.Error!.Type);
        Assert.Contains(_audit.Entries, e => e.Action == AuditActions.LoginLocked);

        var correct = await Handler().HandleAsync(new LoginCommand("admin", "Secreta*1"), default);
        Assert.Equal(ErrorType.Locked, correct.Error!.Type);
        Assert.Empty(_tokens.Items);

        _clock.UtcNow = Now.Add(LoginPolicy.LockoutDuration).AddSeconds(1);
        Assert.True((await Handler().HandleAsync(new LoginCommand("admin", "Secreta*1"), default)).IsSuccess);
    }

    [Fact]
    public async Task Campos_vacios_lanzan_error_de_validacion()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler().HandleAsync(new LoginCommand("", ""), default));
        Assert.Equal(2, error.Errors.Count());
    }
}

public sealed class RefreshSessionHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    private readonly FakeUsers _users = new();
    private readonly FakeRefreshTokens _tokens = new();
    private readonly FakeAudit _audit = new();
    private readonly FakeTokens _tokenService = new();
    private readonly FakeClock _clock = new(Now);

    private RefreshSessionHandler Handler() => new(_users, _tokens, new FakeAuthorization(), _tokenService, _audit,
        new FakeUnitOfWork(), new FakeRequest(), _clock);

    private string IssueFor(int userId)
    {
        var generated = _tokenService.GenerateRefreshToken();
        _tokens.Add(RefreshToken.Issue(userId, generated.Hash, Now, TimeSpan.FromDays(7), null));
        return generated.Value;
    }

    [Fact]
    public async Task Rota_el_token_el_usado_queda_revocado_y_apunta_al_nuevo()
    {
        _users.Items.Add(TestUsers.Create(7, "admin", "x", Now));
        var original = IssueFor(7);

        var result = await Handler().HandleAsync(new RefreshSessionCommand(original), default);

        Assert.True(result.IsSuccess);
        var used = _tokens.Items.Single(t => t.TokenHash == "sha:" + original);
        Assert.True(used.IsRevoked);
        Assert.Equal("sha:" + result.Value.RefreshToken, used.ReplacedByTokenHash);
        Assert.True(_tokens.Items.Single(t => t.TokenHash == used.ReplacedByTokenHash).IsActive(Now));
    }

    [Fact]
    public async Task Reutilizar_un_token_revocado_revoca_todas_las_sesiones_del_usuario()
    {
        _users.Items.Add(TestUsers.Create(7, "admin", "x", Now));
        var stolen = IssueFor(7);
        var renewed = await Handler().HandleAsync(new RefreshSessionCommand(stolen), default); // uso legítimo
        IssueFor(7);                                                                          // otra sesión abierta

        var reuse = await Handler().HandleAsync(new RefreshSessionCommand(stolen), default);   // el atacante reusa

        Assert.Equal(ErrorType.Unauthorized, reuse.Error!.Type);
        Assert.All(_tokens.Items, t => Assert.False(t.IsActive(Now)));
        Assert.Contains(_audit.Entries, e => e.Action == AuditActions.RefreshTokenReuse);
        Assert.False((await Handler().HandleAsync(new RefreshSessionCommand(renewed.Value.RefreshToken), default)).IsSuccess);
    }

    [Fact]
    public async Task Un_usuario_desactivado_no_renueva_su_sesion()
    {
        var user = TestUsers.Create(7, "admin", "x", Now);
        typeof(User).GetProperty(nameof(User.IsActive))!.SetValue(user, false);
        _users.Items.Add(user);

        var result = await Handler().HandleAsync(new RefreshSessionCommand(IssueFor(7)), default);

        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.All(_tokens.Items, t => Assert.True(t.IsRevoked));
    }
}
