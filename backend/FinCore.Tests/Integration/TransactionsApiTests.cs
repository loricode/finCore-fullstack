using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCore.Domain.Common;
using Microsoft.Data.SqlClient;

namespace FinCore.Tests.Integration;

/// <summary>Etapa 2 · HU-06 a HU-12 contra la API y la base reales (triggers incluidos).</summary>
[Collection(IntegrationCollection.Name)]
public sealed class TransactionsApiTests(FinCoreApiFactory factory)
{
    private static readonly DateOnly Today = BusinessCalendar.Today(DateTime.UtcNow);

    private static string NewReference(string prefix = "IT") => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}".ToUpperInvariant();

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<int> AccountIdAsync(HttpClient client, string code) =>
        (await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/accounts"))!
            .Single(a => a.GetProperty("code").GetString() == code).GetProperty("id").GetInt32();

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions") { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static object Income(int accountId, string reference, decimal amount = 250_000m) =>
        new { accountId, type = "ING", reference, operationDate = Today, amount, description = "Recaudo de integración" };

    private static string PublicId(JsonElement detail) => detail.GetProperty("transaction").GetProperty("publicId").GetString()!;
    private static string Status(JsonElement detail) => detail.GetProperty("transaction").GetProperty("statusCode").GetString()!;

    [IntegrationFact]
    public async Task Registrar_queda_pendiente_y_el_reintento_con_la_misma_clave_no_duplica()
    {
        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        var account = await AccountIdAsync(op, "MON-001");
        var reference = NewReference();
        var key = Guid.NewGuid().ToString("N");

        var created = await RegisterAsync(op, Income(account, reference), key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var detail = await JsonAsync(created);
        Assert.Equal("PENDING", Status(detail));
        Assert.Equal(reference, detail.GetProperty("transaction").GetProperty("reference").GetString());
        Assert.True(detail.GetProperty("actions").GetProperty("canEdit").GetBoolean());
        Assert.False(detail.GetProperty("actions").GetProperty("canProcess").GetBoolean()); // el operador no procesa

        var replay = await RegisterAsync(op, Income(account, reference), key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(PublicId(detail), PublicId(await JsonAsync(replay)));

        var conflict = await RegisterAsync(op, Income(account, reference, 999_999m), key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflict.StatusCode);

        var duplicate = await RegisterAsync(op, Income(account, reference));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var audited = await FinCoreApiFactory.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit.AuditLogs WHERE Action = 'TRANSACTION_CREATED' AND EntityId = @id AND BranchId IS NOT NULL",
            new SqlParameter("@id", PublicId(detail)));
        Assert.Equal(1, audited);
    }

    [IntegrationFact]
    public async Task Datos_invalidos_responden_400_con_el_error_de_cada_campo()
    {
        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        var account = await AccountIdAsync(op, "MON-001");

        var response = await RegisterAsync(op, new { accountId = account, type = "TRF", reference = "ab", operationDate = Today.AddDays(1), amount = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await JsonAsync(response)).GetProperty("errors");
        foreach (var field in new[] { "reference", "operationDate", "amount", "counterpartyName", "counterpartyAccount" })
            Assert.True(errors.TryGetProperty(field, out _), $"Falta el error de {field}");
    }

    [IntegrationFact]
    public async Task El_operador_de_Monteria_no_registra_ni_ve_operaciones_de_Bogota()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var bogota = await AccountIdAsync(supervisor, "BOG-001");
        var bogotaTx = PublicId(await JsonAsync(await RegisterAsync(supervisor, Income(bogota, NewReference("BOG")))));

        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        Assert.Equal(HttpStatusCode.Forbidden, (await RegisterAsync(op, Income(bogota, NewReference()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync($"/api/v1/transactions/{bogotaTx}")).StatusCode);

        var page = await op.GetFromJsonAsync<JsonElement>("/api/v1/transactions?pageSize=100");
        Assert.All(page.GetProperty("items").EnumerateArray(), t => Assert.Equal("MON", t.GetProperty("branchCode").GetString()));

        var accounts = await op.GetFromJsonAsync<List<JsonElement>>("/api/v1/accounts");
        Assert.Equal("MON-001", Assert.Single(accounts!).GetProperty("code").GetString());
    }

    [IntegrationFact]
    public async Task Procesar_y_revertir_deja_historial_con_usuario_y_correlacion()
    {
        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        var account = await AccountIdAsync(op, "MON-001");
        var id = PublicId(await JsonAsync(await RegisterAsync(op, Income(account, NewReference()))));

        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsync($"/api/v1/transactions/{id}/process", null)).StatusCode);

        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var correlationId = "it-process-" + Guid.NewGuid().ToString("N")[..8];
        var processRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/transactions/{id}/process");
        processRequest.Headers.Add("X-Correlation-Id", correlationId);
        var processed = await JsonAsync(await supervisor.SendAsync(processRequest));
        Assert.Equal("PROCESSED", Status(processed));

        var step = processed.GetProperty("history").EnumerateArray().Last();
        Assert.Equal("Procesada", step.GetProperty("toStatus").GetString());
        Assert.Equal("supervisor", step.GetProperty("changedBy").GetString());
        Assert.Equal(correlationId, step.GetProperty("correlationId").GetString()); // trigger + SESSION_CONTEXT

        var noReason = await supervisor.PostAsJsonAsync($"/api/v1/transactions/{id}/reverse", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var reversed = await supervisor.PostAsJsonAsync($"/api/v1/transactions/{id}/reverse", new { reason = "Registrada por error" });
        Assert.Equal(HttpStatusCode.Created, reversed.StatusCode);
        var reversal = (await JsonAsync(reversed)).GetProperty("transaction");
        Assert.Equal("REV", reversal.GetProperty("typeCode").GetString());
        Assert.Equal("PROCESSED", reversal.GetProperty("statusCode").GetString());
        Assert.Equal(-250_000m, reversal.GetProperty("signedAmount").GetDecimal());
        Assert.Equal(id, reversal.GetProperty("originalPublicId").GetString());

        var original = await supervisor.GetFromJsonAsync<JsonElement>($"/api/v1/transactions/{id}");
        Assert.Equal("REVERSED", Status(original));
        Assert.False(original.GetProperty("actions").GetProperty("canReverse").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict,
            (await supervisor.PostAsJsonAsync($"/api/v1/transactions/{id}/reverse", new { reason = "otra vez" })).StatusCode);
    }

    [IntegrationFact]
    public async Task Doble_control_quien_registra_diez_millones_no_los_procesa()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var account = await AccountIdAsync(supervisor, "MED-001");
        var id = PublicId(await JsonAsync(await RegisterAsync(supervisor, Income(account, NewReference(), 10_000_000m))));

        var own = await supervisor.PostAsync($"/api/v1/transactions/{id}/process", null);
        Assert.Equal(HttpStatusCode.Conflict, own.StatusCode);
        Assert.Equal("transaction.dual_control", (await JsonAsync(own)).GetProperty("code").GetString());

        var admin = await factory.CreateAuthenticatedClientAsync("admin");
        Assert.Equal("PROCESSED", Status(await JsonAsync(await admin.PostAsync($"/api/v1/transactions/{id}/process", null))));
    }

    [IntegrationFact]
    public async Task Rechazar_exige_motivo_y_una_rechazada_ya_no_se_procesa()
    {
        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        var id = PublicId(await JsonAsync(await RegisterAsync(op, Income(await AccountIdAsync(op, "MON-001"), NewReference()))));
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");

        Assert.Equal(HttpStatusCode.BadRequest, (await supervisor.PostAsJsonAsync($"/api/v1/transactions/{id}/reject", new { reason = " " })).StatusCode);

        var rejected = await JsonAsync(await supervisor.PostAsJsonAsync($"/api/v1/transactions/{id}/reject", new { reason = "Sin soporte" }));
        Assert.Equal("REJECTED", Status(rejected));
        Assert.Equal("Sin soporte", rejected.GetProperty("history").EnumerateArray().Last().GetProperty("reason").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await supervisor.PostAsync($"/api/v1/transactions/{id}/process", null)).StatusCode);
    }

    [IntegrationFact]
    public async Task Editar_una_pendiente_con_version_vieja_responde_409()
    {
        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        var created = await JsonAsync(await RegisterAsync(op, Income(await AccountIdAsync(op, "MON-001"), NewReference())));
        var id = PublicId(created);
        var rowVersion = created.GetProperty("transaction").GetProperty("rowVersion").GetString();
        var change = new { operationDate = Today, amount = 260_000m, description = "Corregido", rowVersion };

        var updated = await op.PutAsJsonAsync($"/api/v1/transactions/{id}", change);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(260_000m, (await JsonAsync(updated)).GetProperty("transaction").GetProperty("amount").GetDecimal());

        Assert.Equal(HttpStatusCode.Conflict, (await op.PutAsJsonAsync($"/api/v1/transactions/{id}", change)).StatusCode);
    }

    [IntegrationFact]
    public async Task El_resumen_cuenta_las_pendientes_de_las_sucursales_autorizadas()
    {
        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        var before = (await op.GetFromJsonAsync<JsonElement>("/api/v1/transactions/summary")).GetProperty("pendingCount").GetInt32();
        await RegisterAsync(op, Income(await AccountIdAsync(op, "MON-001"), NewReference()));
        var after = (await op.GetFromJsonAsync<JsonElement>("/api/v1/transactions/summary")).GetProperty("pendingCount").GetInt32();
        Assert.Equal(before + 1, after);
    }
}
