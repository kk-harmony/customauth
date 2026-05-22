using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace CustomOAuthServer.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public sealed class MeController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        return Ok(new
        {
            sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
            name = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name"),
            email = User.FindFirstValue(ClaimTypes.Email),
            client_id = User.FindFirstValue("client_id"),
            scope = User.FindFirstValue("scope")
        });
    }
}
