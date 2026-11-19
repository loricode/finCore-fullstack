using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinCore.Api.Controllers;
using FinCore.Application.Auth.Me;
using FinCore.Application.Common.Abstractions;
using Microsoft.Data.SqlClient;

namespace FinCore.Tests.Integration;

/// <summary>HU-01, HU-04, HU-27, HU-28, HU-29 contra la API y la base reales.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthApiTests(FinCoreApiFactory factory)
{
    private const string CookieName = "fincore_rt";

    private async Task<(HttpResponseMessage Response, SessionResponse? Session, string? RefreshCookie)> LoginAsync(
        HttpClient client, string userName, string password, string? correlationId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(userName, password)),
        };
        if (correlationId is not null) request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await client.SendAsync(request);
        var session = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<SessionResponse>() : null;
        return (response, session, ReadRefreshCookie(response));
    }

    private static string? ReadRefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Where(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal))
                     .Select(c => c.Split(';')[0][(CookieName.Length + 1)..])
                     .FirstOrDefault(v => v.Length > 0)
            : null;

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{CookieName}={refreshToken}");
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [IntegrationFact]
    public async Task Login_correcto_entrega_token_cookie_segura_y_perfil_completo()
    {
        var client = factory.CreateApiClient();
        var (response, session, cookie) = await LoginAsync(client, "admin", FinCoreApiFactory.DemoPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(session?.AccessToken));
        Assert.NotNull(cookie);
        var setCookie = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(CookieName));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(cookie!, await response.Content.ReadAsStringAsync()); // el refresh token no va en el cuerpo

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/v1/auth/me");

        Assert.Equal("admin", me!.UserName);
        Assert.Contains("ADMIN", me.Roles);
        Assert.Contains("users.manage", me.Permissions);
        Assert.Equal(4, me.Branches.Count);
    }

    [IntegrationFact]
    public async Task El_CorrelationId_de_la_peticion_llega_a_la_respuesta_y_a_la_auditoria()
    {
        var correlationId = "it-" + Guid.NewGuid().ToString("N");
        var (response, _, _) = await LoginAsync(factory.CreateApiClient(), "supervisor", FinCoreApiFactory.DemoPassword, correlationId);

        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-Id").Single());
        var action = await FinCoreApiFactory.ScalarAsync<string>(
            "SELECT Action FROM audit.AuditLogs WHERE CorrelationId = @c", new SqlParameter("@c", correlationId));
        Assert.Equal(AuditActions.Login, action);
    }

    [IntegrationFact]
    public async Task Contrasena_incorrecta_responde_401_problem_json_con_correlationId()
    {
        var (response, _, cookie) = await LoginAsync(factory.CreateApiClient(), "auditor", "incorrecta");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(cookie);
        var problem = await ProblemAsync(response);
        Assert.Equal("auth.invalid_credentials", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("correlationId").GetString()));

        // Deja al auditor sin intentos acumulados para las demás pruebas.
        await LoginAsync(factory.CreateApiClient(), "auditor", FinCoreApiFactory.DemoPassword);
    }

    [IntegrationFact]
    public async Task Datos_vacios_responden_400_con_el_error_de_cada_campo()
    {
        var (response, _, _) = await LoginAsync(factory.CreateApiClient(), "", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("userName", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [IntegrationFact]
    public async Task Cinco_intentos_fallidos_bloquean_la_cuenta_con_423()
    {
        var userName = "it.lock." + Guid.NewGuid().ToString("N")[..8];
        var userId = await FinCoreApiFactory.CreateTemporaryUserAsync(userName, "Correcta*2026");
        try
        {
            var client = factory.CreateApiClient();
            HttpResponseMessage? last = null;
            for (var i = 0; i < 5; i++) (last, _, _) = await LoginAsync(client, userName, "mala");

            Assert.Equal(HttpStatusCode.Locked, last!.StatusCode);
            Assert.Equal("auth.locked_out", (await ProblemAsync(last)).GetProperty("code").GetString());

            var (correct, _, _) = await LoginAsync(client, userName, "Correcta*2026");
            Assert.Equal(HttpStatusCode.Locked, correct.StatusCode);
        }
        finally
        {
            await FinCoreApiFactory.DeleteTemporaryUserAsync(userId);
        }
    }

    [IntegrationFact]
    public async Task Refresh_rota_el_token_y_reutilizar_el_anterior_cierra_todas_las_sesiones()
    {
        var userName = "it.refresh." + Guid.NewGuid().ToString("N")[..8];
        var userId = await FinCoreApiFactory.CreateTemporaryUserAsync(userName, "Correcta*2026");
        try
        {
            var client = factory.CreateApiClient();
            var (_, _, first) = await LoginAsync(client, userName, "Correcta*2026");

            var renewed = await RefreshAsync(client, first!);
            Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
            var second = ReadRefreshCookie(renewed);
            Assert.NotNull(second);
            Assert.NotEqual(first, second);

            var reuse = await RefreshAsync(client, first!);
            Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

            var afterReuse = await RefreshAsync(client, second!);
            Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);

            var active = await FinCoreApiFactory.ScalarAsync<int>(
                "SELECT COUNT(*) FROM sec.RefreshTokens WHERE UserId = @id AND RevokedAtUtc IS NULL", new SqlParameter("@id", userId));
            Assert.Equal(0, active);
        }
        finally
        {
            await FinCoreApiFactory.DeleteTemporaryUserAsync(userId);
        }
    }

    [IntegrationFact]
    public async Task Logout_revoca_el_refresh_token()
    {
        var client = factory.CreateApiClient();
        var (_, _, cookie) = await LoginAsync(client, "operador.bogota", FinCoreApiFactory.DemoPassword);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Add("Cookie", $"{CookieName}={cookie}");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, cookie!)).StatusCode);
    }

    [IntegrationFact]
    public async Task El_operador_de_Monteria_solo_ve_su_sucursal_y_no_administra_usuarios()
    {
        var client = factory.CreateApiClient();
        var (_, session, _) = await LoginAsync(client, "operador.monteria", FinCoreApiFactory.DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);

        var branches = await client.GetFromJsonAsync<List<AuthorizedBranch>>("/api/v1/branches");
        Assert.Equal("MON", Assert.Single(branches!).Code);

        var users = await client.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
        await ProblemAsync(users);
    }

    [IntegrationFact]
    public async Task El_administrador_lista_los_usuarios_de_su_empresa()
    {
        var client = factory.CreateApiClient();
        var (_, session, _) = await LoginAsync(client, "admin", FinCoreApiFactory.DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);

        var users = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/users");
        Assert.Contains(users!, u => u.GetProperty("userName").GetString() == "operador.monteria"
                                  && u.GetProperty("branchCodes")[0].GetString() == "MON");
    }

    [IntegrationFact]
    public async Task Sin_token_los_endpoints_responden_401_problem_json()
    {
        var response = await factory.CreateApiClient().GetAsync("/api/v1/branches");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ProblemAsync(response);
    }

    [IntegrationFact]
    public async Task Health_checks_responden_sin_autenticacion()
    {
        var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
}
