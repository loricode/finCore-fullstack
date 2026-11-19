using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace FinCore.Tests.Integration;

/// <summary>
/// Prueba que necesita la base FinCore desplegada (docker compose up db db-migrator).
/// Se omite si no está definida la variable FINCORE_TEST_CONNECTION, para que las pruebas unitarias
/// corran en cualquier equipo.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(FinCoreApiFactory.ConnectionString))
            Skip = "Define FINCORE_TEST_CONNECTION para ejecutar las pruebas de integración.";
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<FinCoreApiFactory>
{
    public const string Name = "integración";
}

/// <summary>Levanta la API completa en memoria contra la base real.</summary>
public sealed class FinCoreApiFactory : WebApplicationFactory<Program>
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("FINCORE_TEST_CONNECTION");

    public const string DemoPassword = "FinCore*2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FinCore", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", "clave-de-pruebas-de-integracion-de-32-caracteres-o-mas");
        builder.UseSetting("RateLimiting:LoginPerMinute", "1000");
        builder.UseSetting("Logging:Json", "false");
    }

    /// <summary>Cliente sin manejo automático de cookies: cada prueba controla la cookie de refresh.</summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
        AllowAutoRedirect = false,
    });

    /// <summary>Cliente con sesión iniciada como <paramref name="userName"/> (access token en la cabecera Authorization).</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string userName, string password = DemoPassword)
    {
        var client = CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        return client;
    }

    public static async Task<T?> ScalarAsync<T>(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    public static async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Crea un usuario operador temporal en la empresa de demostración. Devuelve su Id.</summary>
    public static async Task<int> CreateTemporaryUserAsync(string userName, string password)
    {
        var hash = new PasswordHasher<object>().HashPassword(new object(), password);
        return await ScalarAsync<int>("""
            DECLARE @company INT = (SELECT Id FROM sec.Companies WHERE TaxId = N'900123456-7');
            INSERT sec.Users (CompanyId, UserName, Email, FullName, PasswordHash)
            VALUES (@company, @userName, @userName + N'@test.local', N'Usuario temporal de prueba', @hash);
            DECLARE @id INT = CAST(SCOPE_IDENTITY() AS INT);
            INSERT sec.UserRoles (UserId, RoleId) VALUES (@id, 3);
            SELECT @id;
            """, new SqlParameter("@userName", userName), new SqlParameter("@hash", hash));
    }

    /// <summary>Borra el usuario temporal. Su auditoría se conserva (es de solo inserción y no tiene FK al usuario).</summary>
    public static Task DeleteTemporaryUserAsync(int userId) => ExecuteAsync("""
        DELETE sec.RefreshTokens WHERE UserId = @id;
        DELETE sec.UserRoles WHERE UserId = @id;
        DELETE sec.UserBranches WHERE UserId = @id;
        DELETE sec.Users WHERE Id = @id;
        """, new SqlParameter("@id", userId));
}
