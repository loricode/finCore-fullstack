using System.Text.RegularExpressions;
using Serilog.Context;

namespace FinCore.Api.Middlewares;

/// <summary>
/// Asigna a cada petición un CorrelationId: el de la cabecera X-Correlation-Id si es válido, o uno nuevo.
/// Lo devuelve en la respuesta y lo agrega a todos los logs de la petición. La auditoría y SQL Server
/// (SESSION_CONTEXT) lo toman de <see cref="HttpRequestContext"/>.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex ValidId();

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = ValidId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
