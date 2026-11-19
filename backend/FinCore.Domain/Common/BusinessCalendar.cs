namespace FinCore.Domain.Common;

/// <summary>
/// Fecha de negocio: hora de Colombia (UTC-5 todo el año, sin horario de verano).
/// Es la misma regla de dbo.fn_BusinessDate en la base.
/// </summary>
public static class BusinessCalendar
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(utcNow.Add(Offset));
}
