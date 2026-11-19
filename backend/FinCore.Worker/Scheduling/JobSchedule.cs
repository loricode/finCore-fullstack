using FinCore.Domain.Common;

namespace FinCore.Worker.Scheduling;

/// <summary>
/// Programación de un trabajo (sección Worker:Jobs:{Nombre}):
///   - IntervalSeconds: cada N segundos (Oracle, recuperación de eventos).
///   - DailyAt: una vez al día a esa hora de Colombia, "HH:mm" (conciliación nocturna, depuración).
/// RunOnStartup ejecuta además una vez al arrancar (útil en demostraciones).
/// </summary>
public sealed class JobSchedule
{
    public bool Enabled { get; init; } = true;
    public int? IntervalSeconds { get; init; }
    public string? DailyAt { get; init; }
    public bool RunOnStartup { get; init; }
    public int InitialDelaySeconds { get; init; } = 5;

    /// <summary>Tiempo hasta la próxima ejecución desde <paramref name="utcNow"/>.</summary>
    public TimeSpan DelayUntilNext(DateTime utcNow)
    {
        if (IntervalSeconds is { } seconds)
            return TimeSpan.FromSeconds(Math.Max(seconds, 5));

        if (DailyAt is not null && TimeOnly.TryParse(DailyAt, out var at))
        {
            var local = utcNow.Add(BusinessCalendar.Offset);           // hora de Colombia
            var next = local.Date.Add(at.ToTimeSpan());
            if (next <= local) next = next.AddDays(1);
            return next - local;
        }

        throw new InvalidOperationException("El trabajo necesita IntervalSeconds o DailyAt.");
    }
}
