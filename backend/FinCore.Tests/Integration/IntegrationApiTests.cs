using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCore.Application.Common.Abstractions;
using FinCore.Application.Integration;
using FinCore.Domain.Common;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Tests.Integration;

/// <summary>Etapa 4 · HU-19 a HU-21 contra la API y la base reales.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class IntegrationApiTests(FinCoreApiFactory factory)
{
    private static readonly string Today = BusinessCalendar.Today(DateTime.UtcNow).ToString("yyyy-MM-dd");

    // Instante fijo: un reenvío debe ser byte a byte igual (con DateTime.UtcNow cambiaría el hash y la API respondería 409).
    private static readonly DateTime OccurredAt = DateTime.UtcNow;

    private static object Event(string eventId, string account, string movementId, decimal amount = 125000.50m) => new
    {
        provider = "banco-simulado",
        externalEventId = eventId,
        eventType = "movement.created",
        occurredAt = OccurredAt,
        movement = new { accountNumber = account, movementId, reference = $"wh-{movementId}", date = Today, amount, description = "ABONO EN LINEA" },
    };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [IntegrationFact]
    public async Task Un_evento_nuevo_crea_el_movimiento_y_reenviarlo_no_lo_duplica()
    {
        var svc = await factory.CreateAuthenticatedClientAsync("svc.integration");
        var id = "evt_" + Guid.NewGuid().ToString("N")[..10];
        var movementId = "WH-" + Guid.NewGuid().ToString("N")[..8];

        var first = await svc.PostAsJsonAsync("/api/v1/integrations/bank-events", Event(id, "123-456789-01", movementId));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var r1 = await JsonAsync(first);
        Assert.Equal("PROCESSED", r1.GetProperty("status").GetString());
        Assert.False(r1.GetProperty("duplicate").GetBoolean());
        var detailId = r1.GetProperty("bankStatementDetailId").GetInt64();

        var again = await JsonAsync(await svc.PostAsJsonAsync("/api/v1/integrations/bank-events", Event(id, "123-456789-01", movementId)));
        Assert.True(again.GetProperty("duplicate").GetBoolean());
        Assert.Equal(detailId, again.GetProperty("bankStatementDetailId").GetInt64());

        var conflict = await svc.PostAsJsonAsync("/api/v1/integrations/bank-events", Event(id, "123-456789-01", movementId, 1m));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var stored = await FinCoreApiFactory.ScalarAsync<string>("""
            SELECT CONCAT(s.Source, '|', d.BankReference, '|', d.Amount)
            FROM rec.BankStatementDetails d JOIN rec.BankStatements s ON s.Id = d.BankStatementId WHERE d.Id = @id
            """, new SqlParameter("@id", detailId));
        Assert.Equal($"WEBHOOK|WH-{movementId}|125000.50".ToUpperInvariant(), stored); // referencia normalizada en mayúsculas
    }

    [IntegrationFact]
    public async Task Cuenta_inexistente_responde_422_y_un_movimiento_ya_cargado_queda_ignorado()
    {
        var svc = await factory.CreateAuthenticatedClientAsync("svc.integration");

        var unknown = await svc.PostAsJsonAsync("/api/v1/integrations/bank-events",
            Event("evt_" + Guid.NewGuid().ToString("N")[..10], "000-000000-00", "WH-X"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        var problem = await JsonAsync(unknown);
        Assert.Equal("integration.business_error", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("000-000000-00", problem.GetProperty("detail").GetString()); // cuenta enmascarada

        // BCO-0001 ya llegó por el extracto CSV de la demo: el webhook no lo duplica.
        var known = await JsonAsync(await svc.PostAsJsonAsync("/api/v1/integrations/bank-events",
            Event("evt_" + Guid.NewGuid().ToString("N")[..10], "123-456789-01", "BCO-0001")));
        Assert.Equal("IGNORED", known.GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task Solo_la_cuenta_de_integracion_puede_enviar_eventos()
    {
        var operatorClient = await factory.CreateAuthenticatedClientAsync("operador.monteria");
        var response = await operatorClient.PostAsJsonAsync("/api/v1/integrations/bank-events", Event("evt_x", "123-456789-01", "WH-Y"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationFact]
    public async Task La_extraccion_de_Oracle_es_incremental_por_marca_de_agua()
    {
        var reader = new FakeLegacyReader();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        reader.Rows.AddRange([
            new LegacyMovement(10, "444-222333-04", $"ORA-{suffix}-1", $"ING-{suffix}", Today, "500000.00", "CONSIGNACION"),
            new LegacyMovement(11, "444-222333-04", $"ORA-{suffix}-2", $"PAG-{suffix}", Today, "-120000.00", "PAGO"),
            new LegacyMovement(12, "999-999999-99", $"ORA-{suffix}-3", "X", Today, "-1.00", "OTRA ENTIDAD"),
        ]);

        await using var scope = await WorkerScopeAsync(reader);
        var handler = scope.ServiceProvider.GetRequiredService<ImportFromLegacyBankHandler>();

        var first = await handler.HandleAsync(default);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(0, first.Value.PreviousWatermark);
        Assert.Equal(12, first.Value.NewWatermark);
        Assert.Equal(2, first.Value.Import!.RowsLoaded);
        Assert.Equal("ACCOUNT_NOT_FOUND", Assert.Single(first.Value.Import.Errors).ErrorCode);

        var source = await FinCoreApiFactory.ScalarAsync<string>(
            "SELECT SourceSystem FROM etl.ETLExecutions WHERE Id = @id", new SqlParameter("@id", first.Value.Import.EtlExecutionId));
        Assert.Equal("ORACLE", source);

        var second = await handler.HandleAsync(default);
        Assert.Null(second.Value.Import); // nada nuevo después de la marca de agua

        reader.Rows.Add(new LegacyMovement(13, "444-222333-04", $"ORA-{suffix}-4", $"ING2-{suffix}", Today, "80000", "NUEVO"));
        var third = await handler.HandleAsync(default);
        Assert.Equal(1, third.Value.Import!.RowsRead);
        Assert.Equal(13, third.Value.NewWatermark);
    }

    [IntegrationFact]
    public async Task La_conciliacion_programada_recorre_todas_las_cuentas_activas()
    {
        await using var scope = await WorkerScopeAsync(new FakeLegacyReader());
        var result = await scope.ServiceProvider.GetRequiredService<ScheduledReconciliationHandler>().HandleAsync(1, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Count);
        Assert.All(result.Value, r => Assert.Equal("COMPLETED", r.Outcome));
    }

    /// <summary>Ámbito de servicios como el del Worker: identidad de svc.integration y lector de Oracle simulado.</summary>
    private async Task<AsyncServiceScope> WorkerScopeAsync(ILegacyBankReader reader)
    {
        var userId = await FinCoreApiFactory.ScalarAsync<int>("SELECT Id FROM sec.Users WHERE UserName = 'svc.integration'");
        var companyId = await FinCoreApiFactory.ScalarAsync<int>("SELECT CompanyId FROM sec.Users WHERE Id = @id", new SqlParameter("@id", userId));
        var context = new WorkerTestContext(userId, companyId);

        var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(reader);
            s.AddScoped<ICurrentUser>(_ => context);
            s.AddScoped<IRequestContext>(_ => context);
        }));
        return host.Services.CreateAsyncScope();
    }

    private sealed class WorkerTestContext(int userId, int companyId) : ICurrentUser, IRequestContext
    {
        public bool IsAuthenticated => true;
        public int? UserId => userId;
        public int? CompanyId => companyId;
        public string? UserName => "svc.integration";
        public bool HasPermission(string permission) => true;
        public string CorrelationId { get; } = "it-worker-" + Guid.NewGuid().ToString("N")[..8];
        public string? IpAddress => null;
    }

    private sealed class FakeLegacyReader : ILegacyBankReader
    {
        public List<LegacyMovement> Rows { get; } = [];
        public string SourceName { get; } = "TEST:ORACLE-" + Guid.NewGuid().ToString("N")[..8];
        public bool IsConfigured => true;

        public Task<IReadOnlyList<LegacyMovement>> ReadAfterAsync(long lastSequence, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LegacyMovement>>(Rows.Where(r => r.Sequence > lastSequence).Take(maxRows).ToList());
    }
}
