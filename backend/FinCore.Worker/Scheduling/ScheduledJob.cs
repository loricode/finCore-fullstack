using Serilog.Context;

namespace FinCore.Worker.Scheduling;

/// <summary>
/// Base de los trabajos programados. Cada ejecución:
///   - crea un ámbito de servicios nuevo (DbContext limpio),
///   - abre un CorrelationId propio (logs, auditoría y SESSION_CONTEXT de SQL lo llevan),
///   - registra el resultado; un error no detiene el Worker: se registra y se reintenta en la siguiente ejecución.
/// </summary>
public abstract class ScheduledJob(IServiceScopeFactory scopes, WorkerStartup startup, IConfiguration configuration, ILogger logger)
    : BackgroundService
{
    /// <summary>Nombre del trabajo: clave de configuración (Worker:Jobs:{Name}) y prefijo del CorrelationId.</summary>
    protected abstract string Name { get; }

    protected abstract Task RunAsync(IServiceProvider services, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = configuration.GetSection($"Worker:Jobs:{Name}").Get<JobSchedule>() ?? new JobSchedule();
        if (!schedule.Enabled)
        {
            logger.LogInformation("Trabajo {Job} desactivado por configuración", Name);
            return;
        }

        await startup.WaitUntilReadyAsync(stoppingToken);
        await Task.Delay(TimeSpan.FromSeconds(schedule.InitialDelaySeconds), stoppingToken);

        if (schedule.RunOnStartup) await RunOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = schedule.DelayUntilNext(DateTime.UtcNow);
            logger.LogDebug("Trabajo {Job}: próxima ejecución en {Delay}", Name, delay);
            await Task.Delay(delay, stoppingToken);
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var correlationId = $"{Name.ToLowerInvariant()}-{Guid.NewGuid():N}"[..Math.Min(64, Name.Length + 33)];
        using var run = WorkerExecutionContext.BeginRun(correlationId);
        using var log = LogContext.PushProperty("CorrelationId", correlationId);
        using var job = LogContext.PushProperty("Job", Name);

        var started = DateTime.UtcNow;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await RunAsync(scope.ServiceProvider, ct);
            logger.LogInformation("Trabajo {Job} terminado en {ElapsedMs} ms", Name, (int)(DateTime.UtcNow - started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // el servicio se está deteniendo
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Trabajo {Job} falló; se reintentará en la próxima ejecución", Name);
        }
    }
}
