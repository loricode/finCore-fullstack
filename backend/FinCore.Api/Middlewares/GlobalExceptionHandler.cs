using FinCore.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Api.Middlewares;

/// <summary>
/// Traduce excepciones a application/problem+json (RFC 9457). Los errores inesperados devuelven 500 con un
/// mensaje genérico; el detalle solo va al log, junto con el CorrelationId que también recibe el cliente.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    /// <summary>Código no estándar (nginx) para "el cliente cerró la petición antes de la respuesta".</summary>
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // El navegador canceló la petición (navegó a otra página, cerró la pestaña). No es un error del servidor:
        // registrarlo como 500 llenaría los logs de falsas alarmas para soporte.
        if (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Petición cancelada por el cliente: {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = exception switch
        {
            ValidationException v => Validation(v),
            DomainException d => Create(StatusCodes.Status409Conflict, "Regla de negocio", d.Message, d.Code),
            FinCore.Application.Reconciliation.ReconciliationAlreadyRunningException r =>
                Create(StatusCodes.Status409Conflict, "Conciliación en curso", r.Message, "sql.50104"),
            DbUpdateConcurrencyException => Create(StatusCodes.Status409Conflict, "Conflicto de concurrencia",
                "El registro fue modificado por otra persona. Vuelva a cargarlo.", "concurrency"),
            _ when FindSqlException(exception) is { } sql => SqlErrorCatalog.ToProblem(sql),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
            problem = Create(StatusCodes.Status500InternalServerError, "Error interno",
                "Ocurrió un error inesperado. Informe a soporte el correlationId.", "internal");
        }
        else
        {
            logger.LogWarning("Error de negocio {Code} en {Method} {Path}: {Detail}",
                problem.Extensions.TryGetValue("code", out var code) ? code : null,
                context.Request.Method, context.Request.Path, problem.Detail);
        }

        context.Response.StatusCode = problem.Status ?? 500;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Validation(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Datos inválidos",
            Detail = "Revise los campos indicados.",
        };
    }

    internal static ProblemDetails Create(int status, string title, string detail, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        return problem;
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
            if (e is SqlException sql) return sql;
        return null;
    }
}

/// <summary>Errores de negocio de SQL Server (50000–50999) y de unicidad. Catálogo: database/README.md.</summary>
internal static class SqlErrorCatalog
{
    private static readonly Dictionary<int, int> StatusByNumber = new()
    {
        [50010] = 409, [50011] = 409, [50012] = 409, [50090] = 409, [50091] = 409,
        [50101] = 404, [50102] = 400, [50103] = 400, [50104] = 409, [50105] = 400,
        [50201] = 404, [50202] = 409, [50203] = 400, [50204] = 400,
        [50300] = 403, [50301] = 400, [50302] = 400, [50303] = 404, [50304] = 403,
    };

    public static ProblemDetails? ToProblem(SqlException sql)
    {
        if (StatusByNumber.TryGetValue(sql.Number, out var status))
            return GlobalExceptionHandler.Create(status, "Regla de negocio", sql.Message, $"sql.{sql.Number}");

        if (sql.Number is 2601 or 2627)
            return GlobalExceptionHandler.Create(409, "Registro duplicado", "Ya existe un registro con esos datos.", "duplicate");

        return null; // no es un error de negocio: lo trata el manejador genérico (500)
    }
}
