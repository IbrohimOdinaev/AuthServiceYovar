using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthService.Api.Security;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/debug")]
public sealed class DebugApiController : ControllerBase
{
    [HttpGet("me")]
    [Authorize(Policy = AuthPolicies.OrdersRead)]
    public IActionResult Me()
    {
        return Ok(new
        {
            authenticated = User.Identity?.IsAuthenticated,
            name = User.Identity?.Name,
            claims = User.Claims.Select(claim => new
            {
                claim.Type,
                claim.Value
            })
        });
    }


}
