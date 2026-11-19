using FinCore.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

/// <summary>
/// Base de los controladores: traduce el resultado de un caso de uso a HTTP.
/// Los controladores no contienen reglas ni acceso a datos; solo HTTP ↔ caso de uso.
/// </summary>
// Sin [Produces("application/json")]: ese filtro reemplazaría el application/problem+json de los errores.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult<T>(Result<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess?.Invoke(result.Value) ?? Ok(result.Value);

        var error = result.Error!;
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Locked => StatusCodes.Status423Locked,
            _ => StatusCodes.Status500InternalServerError,
        };

        ProblemDetails problem;
        if (error.Type == ErrorType.Validation && error.Code.StartsWith("validation.", StringComparison.Ordinal))
        {
            // Error de un campo detectado por una regla del caso de uso: mismo formato que FluentValidation.
            var field = error.Code["validation.".Length..];
            problem = ProblemDetailsFactory.CreateValidationProblemDetails(HttpContext,
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary(), status, "Datos inválidos", detail: error.Message);
            ((ValidationProblemDetails)problem).Errors[field] = [error.Message];
        }
        else
        {
            problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, Title(error.Type), detail: error.Message);
        }

        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    private static string Title(ErrorType type) => type switch
    {
        ErrorType.Validation => "Datos inválidos",
        ErrorType.Unauthorized => "No autenticado",
        ErrorType.Forbidden => "Sin permiso",
        ErrorType.NotFound => "No encontrado",
        ErrorType.Conflict => "Conflicto",
        ErrorType.Unprocessable => "Solicitud no procesable",
        ErrorType.Locked => "Cuenta bloqueada",
        _ => "Error",
    };
}
