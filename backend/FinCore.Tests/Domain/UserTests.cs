using FinCore.Domain.Common;
using FinCore.Domain.Security;

namespace FinCore.Tests.Domain;

public sealed class UserTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    private static User NewUser() => User.Create(1, "operador", "op@fincore.demo", "Operador", "hash", Now);

    [Fact]
    public void Bloquea_al_quinto_intento_fallido_y_reinicia_el_contador()
    {
        var user = NewUser();

        for (var i = 1; i < LoginPolicy.MaxFailedAttempts; i++)
            Assert.False(user.RegisterFailedLogin(Now));

        Assert.True(user.RegisterFailedLogin(Now));
        Assert.True(user.IsLockedOut(Now));
        Assert.Equal(Now + LoginPolicy.LockoutDuration, user.LockoutEndUtc);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void El_bloqueo_vence_a_los_15_minutos()
    {
        var user = NewUser();
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++) user.RegisterFailedLogin(Now);

        Assert.False(user.CanSignIn(Now.AddMinutes(14)));
        Assert.True(user.CanSignIn(Now.AddMinutes(15).AddSeconds(1)));
    }

    [Fact]
    public void Un_ingreso_correcto_reinicia_intentos_y_registra_la_fecha()
    {
        var user = NewUser();
        user.RegisterFailedLogin(Now);
        user.RegisterFailedLogin(Now);

        user.RegisterSuccessfulLogin(Now.AddMinutes(1));

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(Now.AddMinutes(1), user.LastLoginAtUtc);
    }

    [Fact]
    public void Un_usuario_bloqueado_no_puede_registrar_ingreso()
    {
        var user = NewUser();
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++) user.RegisterFailedLogin(Now);

        var error = Assert.Throws<DomainException>(() => user.RegisterSuccessfulLogin(Now));
        Assert.Equal("user.cannot_sign_in", error.Code);
    }
}

public sealed class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Activo_hasta_expirar_o_revocarse()
    {
        var token = RefreshToken.Issue(1, new string('a', 64), Now, TimeSpan.FromDays(7), "127.0.0.1");

        Assert.True(token.IsActive(Now.AddDays(6)));
        Assert.False(token.IsActive(Now.AddDays(7)));

        token.Revoke(Now, new string('b', 64));
        Assert.False(token.IsActive(Now));
        Assert.Equal(new string('b', 64), token.ReplacedByTokenHash);
    }

    [Fact]
    public void Revocar_dos_veces_conserva_la_primera_revocacion()
    {
        var token = RefreshToken.Issue(1, new string('a', 64), Now, TimeSpan.FromDays(7), null);
        token.Revoke(Now, "primero");
        token.Revoke(Now.AddMinutes(5), "segundo");

        Assert.Equal(Now, token.RevokedAtUtc);
        Assert.Equal("primero", token.ReplacedByTokenHash);
    }
}
