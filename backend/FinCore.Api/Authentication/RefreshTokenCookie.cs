namespace FinCore.Api.Authentication;

/// <summary>
/// El refresh token viaja en una cookie HttpOnly, Secure y SameSite=Strict, limitada a /api/v1/auth:
/// el JavaScript de la página no puede leerlo (protección ante XSS) y el navegador no lo envía desde otros sitios.
///
/// "Mantener sesión iniciada": si el usuario no lo marca, la cookie es de sesión (sin Expires) y el navegador la borra
/// al cerrarse. La preferencia viaja en una segunda cookie (fincore_rp) para que cada renovación la respete.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "fincore_rt";
    public const string PersistenceName = "fincore_rp";
    public const string Path = "/api/v1/auth";

    public static void Write(HttpResponse response, string value, DateTime expiresAtUtc, bool persistent)
    {
        var options = Options(persistent ? expiresAtUtc : null);
        response.Cookies.Append(Name, value, options);
        response.Cookies.Append(PersistenceName, persistent ? "1" : "0", options);
    }

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    /// <summary>Si no hay preferencia guardada se asume persistente (comportamiento anterior).</summary>
    public static bool ReadPersistent(HttpRequest request) => request.Cookies[PersistenceName] != "0";

    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(Name, Options(null));
        response.Cookies.Delete(PersistenceName, Options(null));
    }

    private static CookieOptions Options(DateTime? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,          // los navegadores aceptan Secure en http://localhost
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAtUtc is null ? null : new DateTimeOffset(expiresAtUtc.Value, TimeSpan.Zero),
        IsEssential = true,
    };
}
