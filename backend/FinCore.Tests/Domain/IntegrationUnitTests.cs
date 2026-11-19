extern alias worker;
using FinCore.Domain.Integration;
using worker::FinCore.Worker.Scheduling;

namespace FinCore.Tests.Domain;

public sealed class InboundEventTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    private static InboundEvent New() => InboundEvent.Receive("banco-simulado", "evt_1", "movement.created", "{}", new string('a', 64), "c1", Now);

    [Fact]
    public void Un_evento_recibido_se_procesa_y_ya_no_se_vuelve_a_procesar()
    {
        var e = New();
        Assert.True(e.CanBeProcessed);
        e.StartAttempt();
        e.MarkProcessed(42, Now);

        Assert.Equal(InboundEventStatus.Processed, e.Status);
        Assert.Equal(42, e.BankStatementDetailId);
        Assert.False(e.CanBeProcessed);
    }

    [Fact]
    public void Tras_cinco_intentos_no_se_reintenta_mas()
    {
        var e = New();
        for (var i = 0; i < InboundEvent.MaxAttempts; i++) e.StartAttempt();
        Assert.False(e.CanBeProcessed);
    }

    [Fact]
    public void Fallido_e_ignorado_son_finales()
    {
        var failed = New();
        failed.MarkFailed("cuenta inexistente", Now);
        Assert.False(failed.CanBeProcessed);

        var ignored = New();
        ignored.MarkIgnored("ya cargado", Now);
        Assert.Equal(InboundEventStatus.Ignored, ignored.Status);
        Assert.False(ignored.CanBeProcessed);
    }
}

public sealed class JobScheduleTests
{
    [Fact]
    public void Por_intervalo_espera_los_segundos_configurados_con_un_minimo_de_5()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), new JobSchedule { IntervalSeconds = 60 }.DelayUntilNext(DateTime.UtcNow));
        Assert.Equal(TimeSpan.FromSeconds(5), new JobSchedule { IntervalSeconds = 1 }.DelayUntilNext(DateTime.UtcNow));
    }

    [Fact]
    public void Diario_calcula_la_hora_de_Colombia()
    {
        var schedule = new JobSchedule { DailyAt = "02:00" };
        // 19/11/2026 06:00 UTC = 01:00 en Colombia → faltan 60 minutos para las 02:00
        Assert.Equal(TimeSpan.FromHours(1), schedule.DelayUntilNext(new DateTime(2026, 11, 19, 6, 0, 0, DateTimeKind.Utc)));
        // 19/11/2026 08:00 UTC = 03:00 en Colombia → ya pasó: mañana a las 02:00 (23 h)
        Assert.Equal(TimeSpan.FromHours(23), schedule.DelayUntilNext(new DateTime(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Sin_intervalo_ni_hora_es_un_error_de_configuracion() =>
        Assert.Throws<InvalidOperationException>(() => new JobSchedule().DelayUntilNext(DateTime.UtcNow));
}
