using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinCore.Domain.Common;

namespace FinCore.Tests.Integration;

/// <summary>
/// Etapa 3 · HU-13 a HU-18 contra la API y la base reales.
/// Requiere la base de pruebas recién creada con la demo (docker compose --profile test: FinCore_Tests se recrea en cada corrida).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ReconciliationApiTests(FinCoreApiFactory factory)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<int> AccountIdAsync(HttpClient client, string code) =>
        (await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/accounts"))!
            .Single(a => a.GetProperty("code").GetString() == code).GetProperty("id").GetInt32();

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string csv, string fileName = "extracto.csv")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return client.PostAsync("/api/v1/bank-statements/import", content);
    }

    [IntegrationFact]
    public async Task Conciliar_Monteria_produce_los_siete_tipos_de_resultado_y_la_bandeja()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var account = await AccountIdAsync(supervisor, "MON-001");

        var response = await supervisor.PostAsJsonAsync("/api/v1/reconciliations",
            new { accountId = account, periodFrom = "2026-11-01", periodTo = "2026-11-15", dateToleranceDays = 1 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var s = await JsonAsync(response);
        Assert.Equal("COMPLETED", s.GetProperty("status").GetString());
        Assert.Equal(4, s.GetProperty("matchedCount").GetInt32());
        Assert.Equal(1, s.GetProperty("amountMismatchCount").GetInt32());
        Assert.Equal(1, s.GetProperty("dateMismatchCount").GetInt32());
        Assert.Equal(1, s.GetProperty("missingInBankCount").GetInt32());
        Assert.Equal(1, s.GetProperty("missingInternalCount").GetInt32());
        Assert.Equal(2, s.GetProperty("duplicateReferenceCount").GetInt32());
        Assert.Equal(1, s.GetProperty("pendingReviewCount").GetInt32());
        Assert.Equal("api", s.GetProperty("executedBy").GetString());

        var id = s.GetProperty("reconciliationId").GetInt32();
        var detail = await supervisor.GetFromJsonAsync<JsonElement>($"/api/v1/reconciliations/{id}");
        Assert.Equal(11, detail.GetProperty("details").GetArrayLength());

        var inbox = await supervisor.GetFromJsonAsync<List<JsonElement>>("/api/v1/reconciliations/open-differences");
        var mine = inbox!.Where(d => d.GetProperty("reconciliationId").GetInt32() == id).ToList();
        Assert.Equal(7, mine.Count);

        // Resolver una diferencia: sin nota 400, con nota 204, dos veces 409; desaparece de la bandeja.
        var commission = mine.Single(d => d.GetProperty("resultTypeCode").GetString() == "MISSING_INTERNAL");
        var detailId = commission.GetProperty("reconciliationDetailId").GetInt64();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await supervisor.PostAsJsonAsync($"/api/v1/reconciliation-details/{detailId}/resolve", new { note = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await supervisor.PostAsJsonAsync($"/api/v1/reconciliation-details/{detailId}/resolve", new { note = "Comisión: se registra egreso" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await supervisor.PostAsJsonAsync($"/api/v1/reconciliation-details/{detailId}/resolve", new { note = "otra" })).StatusCode);
        var after = await supervisor.GetFromJsonAsync<List<JsonElement>>("/api/v1/reconciliations/open-differences");
        Assert.DoesNotContain(after!, d => d.GetProperty("reconciliationDetailId").GetInt64() == detailId);

        // Un resultado conciliado no se resuelve.
        var matched = detail.GetProperty("details").EnumerateArray().First(d => d.GetProperty("resultTypeCode").GetString() == "MATCHED");
        Assert.Equal(HttpStatusCode.Conflict, (await supervisor.PostAsJsonAsync(
            $"/api/v1/reconciliation-details/{matched.GetProperty("id").GetInt64()}/resolve", new { note = "x" })).StatusCode);
    }

    [IntegrationFact]
    public async Task Periodo_invalido_responde_400_y_el_operador_no_concilia()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var account = await AccountIdAsync(supervisor, "MED-001");

        var inverted = await supervisor.PostAsJsonAsync("/api/v1/reconciliations",
            new { accountId = account, periodFrom = "2026-11-15", periodTo = "2026-11-01" });
        Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);

        var tooLong = await supervisor.PostAsJsonAsync("/api/v1/reconciliations",
            new { accountId = account, periodFrom = "2026-01-01", periodTo = "2026-11-01" });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/v1/reconciliations",
            new { accountId = account, periodFrom = "2026-11-01", periodTo = "2026-11-15" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(op, "account_number;movement_id;reference;date;amount\nX;1;R;2026-11-01;1")).StatusCode);
    }

    [IntegrationFact]
    public async Task Importar_un_CSV_reporta_cargados_y_rechazados_y_una_recarga_no_duplica()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var today = BusinessCalendar.Today(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var csv = "account_number;movement_id;reference;date;amount;description\n" +
                  $"444-222333-04;IT-{suffix}-1;ING-{suffix};{today};150000.00;Consignación\n" +
                  $"444-222333-04;IT-{suffix}-2;PAG-{suffix};{today};-80000;Pago\n" +
                  $"444-222333-04;IT-{suffix}-3;X-{suffix};19/11/2026;100;Fecha mala\n" +
                  $"000-000000-00;IT-{suffix}-4;Y-{suffix};{today};100;Cuenta inexistente\n";

        var first = await UploadAsync(supervisor, csv);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var r = await JsonAsync(first);
        Assert.Equal("COMPLETED_WITH_ERRORS", r.GetProperty("status").GetString());
        Assert.Equal(4, r.GetProperty("rowsRead").GetInt32());
        Assert.Equal(2, r.GetProperty("rowsLoaded").GetInt32());
        Assert.Equal(2, r.GetProperty("rowsRejected").GetInt32());
        var codes = r.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("errorCode").GetString()).ToList();
        Assert.Equal(["INVALID_DATE", "ACCOUNT_NOT_FOUND"], codes);
        Assert.Equal(4, r.GetProperty("errors")[0].GetProperty("rowNumber").GetInt32()); // número de línea del archivo

        var again = await JsonAsync(await UploadAsync(supervisor, csv));
        Assert.Equal(0, again.GetProperty("rowsLoaded").GetInt32());
        Assert.Contains(again.GetProperty("errors").EnumerateArray(), e => e.GetProperty("errorCode").GetString() == "ALREADY_LOADED");

        var history = await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/etl-executions");
        Assert.Equal(again.GetProperty("etlExecutionId").GetInt64(), history.GetProperty("items")[0].GetProperty("etlExecutionId").GetInt64());
    }

    [IntegrationFact]
    public async Task Un_archivo_sin_el_formato_esperado_responde_400_sin_abrir_una_carga()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var before = (await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/etl-executions")).GetProperty("totalCount").GetInt32();

        var badHeader = await UploadAsync(supervisor, "cuenta;valor\n1;2\n");
        Assert.Equal(HttpStatusCode.BadRequest, badHeader.StatusCode);
        Assert.True((await JsonAsync(badHeader)).GetProperty("errors").TryGetProperty("file", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(supervisor, "a;b", "extracto.xlsx")).StatusCode);

        var after = (await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/etl-executions")).GetProperty("totalCount").GetInt32();
        Assert.Equal(before, after);
    }

    [IntegrationFact]
    public async Task Sin_mantener_sesion_la_cookie_de_refresco_es_de_sesion()
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "auditor", password = FinCoreApiFactory.DemoPassword, rememberMe = false });
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.DoesNotContain("expires", cookies.First(c => c.StartsWith("fincore_rt=")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cookies, c => c.StartsWith("fincore_rp=0"));

        var persistent = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "auditor", password = FinCoreApiFactory.DemoPassword, rememberMe = true });
        Assert.Contains("expires", persistent.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("fincore_rt=")),
            StringComparison.OrdinalIgnoreCase);
    }
}
