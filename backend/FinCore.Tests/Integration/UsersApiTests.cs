using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinCore.Tests.Integration;

/// <summary>HU-02 y HU-04 contra la API y la base reales.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class UsersApiTests(FinCoreApiFactory factory)
{
    private const string StrongPassword = "Segura*2026x";

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Dictionary<string, int>> BranchIdsAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/branches"))!
            .ToDictionary(b => b.GetProperty("code").GetString()!, b => b.GetProperty("branchId").GetInt32());

    private static string NewUserName() => "it.user." + Guid.NewGuid().ToString("N")[..8];

    private static Task<HttpResponseMessage> CreateAsync(HttpClient admin, string userName, string[] roles, int[] branches,
        string password = StrongPassword) =>
        admin.PostAsJsonAsync("/api/v1/users", new
        {
            userName, email = $"{userName}@test.local", fullName = "Usuario de integración", password, roles, branchIds = branches,
        });

    [IntegrationFact]
    public async Task Crear_un_operador_con_su_sucursal_y_que_inicie_sesion_viendo_solo_esa()
    {
        var admin = await factory.CreateAuthenticatedClientAsync("admin");
        var branches = await BranchIdsAsync(admin);
        var userName = NewUserName();

        var created = await CreateAsync(admin, userName, ["OPERATOR"], [branches["MED"]]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var body = await JsonAsync(created);
        Assert.Equal(userName, body.GetProperty("userName").GetString());
        Assert.False(body.TryGetProperty("password", out _));

        var user = await factory.CreateAuthenticatedClientAsync(userName, StrongPassword);
        var visible = await user.GetFromJsonAsync<List<JsonElement>>("/api/v1/branches");
        Assert.Equal("MED", Assert.Single(visible!).GetProperty("code").GetString());
    }

    [IntegrationFact]
    public async Task Usuario_repetido_contrasena_debil_y_operador_sin_sucursal_se_rechazan()
    {
        var admin = await factory.CreateAuthenticatedClientAsync("admin");
        var branches = await BranchIdsAsync(admin);

        Assert.Equal(HttpStatusCode.Conflict, (await CreateAsync(admin, "admin", ["OPERATOR"], [branches["MON"]])).StatusCode);

        var weak = await CreateAsync(admin, NewUserName(), ["OPERATOR"], [branches["MON"]], password: "123");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.True((await JsonAsync(weak)).GetProperty("errors").TryGetProperty("password", out _));

        var noBranch = await CreateAsync(admin, NewUserName(), ["OPERATOR"], []);
        Assert.Equal(HttpStatusCode.BadRequest, noBranch.StatusCode);
        Assert.True((await JsonAsync(noBranch)).GetProperty("errors").TryGetProperty("branchIds", out _));
    }

    [IntegrationFact]
    public async Task Cambiar_sucursales_cambia_lo_que_ve_el_usuario_y_una_version_vieja_responde_409()
    {
        var admin = await factory.CreateAuthenticatedClientAsync("admin");
        var branches = await BranchIdsAsync(admin);
        var userName = NewUserName();
        var id = (await JsonAsync(await CreateAsync(admin, userName, ["OPERATOR"], [branches["MON"]]))).GetProperty("id").GetInt32();

        var moved = await admin.PutAsJsonAsync($"/api/v1/users/{id}/branches", new { branchIds = new[] { branches["BAQ"] } });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var user = await factory.CreateAuthenticatedClientAsync(userName, StrongPassword);
        Assert.Equal("BAQ", Assert.Single((await user.GetFromJsonAsync<List<JsonElement>>("/api/v1/branches"))!).GetProperty("code").GetString());

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/users/{id}");
        var rowVersion = detail.GetProperty("rowVersion").GetString();
        var update = new { email = $"{userName}@nuevo.local", fullName = "Nombre actualizado", roles = new[] { "OPERATOR" },
                           branchIds = new[] { branches["BAQ"] }, rowVersion };
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/users/{id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/users/{id}", update)).StatusCode); // versión vieja
    }

    [IntegrationFact]
    public async Task Desactivar_cierra_las_sesiones_y_nadie_se_desactiva_a_si_mismo()
    {
        var admin = await factory.CreateAuthenticatedClientAsync("admin");
        var branches = await BranchIdsAsync(admin);
        var userName = NewUserName();
        var id = (await JsonAsync(await CreateAsync(admin, userName, ["OPERATOR"], [branches["MON"]]))).GetProperty("id").GetInt32();
        await factory.CreateAuthenticatedClientAsync(userName, StrongPassword);

        var deactivated = await admin.PostAsync($"/api/v1/users/{id}/deactivate", null);
        Assert.False((await JsonAsync(deactivated)).GetProperty("isActive").GetBoolean());

        var login = await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/login", new { userName, password = StrongPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var active = await FinCoreApiFactory.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sec.RefreshTokens WHERE UserId = @id AND RevokedAtUtc IS NULL", new Microsoft.Data.SqlClient.SqlParameter("@id", id));
        Assert.Equal(0, active);

        var me = await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var self = await admin.PostAsync($"/api/v1/users/{me.GetProperty("id").GetInt32()}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
    }

    [IntegrationFact]
    public async Task El_catalogo_de_roles_indica_cuales_ven_todas_las_sucursales()
    {
        var admin = await factory.CreateAuthenticatedClientAsync("admin");
        var roles = await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/roles");
        Assert.True(roles!.Single(r => r.GetProperty("code").GetString() == "SUPERVISOR").GetProperty("grantsAllBranches").GetBoolean());
        Assert.False(roles!.Single(r => r.GetProperty("code").GetString() == "OPERATOR").GetProperty("grantsAllBranches").GetBoolean());
    }

    [IntegrationFact]
    public async Task Asociar_cuenta_de_Windows_para_Reporting_Services_se_valida_audita_y_no_se_repite()
    {
        var admin = await factory.CreateAuthenticatedClientAsync("admin");
        var branches = await BranchIdsAsync(admin);
        var first = await JsonAsync(await CreateAsync(admin, NewUserName(), ["AUDITOR"], []));
        var second = await JsonAsync(await CreateAsync(admin, NewUserName(), ["AUDITOR"], [branches["MON"]]));
        var firstId = first.GetProperty("id").GetInt32();
        var account = @"ITPC\" + Guid.NewGuid().ToString("N")[..10];

        var set = await admin.PutAsJsonAsync($"/api/v1/users/{firstId}/windows-account", new { windowsAccount = $"  {account}  " });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(account, (await JsonAsync(set)).GetProperty("windowsAccount").GetString());

        var list = await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/users");
        Assert.Equal(account, list!.Single(u => u.GetProperty("id").GetInt32() == firstId).GetProperty("windowsAccount").GetString());

        // La misma cuenta (con otras mayúsculas) no puede pertenecer a otro usuario.
        var taken = await admin.PutAsJsonAsync($"/api/v1/users/{second.GetProperty("id").GetInt32()}/windows-account",
            new { windowsAccount = account.ToLowerInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);

        var invalid = await admin.PutAsJsonAsync($"/api/v1/users/{firstId}/windows-account", new { windowsAccount = "sin dominio" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await JsonAsync(invalid)).GetProperty("errors").TryGetProperty("windowsAccount", out _));

        var cleared = await admin.PutAsJsonAsync($"/api/v1/users/{firstId}/windows-account", new { windowsAccount = (string?)null });
        Assert.Equal(JsonValueKind.Null, (await JsonAsync(cleared)).GetProperty("windowsAccount").ValueKind);

        var operatorClient = await factory.CreateAuthenticatedClientAsync("operador.bogota");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await operatorClient.PutAsJsonAsync($"/api/v1/users/{firstId}/windows-account", new { windowsAccount = account })).StatusCode);
    }
}
