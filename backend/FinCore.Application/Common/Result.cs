namespace FinCore.Application.Common;

/// <summary>Tipo de error de un caso de uso. La API lo traduce a un código HTTP.</summary>
public enum ErrorType
{
    Validation,    // 400
    Unauthorized,  // 401
    Forbidden,     // 403
    NotFound,      // 404
    Conflict,      // 409
    Unprocessable, // 422
    Locked,        // 423
}

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Locked(string code, string message) => new(code, message, ErrorType.Locked);
}

/// <summary>
/// Resultado de un caso de uso: un valor o un error esperado. Los errores esperados (credenciales inválidas,
/// recurso inexistente) no son excepciones; las excepciones quedan para lo inesperado.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    private Result(T value) { _value = value; Error = null; }
    private Result(Error error) { _value = default; Error = error; }

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("El resultado es un error; no tiene valor.");

    /// <summary>Crea un resultado exitoso. Necesario cuando T es una interfaz (C# no permite conversiones implícitas desde interfaces).</summary>
    public static Result<T> From(T value) => new(value);

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}

/// <summary>Valor vacío para casos de uso que no devuelven datos.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
