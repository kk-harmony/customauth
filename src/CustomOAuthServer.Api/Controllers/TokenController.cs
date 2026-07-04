using System.Security.Claims;
using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Authorization;
using CustomOAuthServer.Application.OAuth;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CustomOAuthServer.Api.Controllers;

[ApiController]
public sealed class TokenController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    IUserRepository userRepository,
    IAuditService auditService,
    ILogger<TokenController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
        {
            return await HandleClientCredentialsAsync(request, cancellationToken);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            return await HandleAuthorizationCodeOrRefreshAsync(request, cancellationToken);
        }

        if (string.Equals(request.GrantType, OAuthGrantTypes.TokenExchange, StringComparison.Ordinal))
        {
            return await HandleTokenExchangeAsync(request, cancellationToken);
        }

        return BadRequest(new OpenIddictResponse
        {
            Error = Errors.UnsupportedGrantType,
            ErrorDescription = "The specified grant type is not supported."
        });
    }

    private async Task<IActionResult> HandleClientCredentialsAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var application = await applicationManager.FindByClientIdAsync(request.ClientId!, cancellationToken)
            ?? throw new InvalidOperationException("The application cannot be found.");

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, await applicationManager.GetClientIdAsync(application, cancellationToken));
        identity.SetClaim(Claims.Name, await applicationManager.GetDisplayNameAsync(application, cancellationToken));
        identity.SetScopes(request.GetScopes());

        var resources = await scopeManager.ListResourcesAsync(identity.GetScopes(), cancellationToken).ToListAsync(cancellationToken);
        if (resources.Count > 0)
        {
            identity.SetResources(resources);
        }
        else
        {
            identity.SetResources("resource_server");
        }

        identity.SetDestinations(_ => [Destinations.AccessToken]);

        logger.LogInformation("Client credentials token issued for {ClientId}", request.ClientId);
        await auditService.WriteAsync(
            "token.issued",
            request.ClientId,
            "client_credentials",
            new { request.ClientId, Scopes = request.GetScopes() },
            cancellationToken);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleAuthorizationCodeOrRefreshAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var userId = result.Principal?.GetClaim(Claims.Subject);
        if (userId is null)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                }));
        }

        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                }));
        }

        var identity = new ClaimsIdentity(result.Principal!.Claims,
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        foreach (var role in user.Roles)
        {
            identity.AddClaim(new Claim(Claims.Role, role));
        }

        var requestedScopes = request.GetScopes();
        if (requestedScopes.Length == 0)
        {
            requestedScopes = result.Principal!.GetScopes();
        }

        identity.SetDestinations(GetDestinations);
        identity.SetScopes(ScopeAuthorization.FilterScopesForUser(requestedScopes, user.Roles));
        identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes(), cancellationToken).ToListAsync());

        logger.LogInformation("Token exchanged for user {UserId}", userId);
        await auditService.WriteAsync(
            "token.issued",
            userId,
            request.GrantType,
            new { userId, request.ClientId, Scopes = request.GetScopes() },
            cancellationToken);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleTokenExchangeAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var subjectTokenType = request.GetParameter("subject_token_type")?.ToString();
        var validationFailure = TokenExchangeValidator.ValidateSubjectTokenType(subjectTokenType);
        if (validationFailure is not null)
        {
            return TokenExchangeFailureResult(validationFailure);
        }

        var audience = request.GetParameter("audience")?.ToString();
        validationFailure = TokenExchangeValidator.ValidateAudience(audience);
        if (validationFailure is not null)
        {
            return TokenExchangeFailureResult(validationFailure);
        }

        var subjectPrincipal = await ResolveSubjectPrincipalAsync(request, cancellationToken);
        if (subjectPrincipal is null)
        {
            return InvalidGrant("The subject token is invalid.");
        }

        var subject = subjectPrincipal.GetClaim(Claims.Subject)!;
        if (await applicationManager.FindByClientIdAsync(subject, cancellationToken) is not null)
        {
            return InvalidGrant("Subject token must represent an end user.");
        }

        var user = await userRepository.FindByIdAsync(subject, cancellationToken);
        if (user is null)
        {
            return InvalidGrant("Subject token must represent an end user.");
        }

        var application = await applicationManager.FindByClientIdAsync(request.ClientId!, cancellationToken)
            ?? throw new InvalidOperationException("The application cannot be found.");

        var clientDescriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(clientDescriptor, application, cancellationToken);
        var allowedAudiences = ClientAudienceProperties.GetAllowedAudiences(clientDescriptor.Properties);

        validationFailure = TokenExchangeValidator.ValidateAudienceAllowlist(audience!, allowedAudiences);
        if (validationFailure is not null)
        {
            return TokenExchangeFailureResult(validationFailure);
        }

        var exchangeScopes = TokenExchangeValidator.ResolveExchangeScopes(
            request.GetScopes(),
            subjectPrincipal,
            user.Roles);

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id)
            .SetClaim(Claims.Name, user.DisplayName ?? user.Username)
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.PreferredUsername, user.Username);

        foreach (var role in user.Roles)
        {
            identity.AddClaim(new Claim(Claims.Role, role));
        }

        identity.SetClaim("act", request.ClientId);
        identity.SetDestinations(GetDestinations);
        identity.SetScopes(exchangeScopes);
        identity.SetResources(audience!);

        logger.LogInformation(
            "On-behalf-of token issued. Subject={Subject}, RequestedClient={ClientId}, Audience={Audience}",
            user.Id, request.ClientId, audience);
        await auditService.WriteAsync(
            "token.issued",
            user.Id,
            OAuthGrantTypes.TokenExchange,
            new { subject = user.Id, request.ClientId, audience, Scopes = exchangeScopes },
            cancellationToken);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private IActionResult TokenExchangeFailureResult(TokenExchangeFailure failure) =>
        failure.Kind switch
        {
            TokenExchangeFailureKind.InvalidRequest => BadRequest(new OpenIddictResponse
            {
                Error = Errors.InvalidRequest,
                ErrorDescription = failure.Description
            }),
            TokenExchangeFailureKind.InvalidTarget => BadRequest(new OpenIddictResponse
            {
                Error = "invalid_target",
                ErrorDescription = failure.Description
            }),
            _ => InvalidGrant(failure.Description)
        };

    private IActionResult InvalidGrant(string description) =>
        Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }));

    private async Task<ClaimsPrincipal?> ResolveSubjectPrincipalAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var subjectToken = request.GetParameter("subject_token")?.ToString();
        if (string.IsNullOrEmpty(subjectToken))
        {
            return null;
        }

        var previousAuthorization = Request.Headers.Authorization.ToString();
        Request.Headers.Authorization = $"Bearer {subjectToken}";
        try
        {
            var result = await HttpContext.AuthenticateAsync(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            return result.Succeeded ? result.Principal : null;
        }
        finally
        {
            if (string.IsNullOrEmpty(previousAuthorization))
            {
                Request.Headers.Remove("Authorization");
            }
            else
            {
                Request.Headers.Authorization = previousAuthorization;
            }
        }
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Name or Claims.Email or Claims.PreferredUsername:
                yield return Destinations.AccessToken;
                if (claim.Subject!.HasScope(Scopes.Profile))
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Subject:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                if (claim.Subject!.HasScope(Scopes.Roles))
                    yield return Destinations.IdentityToken;
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
