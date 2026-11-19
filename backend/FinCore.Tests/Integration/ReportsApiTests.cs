using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace FinCore.Tests.Integration;

/// <summary>Etapa 5 · HU-23 a HU-26 contra la API y la base reales.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class ReportsApiTests(FinCoreApiFactory factory)
{
    private const string Period = "from=2026-11-01&to=2026-11-30";

    [IntegrationFact]
    public async Task Los_indicadores_y_el_resumen_por_sucursal_salen_de_los_procedimientos_rpt()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");

        var indicators = await supervisor.GetFromJsonAsync<JsonElement>($"/api/v1/reports/indicators?{Period}");
        Assert.True(indicators.GetProperty("operationCount").GetInt32() >= 19);
        Assert.Equal(indicators.GetProperty("totalIncome").GetDecimal() - indicators.GetProperty("totalExpense").GetDecimal(),
            indicators.GetProperty("netAmount").GetDecimal());

        var branches = await supervisor.GetFromJsonAsync<List<JsonElement>>($"/api/v1/reports/branch-summary?{Period}");
        Assert.Equal(4, branches!.Count);
        var monteria = branches.Single(b => b.GetProperty("branchCode").GetString() == "MON");
        Assert.True(monteria.GetProperty("totalOperations").GetInt32() >= 10);

        var byMonth = await supervisor.GetFromJsonAsync<List<JsonElement>>($"/api/v1/reports/operations-by-period?{Period}&granularity=MONTH");
        Assert.All(byMonth!, r => Assert.Equal("2026-11-01", r.GetProperty("periodStart").GetString()));
    }

    [IntegrationFact]
    public async Task Exportar_a_CSV_entrega_un_archivo_para_Excel_y_queda_auditado()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var correlationId = "it-export-" + Guid.NewGuid().ToString("N")[..8];
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/reports/branch-summary?{Period}&format=csv");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await supervisor.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("fincore-resumen-sucursales-2026-11-01_2026-11-30.csv", response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Código;Sucursal;Ciudad;Operaciones;Ingresos;Egresos;Neto;Pendientes;Procesadas;Conciliadas;Rechazadas;Revertidas", lines[0]);
        Assert.Equal(5, lines.Length); // encabezado + 4 sucursales
        Assert.Contains(lines, l => l.StartsWith("MON;Sucursal Montería;Montería;", StringComparison.Ordinal));

        var audited = await FinCoreApiFactory.ScalarAsync<string>(
            "SELECT EntityId FROM audit.AuditLogs WHERE Action = 'EXPORT' AND CorrelationId = @c", new SqlParameter("@c", correlationId));
        Assert.Equal("resumen-sucursales", audited);
    }

    [IntegrationFact]
    public async Task Permisos_operador_sin_reportes_supervisor_sin_actividad_y_auditor_con_actividad()
    {
        var op = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync($"/api/v1/reports/indicators?{Period}")).StatusCode);

        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        var activity = await supervisor.GetAsync($"/api/v1/reports/user-activity?{Period}");
        Assert.Equal(HttpStatusCode.Forbidden, activity.StatusCode); // rpt.usp_UserActivity exige audit.read (SQL 50304)

        var auditor = await factory.CreateAuthenticatedClientAsync("auditor");
        var rows = await auditor.GetFromJsonAsync<List<JsonElement>>($"/api/v1/reports/user-activity?{Period}");
        Assert.Contains(rows!, r => r.GetProperty("userName").GetString() == "operador.monteria");
    }

    [IntegrationFact]
    public async Task Periodo_invertido_o_granularidad_desconocida_responden_400()
    {
        var supervisor = await factory.CreateAuthenticatedClientAsync("supervisor");
        Assert.Equal(HttpStatusCode.BadRequest, (await supervisor.GetAsync("/api/v1/reports/indicators?from=2026-11-30&to=2026-11-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await supervisor.GetAsync($"/api/v1/reports/operations-by-period?{Period}&granularity=WEEK")).StatusCode);
    }
}
