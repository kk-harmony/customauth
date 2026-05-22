using System.Security.Claims;
using CustomOAuthServer.Application.Abstractions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CustomOAuthServer.Api.Controllers;

[ApiController]
public sealed class AuthorizationController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictScopeManager scopeManager,
    IUserRepository userRepository,
    ILogger<AuthorizationController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            return Challenge(
                authenticationSchemes: CookieAuthenticationDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                });
        }

        var userId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await userRepository.FindByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        var application = await applicationManager.FindByClientIdAsync(request.ClientId!, cancellationToken)
            ?? throw new InvalidOperationException("The application details cannot be found.");

        var clientId = await applicationManager.GetIdAsync(application, cancellationToken)
            ?? throw new InvalidOperationException("The application identifier cannot be retrieved.");

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id)
            .SetClaim(Claims.Name, user.Username)
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.PreferredUsername, user.Username);

        identity.SetScopes(request.GetScopes());

        var authorizations = await authorizationManager.FindAsync(
            subject: user.Id,
            client: clientId,
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: null,
            cancellationToken: cancellationToken).ToListAsync(cancellationToken);

        var authorization = authorizations.LastOrDefault();
        if (authorization is null)
        {
            authorization = await authorizationManager.CreateAsync(
                identity: identity,
                subject: user.Id,
                client: clientId,
                type: AuthorizationTypes.Permanent,
                scopes: identity.GetScopes(),
                cancellationToken: cancellationToken);
        }

        identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes(), cancellationToken).ToListAsync(cancellationToken));
        identity.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization, cancellationToken));
        identity.SetDestinations(GetDestinations);

        logger.LogInformation("Authorization granted for user {UserId} client {ClientId}",
            user.Id, request.ClientId);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo")]
    public async Task<IActionResult> Userinfo(CancellationToken cancellationToken)
    {
        var userId = User.GetClaim(Claims.Subject)!;
        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Ok(new
        {
            sub = user.Id,
            name = user.DisplayName ?? user.Username,
            preferred_username = user.Username,
            email = user.Email
        });
    }

    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Name or Claims.Email or Claims.PreferredUsername:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                break;
            case Claims.Subject:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                break;
            default:
                yield return Destinations.AccessToken;
                break;
        }
    }
}
