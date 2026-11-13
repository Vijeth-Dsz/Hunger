using Hunger.Api.Data;
using Hunger.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;

namespace Hunger.Api.Controllers;

[ApiController]
[Route("api/roles")]
public sealed class RolesController(HungerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var roles = new List<RoleOption>();
        await foreach (var role in HungerQueries.EnabledRoles(db).WithCancellation(cancellationToken))
        {
            roles.Add(role);
        }

        return Ok(roles);
    }
}
