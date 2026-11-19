using FinCore.Application.Common.Abstractions;
using FinCore.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

[Route("api/v1/branches")]
[Authorize]
public sealed class BranchesController : ApiControllerBase
{
    /// <summary>Sucursales autorizadas al usuario (asignadas, o todas las de su empresa con branches.all).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuthorizedBranch>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromServices] GetAuthorizedBranchesHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(ct));
}
