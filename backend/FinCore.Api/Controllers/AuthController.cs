using FinCore.Api.Authentication;
using FinCore.Application.Auth;
using FinCore.Application.Auth.Login;
using FinCore.Application.Auth.Logout;
using FinCore.Application.Auth.Me;
using FinCore.Application.Auth.Refresh;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinCore.Api.Controllers;

/// <summary>RememberMe: false = la sesión termina al cerrar el navegador. Por defecto true.</summary>
public sealed record LoginRequest(string UserName, string Password, bool? RememberMe = null);

/// <summary>Respuesta de login y refresh. El refresh token NO va aquí: va en la cookie HttpOnly.</summary>
public sealed record SessionResponse(string AccessToken, DateTime ExpiresAtUtc);

[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Inicia sesión. 401 credenciales inválidas · 423 cuenta bloqueada.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login(LoginRequest request, [FromServices] LoginHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.UserName ?? "", request.Password ?? ""), ct);
        return FromResult(result, tokens => Session(tokens, request.RememberMe ?? true));
    }

    /// <summary>Renueva el access token con la cookie de refresh token (y la rota).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromServices] RefreshSessionHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RefreshSessionCommand(RefreshTokenCookie.Read(Request)), ct);
        if (!result.IsSuccess) RefreshTokenCookie.Delete(Response);
        var persistent = RefreshTokenCookie.ReadPersistent(Request);
        return FromResult(result, tokens => Session(tokens, persistent));
    }

    /// <summary>Cierra la sesión: revoca el refresh token y borra la cookie.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromServices] LogoutHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(new LogoutCommand(RefreshTokenCookie.Read(Request)), ct);
        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    /// <summary>Usuario autenticado con roles, permisos y sucursales autorizadas.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me([FromServices] GetCurrentUserHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(ct));

    private OkObjectResult Session(AuthTokens tokens, bool persistent)
    {
        RefreshTokenCookie.Write(Response, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc, persistent);
        return Ok(new SessionResponse(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc));
    }
}
