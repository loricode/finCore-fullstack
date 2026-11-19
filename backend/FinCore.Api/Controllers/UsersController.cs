using FinCore.Application.Users;
using FinCore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

public sealed record CreateUserRequest(string UserName, string Email, string FullName, string Password, List<string>? Roles, List<int>? BranchIds);

public sealed record UpdateUserRequest(string Email, string FullName, List<string>? Roles, List<int>? BranchIds, string RowVersion);

public sealed record UpdateUserBranchesRequest(List<int>? BranchIds);

public sealed record SetWindowsAccountRequest(string? WindowsAccount);

/// <summary>HU-02 y HU-04 · Administración de usuarios de la empresa (permiso users.manage).</summary>
[Route("api/v1/users")]
[Authorize(Policy = Permissions.UsersManage)]
public sealed class UsersController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromServices] GetUsersHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, [FromServices] GetUserHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(id, ct));

    /// <summary>Crea un usuario. 400 datos inválidos · 409 usuario o correo repetido.</summary>
    [HttpPost]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateUserRequest request, [FromServices] CreateUserHandler handler,
        [FromServices] GetUserHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CreateUserCommand(request.UserName ?? "", request.Email ?? "",
            request.FullName ?? "", request.Password ?? "", request.Roles ?? [], request.BranchIds ?? []), ct);
        if (!result.IsSuccess) return FromResult(result);

        var created = await get.HandleAsync(result.Value, ct);
        return FromResult(created, user => CreatedAtAction(nameof(Get), new { id = user.Id }, user));
    }

    /// <summary>Edita perfil, roles y sucursales. Exige el rowVersion leído: 409 si otra persona lo modificó.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpdateUserRequest request, [FromServices] UpdateUserHandler handler,
        [FromServices] GetUserHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new UpdateUserCommand(id, request.Email ?? "", request.FullName ?? "",
            request.Roles ?? [], request.BranchIds ?? [], request.RowVersion ?? ""), ct);
        return result.IsSuccess ? FromResult(await get.HandleAsync(id, ct)) : FromResult(result);
    }

    /// <summary>HU-04 · Reemplaza las sucursales asignadas.</summary>
    [HttpPut("{id:int}/branches")]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBranches(int id, UpdateUserBranchesRequest request,
        [FromServices] UpdateUserBranchesHandler handler, [FromServices] GetUserHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new UpdateUserBranchesCommand(id, request.BranchIds ?? []), ct);
        return result.IsSuccess ? FromResult(await get.HandleAsync(id, ct)) : FromResult(result);
    }

    /// <summary>
    /// Etapa 6 · Cuenta de Windows (DOMINIO\usuario) con la que el usuario abre los informes de Reporting Services.
    /// null o vacío la quita. 400 formato inválido · 409 la cuenta ya pertenece a otro usuario.
    /// </summary>
    [HttpPut("{id:int}/windows-account")]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetWindowsAccount(int id, SetWindowsAccountRequest request,
        [FromServices] SetUserWindowsAccountHandler handler, [FromServices] GetUserHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new SetUserWindowsAccountCommand(id, request.WindowsAccount), ct);
        return result.IsSuccess ? FromResult(await get.HandleAsync(id, ct)) : FromResult(result);
    }

    /// <summary>Desactiva el usuario y revoca sus sesiones. No se puede desactivar el propio usuario (409).</summary>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(int id, [FromServices] SetUserActiveHandler handler,
        [FromServices] GetUserHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, active: false, ct);
        return result.IsSuccess ? FromResult(await get.HandleAsync(id, ct)) : FromResult(result);
    }

    /// <summary>Activa el usuario y levanta un bloqueo por intentos fallidos.</summary>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(int id, [FromServices] SetUserActiveHandler handler,
        [FromServices] GetUserHandler get, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, active: true, ct);
        return result.IsSuccess ? FromResult(await get.HandleAsync(id, ct)) : FromResult(result);
    }
}

[Route("api/v1/roles")]
[Authorize(Policy = Permissions.UsersManage)]
public sealed class RolesController : ApiControllerBase
{
    /// <summary>Catálogo de roles con sus permisos.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromServices] GetRolesHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(ct));
}
