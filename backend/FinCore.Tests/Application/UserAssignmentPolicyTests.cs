using FinCore.Application.Common;
using FinCore.Application.Users;
using FinCore.Domain.Common;
using FinCore.Domain.Security;

namespace FinCore.Tests.Application;

/// <summary>HU-02 y HU-04 · Reglas de asignación de roles y sucursales.</summary>
public sealed class UserAssignmentPolicyTests
{
    private readonly UserAssignmentPolicy _policy = new(new FakeAssignments());

    [Fact]
    public async Task Un_operador_sin_sucursales_no_se_permite()
    {
        var result = await _policy.ResolveAsync(["OPERATOR"], [], companyId: 1, isSelf: false, default);
        Assert.Equal("validation.branchIds", result.Error!.Code);
    }

    [Fact]
    public async Task Un_rol_con_todas_las_sucursales_no_necesita_asignacion()
    {
        var result = await _policy.ResolveAsync(["supervisor"], [], 1, false, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(["SUPERVISOR"], result.Value.RoleCodes);
    }

    [Fact]
    public async Task Roles_inexistentes_y_sucursales_de_otra_empresa_se_rechazan()
    {
        Assert.Equal("validation.roles", (await _policy.ResolveAsync(["MAGO"], [1], 1, false, default)).Error!.Code);
        Assert.Equal("validation.branchIds", (await _policy.ResolveAsync(["OPERATOR"], [99], 1, false, default)).Error!.Code);
    }

    [Fact]
    public async Task Un_administrador_no_puede_quitarse_el_permiso_de_administrar_usuarios()
    {
        var result = await _policy.ResolveAsync(["OPERATOR"], [1], 1, isSelf: true, default);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public void Nadie_se_desactiva_a_si_mismo_y_activar_levanta_el_bloqueo()
    {
        var now = new DateTime(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);
        var user = TestUsers.Create(7, "ana", "x", now);
        Assert.Equal("user.cannot_deactivate_self", Assert.Throws<DomainException>(() => user.Deactivate(7, now)).Code);

        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++) user.RegisterFailedLogin(now);
        user.Deactivate(1, now);
        user.Activate(now);
        Assert.True(user.CanSignIn(now));
    }

    [Theory]
    [InlineData("Corta*1A", false)]
    [InlineData("sinmayuscula*2026", false)]
    [InlineData("SinSimbolo2026", false)]
    [InlineData("Segura*2026", true)]
    public void Politica_de_contrasenas(string password, bool valid) => Assert.Equal(valid, PasswordPolicy.IsSatisfiedBy(password));
}
